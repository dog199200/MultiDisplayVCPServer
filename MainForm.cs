using MultiDisplayVCPServer.Properties;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Threading.Tasks; // Added for Task

namespace MultiDisplayVCPServer
{
    /// <summary>
    /// The main window for the server application.
    /// Handles user configuration, system tray icon, and server lifecycle events.
    /// </summary>
    public partial class MainForm : Form
    {
        /// <summary>
        /// A set of common network ports that the server is forbidden from using
        /// to prevent conflicts with standard services (e.g., HTTP, FTP, RDP).
        /// </summary>
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

        /// <summary>
        /// Initializes the form and its components.
        /// </summary>
        public MainForm()
        {
            try
            {
                // Upgrades application settings from a previous version, if available.
                Settings.Default.Upgrade();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Settings upgrade error: {ex.Message}");
            }

            InitializeComponent();

            // Wire up event handlers for UI controls
            portTextBox.Leave += portTextBox_Leave;
            passwordTextBox.Leave += passwordTextBox_Leave;
            runOnStartupCheckBox.CheckedChanged += runOnStartupCheckBox_CheckedChanged;
            minimizeToTrayCheckBox.CheckedChanged += minimizeToTrayCheckBox_CheckedChanged;
            this.showPasswordCheckBox.CheckedChanged += showPasswordCheckBox_CheckedChanged;

            // Subscribe to server state changes from the Program class
            Program.ServerStateChanged += OnServerStateChanged;
        }

        /// <summary>
        /// Event handler for server state changes (e.g., Running, Stopped, Restarting).
        /// Safely updates the UI from the correct thread.
        /// </summary>
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

        /// <summary>
        /// Updates the UI elements based on the server's state.
        /// Disables configuration fields when the server is restarting.
        /// </summary>
        /// <param name="state">The new server state (0=Stopped, 1=Running, 2=Restarting).</param>
        private void UpdateUIForState(int state)
        {
            bool isEnabled = (state != 2); // Disable fields if state is "Restarting"
            SetConfigurationFieldsEnabled(isEnabled);
        }

        /// <summary>
        /// Checks if a given TCP port is available to be bound.
        /// </summary>
        /// <param name="port">The port number to check.</param>
        /// <returns>True if the port is available, otherwise false.</returns>
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
                tcpListener?.Stop();
            }
        }

        /// <summary>
        /// Handles the form's Load event.
        /// Loads all saved settings into the UI controls and starts the server.
        /// </summary>
        private async void MainForm_Load(object sender, EventArgs e)
        {
            // Load saved settings into UI
            portTextBox.Text = Settings.Default.Port.ToString();
            passwordTextBox.Text = Settings.Default.Password;

            passwordTextBox.PasswordChar = '*';
            showPasswordCheckBox.Checked = false;
            showPasswordCheckBox.ImageIndex = 0;

            runOnStartupCheckBox.Checked = Settings.Default.RunOnStartup;
            minimizeToTrayCheckBox.Checked = Settings.Default.MinimizeToTray;

            // Sync tray menu item check states
            runOnStartupToolStripMenuItem.Checked = Settings.Default.RunOnStartup;
            minimizeToSystemTrayToolStripMenuItem.Checked = Settings.Default.MinimizeToTray;

            UpdateUIForState(Settings.Default.ServerState);

            // Start the server
            await RestartServerAndHandleUI();

            // Set the initial visibility of the form (hidden in tray or visible)
            SetFormInitialState();
        }

        /// <summary>
        /// Enables or disables the configuration text boxes and their visual style.
        /// </summary>
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

        /// <summary>
        /// Asynchronously calls the Program's RestartServer method and handles any exceptions
        /// by showing a user-friendly MessageBox.
        /// </summary>
        private async Task RestartServerAndHandleUI()
        {
            try
            {
                await Task.Run(() => Program.RestartServer());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Server failed to start. The port may still be in use by another application. Details: {ex.InnerException?.Message ?? ex.Message}",
                                 "Server Connection Error",
                                 MessageBoxButtons.OK,
                                 MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Sets the initial state of the form (minimized, in tray, or visible)
        /// based on the "Run on Startup" and "Minimize to Tray" settings.
        /// </summary>
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

        /// <summary>
        /// Validates the port when the user leaves the text box.
        /// Restarts the server if the port is valid and has changed.
        /// </summary>
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

        /// <summary>
        /// Validates and saves the password when the user leaves the text box.
        /// </summary>
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

        /// <summary>
        /// Handles the "Run on Startup" checkbox change.
        /// Saves the setting and updates the Windows Registry.
        /// </summary>
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

        /// <summary>
        /// Handles the "Minimize to Tray" checkbox change.
        /// Saves the setting and updates the tray icon's behavior.
        /// </summary>
        private void minimizeToTrayCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            bool newState = minimizeToTrayCheckBox.Checked;
            if (Settings.Default.MinimizeToTray != newState)
            {
                Settings.Default.MinimizeToTray = newState;
                Settings.Default.Save();
                minimizeToSystemTrayToolStripMenuItem.Checked = newState;
                // Re-evaluate form state based on new setting
                SetFormInitialState();
            }
        }

        /// <summary>
        /// Helper method to save any pending changes in the focused text box
        /// before minimizing or closing the form.
        /// </summary>
        /// <returns>True if save was successful, false if validation failed.</returns>
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

                    // If validation failed, passwordTextBox will re-gain focus.
                    if (this.ActiveControl == passwordTextBox)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Handles the form's closing event.
        /// Intercepts the close action to minimize to tray if the setting is enabled.
        /// </summary>
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

            // If user clicks "X" and "Minimize to Tray" is on, intercept the close
            if (isClosingDueToUser && shouldMinimize)
            {
                if (!SaveActiveSettings())
                {
                    e.Cancel = true; // Cancel close if validation fails
                    return;
                }

                this.Hide();
                this.ShowInTaskbar = false;
                notifyIcon1.Visible = true;
                this.WindowState = FormWindowState.Minimized;

                e.Cancel = true; // Cancel the form closing
                return;
            }

            // If not minimizing, save settings and shut down the server
            if (!SaveActiveSettings())
            {
                e.Cancel = true; // Cancel close if validation fails
                return;
            }

            Program.ShutdownServer();
            notifyIcon1.Visible = false;
        }

        /// <summary>
        /// Handles the form's resize event.
        /// Hides the form and shows the tray icon if minimized.
        /// </summary>
        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized && Settings.Default.MinimizeToTray)
            {
                if (SaveActiveSettings()) // Only hide if settings are valid
                {
                    this.Hide();
                    this.ShowInTaskbar = false;
                    notifyIcon1.Visible = true;
                }
                else
                {
                    // If validation failed (e.g., empty password), force the window to stay open
                    this.WindowState = FormWindowState.Normal;
                }
            }
        }

        /// <summary>
        /// Handles the "Configure" click from the tray icon menu.
        /// Shows the main window.
        /// </summary>
        private void configureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            notifyIcon1.Visible = false;
        }

        /// <summary>
        /// Handles the "Restart" click from the tray icon menu.
        /// </summary>
        private async void restartToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await RestartServerAndHandleUI();
        }

        /// <summary>
        /// Toggles the "Run on Startup" setting from the tray icon menu.
        /// </summary>
        private void runOnStartupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // We set it to the *new* state, which is the opposite of the current checked state
            bool newState = runOnStartupToolStripMenuItem.Checked;
            runOnStartupCheckBox.Checked = newState;
        }

        /// <summary>
        /// Toggles the "Minimize to Tray" setting from the tray icon menu.
        /// </summary>
        private void minimizeToSystemTrayToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool newState = minimizeToSystemTrayToolStripMenuItem.Checked;
            minimizeToTrayCheckBox.Checked = newState;
        }

        /// <summary>
        /// Handles the "Exit" click from the tray icon menu.
        /// Forces the "MinimizeToTray" setting off and exits the application.
        /// </summary>
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // We must set this to false, otherwise the FormClosing event
            // will just re-minimize the app instead of closing it.
            Settings.Default.MinimizeToTray = false;
            Settings.Default.Save();

            Application.Exit();
        }

        /// <summary>
        /// Handles double-clicking the tray icon (same as clicking "Configure").
        /// </summary>
        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            configureToolStripMenuItem_Click(sender, e);
        }

        /// <summary>
        /// Toggles the password visibility in the text box.
        /// </summary>
        private void showPasswordCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (showPasswordCheckBox.Checked)
            {
                passwordTextBox.PasswordChar = '\0'; // Show text
                showPasswordCheckBox.ImageIndex = 1; // "hide" icon
            }
            else
            {
                passwordTextBox.PasswordChar = '*'; // Hide text
                showPasswordCheckBox.ImageIndex = 0; // "show" icon
            }
        }
    }
}