# MemoryOptimizer.ps1
# Memory and CPU reporting, plus the two actions that relieve them.
#
# Kept separate from the UI so the logic can be exercised on its own.
# Memory approach ported from MemoryOptimizerPro (MemoryPower.cs): rather than
# freeing the app's own working set, trim the working set of every process we may
# touch, which pushes those pages onto the standby list.
#
# CPU cannot be "freed" the way memory can. What actually works is lowering the
# priority of background processes that are burning cycles, so the machine stays
# responsive; processes with a visible window are left alone.
#
# Plain ASCII on purpose: Windows PowerShell 5.1 reads this without a BOM.

if (-not ('Native.MemoryTools' -as [type])) {
    Add-Type -Namespace Native -Name MemoryTools -MemberDefinition @'
[StructLayout(LayoutKind.Sequential)]
public struct MEMORYSTATUSEX
{
    public uint dwLength;
    public uint dwMemoryLoad;
    public ulong ullTotalPhys;
    public ulong ullAvailPhys;
    public ulong ullTotalPageFile;
    public ulong ullAvailPageFile;
    public ulong ullTotalVirtual;
    public ulong ullAvailVirtual;
    public ulong ullAvailExtendedVirtual;
}

[DllImport("kernel32.dll", SetLastError = true)]
public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

[DllImport("psapi.dll", SetLastError = true)]
public static extern bool EmptyWorkingSet(IntPtr hProcess);
'@
}

if (-not ('Native.ResourceTools' -as [type])) {
    Add-Type -Namespace Native -Name ResourceTools -MemberDefinition @'
[StructLayout(LayoutKind.Sequential)]
public struct FILETIME
{
    public uint Low;
    public uint High;
}

[DllImport("kernel32.dll", SetLastError = true)]
public static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

public static ulong ToUInt64(FILETIME value)
{
    return ((ulong)value.High << 32) | value.Low;
}
'@
}

# Current physical memory usage, straight from GlobalMemoryStatusEx so it is
# cheap enough to poll once a second.
function Get-MemoryStatus {
    $status = New-Object -TypeName 'Native.MemoryTools+MEMORYSTATUSEX'
    $status.dwLength = [uint32][System.Runtime.InteropServices.Marshal]::SizeOf($status)

    if (-not [Native.MemoryTools]::GlobalMemoryStatusEx([ref]$status)) {
        throw 'GlobalMemoryStatusEx failed.'
    }

    $total = [double]$status.ullTotalPhys
    $available = [double]$status.ullAvailPhys
    $used = $total - $available

    if ($total -gt 0) { $percent = ($used / $total) * 100 }
    else { $percent = [double]$status.dwMemoryLoad }

    [pscustomobject]@{
        PercentUsed    = [double]$percent
        TotalBytes     = [int64]$total
        AvailableBytes = [int64]$available
        UsedBytes      = [int64]$used
        TotalGB        = [double]($total / 1GB)
        UsedGB         = [double]($used / 1GB)
        AvailableGB    = [double]($available / 1GB)
    }
}

# Decides whether an automatic trim should fire. Kept pure so the threshold and
# cooldown rules can be checked without actually filling up RAM or the CPU.
function Test-ShouldAutoOptimize {
    param(
        [double]$PercentUsed,
        [int]$ThresholdPercent = 70,
        [bool]$Enabled = $true,
        [datetime]$CooldownUntil = [datetime]::MinValue,
        [datetime]$Now = (Get-Date)
    )

    if (-not $Enabled) { return $false }
    if ($PercentUsed -lt $ThresholdPercent) { return $false }
    return ($Now -ge $CooldownUntil)
}

# Trims the working set of every accessible process with a meaningful footprint.
# Processes we cannot open (elevated or protected ones) are skipped silently.
function Optimize-Memory {
    param(
        [int]$MinimumWorkingSetMB = 8
    )

    $before = Get-MemoryStatus
    $minimumBytes = [int64]$MinimumWorkingSetMB * 1MB
    $trimmed = 0
    $skipped = 0
    $failed = 0

    foreach ($process in [System.Diagnostics.Process]::GetProcesses()) {
        try {
            if ($process.Id -eq $PID) { $skipped++; continue }
            if ($process.WorkingSet64 -lt $minimumBytes) { $skipped++; continue }

            $handle = $process.Handle
            if ($handle -eq [IntPtr]::Zero) { $failed++; continue }

            if ([Native.MemoryTools]::EmptyWorkingSet($handle)) { $trimmed++ }
            else { $failed++ }
        }
        catch {
            $failed++
        }
        finally {
            $process.Dispose()
        }
    }

    # Trimmed pages go to the standby list; give Windows a moment to update the
    # available-memory figure before measuring the result.
    Start-Sleep -Milliseconds 300
    $after = Get-MemoryStatus

    [pscustomobject]@{
        Trimmed    = $trimmed
        Skipped    = $skipped
        Failed     = $failed
        Before     = $before
        After      = $after
        FreedBytes = [int64]($after.AvailableBytes - $before.AvailableBytes)
    }
}

# ---------------------------------------------------------------- CPU readings
# A single point-in-time reading of the system idle and busy counters. Two of
# these taken a moment apart give the CPU load in between, with no sleeping and
# no performance-counter dependency.
function Get-CpuSnapshot {
    $idle = New-Object -TypeName 'Native.ResourceTools+FILETIME'
    $kernel = New-Object -TypeName 'Native.ResourceTools+FILETIME'
    $user = New-Object -TypeName 'Native.ResourceTools+FILETIME'

    if (-not [Native.ResourceTools]::GetSystemTimes([ref]$idle, [ref]$kernel, [ref]$user)) {
        throw 'GetSystemTimes failed.'
    }

    [pscustomobject]@{
        Idle  = [Native.ResourceTools]::ToUInt64($idle)
        Total = ([Native.ResourceTools]::ToUInt64($kernel) + [Native.ResourceTools]::ToUInt64($user))
    }
}

# CPU load between two snapshots, as a percentage of the whole machine.
function Get-CpuPercent {
    param($Previous, $Current)

    if ($null -eq $Previous -or $null -eq $Current) { return [double]0 }

    $totalDelta = [double]($Current.Total - $Previous.Total)
    $idleDelta = [double]($Current.Idle - $Previous.Idle)
    if ($totalDelta -le 0) { return [double]0 }

    $percent = (1 - ($idleDelta / $totalDelta)) * 100
    if ($percent -lt 0) { return [double]0 }
    if ($percent -gt 100) { return [double]100 }
    return [double]$percent
}

# Blocking average CPU load over a window. Only used around an optimization,
# where a pause is acceptable; the live display uses the delta between ticks.
function Measure-CpuPercent {
    param([int]$Milliseconds = 500)

    $first = Get-CpuSnapshot
    Start-Sleep -Milliseconds $Milliseconds
    $second = Get-CpuSnapshot
    return [double][Math]::Round((Get-CpuPercent -Previous $first -Current $second), 1)
}

# Processes currently using the most CPU, measured over a short window.
function Get-TopCpuProcesses {
    param(
        [int]$SampleMilliseconds = 350,
        [double]$MinimumPercent = 3
    )

    $first = @{}
    foreach ($process in [System.Diagnostics.Process]::GetProcesses()) {
        try { $first[$process.Id] = $process.TotalProcessorTime.Ticks } catch { }
    }

    Start-Sleep -Milliseconds $SampleMilliseconds

    $cores = [Environment]::ProcessorCount
    $results = @()

    foreach ($process in [System.Diagnostics.Process]::GetProcesses()) {
        try {
            if ($process.Id -eq $PID) { continue }
            if (-not $first.ContainsKey($process.Id)) { continue }

            $deltaTicks = $process.TotalProcessorTime.Ticks - $first[$process.Id]
            if ($deltaTicks -le 0) { continue }

            # 100ns ticks -> milliseconds of CPU, then a share of all cores.
            $percent = (($deltaTicks / 10000.0) / $SampleMilliseconds) * (100.0 / $cores)
            if ($percent -ge $MinimumPercent) {
                $results += [pscustomobject]@{
                    Id      = $process.Id
                    Name    = $process.ProcessName
                    Percent = [double]$percent
                }
            }
        }
        catch { }
    }

    return @($results | Sort-Object -Property Percent -Descending)
}

# Lowers the priority of busy background processes so they stop competing with
# whatever the user is doing. Processes with a visible window are left alone.
function Optimize-Cpu {
    param(
        [double]$MinimumProcessPercent = 3,
        [int]$MaxProcesses = 12
    )

    $before = Measure-CpuPercent -Milliseconds 700
    $targets = @(Get-TopCpuProcesses -SampleMilliseconds 350 -MinimumPercent $MinimumProcessPercent |
            Select-Object -First $MaxProcesses)

    $deprioritized = 0
    foreach ($target in $targets) {
        try {
            $process = Get-Process -Id $target.Id -ErrorAction Stop
            if ($process.MainWindowHandle -ne [IntPtr]::Zero) { continue }

            $priority = $process.PriorityClass
            if ($priority -eq [System.Diagnostics.ProcessPriorityClass]::BelowNormal -or
                $priority -eq [System.Diagnostics.ProcessPriorityClass]::Idle) { continue }

            $process.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::BelowNormal
            $deprioritized++
        }
        catch { }
    }

    $after = Measure-CpuPercent -Milliseconds 900

    [pscustomobject]@{
        Deprioritized = $deprioritized
        Candidates    = $targets.Count
        BeforePercent = $before
        AfterPercent  = $after
        Reduction     = [double][Math]::Max(0, $before - $after)
    }
}
