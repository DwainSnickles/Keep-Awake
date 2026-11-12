using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace KeepAwake
{
    internal sealed class MemoryStatus
    {
        public double PercentUsed { get; set; }
        public double TotalGb { get; set; }
        public double UsedGb { get; set; }
        public double AvailableGb { get; set; }
    }

    internal sealed class MemoryResult
    {
        public int Trimmed { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public MemoryStatus Before { get; set; }
        public MemoryStatus After { get; set; }
    }

    internal sealed class CpuResult
    {
        public int Deprioritized { get; set; }
        public int Candidates { get; set; }
        public double BeforePercent { get; set; }
        public double AfterPercent { get; set; }
        public double Reduction { get; set; }
    }

    internal struct CpuSnapshot
    {
        public ulong Idle;
        public ulong Total;
    }

    /// <summary>
    /// RAM and CPU readings plus the two actions that relieve them. Ported from
    /// the MemoryPower class in MemoryOptimizerPro.
    /// </summary>
    internal static class ResourceMonitor
    {
        private const long MinimumWorkingSetBytes = 8L * 1024 * 1024;
        private const double BytesPerGb = 1024.0 * 1024.0 * 1024.0;

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatusEx
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhys;
            public ulong AvailPhys;
            public ulong TotalPageFile;
            public ulong AvailPageFile;
            public ulong TotalVirtual;
            public ulong AvailVirtual;
            public ulong AvailExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileTime
        {
            public uint LowDateTime;
            public uint HighDateTime;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EmptyWorkingSet(IntPtr processHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

        public static MemoryStatus GetMemoryStatus()
        {
            var status = new MemoryStatusEx();
            status.Length = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));

            if (!GlobalMemoryStatusEx(ref status))
            {
                throw new InvalidOperationException("GlobalMemoryStatusEx failed.");
            }

            double total = status.TotalPhys;
            double available = status.AvailPhys;
            double used = total - available;
            double percent = total > 0 ? (used / total) * 100.0 : status.MemoryLoad;

            return new MemoryStatus
            {
                PercentUsed = percent,
                TotalGb = total / BytesPerGb,
                UsedGb = used / BytesPerGb,
                AvailableGb = available / BytesPerGb
            };
        }

        public static CpuSnapshot GetCpuSnapshot()
        {
            FileTime idle;
            FileTime kernel;
            FileTime user;

            if (!GetSystemTimes(out idle, out kernel, out user))
            {
                throw new InvalidOperationException("GetSystemTimes failed.");
            }

            return new CpuSnapshot
            {
                Idle = ToUInt64(idle),
                Total = ToUInt64(kernel) + ToUInt64(user)
            };
        }

        public static double GetCpuPercent(CpuSnapshot previous, CpuSnapshot current)
        {
            double totalDelta = current.Total - previous.Total;
            double idleDelta = current.Idle - previous.Idle;

            if (totalDelta <= 0)
            {
                return 0;
            }

            double percent = (1.0 - (idleDelta / totalDelta)) * 100.0;
            return Math.Min(100.0, Math.Max(0.0, percent));
        }

        public static double MeasureCpuPercent(int milliseconds)
        {
            CpuSnapshot first = GetCpuSnapshot();
            Thread.Sleep(milliseconds);
            CpuSnapshot second = GetCpuSnapshot();
            return Math.Round(GetCpuPercent(first, second), 1);
        }

        public static bool ShouldAutoOptimize(double percentUsed, int thresholdPercent, bool enabled, DateTime cooldownUntil, DateTime now)
        {
            if (!enabled)
            {
                return false;
            }

            if (percentUsed < thresholdPercent)
            {
                return false;
            }

            return now >= cooldownUntil;
        }

        public static MemoryResult OptimizeMemory()
        {
            var result = new MemoryResult { Before = GetMemoryStatus() };
            int currentId = Environment.ProcessId;

            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    if (process.Id == currentId)
                    {
                        result.Skipped++;
                        continue;
                    }

                    if (process.WorkingSet64 < MinimumWorkingSetBytes)
                    {
                        result.Skipped++;
                        continue;
                    }

                    if (EmptyWorkingSet(process.Handle))
                    {
                        result.Trimmed++;
                    }
                    else
                    {
                        result.Failed++;
                    }
                }
                catch
                {
                    result.Failed++;
                }
                finally
                {
                    process.Dispose();
                }
            }

            // Trimmed pages go to the standby list; give Windows a moment to
            // update the available-memory figure before measuring the result.
            Thread.Sleep(300);
            result.After = GetMemoryStatus();
            return result;
        }

        /// <summary>
        /// CPU cannot be freed the way memory can, so busy background processes
        /// are dropped to BelowNormal. The reported reduction is how much less CPU
        /// those specific processes used afterwards, which only becomes a real
        /// number when something else is competing for the CPU.
        /// </summary>
        public static CpuResult OptimizeCpu(double minimumProcessPercent = 3.0, int maxProcesses = 12)
        {
            var result = new CpuResult();
            result.BeforePercent = MeasureCpuPercent(500);

            Dictionary<int, double> beforeMap = GetProcessCpuMap(400);
            int currentId = Environment.ProcessId;

            List<KeyValuePair<int, double>> targets = beforeMap
                .Where(pair => pair.Key != currentId && pair.Value >= minimumProcessPercent)
                .OrderByDescending(pair => pair.Value)
                .Take(maxProcesses)
                .ToList();

            result.Candidates = targets.Count;

            var changed = new List<int>();
            foreach (KeyValuePair<int, double> target in targets)
            {
                try
                {
                    using (Process process = Process.GetProcessById(target.Key))
                    {
                        // Leave whatever the user is looking at alone.
                        if (process.MainWindowHandle != IntPtr.Zero)
                        {
                            continue;
                        }

                        ProcessPriorityClass priority = process.PriorityClass;
                        if (priority == ProcessPriorityClass.BelowNormal || priority == ProcessPriorityClass.Idle)
                        {
                            continue;
                        }

                        process.PriorityClass = ProcessPriorityClass.BelowNormal;
                        changed.Add(target.Key);
                    }
                }
                catch
                {
                }
            }

            result.Deprioritized = changed.Count;

            Dictionary<int, double> afterMap = GetProcessCpuMap(400);
            double reduction = 0;

            foreach (int id in changed)
            {
                double after;
                if (!afterMap.TryGetValue(id, out after))
                {
                    continue;
                }

                double beforePercent = beforeMap[id];
                if (after < beforePercent)
                {
                    reduction += beforePercent - after;
                }
            }

            result.Reduction = Math.Round(reduction, 1);
            result.AfterPercent = MeasureCpuPercent(500);
            return result;
        }

        private static Dictionary<int, double> GetProcessCpuMap(int sampleMilliseconds)
        {
            var first = new Dictionary<int, long>();

            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    first[process.Id] = process.TotalProcessorTime.Ticks;
                }
                catch
                {
                }
                finally
                {
                    process.Dispose();
                }
            }

            Thread.Sleep(sampleMilliseconds);

            int cores = Environment.ProcessorCount;
            var map = new Dictionary<int, double>();

            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    long before;
                    if (!first.TryGetValue(process.Id, out before))
                    {
                        continue;
                    }

                    long delta = process.TotalProcessorTime.Ticks - before;
                    if (delta <= 0)
                    {
                        continue;
                    }

                    map[process.Id] = (delta / 10000.0) / sampleMilliseconds * (100.0 / cores);
                }
                catch
                {
                }
                finally
                {
                    process.Dispose();
                }
            }

            return map;
        }

        private static ulong ToUInt64(FileTime value)
        {
            return ((ulong)value.HighDateTime << 32) | value.LowDateTime;
        }
    }
}
