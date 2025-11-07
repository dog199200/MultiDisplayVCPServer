using System.Drawing; // Added for Icon reference
using System.Windows.Forms; // Required for all control types

namespace MultiDisplayVCPServer
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            notifyIcon1 = new NotifyIcon(components);
            contextMenuStrip1 = new ContextMenuStrip(components);
            configureToolStripMenuItem = new ToolStripMenuItem();
            restartToolStripMenuItem = new ToolStripMenuItem();
            runOnStartupToolStripMenuItem = new ToolStripMenuItem();
            minimizeToSystemTrayToolStripMenuItem = new ToolStripMenuItem();
            exitToolStripMenuItem = new ToolStripMenuItem();
            portTextBox = new TextBox();
            portLabel = new Label();
            passwordLabel = new Label();
            passwordTextBox = new TextBox();
            runOnStartupCheckBox = new CheckBox();
            minimizeToTrayCheckBox = new CheckBox();
            showPasswordCheckBox = new CheckBox();
            imageList1 = new ImageList(components);
            contextMenuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // notifyIcon1
            // 
            notifyIcon1.ContextMenuStrip = contextMenuStrip1;
            notifyIcon1.Icon = (Icon)resources.GetObject("notifyIcon1.Icon");
            notifyIcon1.Text = "DDC/CI Server";
            notifyIcon1.Visible = true;
            notifyIcon1.DoubleClick += notifyIcon1_DoubleClick;
            // 
            // contextMenuStrip1
            // 
            contextMenuStrip1.Items.AddRange(new ToolStripItem[] { configureToolStripMenuItem, restartToolStripMenuItem, runOnStartupToolStripMenuItem, minimizeToSystemTrayToolStripMenuItem, exitToolStripMenuItem });
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.Size = new Size(204, 114);
            // 
            // configureToolStripMenuItem
            // 
            configureToolStripMenuItem.Name = "configureToolStripMenuItem";
            configureToolStripMenuItem.Size = new Size(203, 22);
            configureToolStripMenuItem.Text = "Configure";
            configureToolStripMenuItem.Click += configureToolStripMenuItem_Click;
            // 
            // restartToolStripMenuItem
            // 
            restartToolStripMenuItem.Name = "restartToolStripMenuItem";
            restartToolStripMenuItem.Size = new Size(203, 22);
            restartToolStripMenuItem.Text = "Restart";
            restartToolStripMenuItem.Click += restartToolStripMenuItem_Click;
            // 
            // runOnStartupToolStripMenuItem
            // 
            runOnStartupToolStripMenuItem.CheckOnClick = true;
            runOnStartupToolStripMenuItem.Name = "runOnStartupToolStripMenuItem";
            runOnStartupToolStripMenuItem.Size = new Size(203, 22);
            runOnStartupToolStripMenuItem.Text = "Run on Startup";
            runOnStartupToolStripMenuItem.Click += runOnStartupToolStripMenuItem_Click;
            // 
            // minimizeToSystemTrayToolStripMenuItem
            // 
            minimizeToSystemTrayToolStripMenuItem.CheckOnClick = true;
            minimizeToSystemTrayToolStripMenuItem.Name = "minimizeToSystemTrayToolStripMenuItem";
            minimizeToSystemTrayToolStripMenuItem.Size = new Size(203, 22);
            minimizeToSystemTrayToolStripMenuItem.Text = "Minimize to System Tray";
            minimizeToSystemTrayToolStripMenuItem.Click += minimizeToSystemTrayToolStripMenuItem_Click;
            // 
            // exitToolStripMenuItem
            // 
            exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            exitToolStripMenuItem.Size = new Size(203, 22);
            exitToolStripMenuItem.Text = "Exit";
            exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;
            // 
            // portTextBox
            // 
            portTextBox.Location = new Point(82, 27);
            portTextBox.Name = "portTextBox";
            portTextBox.Size = new Size(136, 23);
            portTextBox.TabIndex = 1;
            // 
            // portLabel
            // 
            portLabel.AutoSize = true;
            portLabel.Location = new Point(19, 30);
            portLabel.Name = "portLabel";
            portLabel.Size = new Size(42, 15);
            portLabel.TabIndex = 2;
            portLabel.Text = "Port #:";
            // 
            // passwordLabel
            // 
            passwordLabel.AutoSize = true;
            passwordLabel.Location = new Point(19, 59);
            passwordLabel.Name = "passwordLabel";
            passwordLabel.Size = new Size(60, 15);
            passwordLabel.TabIndex = 4;
            passwordLabel.Text = "Password:";
            // 
            // passwordTextBox
            // 
            passwordTextBox.Location = new Point(82, 59);
            passwordTextBox.Name = "passwordTextBox";
            passwordTextBox.Size = new Size(136, 23);
            passwordTextBox.TabIndex = 3;
            // 
            // runOnStartupCheckBox
            // 
            runOnStartupCheckBox.AutoSize = true;
            runOnStartupCheckBox.Location = new Point(19, 93);
            runOnStartupCheckBox.Name = "runOnStartupCheckBox";
            runOnStartupCheckBox.Size = new Size(107, 19);
            runOnStartupCheckBox.TabIndex = 6;
            runOnStartupCheckBox.Text = "Run On Startup";
            runOnStartupCheckBox.UseVisualStyleBackColor = true;
            // 
            // minimizeToTrayCheckBox
            // 
            minimizeToTrayCheckBox.AutoSize = true;
            minimizeToTrayCheckBox.Location = new Point(132, 93);
            minimizeToTrayCheckBox.Name = "minimizeToTrayCheckBox";
            minimizeToTrayCheckBox.Size = new Size(155, 19);
            minimizeToTrayCheckBox.TabIndex = 7;
            minimizeToTrayCheckBox.Text = "Minimize to System Tray";
            minimizeToTrayCheckBox.TextAlign = ContentAlignment.TopLeft;
            minimizeToTrayCheckBox.UseVisualStyleBackColor = true;
            // 
            // showPasswordCheckBox
            // 
            showPasswordCheckBox.Appearance = Appearance.Button;
            showPasswordCheckBox.AutoSize = true;
            showPasswordCheckBox.ImageIndex = 0;
            showPasswordCheckBox.ImageList = imageList1;
            showPasswordCheckBox.Location = new Point(224, 59);
            showPasswordCheckBox.Name = "showPasswordCheckBox";
            showPasswordCheckBox.Size = new Size(22, 22);
            showPasswordCheckBox.TabIndex = 8;
            showPasswordCheckBox.TextAlign = ContentAlignment.TopLeft;
            showPasswordCheckBox.UseVisualStyleBackColor = true;
            // 
            // imageList1
            // 
            imageList1.ColorDepth = ColorDepth.Depth32Bit;
            imageList1.ImageStream = (ImageListStreamer)resources.GetObject("imageList1.ImageStream");
            imageList1.TransparentColor = Color.Transparent;
            imageList1.Images.SetKeyName(0, "show.png");
            imageList1.Images.SetKeyName(1, "hide.png");
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(297, 130);
            Controls.Add(showPasswordCheckBox);
            Controls.Add(minimizeToTrayCheckBox);
            Controls.Add(runOnStartupCheckBox);
            Controls.Add(passwordLabel);
            Controls.Add(passwordTextBox);
            Controls.Add(portLabel);
            Controls.Add(portTextBox);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "MainForm";
            Text = "Multi-Display VCP Server";
            FormClosing += MainForm_FormClosing;
            Load += MainForm_Load;
            Resize += MainForm_Resize;
            contextMenuStrip1.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        // Renamed and corrected declarations
        private System.Windows.Forms.NotifyIcon notifyIcon1;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem configureToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem restartToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem runOnStartupToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem minimizeToSystemTrayToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem; // Renamed to the cleaner name
        private TextBox portTextBox;
        private Label portLabel;
        private Label passwordLabel;
        private TextBox passwordTextBox;
        private CheckBox runOnStartupCheckBox;
        private CheckBox minimizeToTrayCheckBox;
        private CheckBox showPasswordCheckBox;
        private ImageList imageList1;
    }
}
