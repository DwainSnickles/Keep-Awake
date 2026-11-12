using System.Drawing;
using System.Windows.Forms;

namespace KeepAwake
{
    partial class ToolbarForm
    {
        private System.ComponentModel.IContainer components = null;

        private Label lblResource;
        private Label lblDelta;
        private Button btnFull;
        private Button btnExit;
        private Button btnKeepAwake;
        private Button btnOptions;
        private Timer topMostTimer;
        private ToolTip tips;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();

            lblResource = new Label();
            lblDelta = new Label();
            btnFull = new Button();
            btnExit = new Button();
            btnKeepAwake = new Button();
            btnOptions = new Button();
            topMostTimer = new Timer(components);
            tips = new ToolTip(components);

            SuspendLayout();

            // 
            // lblResource
            // 
            lblResource.AutoSize = true;
            lblResource.Font = new Font("Segoe UI", 11.5F, FontStyle.Bold);
            lblResource.ForeColor = Color.FromArgb(236, 239, 246);
            lblResource.Location = new Point(12, 8);
            lblResource.Name = "lblResource";
            lblResource.Text = "RAM --   CPU --";
            lblResource.DoubleClick += ExpandDoubleClick;
            lblResource.MouseDown += DragStart;
            lblResource.MouseMove += DragMove;
            lblResource.MouseUp += DragEnd;

            // 
            // lblDelta
            // 
            lblDelta.AutoSize = false;
            lblDelta.Font = new Font("Segoe UI", 8.25F);
            lblDelta.ForeColor = Color.FromArgb(74, 222, 128);
            lblDelta.Location = new Point(12, 32);
            lblDelta.Name = "lblDelta";
            lblDelta.Size = new Size(160, 16);
            lblDelta.Text = "";
            lblDelta.Visible = false;
            lblDelta.DoubleClick += ExpandDoubleClick;
            lblDelta.MouseDown += DragStart;
            lblDelta.MouseMove += DragMove;
            lblDelta.MouseUp += DragEnd;

            // 
            // btnFull
            // 
            btnFull.BackColor = Color.FromArgb(38, 42, 52);
            btnFull.Cursor = Cursors.Hand;
            btnFull.FlatAppearance.BorderSize = 0;
            btnFull.FlatAppearance.MouseDownBackColor = Color.FromArgb(52, 57, 70);
            btnFull.FlatAppearance.MouseOverBackColor = Color.FromArgb(44, 48, 58);
            btnFull.FlatStyle = FlatStyle.Flat;
            btnFull.Font = new Font("Segoe MDL2 Assets", 9F);
            btnFull.ForeColor = Color.FromArgb(236, 239, 246);
            btnFull.Location = new Point(254, 9);
            btnFull.Name = "btnFull";
            btnFull.Size = new Size(34, 26);
            btnFull.Text = "\uE922";
            btnFull.UseVisualStyleBackColor = false;
            btnFull.Click += btnFull_Click;

            // 
            // btnExit
            // 
            btnExit.BackColor = Color.FromArgb(38, 42, 52);
            btnExit.Cursor = Cursors.Hand;
            btnExit.FlatAppearance.BorderSize = 0;
            btnExit.FlatAppearance.MouseDownBackColor = Color.FromArgb(150, 60, 60);
            btnExit.FlatAppearance.MouseOverBackColor = Color.FromArgb(120, 52, 52);
            btnExit.FlatStyle = FlatStyle.Flat;
            btnExit.Font = new Font("Segoe MDL2 Assets", 9F);
            btnExit.ForeColor = Color.FromArgb(236, 239, 246);
            btnExit.Location = new Point(292, 9);
            btnExit.Name = "btnExit";
            btnExit.Size = new Size(34, 26);
            btnExit.Text = "\uE8BB";
            btnExit.UseVisualStyleBackColor = false;
            btnExit.Click += btnExit_Click;

            // 
            // btnKeepAwake
            // 
            btnKeepAwake.BackColor = Color.FromArgb(38, 42, 52);
            btnKeepAwake.Cursor = Cursors.Hand;
            btnKeepAwake.FlatAppearance.BorderSize = 0;
            btnKeepAwake.FlatAppearance.MouseDownBackColor = Color.FromArgb(52, 57, 70);
            btnKeepAwake.FlatAppearance.MouseOverBackColor = Color.FromArgb(44, 48, 58);
            btnKeepAwake.FlatStyle = FlatStyle.Flat;
            btnKeepAwake.Font = new Font("Segoe MDL2 Assets", 10F);
            btnKeepAwake.ForeColor = Color.FromArgb(74, 222, 128);
            btnKeepAwake.Location = new Point(178, 9);
            btnKeepAwake.Name = "btnKeepAwake";
            btnKeepAwake.Size = new Size(34, 26);
            btnKeepAwake.Text = "\uE7E8";
            btnKeepAwake.UseVisualStyleBackColor = false;
            btnKeepAwake.Click += BtnKeepAwake_Click;

            // 
            // btnOptions
            // 
            btnOptions.BackColor = Color.FromArgb(38, 42, 52);
            btnOptions.Cursor = Cursors.Hand;
            btnOptions.FlatAppearance.BorderSize = 0;
            btnOptions.FlatAppearance.MouseDownBackColor = Color.FromArgb(52, 57, 70);
            btnOptions.FlatAppearance.MouseOverBackColor = Color.FromArgb(44, 48, 58);
            btnOptions.FlatStyle = FlatStyle.Flat;
            btnOptions.Font = new Font("Segoe MDL2 Assets", 10F);
            btnOptions.ForeColor = Color.FromArgb(236, 239, 246);
            btnOptions.Location = new Point(216, 9);
            btnOptions.Name = "btnOptions";
            btnOptions.Size = new Size(34, 26);
            btnOptions.Text = "\uE713";
            btnOptions.UseVisualStyleBackColor = false;
            btnOptions.Click += btnOptions_Click;

            // 
            // tips
            // 
            tips.AutoPopDelay = 8000;
            tips.InitialDelay = 300;
            tips.ReshowDelay = 100;
            tips.ShowAlways = true;
            tips.SetToolTip(btnKeepAwake, "Keep this PC awake - blocks sleep and hibernation");
            tips.SetToolTip(btnOptions, "Options - start or stop keep awake, RAM and CPU, or optimize now");
            tips.SetToolTip(btnFull, "Show the main window");
            tips.SetToolTip(btnExit, "Exit Keep Awake and allow sleep again");

            // 
            // topMostTimer
            // 
            topMostTimer.Enabled = true;
            topMostTimer.Interval = 800;
            topMostTimer.Tick += topMostTimer_Tick;

            // 
            // ToolbarForm
            // 
            BackColor = Color.FromArgb(23, 25, 31);
            ClientSize = new Size(336, 58);
            Controls.Add(lblResource);
            Controls.Add(lblDelta);
            Controls.Add(btnFull);
            Controls.Add(btnExit);
            Controls.Add(btnKeepAwake);
            Controls.Add(btnOptions);
            Font = new Font("Segoe UI", 9F);
            ForeColor = Color.FromArgb(236, 239, 246);
            FormBorderStyle = FormBorderStyle.None;
            Icon = AppIcon.Value;
            Name = "ToolbarForm";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Text = "Keep Awake toolbar";
            TopMost = true;
            DoubleClick += ExpandDoubleClick;
            MouseDown += DragStart;
            MouseMove += DragMove;
            MouseUp += DragEnd;
            Paint += ToolbarForm_Paint;

            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
    }
}
