using System.Drawing;
using System.Windows.Forms;

namespace KeepAwake
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private Label lblTitle;
        private Label lblSubtitle;
        private Panel panelSleep;
        private Label lblSleepDot;
        private Label lblSleepStatus;
        private Label lblSleepElapsed;
        private Panel panelResources;
        private Label lblRam;
        private Label lblRamDetail;
        private Panel panelRamTrack;
        private Panel panelRamFill;
        private CheckBox chkAutoRam;
        private Label lblCpu;
        private Label lblCpuDetail;
        private Panel panelCpuTrack;
        private Panel panelCpuFill;
        private CheckBox chkAutoCpu;
        private Button btnOptimize;
        private Button btnOptions;
        private Button btnToggle;
        private Label lblLog;
        private Label lblFooter;
        private Timer timer;
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

            lblTitle = new Label();
            lblSubtitle = new Label();
            panelSleep = new Panel();
            lblSleepDot = new Label();
            lblSleepStatus = new Label();
            lblSleepElapsed = new Label();
            panelResources = new Panel();
            lblRam = new Label();
            lblRamDetail = new Label();
            panelRamTrack = new Panel();
            panelRamFill = new Panel();
            chkAutoRam = new CheckBox();
            lblCpu = new Label();
            lblCpuDetail = new Label();
            panelCpuTrack = new Panel();
            panelCpuFill = new Panel();
            chkAutoCpu = new CheckBox();
            btnOptimize = new Button();
            btnOptions = new Button();
            btnToggle = new Button();
            lblLog = new Label();
            lblFooter = new Label();
            timer = new Timer(components);
            tips = new ToolTip(components);

            panelSleep.SuspendLayout();
            panelResources.SuspendLayout();
            panelRamTrack.SuspendLayout();
            panelCpuTrack.SuspendLayout();
            SuspendLayout();

            // 
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(236, 239, 246);
            lblTitle.Location = new Point(24, 16);
            lblTitle.Name = "lblTitle";
            lblTitle.Text = "Keep Awake";

            // 
            // lblSubtitle
            // 
            lblSubtitle.Font = new Font("Segoe UI", 9F);
            lblSubtitle.ForeColor = Color.FromArgb(146, 154, 170);
            lblSubtitle.Location = new Point(26, 54);
            lblSubtitle.Name = "lblSubtitle";
            lblSubtitle.Size = new Size(388, 36);
            lblSubtitle.Text = "Blocks sleep and hibernation. Your screensaver and screen timeout keep working as usual.";

            // 
            // panelSleep
            // 
            panelSleep.BackColor = Color.FromArgb(32, 35, 43);
            panelSleep.Controls.Add(lblSleepDot);
            panelSleep.Controls.Add(lblSleepStatus);
            panelSleep.Controls.Add(lblSleepElapsed);
            panelSleep.Location = new Point(24, 98);
            panelSleep.Name = "panelSleep";
            panelSleep.Size = new Size(392, 68);

            // 
            // lblSleepDot
            // 
            lblSleepDot.AutoSize = true;
            lblSleepDot.Font = new Font("Segoe UI", 12F);
            lblSleepDot.ForeColor = Color.FromArgb(146, 154, 170);
            lblSleepDot.Location = new Point(14, 19);
            lblSleepDot.Name = "lblSleepDot";
            lblSleepDot.Text = "\u25CF";

            // 
            // lblSleepStatus
            // 
            lblSleepStatus.AutoSize = true;
            lblSleepStatus.Font = new Font("Segoe UI", 11.5F, FontStyle.Bold);
            lblSleepStatus.ForeColor = Color.FromArgb(236, 239, 246);
            lblSleepStatus.Location = new Point(38, 17);
            lblSleepStatus.Name = "lblSleepStatus";
            lblSleepStatus.Text = "Not running";

            // 
            // lblSleepElapsed
            // 
            lblSleepElapsed.AutoSize = true;
            lblSleepElapsed.Font = new Font("Segoe UI", 8.75F);
            lblSleepElapsed.ForeColor = Color.FromArgb(146, 154, 170);
            lblSleepElapsed.Location = new Point(40, 41);
            lblSleepElapsed.Name = "lblSleepElapsed";
            lblSleepElapsed.Text = "Sleep is allowed right now";

            // 
            // panelResources
            // 
            panelResources.BackColor = Color.FromArgb(32, 35, 43);
            panelResources.Controls.Add(lblRam);
            panelResources.Controls.Add(lblRamDetail);
            panelResources.Controls.Add(panelRamTrack);
            panelResources.Controls.Add(chkAutoRam);
            panelResources.Controls.Add(lblCpu);
            panelResources.Controls.Add(lblCpuDetail);
            panelResources.Controls.Add(panelCpuTrack);
            panelResources.Controls.Add(chkAutoCpu);
            panelResources.Location = new Point(24, 178);
            panelResources.Name = "panelResources";
            panelResources.Size = new Size(392, 176);

            // 
            // lblRam
            // 
            lblRam.AutoSize = true;
            lblRam.Font = new Font("Segoe UI", 11.5F, FontStyle.Bold);
            lblRam.ForeColor = Color.FromArgb(236, 239, 246);
            lblRam.Location = new Point(14, 10);
            lblRam.Name = "lblRam";
            lblRam.Text = "RAM --";

            // 
            // lblRamDetail
            // 
            lblRamDetail.AutoSize = true;
            lblRamDetail.Font = new Font("Segoe UI", 8.5F);
            lblRamDetail.ForeColor = Color.FromArgb(146, 154, 170);
            lblRamDetail.Location = new Point(16, 32);
            lblRamDetail.Name = "lblRamDetail";
            lblRamDetail.Text = "Reading memory usage...";

            // 
            // panelRamTrack
            // 
            panelRamTrack.BackColor = Color.FromArgb(52, 57, 70);
            panelRamTrack.Controls.Add(panelRamFill);
            panelRamTrack.Location = new Point(14, 54);
            panelRamTrack.Name = "panelRamTrack";
            panelRamTrack.Size = new Size(364, 8);

            // 
            // panelRamFill
            // 
            panelRamFill.BackColor = Color.FromArgb(59, 130, 246);
            panelRamFill.Location = new Point(0, 0);
            panelRamFill.Name = "panelRamFill";
            panelRamFill.Size = new Size(0, 8);

            // 
            // chkAutoRam
            // 
            chkAutoRam.AutoSize = true;
            chkAutoRam.BackColor = Color.FromArgb(32, 35, 43);
            chkAutoRam.Checked = true;
            chkAutoRam.CheckState = CheckState.Checked;
            chkAutoRam.FlatStyle = FlatStyle.Flat;
            chkAutoRam.Font = new Font("Segoe UI", 8.5F);
            chkAutoRam.ForeColor = Color.FromArgb(236, 239, 246);
            chkAutoRam.Location = new Point(13, 68);
            chkAutoRam.Name = "chkAutoRam";
            chkAutoRam.Text = "Auto-optimize RAM at 70%";
            chkAutoRam.UseVisualStyleBackColor = false;
            chkAutoRam.CheckedChanged += chkAutoRam_CheckedChanged;

            // 
            // lblCpu
            // 
            lblCpu.AutoSize = true;
            lblCpu.Font = new Font("Segoe UI", 11.5F, FontStyle.Bold);
            lblCpu.ForeColor = Color.FromArgb(236, 239, 246);
            lblCpu.Location = new Point(14, 92);
            lblCpu.Name = "lblCpu";
            lblCpu.Text = "CPU --";

            // 
            // lblCpuDetail
            // 
            lblCpuDetail.AutoSize = true;
            lblCpuDetail.Font = new Font("Segoe UI", 8.5F);
            lblCpuDetail.ForeColor = Color.FromArgb(146, 154, 170);
            lblCpuDetail.Location = new Point(16, 114);
            lblCpuDetail.Name = "lblCpuDetail";
            lblCpuDetail.Text = "Measuring CPU usage...";

            // 
            // panelCpuTrack
            // 
            panelCpuTrack.BackColor = Color.FromArgb(52, 57, 70);
            panelCpuTrack.Controls.Add(panelCpuFill);
            panelCpuTrack.Location = new Point(14, 136);
            panelCpuTrack.Name = "panelCpuTrack";
            panelCpuTrack.Size = new Size(364, 8);

            // 
            // panelCpuFill
            // 
            panelCpuFill.BackColor = Color.FromArgb(59, 130, 246);
            panelCpuFill.Location = new Point(0, 0);
            panelCpuFill.Name = "panelCpuFill";
            panelCpuFill.Size = new Size(0, 8);

            // 
            // chkAutoCpu
            // 
            chkAutoCpu.AutoSize = true;
            chkAutoCpu.BackColor = Color.FromArgb(32, 35, 43);
            chkAutoCpu.Checked = true;
            chkAutoCpu.CheckState = CheckState.Checked;
            chkAutoCpu.FlatStyle = FlatStyle.Flat;
            chkAutoCpu.Font = new Font("Segoe UI", 8.5F);
            chkAutoCpu.ForeColor = Color.FromArgb(236, 239, 246);
            chkAutoCpu.Location = new Point(13, 150);
            chkAutoCpu.Name = "chkAutoCpu";
            chkAutoCpu.Text = "Auto-optimize CPU at 70%";
            chkAutoCpu.UseVisualStyleBackColor = false;
            chkAutoCpu.CheckedChanged += chkAutoCpu_CheckedChanged;

            // 
            // btnOptimize
            // 
            btnOptimize.BackColor = Color.FromArgb(38, 42, 52);
            btnOptimize.Cursor = Cursors.Hand;
            btnOptimize.FlatAppearance.BorderSize = 0;
            btnOptimize.FlatAppearance.MouseDownBackColor = Color.FromArgb(52, 57, 70);
            btnOptimize.FlatAppearance.MouseOverBackColor = Color.FromArgb(44, 48, 58);
            btnOptimize.FlatStyle = FlatStyle.Flat;
            btnOptimize.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnOptimize.ForeColor = Color.FromArgb(236, 239, 246);
            btnOptimize.Location = new Point(24, 368);
            btnOptimize.Name = "btnOptimize";
            btnOptimize.Size = new Size(392, 40);
            btnOptimize.Text = "Optimize RAM and CPU now";
            btnOptimize.UseVisualStyleBackColor = false;
            btnOptimize.Click += btnOptimize_Click;

            // 
            // btnOptions
            // 
            btnOptions.BackColor = Color.FromArgb(38, 42, 52);
            btnOptions.Cursor = Cursors.Hand;
            btnOptions.FlatAppearance.BorderSize = 0;
            btnOptions.FlatAppearance.MouseDownBackColor = Color.FromArgb(52, 57, 70);
            btnOptions.FlatAppearance.MouseOverBackColor = Color.FromArgb(44, 48, 58);
            btnOptions.FlatStyle = FlatStyle.Flat;
            btnOptions.Font = new Font("Segoe MDL2 Assets", 12F);
            btnOptions.ForeColor = Color.FromArgb(236, 239, 246);
            btnOptions.Location = new Point(384, 14);
            btnOptions.Name = "btnOptions";
            btnOptions.Size = new Size(32, 32);
            btnOptions.Text = "\uE713";
            btnOptions.UseVisualStyleBackColor = false;
            btnOptions.Click += btnOptions_Click;

            // 
            // btnToggle
            // 
            btnToggle.BackColor = Color.FromArgb(59, 130, 246);
            btnToggle.Cursor = Cursors.Hand;
            btnToggle.FlatAppearance.BorderSize = 0;
            btnToggle.FlatAppearance.MouseDownBackColor = Color.FromArgb(59, 130, 246);
            btnToggle.FlatAppearance.MouseOverBackColor = Color.FromArgb(82, 148, 250);
            btnToggle.FlatStyle = FlatStyle.Flat;
            btnToggle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            btnToggle.ForeColor = Color.White;
            btnToggle.Location = new Point(24, 418);
            btnToggle.Name = "btnToggle";
            btnToggle.Size = new Size(392, 50);
            btnToggle.Text = "Start";
            btnToggle.UseVisualStyleBackColor = false;
            btnToggle.Click += btnToggle_Click;

            // 
            // lblLog
            // 
            lblLog.Font = new Font("Segoe UI", 8.25F);
            lblLog.ForeColor = Color.FromArgb(146, 154, 170);
            lblLog.Location = new Point(24, 478);
            lblLog.Name = "lblLog";
            lblLog.Size = new Size(392, 34);
            lblLog.Text = "No optimization run yet.";

            // 
            // lblFooter
            // 
            lblFooter.AutoSize = true;
            lblFooter.Font = new Font("Segoe UI", 8.25F);
            lblFooter.ForeColor = Color.FromArgb(112, 119, 133);
            lblFooter.Location = new Point(24, 520);
            lblFooter.Name = "lblFooter";
            lblFooter.Text = "Minimize this window to get the small RAM and CPU toolbar.";

            // 
            // tips
            // 
            tips.AutoPopDelay = 8000;
            tips.InitialDelay = 300;
            tips.ReshowDelay = 100;
            tips.ShowAlways = true;
            tips.SetToolTip(btnOptions, "Options - start or stop keep awake, RAM and CPU, or optimize now");

            // 
            // timer
            // 
            timer.Interval = 1000;
            timer.Tick += timer_Tick;

            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(23, 25, 31);
            ClientSize = new Size(440, 548);
            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(panelSleep);
            Controls.Add(panelResources);
            Controls.Add(btnOptimize);
            Controls.Add(btnOptions);
            Controls.Add(btnToggle);
            Controls.Add(lblLog);
            Controls.Add(lblFooter);
            Font = new Font("Segoe UI", 9F);
            ForeColor = Color.FromArgb(236, 239, 246);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = AppIcon.Value;
            MaximizeBox = false;
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Keep Awake";
            FormClosing += MainForm_FormClosing;
            Resize += MainForm_Resize;
            Shown += MainForm_Shown;

            panelRamTrack.ResumeLayout(false);
            panelCpuTrack.ResumeLayout(false);
            panelResources.ResumeLayout(false);
            panelResources.PerformLayout();
            panelSleep.ResumeLayout(false);
            panelSleep.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
    }
}
