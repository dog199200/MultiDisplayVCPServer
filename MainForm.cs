using MultiDisplayVCPServer.Properties;
using System.Net.Sockets;
using System.Net;

namespace MultiDisplayVCPServer
{
    public partial class MainForm : Form
    {
        private static readonly HashSet<int> forbiddenPorts = new HashSet<int>
        {
            20, 21,     // FTP
            22,         // SSH
            23,         // Telnet
            25,         // SMTP
            53,         // DNS
            80,         // HTTP
            110,        // POP3
            143,        // IMAP
            443,        // HTTPS
            3306,       // MySQL
            3389,       // RDP
            5432,       // PostgreSQL
            5900,       // VNC
            8080,       // HTTP-alt
            8443        // HTTPS-alt
        };

        public MainForm()
        {
            try
            {
                Settings.Default.Upgrade();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Settings upgrade error: {ex.Message}");
            }

            InitializeComponent();
            portTextBox.Leave += portTextBox_Leave;
            passwordTextBox.Leave += passwordTextBox_Leave;
            runOnStartupCheckBox.CheckedChanged += runOnStartupCheckBox_CheckedChanged;
            minimizeToTrayCheckBox.CheckedChanged += minimizeToTrayCheckBox_CheckedChanged;
            this.showPasswordCheckBox.CheckedChanged += showPasswordCheckBox_CheckedChanged;

            Program.ServerStateChanged += OnServerStateChanged;
        }

        private void OnServerStateChanged(object sender, int newState)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => UpdateUIForState(newState)));
            }
            else
            {
                UpdateUIForState(newState);
            }
        }

        private void UpdateUIForState(int state)
        {
            bool isEnabled = (state != 2);
            SetConfigurationFieldsEnabled(isEnabled);
        }

        private bool IsPortAvailable(int port)
        {
            TcpListener tcpListener = null;
            try
            {
                tcpListener = new TcpListener(IPAddress.Any, port);
                tcpListener.Start();
                return true; 
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Port check failed for port {port}: {ex.Message}");
                return false; 
            }
            finally
            {
                if (tcpListener != null)
                {
                    tcpListener.Stop();
                }
            }
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            portTextBox.Text = Settings.Default.Port.ToString();
            passwordTextBox.Text = Settings.Default.Password;

            passwordTextBox.PasswordChar = '*';
            showPasswordCheckBox.Checked = false;
            showPasswordCheckBox.ImageIndex = 0;

            runOnStartupCheckBox.Checked = Settings.Default.RunOnStartup;
            minimizeToTrayCheckBox.Checked = Settings.Default.MinimizeToTray;

            runOnStartupToolStripMenuItem.Checked = Settings.Default.RunOnStartup;
            minimizeToSystemTrayToolStripMenuItem.Checked = Settings.Default.MinimizeToTray;

            UpdateUIForState(Settings.Default.ServerState);

            await RestartServerAndHandleUI();

            SetFormInitialState();
        }

        private void SetConfigurationFieldsEnabled(bool enabled)
        {
            portTextBox.Enabled = enabled;
            passwordTextBox.Enabled = enabled;
            showPasswordCheckBox.Enabled = enabled;

            if (enabled)
            {
                portTextBox.BackColor = System.Drawing.SystemColors.Window;
                passwordTextBox.BackColor = System.Drawing.SystemColors.Window;
            }
            else
            {
                portTextBox.BackColor = System.Drawing.Color.LightGray;
                passwordTextBox.BackColor = System.Drawing.Color.LightGray;
            }
        }

        private async Task RestartServerAndHandleUI()
        {
            try
            {
                await Task.Run(() => Program.RestartServer());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Server failed to start. The port may still be in use by another application. Details: {ex.InnerException?.Message ?? ex.Message}",
                                 "Server ConnectionError",
                                 MessageBoxButtons.OK,
                                 MessageBoxIcon.Error);
            }
        }

        private void SetFormInitialState()
        {
            bool runOnStartup = Settings.Default.RunOnStartup;
            bool minimizeToTray = Settings.Default.MinimizeToTray;

            if (runOnStartup)
            {
                if (minimizeToTray)
                {
                    this.Hide();
                    this.ShowInTaskbar = false;
                    notifyIcon1.Visible = true;
                    this.WindowState = FormWindowState.Minimized;
                }
                else
                {
                    this.ShowInTaskbar = true;
                    notifyIcon1.Visible = false;
                    this.WindowState = FormWindowState.Minimized;
                }
            }
            else
            {
                this.Show();
                this.ShowInTaskbar = true;
                notifyIcon1.Visible = false;
                this.WindowState = FormWindowState.Normal;
            }
        }

        private async void portTextBox_Leave(object sender, EventArgs e)
        {
            int currentPort = Settings.Default.Port;

            if (!int.TryParse(portTextBox.Text, out int newPort))
            {
                MessageBox.Show("Please enter a valid whole number for the port.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                portTextBox.Text = currentPort.ToString();
                return;
            }

            if (newPort < 1 || newPort > 65535)
            {
                MessageBox.Show("Port number must be between 1 and 65535.", "Port Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                portTextBox.Text = currentPort.ToString();
                return;
            }

            if (forbiddenPorts.Contains(newPort))
            {
                MessageBox.Show($"Port {newPort} is a common service port (like HTTP, FTP, etc.) and cannot be used. Please choose a different port.",
                                "Port Configuration Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                portTextBox.Text = currentPort.ToString();
                return;
            }

            bool portChanged = currentPort != newPort;

            if (portChanged)
            {
                if (!IsPortAvailable(newPort))
                {
                    MessageBox.Show($"Port {newPort} is already in use by another application. Please choose a different port.", "Port Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    portTextBox.Text = currentPort.ToString();
                    return;
                }

                Settings.Default.Port = newPort;
                Settings.Default.Save();
                Console.WriteLine($"Application settings saved. New active port: {newPort}.");

                await RestartServerAndHandleUI();
            }
        }

        private void passwordTextBox_Leave(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(passwordTextBox.Text))
            {
                MessageBox.Show("Please enter a password for security.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                if (this.CanFocus)
                {
                    passwordTextBox.Focus();
                }
                return;
            }

            if (Settings.Default.Password != passwordTextBox.Text)
            {
                Settings.Default.Password = passwordTextBox.Text;
                Settings.Default.Save();
            }
        }

        private void runOnStartupCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            bool newState = runOnStartupCheckBox.Checked;
            if (Settings.Default.RunOnStartup != newState)
            {
                Settings.Default.RunOnStartup = newState;
                Settings.Default.Save();
                Program.SetStartup(newState);
                runOnStartupToolStripMenuItem.Checked = newState;
            }
        }

        private void minimizeToTrayCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            bool newState = minimizeToTrayCheckBox.Checked;
            if (Settings.Default.MinimizeToTray != newState)
            {
                Settings.Default.MinimizeToTray = newState;
                Settings.Default.Save();
                minimizeToSystemTrayToolStripMenuItem.Checked = newState;
                SetFormInitialState();
            }
        }

        private bool SaveActiveSettings()
        {
            if (this.ActiveControl is TextBox activeTextBox)
            {
                if (activeTextBox == portTextBox)
                {
                    portTextBox_Leave(activeTextBox, EventArgs.Empty);
                }
                else if (activeTextBox == passwordTextBox)
                {
                    passwordTextBox_Leave(activeTextBox, EventArgs.Empty);

                    if (this.ActiveControl == passwordTextBox)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            bool isClosingDueToUser = e.CloseReason == CloseReason.UserClosing;
            bool shouldMinimize = minimizeToTrayCheckBox.Checked;

            if (Settings.Default.MinimizeToTray != shouldMinimize)
            {
                Settings.Default.MinimizeToTray = shouldMinimize;
                Settings.Default.Save();
                minimizeToSystemTrayToolStripMenuItem.Checked = shouldMinimize;
            }

            if (isClosingDueToUser && shouldMinimize)
            {
                SaveActiveSettings();

                this.Hide();
                this.ShowInTaskbar = false;
                notifyIcon1.Visible = true;
                this.WindowState = FormWindowState.Minimized;

                e.Cancel = true;
                return;
            }

            if (!SaveActiveSettings())
            {
                e.Cancel = true;
                return;
            }

            Program.ShutdownServer();
            notifyIcon1.Visible = false;
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized && Settings.Default.MinimizeToTray)
            {
                SaveActiveSettings();

                this.Hide();
                this.ShowInTaskbar = false;
                notifyIcon1.Visible = true;
            }
        }

        private void configureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            notifyIcon1.Visible = false;
        }

        private async void restartToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await RestartServerAndHandleUI();
        }

        private void runOnStartupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool newState = !runOnStartupToolStripMenuItem.Checked;
            runOnStartupCheckBox.Checked = newState;
        }



        private void minimizeToSystemTrayToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool newState = !minimizeToSystemTrayToolStripMenuItem.Checked;
            minimizeToTrayCheckBox.Checked = newState;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.MinimizeToTray = false;
            Settings.Default.Save();

            Application.Exit();
        }



        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            configureToolStripMenuItem_Click(sender, e);
        }

        private void showPasswordCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (showPasswordCheckBox.Checked)
            {
                passwordTextBox.PasswordChar = '\0';
                showPasswordCheckBox.ImageIndex = 1;
            }
            else
            {
                passwordTextBox.PasswordChar = '*';
                showPasswordCheckBox.ImageIndex = 0;
            }
        }
    }
}