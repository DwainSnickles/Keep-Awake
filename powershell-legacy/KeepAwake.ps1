# Keep Awake
# Stops Windows from sleeping / hibernating while leaving the screensaver and
# display timeout alone, and relieves RAM and CPU pressure on demand or once a
# metric passes a threshold (70% by default).
#
# Sleep side: SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED)
#   ES_SYSTEM_REQUIRED  -> the system stays awake
#   ES_DISPLAY_REQUIRED -> deliberately NOT set, so the display can turn off and
#                          the screensaver can start normally.
#
# Awake state: while sleep blocking is on, the window collapses to a small toolbar
# that shows only RAM and CPU, plus a note when either one was reduced.
#
# Resource side lives in MemoryOptimizer.ps1 (Get-MemoryStatus, Optimize-Memory,
# Optimize-Cpu and friends), ported from MemoryOptimizerPro.
#
# This file is intentionally plain ASCII so Windows PowerShell 5.1 reads it
# correctly without a byte order mark.

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

. (Join-Path $PSScriptRoot 'MemoryOptimizer.ps1')

if (-not ('Native.Power' -as [type])) {
    Add-Type -Namespace Native -Name Power -MemberDefinition @'
[DllImport("kernel32.dll", SetLastError = true)]
public static extern uint SetThreadExecutionState(uint esFlags);
'@
}

# Hide the PowerShell console if we were given a private one, so the only thing
# on screen is the app window no matter how the script was started. When the
# console is shared with another process (a terminal the user is working in) it
# is left alone.
if (-not ('Native.ConsoleWindow' -as [type])) {
    Add-Type -Namespace Native -Name ConsoleWindow -MemberDefinition @'
[DllImport("kernel32.dll")]
public static extern IntPtr GetConsoleWindow();

[DllImport("kernel32.dll")]
public static extern uint GetConsoleProcessList(uint[] processList, uint count);

[DllImport("user32.dll")]
public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
'@
}

try {
    $consoleHandle = [Native.ConsoleWindow]::GetConsoleWindow()
    if ($consoleHandle -ne [IntPtr]::Zero) {
        $processList = [uint32[]]::new(8)
        $attached = [Native.ConsoleWindow]::GetConsoleProcessList($processList, [uint32]$processList.Length)
        # Exactly 1 means this process owns the console, so hiding it hides
        # nothing else. A shared console (a terminal the user is working in) is
        # left visible, and an unreadable count is treated the same way.
        if ($attached -eq 1) {
            [void][Native.ConsoleWindow]::ShowWindow($consoleHandle, 0)
        }
    }
}
catch { }

# 0x80000000 - ES_CONTINUOUS
$ES_CONTINUOUS = [uint32]2147483648
# 0x00000001 - ES_SYSTEM_REQUIRED
$ES_SYSTEM_REQUIRED = [uint32]1
# Keep the system awake, leave the display alone.
$AWAKE_FLAGS = [uint32]($ES_CONTINUOUS -bor $ES_SYSTEM_REQUIRED)

$script:isActive = $false
$script:startedAt = $null
$script:tickCount = 0
$script:previousCpu = $null
$script:lastRamPercent = 0
$script:lastCpuPercent = $null
$script:deltaClearAt = [DateTime]::MinValue

# Auto-optimize: fire when a metric reaches this percentage, then wait before
# acting again so a permanently busy machine is not hammered.
$script:autoThresholdPercent = 70
$script:autoCooldownSeconds = 60
$script:autoRamCooldownUntil = [DateTime]::MinValue
$script:autoCpuCooldownUntil = [DateTime]::MinValue

# ---------------------------------------------------------------- theme colours
$bgColor = [System.Drawing.Color]::FromArgb(23, 25, 31)
$panelColor = [System.Drawing.Color]::FromArgb(32, 35, 43)
$trackColor = [System.Drawing.Color]::FromArgb(52, 57, 70)
$textColor = [System.Drawing.Color]::FromArgb(236, 239, 246)
$mutedColor = [System.Drawing.Color]::FromArgb(146, 154, 170)
$accentColor = [System.Drawing.Color]::FromArgb(59, 130, 246)
$accentHover = [System.Drawing.Color]::FromArgb(82, 148, 250)
$warnColor = [System.Drawing.Color]::FromArgb(245, 176, 65)
$dangerColor = [System.Drawing.Color]::FromArgb(232, 100, 100)
$stopColor = [System.Drawing.Color]::FromArgb(69, 74, 88)
$stopHover = [System.Drawing.Color]::FromArgb(84, 90, 106)
$activeColor = [System.Drawing.Color]::FromArgb(74, 222, 128)

$uiFont = 'Segoe UI'
$barTrackWidth = 364
$fullSize = New-Object System.Drawing.Size(440, 548)
$compactSize = New-Object System.Drawing.Size(264, 64)

# ---------------------------------------------------------------------- window
$form = New-Object System.Windows.Forms.Form
$form.Text = 'Keep Awake'
$form.ClientSize = $fullSize
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::FixedSingle
$form.MaximizeBox = $false
$form.BackColor = $bgColor
$form.ForeColor = $textColor
$form.Font = New-Object System.Drawing.Font($uiFont, 9)

$titleLabel = New-Object System.Windows.Forms.Label
$titleLabel.Text = 'Keep Awake'
$titleLabel.Font = New-Object System.Drawing.Font($uiFont, 18, [System.Drawing.FontStyle]::Bold)
$titleLabel.ForeColor = $textColor
$titleLabel.AutoSize = $true
$titleLabel.Location = New-Object System.Drawing.Point(24, 16)
$form.Controls.Add($titleLabel)

$subtitleLabel = New-Object System.Windows.Forms.Label
$subtitleLabel.Text = 'Blocks sleep and hibernation. Your screensaver and screen timeout keep working as usual.'
$subtitleLabel.Font = New-Object System.Drawing.Font($uiFont, 9)
$subtitleLabel.ForeColor = $mutedColor
$subtitleLabel.Location = New-Object System.Drawing.Point(26, 54)
$subtitleLabel.Size = New-Object System.Drawing.Size(388, 36)
$form.Controls.Add($subtitleLabel)

# ------------------------------------------------------------- sleep state card
$statusCard = New-Object System.Windows.Forms.Panel
$statusCard.Location = New-Object System.Drawing.Point(24, 98)
$statusCard.Size = New-Object System.Drawing.Size(392, 68)
$statusCard.BackColor = $panelColor
$form.Controls.Add($statusCard)

$statusDot = New-Object System.Windows.Forms.Label
$statusDot.Text = [string][char]0x25CF
$statusDot.Font = New-Object System.Drawing.Font($uiFont, 12)
$statusDot.ForeColor = $mutedColor
$statusDot.AutoSize = $true
$statusDot.Location = New-Object System.Drawing.Point(14, 19)
$statusCard.Controls.Add($statusDot)

$statusLabel = New-Object System.Windows.Forms.Label
$statusLabel.Text = 'Not running'
$statusLabel.Font = New-Object System.Drawing.Font($uiFont, 11.5, [System.Drawing.FontStyle]::Bold)
$statusLabel.ForeColor = $textColor
$statusLabel.AutoSize = $true
$statusLabel.Location = New-Object System.Drawing.Point(38, 17)
$statusCard.Controls.Add($statusLabel)

$elapsedLabel = New-Object System.Windows.Forms.Label
$elapsedLabel.Text = 'Sleep is allowed right now'
$elapsedLabel.Font = New-Object System.Drawing.Font($uiFont, 8.75)
$elapsedLabel.ForeColor = $mutedColor
$elapsedLabel.AutoSize = $true
$elapsedLabel.Location = New-Object System.Drawing.Point(40, 41)
$statusCard.Controls.Add($elapsedLabel)

# ----------------------------------------------------------- RAM and CPU card
$resourceCard = New-Object System.Windows.Forms.Panel
$resourceCard.Location = New-Object System.Drawing.Point(24, 178)
$resourceCard.Size = New-Object System.Drawing.Size(392, 176)
$resourceCard.BackColor = $panelColor
$form.Controls.Add($resourceCard)

$ramLabel = New-Object System.Windows.Forms.Label
$ramLabel.Text = 'RAM --'
$ramLabel.Font = New-Object System.Drawing.Font($uiFont, 11.5, [System.Drawing.FontStyle]::Bold)
$ramLabel.ForeColor = $textColor
$ramLabel.AutoSize = $true
$ramLabel.Location = New-Object System.Drawing.Point(14, 10)
$resourceCard.Controls.Add($ramLabel)

$ramDetail = New-Object System.Windows.Forms.Label
$ramDetail.Text = 'Reading memory usage...'
$ramDetail.Font = New-Object System.Drawing.Font($uiFont, 8.5)
$ramDetail.ForeColor = $mutedColor
$ramDetail.AutoSize = $true
$ramDetail.Location = New-Object System.Drawing.Point(16, 32)
$resourceCard.Controls.Add($ramDetail)

$ramTrack = New-Object System.Windows.Forms.Panel
$ramTrack.Location = New-Object System.Drawing.Point(14, 54)
$ramTrack.Size = New-Object System.Drawing.Size($barTrackWidth, 8)
$ramTrack.BackColor = $trackColor
$resourceCard.Controls.Add($ramTrack)

$ramFill = New-Object System.Windows.Forms.Panel
$ramFill.Location = New-Object System.Drawing.Point(0, 0)
$ramFill.Size = New-Object System.Drawing.Size(0, 8)
$ramFill.BackColor = $accentColor
$ramTrack.Controls.Add($ramFill)

$autoRamCheck = New-Object System.Windows.Forms.CheckBox
$autoRamCheck.Text = 'Auto-optimize RAM at 70%'
$autoRamCheck.Font = New-Object System.Drawing.Font($uiFont, 8.5)
$autoRamCheck.ForeColor = $textColor
$autoRamCheck.BackColor = $panelColor
$autoRamCheck.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
$autoRamCheck.AutoSize = $true
$autoRamCheck.Checked = $true
$autoRamCheck.Location = New-Object System.Drawing.Point(13, 68)
$resourceCard.Controls.Add($autoRamCheck)

$cpuLabel = New-Object System.Windows.Forms.Label
$cpuLabel.Text = 'CPU --'
$cpuLabel.Font = New-Object System.Drawing.Font($uiFont, 11.5, [System.Drawing.FontStyle]::Bold)
$cpuLabel.ForeColor = $textColor
$cpuLabel.AutoSize = $true
$cpuLabel.Location = New-Object System.Drawing.Point(14, 92)
$resourceCard.Controls.Add($cpuLabel)

$cpuDetail = New-Object System.Windows.Forms.Label
$cpuDetail.Text = 'Measuring CPU usage...'
$cpuDetail.Font = New-Object System.Drawing.Font($uiFont, 8.5)
$cpuDetail.ForeColor = $mutedColor
$cpuDetail.AutoSize = $true
$cpuDetail.Location = New-Object System.Drawing.Point(16, 114)
$resourceCard.Controls.Add($cpuDetail)

$cpuTrack = New-Object System.Windows.Forms.Panel
$cpuTrack.Location = New-Object System.Drawing.Point(14, 136)
$cpuTrack.Size = New-Object System.Drawing.Size($barTrackWidth, 8)
$cpuTrack.BackColor = $trackColor
$resourceCard.Controls.Add($cpuTrack)

$cpuFill = New-Object System.Windows.Forms.Panel
$cpuFill.Location = New-Object System.Drawing.Point(0, 0)
$cpuFill.Size = New-Object System.Drawing.Size(0, 8)
$cpuFill.BackColor = $accentColor
$cpuTrack.Controls.Add($cpuFill)

$autoCpuCheck = New-Object System.Windows.Forms.CheckBox
$autoCpuCheck.Text = 'Auto-optimize CPU at 70%'
$autoCpuCheck.Font = New-Object System.Drawing.Font($uiFont, 8.5)
$autoCpuCheck.ForeColor = $textColor
$autoCpuCheck.BackColor = $panelColor
$autoCpuCheck.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
$autoCpuCheck.AutoSize = $true
$autoCpuCheck.Checked = $true
$autoCpuCheck.Location = New-Object System.Drawing.Point(13, 150)
$resourceCard.Controls.Add($autoCpuCheck)

# --------------------------------------------------------------------- buttons
$optimizeButton = New-Object System.Windows.Forms.Button
$optimizeButton.Text = 'Optimize RAM and CPU now'
$optimizeButton.Location = New-Object System.Drawing.Point(24, 368)
$optimizeButton.Size = New-Object System.Drawing.Size(392, 40)
$optimizeButton.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
$optimizeButton.FlatAppearance.BorderSize = 0
$optimizeButton.FlatAppearance.MouseOverBackColor = [System.Drawing.Color]::FromArgb(44, 48, 58)
$optimizeButton.FlatAppearance.MouseDownBackColor = [System.Drawing.Color]::FromArgb(52, 57, 70)
$optimizeButton.BackColor = [System.Drawing.Color]::FromArgb(38, 42, 52)
$optimizeButton.ForeColor = $textColor
$optimizeButton.Font = New-Object System.Drawing.Font($uiFont, 9.5, [System.Drawing.FontStyle]::Bold)
$optimizeButton.Cursor = [System.Windows.Forms.Cursors]::Hand
$optimizeButton.UseVisualStyleBackColor = $false
$form.Controls.Add($optimizeButton)

$toggleButton = New-Object System.Windows.Forms.Button
$toggleButton.Text = 'Start'
$toggleButton.Location = New-Object System.Drawing.Point(24, 418)
$toggleButton.Size = New-Object System.Drawing.Size(392, 50)
$toggleButton.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
$toggleButton.FlatAppearance.BorderSize = 0
$toggleButton.FlatAppearance.MouseOverBackColor = $accentHover
$toggleButton.FlatAppearance.MouseDownBackColor = $accentColor
$toggleButton.BackColor = $accentColor
$toggleButton.ForeColor = [System.Drawing.Color]::White
$toggleButton.Font = New-Object System.Drawing.Font($uiFont, 11, [System.Drawing.FontStyle]::Bold)
$toggleButton.Cursor = [System.Windows.Forms.Cursors]::Hand
$toggleButton.UseVisualStyleBackColor = $false
$form.Controls.Add($toggleButton)

$logLabel = New-Object System.Windows.Forms.Label
$logLabel.Text = 'No optimization run yet.'
$logLabel.Font = New-Object System.Drawing.Font($uiFont, 8.25)
$logLabel.ForeColor = $mutedColor
$logLabel.Location = New-Object System.Drawing.Point(24, 478)
$logLabel.Size = New-Object System.Drawing.Size(392, 34)
$form.Controls.Add($logLabel)

$footerLabel = New-Object System.Windows.Forms.Label
$footerLabel.Text = 'Closing this window turns sleep blocking off.'
$footerLabel.Font = New-Object System.Drawing.Font($uiFont, 8.25)
$footerLabel.ForeColor = [System.Drawing.Color]::FromArgb(112, 119, 133)
$footerLabel.AutoSize = $true
$footerLabel.Location = New-Object System.Drawing.Point(24, 520)
$form.Controls.Add($footerLabel)

# ------------------------------------------------------- compact toolbar panel
$compactPanel = New-Object System.Windows.Forms.Panel
$compactPanel.Location = New-Object System.Drawing.Point(0, 0)
$compactPanel.Size = $compactSize
$compactPanel.BackColor = $bgColor
$compactPanel.Visible = $false
$form.Controls.Add($compactPanel)

$compactResource = New-Object System.Windows.Forms.Label
$compactResource.Text = 'RAM --   CPU --'
$compactResource.Font = New-Object System.Drawing.Font($uiFont, 11.5, [System.Drawing.FontStyle]::Bold)
$compactResource.ForeColor = $textColor
$compactResource.AutoSize = $true
$compactResource.Location = New-Object System.Drawing.Point(12, 9)
$compactPanel.Controls.Add($compactResource)

$compactDelta = New-Object System.Windows.Forms.Label
$compactDelta.Text = ''
$compactDelta.Font = New-Object System.Drawing.Font($uiFont, 8.25)
$compactDelta.ForeColor = $activeColor
$compactDelta.AutoSize = $true
$compactDelta.Location = New-Object System.Drawing.Point(12, 34)
$compactPanel.Controls.Add($compactDelta)

$expandButton = New-Object System.Windows.Forms.Button
$expandButton.Text = 'Full'
$expandButton.Location = New-Object System.Drawing.Point(208, 10)
$expandButton.Size = New-Object System.Drawing.Size(44, 28)
$expandButton.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
$expandButton.FlatAppearance.BorderSize = 0
$expandButton.FlatAppearance.MouseOverBackColor = [System.Drawing.Color]::FromArgb(44, 48, 58)
$expandButton.BackColor = [System.Drawing.Color]::FromArgb(38, 42, 52)
$expandButton.ForeColor = $textColor
$expandButton.Font = New-Object System.Drawing.Font($uiFont, 8.5, [System.Drawing.FontStyle]::Bold)
$expandButton.Cursor = [System.Windows.Forms.Cursors]::Hand
$expandButton.UseVisualStyleBackColor = $false
$compactPanel.Controls.Add($expandButton)

# ---------------------------------------------------------------- sleep control
function Set-KeepAwake([bool]$on) {
    if ($on) {
        [void][Native.Power]::SetThreadExecutionState($AWAKE_FLAGS)
    }
    else {
        [void][Native.Power]::SetThreadExecutionState($ES_CONTINUOUS)
    }
}

function Update-Elapsed {
    if (-not $script:isActive -or $null -eq $script:startedAt) { return }

    $span = (Get-Date) - $script:startedAt
    $elapsedLabel.Text = 'Keeping awake for {0:00}:{1:00}:{2:00}' -f `
        [int][Math]::Floor($span.TotalHours), $span.Minutes, $span.Seconds
}

# Collapses the window to a small toolbar (RAM and CPU only) while awake, and
# restores the full window otherwise.
function Set-CompactMode([bool]$compact) {
    if ($compact) {
        $titleLabel.Visible = $false
        $subtitleLabel.Visible = $false
        $statusCard.Visible = $false
        $resourceCard.Visible = $false
        $optimizeButton.Visible = $false
        $toggleButton.Visible = $false
        $logLabel.Visible = $false
        $footerLabel.Visible = $false

        $form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::FixedToolWindow
        $form.ClientSize = $compactSize
        $compactPanel.Visible = $true
        $form.TopMost = $true

        $area = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
        $form.Location = New-Object System.Drawing.Point(
            ($area.Right - $form.Width - 16),
            ($area.Top + 16))
    }
    else {
        $compactPanel.Visible = $false

        $form.TopMost = $false
        $form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::FixedSingle
        $form.ClientSize = $fullSize

        $titleLabel.Visible = $true
        $subtitleLabel.Visible = $true
        $statusCard.Visible = $true
        $resourceCard.Visible = $true
        $optimizeButton.Visible = $true
        $toggleButton.Visible = $true
        $logLabel.Visible = $true
        $footerLabel.Visible = $true

        # Keep the window on screen after growing back.
        $area = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
        $x = [Math]::Min([Math]::Max($form.Location.X, $area.Left), $area.Right - $form.Width)
        $y = [Math]::Min([Math]::Max($form.Location.Y, $area.Top), $area.Bottom - $form.Height)
        $form.Location = New-Object System.Drawing.Point($x, $y)
    }
}

function Start-Awake {
    Set-KeepAwake $true
    $script:startedAt = Get-Date
    $script:isActive = $true
    $script:tickCount = 0

    $statusDot.ForeColor = $activeColor
    $statusLabel.Text = 'Awake - sleep is blocked'
    $elapsedLabel.Text = 'Keeping awake for 00:00:00'
    $toggleButton.Text = 'Stop'
    $toggleButton.BackColor = $stopColor
    $toggleButton.FlatAppearance.MouseOverBackColor = $stopHover

    Set-CompactMode $true
}

function Stop-Awake {
    Set-KeepAwake $false
    $script:isActive = $false
    $script:startedAt = $null

    $statusDot.ForeColor = $mutedColor
    $statusLabel.Text = 'Not running'
    $elapsedLabel.Text = 'Sleep is allowed right now'
    $toggleButton.Text = 'Start'
    $toggleButton.BackColor = $accentColor
    $toggleButton.FlatAppearance.MouseOverBackColor = $accentHover

    Set-CompactMode $false
}

# ------------------------------------------------------------- resource display
function Get-BarColor([int]$percent) {
    if ($percent -ge 85) { return $dangerColor }
    if ($percent -ge $script:autoThresholdPercent) { return $warnColor }
    return $accentColor
}

function Update-CompactText {
    $ramText = 'RAM {0}%' -f [int][Math]::Round($script:lastRamPercent)
    if ($null -eq $script:lastCpuPercent) { $cpuText = 'CPU --' }
    else { $cpuText = 'CPU {0}%' -f [int][Math]::Round($script:lastCpuPercent) }
    $compactResource.Text = '{0}   {1}' -f $ramText, $cpuText
}

function Set-MemoryDisplay([object]$status) {
    $percent = [int][Math]::Round($status.PercentUsed)
    $script:lastRamPercent = $percent

    $ramLabel.Text = "RAM $percent%"
    $ramDetail.Text = '{0:0.0} GB of {1:0.0} GB in use, {2:0.0} GB available' -f `
        $status.UsedGB, $status.TotalGB, $status.AvailableGB

    $clamped = [Math]::Min(100, [Math]::Max(0, $percent))
    $ramFill.Width = [int]($barTrackWidth * $clamped / 100)
    $ramFill.BackColor = Get-BarColor $percent

    Update-CompactText
}

function Set-CpuDisplay([double]$percent) {
    $script:lastCpuPercent = $percent
    $rounded = [int][Math]::Round($percent)

    $cpuLabel.Text = "CPU $rounded%"
    $cpuDetail.Text = '{0} logical processors, {1}% busy' -f [Environment]::ProcessorCount, $rounded

    $clamped = [Math]::Min(100, [Math]::Max(0, $rounded))
    $cpuFill.Width = [int]($barTrackWidth * $clamped / 100)
    $cpuFill.BackColor = Get-BarColor $rounded

    Update-CompactText
}

# --------------------------------------------------------- optimization action
function Invoke-Optimize([string]$source, [string]$target = 'Both') {
    $optimizeButton.Enabled = $false
    $form.UseWaitCursor = $true
    try {
        $memResult = $null
        $cpuResult = $null

        if ($target -eq 'Both' -or $target -eq 'Memory') { $memResult = Optimize-Memory }
        if ($target -eq 'Both' -or $target -eq 'Cpu') { $cpuResult = Optimize-Cpu }

        if ($null -ne $memResult) { Set-MemoryDisplay $memResult.After }
        if ($null -ne $cpuResult) { Set-CpuDisplay $cpuResult.AfterPercent }

        $deltaParts = @()
        $detailParts = @()

        if ($null -ne $memResult) {
            $drop = $memResult.Before.PercentUsed - $memResult.After.PercentUsed
            if ($drop -ge 1) { $deltaParts += ('RAM reduced {0}%' -f [int][Math]::Round($drop)) }
            $detailParts += ('trimmed {0} processes' -f $memResult.Trimmed)
        }

        if ($null -ne $cpuResult) {
            if ($cpuResult.Reduction -ge 1) { $deltaParts += ('CPU reduced {0}%' -f [int][Math]::Round($cpuResult.Reduction)) }
            $detailParts += ('deprioritized {0} processes' -f $cpuResult.Deprioritized)
        }

        $stamp = (Get-Date).ToString('HH:mm:ss')
        $detail = ($detailParts -join ', ')

        if ($deltaParts.Count -gt 0) {
            $deltaText = ($deltaParts -join '   ')
            $logLabel.Text = '{0}  {1} ({2}): {3}' -f $stamp, $source, $detail, $deltaText
            $compactDelta.Text = $deltaText
            $compactDelta.ForeColor = $activeColor
            $script:deltaClearAt = (Get-Date).AddSeconds(12)
        }
        else {
            $logLabel.Text = '{0}  {1} ({2}): no measurable change' -f $stamp, $source, $detail
            $compactDelta.Text = ''
            $script:deltaClearAt = [DateTime]::MinValue
        }
    }
    catch {
        $logLabel.Text = '{0}  {1}: failed - {2}' -f (Get-Date).ToString('HH:mm:ss'), $source, $_.Exception.Message
    }
    finally {
        $optimizeButton.Enabled = $true
        $form.UseWaitCursor = $false
    }
}

# ------------------------------------------------------------------ live timer
$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 1000
$timer.Add_Tick({
        $script:tickCount++

        # Re-assert roughly every 30 seconds while blocking is on. Other apps are
        # allowed to clear the thread state, and a long idle stretch is exactly
        # when that matters.
        if ($script:isActive -and ($script:tickCount % 30) -eq 0) {
            Set-KeepAwake $true
        }

        Update-Elapsed

        $memory = $null
        try { $memory = Get-MemoryStatus } catch { }
        if ($null -ne $memory) { Set-MemoryDisplay $memory }

        $cpuPercent = $null
        try {
            $snapshot = Get-CpuSnapshot
            if ($null -ne $script:previousCpu) {
                $cpuPercent = Get-CpuPercent -Previous $script:previousCpu -Current $snapshot
            }
            $script:previousCpu = $snapshot
        }
        catch { }
        if ($null -ne $cpuPercent) { Set-CpuDisplay $cpuPercent }

        # Reduction notes are useful but should not clutter the toolbar forever.
        if ($script:deltaClearAt -ne [DateTime]::MinValue -and (Get-Date) -ge $script:deltaClearAt) {
            $compactDelta.Text = ''
            $script:deltaClearAt = [DateTime]::MinValue
        }

        if ($null -ne $memory -and (Test-ShouldAutoOptimize -PercentUsed $memory.PercentUsed `
                    -ThresholdPercent $script:autoThresholdPercent `
                    -Enabled $autoRamCheck.Checked `
                    -CooldownUntil $script:autoRamCooldownUntil)) {
            Invoke-Optimize ('auto RAM at {0}%' -f $script:autoThresholdPercent) 'Memory'
            $script:autoRamCooldownUntil = (Get-Date).AddSeconds($script:autoCooldownSeconds)
        }

        if ($null -ne $cpuPercent -and (Test-ShouldAutoOptimize -PercentUsed $cpuPercent `
                    -ThresholdPercent $script:autoThresholdPercent `
                    -Enabled $autoCpuCheck.Checked `
                    -CooldownUntil $script:autoCpuCooldownUntil)) {
            Invoke-Optimize ('auto CPU at {0}%' -f $script:autoThresholdPercent) 'Cpu'
            $script:autoCpuCooldownUntil = (Get-Date).AddSeconds($script:autoCooldownSeconds)
        }
    })

$toggleButton.Add_Click({
        if ($script:isActive) { Stop-Awake } else { Start-Awake }
    })

$optimizeButton.Add_Click({ Invoke-Optimize 'manual' 'Both' })

$expandButton.Add_Click({ Set-CompactMode $false })

$compactPanel.Add_DoubleClick({ Set-CompactMode $false })

$form.Add_Shown({
        $form.Activate()
        try { Set-MemoryDisplay (Get-MemoryStatus) } catch { $ramDetail.Text = 'Memory usage unavailable' }
        $script:previousCpu = Get-CpuSnapshot
        $timer.Start()
    })

$form.Add_FormClosing({
        $timer.Stop()
        Set-KeepAwake $false
    })

[void][System.Windows.Forms.Application]::Run($form)

$timer.Dispose()
