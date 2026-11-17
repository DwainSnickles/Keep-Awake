using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KeepAwake
{
    public partial class MainForm : Form
    {
        private enum OptimizeTarget
        {
            Both,
            Memory,
            Cpu
        }

        private static readonly TimeSpan AutoCooldown = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan DeltaLifetime = TimeSpan.FromSeconds(12);

        private const int AutoThresholdPercent = 70;

        private readonly ToolbarForm _toolbar;

        private ContextMenuStrip _optionsMenu;
        private ToolStripMenuItem _itemKeepAwake;
        private ToolStripMenuItem _itemRam;
        private ToolStripMenuItem _itemCpu;
        private ToolStripMenuItem _itemOptimizeNow;

        private bool _awake;
        private DateTime _awakeStarted;
        private int _tickCount;
        private CpuSnapshot _previousCpu;
        private bool _hasCpuSample;
        private double _lastRamPercent;
        private double? _lastCpuPercent;
        private DateTime _deltaUntil = DateTime.MinValue;
        private bool _optimizing;
        private DateTime _ramCooldownUntil = DateTime.MinValue;
        private DateTime _cpuCooldownUntil = DateTime.MinValue;

        public MainForm()
        {
            InitializeComponent();

            _toolbar = new ToolbarForm();
            _toolbar.ExpandRequested += Toolbar_ExpandRequested;
            _toolbar.ExitRequested += Toolbar_ExitRequested;
            _toolbar.KeepAwakeRequested += Toolbar_KeepAwakeRequested;
            _toolbar.OptionsRequested += Toolbar_OptionsRequested;

            BuildOptionsMenu();

            chkAutoRam.Checked = true;
        }

        // ---------------------------------------------------------------- startup

        private void MainForm_Shown(object sender, EventArgs e)
        {
            try
            {
                UpdateMemory(ResourceMonitor.GetMemoryStatus());
                _previousCpu = ResourceMonitor.GetCpuSnapshot();
                _hasCpuSample = true;
            }
            catch (Exception ex)
            {
                lblRamDetail.Text = "Memory usage unavailable: " + ex.Message;
            }

            StartAwake();
            timer.Start();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            timer.Stop();
            PowerKeeper.AllowSleep();
            _toolbar.Hide();
        }

        // The compact toolbar exists only while this window is minimized.
        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized && !_toolbar.Visible)
            {
                Hide();
                ShowToolbar();
            }
        }

        // ------------------------------------------------------------ live update

        private void timer_Tick(object sender, EventArgs e)
        {
            _tickCount++;

            if (_awake)
            {
                // Other applications may clear the thread state, and a long idle
                // stretch is exactly when that matters.
                if (_tickCount % 30 == 0)
                {
                    PowerKeeper.BlockSleep();
                }

                UpdateElapsed();
            }

            try
            {
                MemoryStatus memory = ResourceMonitor.GetMemoryStatus();
                UpdateMemory(memory);

                if (ResourceMonitor.ShouldAutoOptimize(memory.PercentUsed, AutoThresholdPercent, chkAutoRam.Checked, _ramCooldownUntil, DateTime.Now))
                {
                    _ramCooldownUntil = DateTime.Now.Add(AutoCooldown);
                    _ = RunOptimizeAsync("auto RAM at " + AutoThresholdPercent + "%", OptimizeTarget.Memory);
                }
            }
            catch
            {
            }

            try
            {
                CpuSnapshot snapshot = ResourceMonitor.GetCpuSnapshot();

                if (_hasCpuSample)
                {
                    double percent = ResourceMonitor.GetCpuPercent(_previousCpu, snapshot);
                    UpdateCpu(percent);

                    if (ResourceMonitor.ShouldAutoOptimize(percent, AutoThresholdPercent, chkAutoCpu.Checked, _cpuCooldownUntil, DateTime.Now))
                    {
                        _cpuCooldownUntil = DateTime.Now.Add(AutoCooldown);
                        _ = RunOptimizeAsync("auto CPU at " + AutoThresholdPercent + "%", OptimizeTarget.Cpu);
                    }
                }

                _previousCpu = snapshot;
                _hasCpuSample = true;
            }
            catch
            {
            }

            if (_deltaUntil != DateTime.MinValue && DateTime.Now >= _deltaUntil)
            {
                _deltaUntil = DateTime.MinValue;
                _toolbar.SetDelta(string.Empty);
            }
        }

        // -------------------------------------------------------- sleep blocking

        private void btnToggle_Click(object sender, EventArgs e)
        {
            if (_awake)
            {
                StopAwake();
            }
            else
            {
                StartAwake();
            }
        }

        private void StartAwake()
        {
            PowerKeeper.BlockSleep();

            _awake = true;
            _awakeStarted = DateTime.Now;
            _tickCount = 0;

            lblSleepDot.ForeColor = Color.FromArgb(74, 222, 128);
            lblSleepStatus.Text = "Awake - sleep is blocked";
            lblSleepElapsed.Text = "Keeping awake for 00:00:00";
            _toolbar.SetKeepAwakeButtonVisible(false);

            btnToggle.Text = "Stop";
            btnToggle.BackColor = Color.FromArgb(69, 74, 88);
            btnToggle.FlatAppearance.MouseOverBackColor = Color.FromArgb(84, 90, 106);
        }

        private void StopAwake()
        {
            PowerKeeper.AllowSleep();

            _awake = false;
            _awakeStarted = DateTime.MinValue;

            lblSleepDot.ForeColor = Color.FromArgb(146, 154, 170);
            lblSleepStatus.Text = "Not running";
            lblSleepElapsed.Text = "Sleep is allowed right now";
            _toolbar.SetDelta(string.Empty);
            _toolbar.SetKeepAwakeButtonVisible(true);

            btnToggle.Text = "Start";
            btnToggle.BackColor = Color.FromArgb(59, 130, 246);
            btnToggle.FlatAppearance.MouseOverBackColor = Color.FromArgb(82, 148, 250);
        }

        private void UpdateElapsed()
        {
            TimeSpan span = DateTime.Now - _awakeStarted;
            string elapsedText = string.Format("Keeping awake for {0:00}:{1:00}:{2:00}",
                (int)span.TotalHours, span.Minutes, span.Seconds);

            lblSleepElapsed.Text = elapsedText;
            // Mirror the elapsed counter onto the toolbar so the status stays
            // visible while the user is in the compact toolbar view.
            _toolbar.SetDelta(elapsedText);
        }

        // ------------------------------------------------------------- displays

        private void UpdateMemory(MemoryStatus status)
        {
            _lastRamPercent = status.PercentUsed;
            int percent = (int)Math.Round(status.PercentUsed);

            lblRam.Text = "RAM " + percent + "%";
            lblRamDetail.Text = string.Format("{0:0.0} GB of {1:0.0} GB in use, {2:0.0} GB available",
                status.UsedGb, status.TotalGb, status.AvailableGb);

            SetBar(panelRamTrack, panelRamFill, percent);
            UpdateToolbarText();
        }

        private void UpdateCpu(double percent)
        {
            _lastCpuPercent = percent;
            int rounded = (int)Math.Round(percent);

            lblCpu.Text = "CPU " + rounded + "%";
            lblCpuDetail.Text = string.Format("{0} logical processors, {1}% busy",
                Environment.ProcessorCount, rounded);

            SetBar(panelCpuTrack, panelCpuFill, rounded);
            UpdateToolbarText();
        }

        private static void SetBar(Panel track, Panel fill, int percent)
        {
            int clamped = Math.Min(100, Math.Max(0, percent));
            fill.Width = (int)(track.Width * clamped / 100.0);
            fill.BackColor = BarColor(clamped);
        }

        private static Color BarColor(int percent)
        {
            if (percent >= 85)
            {
                return Color.FromArgb(232, 100, 100);
            }

            if (percent >= AutoThresholdPercent)
            {
                return Color.FromArgb(245, 176, 65);
            }

            return Color.FromArgb(59, 130, 246);
        }

        private void UpdateToolbarText()
        {
            string ram = "RAM " + (int)Math.Round(_lastRamPercent) + "%";
            string cpu = _lastCpuPercent.HasValue
                ? "CPU " + (int)Math.Round(_lastCpuPercent.Value) + "%"
                : "CPU --";

            _toolbar.SetResource(ram + "   " + cpu);
        }

        // --------------------------------------------------------------- toolbar

        private void ShowToolbar()
        {
            if (_toolbar.HasSavedPosition)
            {
                // Keeps a remembered spot usable after a monitor is unplugged.
                _toolbar.ClampToVisibleScreen();
            }
            else
            {
                Rectangle area = Screen.FromControl(this).WorkingArea;
                _toolbar.Location = new Point(area.Right - _toolbar.Width - 16, area.Bottom - _toolbar.Height - 16);
            }

            UpdateToolbarText();
            _toolbar.SetKeepAwakeButtonVisible(!_awake);
            _toolbar.Show();
            _toolbar.BringToFront();
        }

        private void RestoreFromToolbar()
        {
            _toolbar.Hide();
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void Toolbar_ExpandRequested(object sender, EventArgs e)
        {
            RestoreFromToolbar();
        }

        private void Toolbar_KeepAwakeRequested(object sender, EventArgs e)
        {
            StartAwake();
        }

        private void Toolbar_ExitRequested(object sender, EventArgs e)
        {
            _toolbar.Hide();
            Application.Exit();
        }

        private void Toolbar_OptionsRequested(object sender, EventArgs e)
        {
            ShowOptionsMenu();
        }

        // ----------------------------------------------------------- options menu

        private void btnOptions_Click(object sender, EventArgs e)
        {
            ShowOptionsMenu();
        }

        private void ShowOptionsMenu()
        {
            RefreshOptionsMenu();
            _optionsMenu.Show(Cursor.Position);
        }

        private void BuildOptionsMenu()
        {
            _optionsMenu = new ContextMenuStrip
            {
                ShowImageMargin = false,
                BackColor = Color.FromArgb(32, 35, 43),
                ForeColor = Color.FromArgb(236, 239, 246),
                Renderer = new ToolStripProfessionalRenderer(new OptionsColorTable())
            };

            _itemKeepAwake = CreateOptionItem();
            _itemKeepAwake.Click += ItemKeepAwake_Click;

            _itemRam = CreateOptionItem();
            _itemRam.Click += ItemRam_Click;

            _itemCpu = CreateOptionItem();
            _itemCpu.Click += ItemCpu_Click;

            _itemOptimizeNow = CreateOptionItem();
            _itemOptimizeNow.Text = "Optimize now";
            _itemOptimizeNow.Click += ItemOptimizeNow_Click;

            _optionsMenu.Items.Add(_itemKeepAwake);
            _optionsMenu.Items.Add(_itemRam);
            _optionsMenu.Items.Add(_itemCpu);
            _optionsMenu.Items.Add(new ToolStripSeparator());
            _optionsMenu.Items.Add(_itemOptimizeNow);
        }

        private static ToolStripMenuItem CreateOptionItem()
        {
            return new ToolStripMenuItem
            {
                AutoSize = false,
                Size = new Size(238, 30),
                ForeColor = Color.FromArgb(236, 239, 246),
                CheckOnClick = false,
                Padding = new Padding(0, 4, 0, 4)
            };
        }

        // Refreshed every time the menu opens, so it always matches the window.
        private void RefreshOptionsMenu()
        {
            _itemKeepAwake.Checked = _awake;
            _itemKeepAwake.Text = _awake
                ? "Keep awake: on (stop)"
                : "Keep awake: off (start)";

            _itemRam.Checked = chkAutoRam.Checked;
            _itemRam.Text = chkAutoRam.Checked
                ? "RAM optimization: on (stop)"
                : "RAM optimization: off (start)";

            _itemCpu.Checked = chkAutoCpu.Checked;
            _itemCpu.Text = chkAutoCpu.Checked
                ? "CPU optimization: on (stop)"
                : "CPU optimization: off (start)";

            _itemOptimizeNow.Enabled = !_optimizing;
        }

        private void ItemKeepAwake_Click(object sender, EventArgs e)
        {
            if (_awake)
            {
                StopAwake();
            }
            else
            {
                StartAwake();
            }
        }

        private void ItemRam_Click(object sender, EventArgs e)
        {
            chkAutoRam.Checked = !chkAutoRam.Checked;
        }

        private void ItemCpu_Click(object sender, EventArgs e)
        {
            chkAutoCpu.Checked = !chkAutoCpu.Checked;
        }

        private async void ItemOptimizeNow_Click(object sender, EventArgs e)
        {
            await RunOptimizeAsync("manual", EnabledTargets());
        }

        private void chkAutoRam_CheckedChanged(object sender, EventArgs e)
        {
            if (_optionsMenu != null)
            {
                RefreshOptionsMenu();
            }
        }

        private void chkAutoCpu_CheckedChanged(object sender, EventArgs e)
        {
            if (_optionsMenu != null)
            {
                RefreshOptionsMenu();
            }
        }

        // An explicit "optimize now" runs both when nothing is switched on.
        private OptimizeTarget EnabledTargets()
        {
            if (chkAutoRam.Checked && chkAutoCpu.Checked)
            {
                return OptimizeTarget.Both;
            }

            if (chkAutoRam.Checked)
            {
                return OptimizeTarget.Memory;
            }

            if (chkAutoCpu.Checked)
            {
                return OptimizeTarget.Cpu;
            }

            return OptimizeTarget.Both;
        }

        // The menu is dark to match the window instead of the default system look.
        private sealed class OptionsColorTable : ProfessionalColorTable
        {
            private static readonly Color Surface = Color.FromArgb(32, 35, 43);
            private static readonly Color Hover = Color.FromArgb(44, 48, 58);
            private static readonly Color Split = Color.FromArgb(64, 70, 84);

            public override Color ToolStripDropDownBackground { get { return Surface; } }
            public override Color ImageMarginGradientBegin { get { return Surface; } }
            public override Color ImageMarginGradientMiddle { get { return Surface; } }
            public override Color ImageMarginGradientEnd { get { return Surface; } }
            public override Color MenuBorder { get { return Split; } }
            public override Color MenuItemBorder { get { return Hover; } }
            public override Color MenuItemSelected { get { return Hover; } }
            public override Color MenuItemSelectedGradientBegin { get { return Hover; } }
            public override Color MenuItemSelectedGradientEnd { get { return Hover; } }
            public override Color SeparatorDark { get { return Split; } }
            public override Color SeparatorLight { get { return Surface; } }
            public override Color CheckBackground { get { return Hover; } }
            public override Color CheckSelectedBackground { get { return Hover; } }
            public override Color CheckPressedBackground { get { return Hover; } }
        }

        // ------------------------------------------------------------- optimizing

        private async void btnOptimize_Click(object sender, EventArgs e)
        {
            await RunOptimizeAsync("manual", EnabledTargets());
        }

        private async Task RunOptimizeAsync(string source, OptimizeTarget target)
        {
            if (_optimizing)
            {
                return;
            }

            _optimizing = true;
            btnOptimize.Enabled = false;
            Cursor = Cursors.WaitCursor;

            try
            {
                MemoryResult memory = null;
                CpuResult cpu = null;

                // Runs off the UI thread, so the window keeps responding while the
                // process sweep and the CPU sampling window are in progress.
                await Task.Run(() =>
                {
                    if (target == OptimizeTarget.Both || target == OptimizeTarget.Memory)
                    {
                        memory = ResourceMonitor.OptimizeMemory();
                    }

                    if (target == OptimizeTarget.Both || target == OptimizeTarget.Cpu)
                    {
                        cpu = ResourceMonitor.OptimizeCpu();
                    }
                });

                if (memory != null)
                {
                    UpdateMemory(memory.After);
                }

                if (cpu != null)
                {
                    UpdateCpu(cpu.AfterPercent);
                }

                ReportResult(source, memory, cpu);
            }
            catch (Exception ex)
            {
                lblLog.Text = string.Format("{0:HH:mm:ss}  {1}: failed - {2}", DateTime.Now, source, ex.Message);
            }
            finally
            {
                _optimizing = false;
                btnOptimize.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        private void ReportResult(string source, MemoryResult memory, CpuResult cpu)
        {
            var deltaParts = new List<string>();
            var detailParts = new List<string>();

            if (memory != null)
            {
                double drop = memory.Before.PercentUsed - memory.After.PercentUsed;
                if (drop >= 1)
                {
                    deltaParts.Add("RAM reduced " + (int)Math.Round(drop) + "%");
                }

                detailParts.Add("trimmed " + memory.Trimmed + " processes");
            }

            if (cpu != null)
            {
                if (cpu.Reduction >= 1)
                {
                    deltaParts.Add("CPU reduced " + (int)Math.Round(cpu.Reduction) + "%");
                }

                detailParts.Add("deprioritized " + cpu.Deprioritized + " processes");
            }

            string stamp = DateTime.Now.ToString("HH:mm:ss");
            string detail = string.Join(", ", detailParts);

            if (deltaParts.Count > 0)
            {
                string deltaText = string.Join("   ", deltaParts);
                lblLog.Text = stamp + "  " + source + " (" + detail + "): " + deltaText;

                _deltaUntil = DateTime.Now.Add(DeltaLifetime);
                _toolbar.SetDelta(deltaText);
            }
            else
            {
                lblLog.Text = stamp + "  " + source + " (" + detail + "): no measurable change";

                _deltaUntil = DateTime.MinValue;
                _toolbar.SetDelta(string.Empty);
            }
        }
    }
}
