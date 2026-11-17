using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace KeepAwake
{
    public partial class ToolbarForm : Form
    {
        public event EventHandler ExpandRequested;
        public event EventHandler ExitRequested;
        public event EventHandler KeepAwakeRequested;
        public event EventHandler OptionsRequested;

        private const uint MonitorDefaultToNearest = 2;

        private bool _dragging;
        private Point _dragStart;
        private Point _formStart;

        // The strip is two stacked rows: the readings bar, then a status line that only needs
        // to be as wide as the text on it. The window is shaped to that text, so the empty
        // block that used to sit to its right is no longer part of the window at all and
        // clicks there reach whatever is underneath.
        private const int DesignHeight = 58;
        private const int ReadingsRowHeight = 40;
        private const int StatusPadding = 12;
        private const float StatusCornerRadius = 8f;

        private readonly int _readingsHeight;
        private readonly int _statusHeight;
        private int _statusWidth;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int Size;
            public RECT Monitor;
            public RECT Work;
            public uint Flags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassNameW(IntPtr hwnd, StringBuilder name, int count);

        public ToolbarForm()
        {
            InitializeComponent();

            int fullHeight = ClientSize.Height;
            _readingsHeight = (int)Math.Round(fullHeight * (double)ReadingsRowHeight / DesignHeight);
            _statusHeight = fullHeight - _readingsHeight;

            RestoreSavedPosition();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            UpdateStripShape();
        }

        public bool HasSavedPosition { get; private set; }

        public void SetResource(string text)
        {
            lblResource.Text = text;
        }

        public void SetDelta(string text)
        {
            lblDelta.Text = text ?? string.Empty;
            lblDelta.Visible = lblDelta.Text.Length > 0;
            UpdateStripShape();
        }

        // Keeps the window snug around what it shows: the readings row, plus the status row
        // trimmed to its text (with a rounded step where the row ends).
        private void UpdateStripShape()
        {
            bool showStatus = lblDelta.Visible && lblDelta.Text.Length > 0;

            _statusWidth = 0;
            if (showStatus)
            {
                int textWidth = TextRenderer.MeasureText(lblDelta.Text, lblDelta.Font).Width;
                _statusWidth = Math.Min(Width, lblDelta.Left + textWidth + StatusPadding);
                lblDelta.SetBounds(
                    lblDelta.Left,
                    _readingsHeight,
                    Math.Max(1, _statusWidth - lblDelta.Left - StatusPadding),
                    lblDelta.Height);
            }

            int height = _readingsHeight + (showStatus ? _statusHeight : 0);
            if (Height != height)
            {
                // The strip grows downwards; the spot it was parked in does not move.
                ClientSize = new Size(Width, height);
                ClampToVisibleScreen();
            }

            using (GraphicsPath shape = CreateStripShape(0f))
            {
                Region previous = Region;
                Region = new Region(shape);
                previous?.Dispose();
            }

            Invalidate();
        }

        private GraphicsPath CreateStripShape(float inset)
        {
            float left = inset;
            float top = inset;
            float right = Width - inset;
            float bottom = Height - inset;
            float rowBottom = _readingsHeight - inset;
            float step = _statusWidth - inset;
            float radius = StatusCornerRadius;

            GraphicsPath shape = new GraphicsPath();

            // With no status line - or one long enough to fill the strip - it is a plain rectangle.
            if (_statusWidth <= 0 || step <= left + radius || step + radius >= right)
            {
                shape.AddRectangle(new RectangleF(left, top, right - left, bottom - top));
                return shape;
            }

            shape.AddLine(left, top, right, top);
            shape.AddLine(right, top, right, rowBottom);
            shape.AddLine(right, rowBottom, step + radius, rowBottom);
            shape.AddArc(step, rowBottom, radius * 2f, radius * 2f, 270f, -90f);
            shape.AddLine(step, rowBottom + radius, step, bottom);
            shape.AddLine(step, bottom, left, bottom);
            shape.AddLine(left, bottom, left, top);
            shape.CloseFigure();

            return shape;
        }

        // Dragging works anywhere on the strip, including over the labels, because
        // a borderless window has no title bar to grab.
        private void DragStart(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            _dragging = true;
            _dragStart = Cursor.Position;
            _formStart = Location;
        }

        private void DragMove(object sender, MouseEventArgs e)
        {
            if (!_dragging)
            {
                return;
            }

            Point current = Cursor.Position;
            Location = new Point(
                _formStart.X + (current.X - _dragStart.X),
                _formStart.Y + (current.Y - _dragStart.Y));
        }

        private void DragEnd(object sender, MouseEventArgs e)
        {
            if (!_dragging)
            {
                return;
            }

            _dragging = false;

            // Letting go past the edge of a screen should not strand the strip half
            // off it, and it keeps the remembered spot usable next launch.
            ClampToVisibleScreen();
            SavePosition();
        }

        private void ExpandDoubleClick(object sender, EventArgs e)
        {
            ExpandRequested?.Invoke(this, EventArgs.Empty);
        }

        private void btnFull_Click(object sender, EventArgs e)
        {
            ExpandRequested?.Invoke(this, EventArgs.Empty);
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }

        private void BtnKeepAwake_Click(object sender, EventArgs e)
        {
            KeepAwakeRequested?.Invoke(this, EventArgs.Empty);
        }

        private void btnOptions_Click(object sender, EventArgs e)
        {
            OptionsRequested?.Invoke(this, EventArgs.Empty);
        }

        public void SetKeepAwakeButtonVisible(bool visible)
        {
            btnKeepAwake.Visible = visible;
        }

        private void ToolbarForm_Paint(object sender, PaintEventArgs e)
        {
            // The outline follows the shaped window, so the step under the readings row reads
            // as an edge rather than a cut-off.
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (GraphicsPath shape = CreateStripShape(1f))
            using (Pen pen = new Pen(Color.FromArgb(64, 70, 84)))
            {
                e.Graphics.DrawPath(pen, shape);
            }
        }

        // ------------------------------------------------------------ remembering

        // The strip should come back where it was left, so the spot is kept in
        // %APPDATA%\KeepAwake\toolbar.txt rather than in the registry.
        private static readonly string StateDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KeepAwake");

        private static readonly string PositionPath = Path.Combine(StateDirectory, "toolbar.txt");

        public void SavePosition()
        {
            try
            {
                Directory.CreateDirectory(StateDirectory);
                File.WriteAllText(PositionPath, Location.X + "," + Location.Y);
            }
            catch
            {
                // Remembering the spot is only a convenience; never break the app for it.
            }
        }

        public void ClampToVisibleScreen()
        {
            Location = ClampToVisibleScreen(Location);
        }

        private void RestoreSavedPosition()
        {
            Point saved;
            if (!TryLoadSavedPosition(out saved))
            {
                return;
            }

            HasSavedPosition = true;
            Location = ClampToVisibleScreen(saved);
        }

        private static bool TryLoadSavedPosition(out Point point)
        {
            point = Point.Empty;

            try
            {
                if (!File.Exists(PositionPath))
                {
                    return false;
                }

                string[] parts = File.ReadAllText(PositionPath).Trim().Split(',');
                int x;
                int y;

                if (parts.Length != 2
                    || !int.TryParse(parts[0].Trim(), out x)
                    || !int.TryParse(parts[1].Trim(), out y))
                {
                    return false;
                }

                point = new Point(x, y);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private Point ClampToVisibleScreen(Point target)
        {
            Rectangle bounds = SystemInformation.VirtualScreen;
            int maxX = bounds.Right - Width;
            int maxY = bounds.Bottom - Height;

            int x = Math.Max(bounds.Left, Math.Min(target.X, maxX));
            int y = Math.Max(bounds.Top, Math.Min(target.Y, maxY));

            return new Point(x, y);
        }

        // The strip floats above ordinary windows, but it must not sit on top of
        // an app that is maximized or running fullscreen - those own the screen.
        private void topMostTimer_Tick(object sender, EventArgs e)
        {
            bool wantTopMost = !IsForegroundAppCoveringScreen();

            if (TopMost != wantTopMost)
            {
                TopMost = wantTopMost;
            }
        }

        private bool IsForegroundAppCoveringScreen()
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero || foreground == Handle)
            {
                return false;
            }

            uint processId;
            GetWindowThreadProcessId(foreground, out processId);
            if (processId == (uint)Environment.ProcessId)
            {
                return false;
            }

            var className = new StringBuilder(64);
            GetClassNameW(foreground, className, className.Capacity);
            string shell = className.ToString();

            // The desktop and the taskbar cover the screen but are not apps.
            if (shell == "Progman" || shell == "WorkerW" || shell == "Shell_TrayWnd")
            {
                return false;
            }

            RECT window;
            if (!GetWindowRect(foreground, out window))
            {
                return false;
            }

            IntPtr monitor = MonitorFromWindow(foreground, MonitorDefaultToNearest);
            var info = new MONITORINFO();
            info.Size = Marshal.SizeOf(typeof(MONITORINFO));

            if (!GetMonitorInfo(monitor, ref info))
            {
                return false;
            }

            // A maximized window covers the work area; a fullscreen one covers the
            // whole monitor. Either way the toolbar steps out of the way.
            return window.Left <= info.Work.Left
                && window.Top <= info.Work.Top
                && window.Right >= info.Work.Right
                && window.Bottom >= info.Work.Bottom;
        }
    }
}
