using Microsoft.Win32;
using MultiDisplayVCPServer.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MultiDisplayVCPServer
{
    /// <summary>
    /// The main window for the server application.
    /// Handles user configuration, system tray icon, and server lifecycle events.
    /// </summary>
    public partial class MainForm : Form
    {
        // --- FIX: (IDE0090) 'new' expression can be simplified ---
        private static readonly HttpClient httpClient = new();
        private const string GitHubApiUrl = "https://api.github.com/repos/dog199200/MultiDisplayVCPServer/releases/latest";

        /// <summary>
        /// A set of common network ports that the server is forbidden from using
        /// to prevent conflicts with standard services (e.g., HTTP, FTP, RDP).
        /// </summary>
        // --- FIX: (IDE0028 & IDE0090) Collection initialization can be simplified ---
        private static readonly HashSet<int> forbiddenPorts = new()
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
            Debug.WriteLine("MainForm() constructor started.");
            try
            {
                Debug.WriteLine("Attempting settings upgrade...");
                Settings.Default.Upgrade();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Settings upgrade error: {ex.Message}");
            }

            InitializeComponent();
            Debug.WriteLine("Components initialized.");

            // Wire up event handlers for UI controls
            // --- FIX: (IDE1006) Naming rule violation ---
            portTextBox.Leave += PortTextBox_Leave;
            passwordTextBox.Leave += PasswordTextBox_Leave;
            runOnStartupCheckBox.CheckedChanged += RunOnStartupCheckBox_CheckedChanged;
            minimizeToTrayCheckBox.CheckedChanged += MinimizeToTrayCheckBox_CheckedChanged;
            this.showPasswordCheckBox.CheckedChanged += ShowPasswordCheckBox_CheckedChanged;
            Debug.WriteLine("Event handlers wired up.");
            Debug.WriteLine("MainForm() constructor finished.");
        }

        /// <summary>
        /// Event handler for server state changes (e.g., Running, Stopped, Restarting).
        /// Safely updates the UI from the correct thread.
        /// </summary>
        private void OnServerStateChanged(object? sender, int newState)
        {
            Debug.WriteLine($"OnServerStateChanged() called. New state: {newState}");
            if (this.InvokeRequired)
            {
                Debug.WriteLine("Invoke required, dispatching to UI thread.");
                this.BeginInvoke(new Action(() => UpdateUIForState(newState)));
            }
            else
            {
                Debug.WriteLine("Running on UI thread, calling UpdateUIForState directly.");
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
            Debug.WriteLine($"UpdateUIForState() called with state: {state}");
            bool isEnabled = (state != 2); // Disable fields if state is "Restarting"
            Debug.WriteLine($"Configuration fields will be set to Enabled: {isEnabled}");
            SetConfigurationFieldsEnabled(isEnabled);
        }

        /// <summary>
        /// Checks if a given TCP port is available to be bound.
        /// </summary>
        /// <param name="port">The port number to check.</param>
        /// <returns>True if the port is available, otherwise false.</returns>
        // --- NOTE: (CA1822) This *can* be static as the analyzer suggests ---
        private static bool IsPortAvailable(int port)
        {
            Debug.WriteLine($"IsPortAvailable() checking port: {port}");
            TcpListener tcpListener = null;
            try
            {
                tcpListener = new TcpListener(IPAddress.Any, port);
                tcpListener.Start();
                Debug.WriteLine($"Port {port} is available.");
                return true;
            }
            catch (SocketException ex)
            {
                Debug.WriteLine($"Port check failed for port {port}: {ex.Message}");
                return false;
            }
            finally
            {
                tcpListener?.Stop();
                Debug.WriteLine($"Port {port} check finished.");
            }
        }

        /// <summary>
        /// Handles the form's Load event.
        /// Loads all saved settings, checks for updates, and then starts the server.
        /// </summary>
        private async void MainForm_Load(object? sender, EventArgs e)
        {
            Debug.WriteLine("MainForm_Load() started.");
            // Load saved settings into UI
            Debug.WriteLine("Loading settings into UI controls...");
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
            Debug.WriteLine("Settings loaded.");

            UpdateUIForState(Settings.Default.ServerState);

            // Set the initial visibility of the form (hidden in tray or visible)
            Debug.WriteLine("Setting initial form state...");
            SetFormInitialState();

            // Check for updates silently on launch
            Debug.WriteLine("Starting silent update check...");
            _ = CheckForUpdatesAsync(silentCheck: true);

            Debug.WriteLine("Subscribing to system and program events...");
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            Program.ServerStateChanged += OnServerStateChanged;
            Debug.WriteLine("MainForm_Load() finished.");
        }

        // --- FIX: (CS1998) 'async void' lacks 'await'. Made non-async. ---
        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] Display settings changed. Triggering cache rebuild.");
            // Call async helper and discard the task
            _ = HandleDisplaySettingsChangedAsync();
        }

        // --- NEW: Helper method to do the async work ---
        private async Task HandleDisplaySettingsChangedAsync()
        {
            Debug.WriteLine("HandleDisplaySettingsChangedAsync: Clearing WMI cache...");
            MonitorWmiHelper.ClearWmiCache();

            // Set server state to "Busy"
            Debug.WriteLine("HandleDisplaySettingsChangedAsync: Setting server state to 2 (Busy).");
            Program.SetServerState(2);

            // Asynchronously rebuild the cache
            Debug.WriteLine("HandleDisplaySettingsChangedAsync: Starting cache rebuild...");
            await Program.BuildMonitorCacheAsync();

            // Set state back to "Running"
            Debug.WriteLine("HandleDisplaySettingsChangedAsync: Cache rebuild complete. Setting server state to 1 (Running).");
            Program.SetServerState(1);
        }

        /// <summary>
        /// Enables or disables the configuration text boxes and their visual style.
        /// </summary>
        private void SetConfigurationFieldsEnabled(bool enabled)
        {
            Debug.WriteLine($"SetConfigurationFieldsEnabled() setting to: {enabled}");
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
            Debug.WriteLine("RestartServerAndHandleUI() started.");
            try
            {
                Debug.WriteLine("Awaiting Program.RestartServer()...");
                await Task.Run(() => Program.RestartServer());
                Debug.WriteLine("Server restart completed.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Server restart failed: {ex.Message}");
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
        // --- NOTE: (CA1822) This *can* be static as the analyzer suggests ---
        private void SetFormInitialState()
        {
            Debug.WriteLine("SetFormInitialState() started.");
            bool runOnStartup = Settings.Default.RunOnStartup;
            bool minimizeToTray = Settings.Default.MinimizeToTray;
            Debug.WriteLine($"Settings: RunOnStartup={runOnStartup}, MinimizeToTray={minimizeToTray}");

            if (runOnStartup)
            {
                Debug.WriteLine("Running on startup.");
                if (minimizeToTray)
                {
                    Debug.WriteLine("Minimizing to system tray.");
                    this.Hide();
                    this.ShowInTaskbar = false;
                    notifyIcon1.Visible = true;
                    this.WindowState = FormWindowState.Minimized;
                }
                else
                {
                    Debug.WriteLine("Minimizing to taskbar.");
                    this.ShowInTaskbar = true;
                    notifyIcon1.Visible = false;
                    this.WindowState = FormWindowState.Minimized;
                }
            }
            else
            {
                Debug.WriteLine("Manual launch. Showing main window.");
                this.Show();
                this.ShowInTaskbar = true;
                notifyIcon1.Visible = false;
                this.WindowState = FormWindowState.Normal;
            }
            Debug.WriteLine("SetFormInitialState() finished.");
        }

        /// <summary>
        /// Validates the port when the user leaves the text box.
        /// Restarts the server if the port is valid and has changed.
        /// </summary>
        // --- FIX: (IDE1006) Naming rule violation ---
        private async void PortTextBox_Leave(object? sender, EventArgs e)
        {
            Debug.WriteLine("portTextBox_Leave() event fired.");
            int currentPort = Settings.Default.Port;

            if (!int.TryParse(portTextBox.Text, out int newPort))
            {
                Debug.WriteLine("Port validation failed: Not an integer.");
                MessageBox.Show("Please enter a valid whole number for the port.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                portTextBox.Text = currentPort.ToString();
                return;
            }

            if (newPort < 1 || newPort > 65535)
            {
                Debug.WriteLine("Port validation failed: Out of range.");
                MessageBox.Show("Port number must be between 1 and 65535.", "Port Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                portTextBox.Text = currentPort.ToString();
                return;
            }

            if (forbiddenPorts.Contains(newPort))
            {
                Debug.WriteLine("Port validation failed: Port is in forbidden list.");
                MessageBox.Show($"Port {newPort} is a common service port (like HTTP, FTP, etc.) and cannot be used. Please choose a different port.",
                                "Port Configuration Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                portTextBox.Text = currentPort.ToString();
                return;
            }

            bool portChanged = currentPort != newPort;
            Debug.WriteLine($"Port changed: {portChanged}");

            if (portChanged)
            {
                Debug.WriteLine("Checking if new port is available...");
                if (!IsPortAvailable(newPort))
                {
                    Debug.WriteLine("Port validation failed: Port not available.");
                    MessageBox.Show($"Port {newPort} is already in use by another application. Please choose a different port.", "Port Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    portTextBox.Text = currentPort.ToString();
                    return;
                }

                Debug.WriteLine("Saving new port and restarting server...");
                Settings.Default.Port = newPort;
                Settings.Default.Save();
                Debug.WriteLine($"Application settings saved. New active port: {newPort}.");

                await RestartServerAndHandleUI();
            }
            Debug.WriteLine("portTextBox_Leave() finished.");
        }

        /// <summary>
        /// Validates and saves the password when the user leaves the text box.
        /// </summary>
        // --- FIX: (IDE1006) Naming rule violation ---
        private void PasswordTextBox_Leave(object? sender, EventArgs e)
        {
            Debug.WriteLine("passwordTextBox_Leave() event fired.");
            if (string.IsNullOrWhiteSpace(passwordTextBox.Text))
            {
                Debug.WriteLine("Password validation failed: IsNullOrWhiteSpace.");
                MessageBox.Show("Please enter a password for security.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                if (this.CanFocus)
                {
                    passwordTextBox.Focus();
                }
                return;
            }

            if (Settings.Default.Password != passwordTextBox.Text)
            {
                Debug.WriteLine("Password changed. Saving new password.");
                Settings.Default.Password = passwordTextBox.Text;
                Settings.Default.Save();
            }
            Debug.WriteLine("passwordTextBox_Leave() finished.");
        }

        /// <summary>
        /// Handles the "Run on Startup" checkbox change.
        /// Saves the setting and updates the Windows Registry.
        /// </summary>
        // --- FIX: (IDE1006) Naming rule violation ---
        private void RunOnStartupCheckBox_CheckedChanged(object? sender, EventArgs e)
        {
            Debug.WriteLine("runOnStartupCheckBox_CheckedChanged() event fired.");
            bool newState = runOnStartupCheckBox.Checked;
            if (Settings.Default.RunOnStartup != newState)
            {
                Debug.WriteLine($"RunOnStartup state changed to {newState}. Saving and updating registry.");
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
        // --- FIX: (IDE1006) Naming rule violation ---
        private void MinimizeToTrayCheckBox_CheckedChanged(object? sender, EventArgs e)
        {
            Debug.WriteLine("minimizeToTrayCheckBox_CheckedChanged() event fired.");
            bool newState = minimizeToTrayCheckBox.Checked;
            if (Settings.Default.MinimizeToTray != newState)
            {
                Debug.WriteLine($"MinimizeToTray state changed to {newState}. Saving and re-evaluating form state.");
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
        // --- NOTE: (CA1822) This cannot be static as it accesses instance controls ---
        private bool SaveActiveSettings()
        {
            Debug.WriteLine("SaveActiveSettings() called.");
            if (this.ActiveControl is TextBox activeTextBox)
            {
                if (activeTextBox == portTextBox)
                {
                    Debug.WriteLine("Active control is portTextBox. Firing its Leave event.");
                    PortTextBox_Leave(activeTextBox, EventArgs.Empty);
                }
                else if (activeTextBox == passwordTextBox)
                {
                    Debug.WriteLine("Active control is passwordTextBox. Firing its Leave event.");
                    PasswordTextBox_Leave(activeTextBox, EventArgs.Empty);

                    // If validation failed, passwordTextBox will re-gain focus.
                    if (this.ActiveControl == passwordTextBox)
                    {
                        Debug.WriteLine("Password validation failed, returning false.");
                        return false;
                    }
                }
            }
            Debug.WriteLine("SaveActiveSettings() finished, returning true.");
            return true;
        }

        /// <summary>
        /// Handles the form's closing event.
        /// Intercepts the close action to minimize to tray if the setting is enabled.
        /// </summary>
        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            Debug.WriteLine($"MainForm_FormClosing() called. Reason: {e.CloseReason}");
            Debug.WriteLine("Unsubscribing from events...");
            Program.ServerStateChanged -= OnServerStateChanged;
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

            bool isClosingDueToUser = e.CloseReason == CloseReason.UserClosing;
            bool shouldMinimize = minimizeToTrayCheckBox.Checked;

            if (Settings.Default.MinimizeToTray != shouldMinimize)
            {
                Debug.WriteLine("Syncing MinimizeToTray setting before close.");
                Settings.Default.MinimizeToTray = shouldMinimize;
                Settings.Default.Save();
                minimizeToSystemTrayToolStripMenuItem.Checked = shouldMinimize;
            }

            // If user clicks "X" and "Minimize to Tray" is on, intercept the close
            if (isClosingDueToUser && shouldMinimize)
            {
                Debug.WriteLine("User clicked 'X' and MinimizeToTray is on. Intercepting close.");
                if (!SaveActiveSettings())
                {
                    Debug.WriteLine("SaveActiveSettings failed. Canceling close.");
                    e.Cancel = true; // Cancel close if validation fails
                    return;
                }

                this.Hide();
                this.ShowInTaskbar = false;
                notifyIcon1.Visible = true;
                this.WindowState = FormWindowState.Minimized;

                e.Cancel = true; // Cancel the form closing
                Debug.WriteLine("Form hidden to tray.");
                return;
            }

            // If not minimizing, save settings and shut down the server
            Debug.WriteLine("Proceeding with form close.");
            if (!SaveActiveSettings())
            {
                Debug.WriteLine("SaveActiveSettings failed. Canceling close.");
                e.Cancel = true; // Cancel close if validation fails
                return;
            }

            Debug.WriteLine("Shutting down server and hiding tray icon.");
            Program.ShutdownServer();
            notifyIcon1.Visible = false;
            Debug.WriteLine("MainForm_FormClosing() finished.");
        }

        /// <summary>
        /// Handles the form's resize event.
        /// Hides the form and shows the tray icon if minimized.
        /// </summary>
        private void MainForm_Resize(object? sender, EventArgs e)
        {
            Debug.WriteLine($"MainForm_Resize() called. WindowState: {this.WindowState}");
            if (this.WindowState == FormWindowState.Minimized && Settings.Default.MinimizeToTray)
            {
                Debug.WriteLine("Window minimized and MinimizeToTray is on.");
                if (SaveActiveSettings()) // Only hide if settings are valid
                {
                    Debug.WriteLine("Settings saved. Hiding form to tray.");
                    this.Hide();
                    this.ShowInTaskbar = false;
                    notifyIcon1.Visible = true;
                }
                else
                {
                    Debug.WriteLine("SaveActiveSettings failed. Forcing window to stay normal.");
                    this.WindowState = FormWindowState.Normal;
                }
            }
        }

        #region Tray Menu Handlers

        /// <summary>
        /// Handles the "Configure" click from the tray icon menu.
        /// Shows the main window.
        /// </summary>
        // --- FIX: (IDE1006) Naming rule violation ---
        private void ConfigureToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            Debug.WriteLine("TrayMenu: 'Configure' clicked.");
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            notifyIcon1.Visible = false;
        }

        /// <summary>
        /// Handles the "Restart" click from the tray icon menu.
        /// </summary>
        // --- FIX: (IDE1006) Naming rule violation ---
        private async void RestartToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            Debug.WriteLine("TrayMenu: 'Restart' clicked.");
            await RestartServerAndHandleUI();
        }

        /// <summary>
        /// Toggles the "Run on Startup" setting from the tray icon menu.
        /// </summary>
        // --- FIX: (IDE1006) Naming rule violation ---
        private void RunOnStartupToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            Debug.WriteLine("TrayMenu: 'Run on Startup' clicked.");
            // We set it to the *new* state, which is the opposite of the current checked state
            bool newState = runOnStartupToolStripMenuItem.Checked;
            Debug.WriteLine($"New state: {newState}");
            runOnStartupCheckBox.Checked = newState;
        }

        /// <summary>
        /// Toggles the "Minimize to Tray" setting from the tray icon menu.
        /// </summary>
        // --- FIX: (IDE1006) Naming rule violation ---
        private void MinimizeToSystemTrayToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            Debug.WriteLine("TrayMenu: 'Minimize to System Tray' clicked.");
            bool newState = minimizeToSystemTrayToolStripMenuItem.Checked;
            Debug.WriteLine($"New state: {newState}");
            minimizeToTrayCheckBox.Checked = newState;
        }

        // --- FIX: (IDE1006) Naming rule violation ---
        private async void CheckUpdatesToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            Debug.WriteLine("TrayMenu: 'Check for Updates' clicked.");
            await CheckForUpdatesAsync(silentCheck: false);
        }

        /// <summary>
        /// Handles the "Exit" click from the tray icon menu.
        /// Forces the "MinimizeToTray" setting off and exits the application.
        /// </summary>
        // --- FIX: (IDE1006) Naming rule violation ---
        private void ExitToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            Debug.WriteLine("TrayMenu: 'Exit' clicked.");
            // We must set this to false, otherwise the FormClosing event
            // will just re-minimize the app instead of closing it.
            Debug.WriteLine("Forcing MinimizeToTray=false to ensure exit.");
            Settings.Default.MinimizeToTray = false;
            Settings.Default.Save();

            Application.Exit();
        }

        /// <summary>
        /// Handles double-clicking the tray icon (same as clicking "Configure").
        /// </summary>
        // --- FIX: (IDE1006) Naming rule violation ---
        private void NotifyIcon1_DoubleClick(object? sender, EventArgs e)
        {
            Debug.WriteLine("TrayIcon: Double-clicked.");
            ConfigureToolStripMenuItem_Click(sender, e);
        }

        #endregion

        #region Form Control Handlers

        /// <summary>
        /// Toggles the password visibility in the text box.
        /// </summary>
        // --- FIX: (IDE1006) Naming rule violation ---
        private void ShowPasswordCheckBox_CheckedChanged(object? sender, EventArgs e)
        {
            Debug.WriteLine($"showPasswordCheckBox_CheckedChanged() called. New state: {showPasswordCheckBox.Checked}");
            if (showPasswordCheckBox.Checked)
            {
                Debug.WriteLine("Showing password text.");
                passwordTextBox.PasswordChar = '\0'; // Show text
                showPasswordCheckBox.ImageIndex = 1; // "hide" icon
            }
            else
            {
                Debug.WriteLine("Hiding password text.");
                passwordTextBox.PasswordChar = '*'; // Hide text
                showPasswordCheckBox.ImageIndex = 0; // "show" icon
            }
        }

        #endregion

        /// <summary>
        /// Checks GitHub for a new version of the application.
        /// </summary>
        /// <param name="silentCheck">If true, suppresses success/error messages (used during launch).</param>
        private async Task CheckForUpdatesAsync(bool silentCheck)
        {
            Debug.WriteLine($"CheckForUpdatesAsync() started. Silent: {silentCheck}");
            // 1. Get current version
            Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
            Debug.WriteLine($"Current version: {currentVersion}");

            if (!silentCheck)
            {
                Debug.WriteLine("Manual check. Setting server state to Busy (2).");
                Program.SetServerState(2);
                MessageBox.Show("Checking for updates...", "Update Check", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            try
            {
                // 2. Make web request (GitHub API requires a User-Agent)
                Debug.WriteLine("Sending request to GitHub API...");
                httpClient.DefaultRequestHeaders.Add("User-Agent", "MultiDisplayVCPServer-Updater");
                string jsonResponse = await httpClient.GetStringAsync(GitHubApiUrl);

                // 3. Parse the JSON
                Debug.WriteLine("Parsing JSON response...");
                // --- FIX: (IDE0090) 'new' expression can be simplified ---
                var release = JsonSerializer.Deserialize<GitHubRelease>(jsonResponse);
                if (release == null || string.IsNullOrEmpty(release.TagName))
                {
                    throw new Exception("Could not parse release information.");
                }

                // 4. Compare versions
                // GitHub tags are often "v1.1.0", so we strip the "v"
                Version latestVersion = new Version(release.TagName.TrimStart('v'));
                Debug.WriteLine($"Latest version: {latestVersion}");

                if (latestVersion > currentVersion)
                {
                    Debug.WriteLine("New version found.");
                    // 5. New version found! Ask user to download.
                    var result = MessageBox.Show($"A new version ({release.TagName}) is available. You are currently on {currentVersion}.\n\nWould you like to open the download page?",
                                    "Update Available",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Information);

                    if (result == DialogResult.Yes)
                    {
                        Debug.WriteLine("User clicked 'Yes'. Opening download page.");
                        // Find the installer asset (e.g., .msi or .exe)
                        string downloadUrl = release.HtmlUrl; // Default to the release page
                        var installerAsset = release.Assets?.FirstOrDefault(a => a.DownloadUrl.EndsWith(".msi") || a.DownloadUrl.EndsWith(".exe"));
                        if (installerAsset != null)
                        {
                            downloadUrl = installerAsset.DownloadUrl;
                        }
                        Debug.WriteLine($"Opening URL: {downloadUrl}");
                        // Open the URL in the default browser
                        Process.Start(new ProcessStartInfo(downloadUrl) { UseShellExecute = true });
                    }
                    else
                    {
                        Debug.WriteLine("User clicked 'No'.");
                    }
                }
                else if (!silentCheck) // Only show success message if run manually
                {
                    Debug.WriteLine("Already on latest version.");
                    MessageBox.Show("You are already running the latest version.", "Up to Date", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update check failed: {ex.Message}");
                if (!silentCheck) // Only show error message if run manually
                {
                    MessageBox.Show($"Failed to check for updates. Please check your internet connection or visit the GitHub page manually.\n\nError: {ex.Message}",
                                "Update Check Failed",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                }
            }
            finally
            {
                if (!silentCheck)
                {
                    Debug.WriteLine("Manual check finished. Setting server state to Running (1).");
                    Program.SetServerState(1);
                }
            }
        }

        /// <summary>
        /// DTO for deserializing the GitHub /releases/latest API response.
        /// </summary>
        private class GitHubRelease
        {
            [JsonPropertyName("tag_name")]
            public string TagName { get; set; }

            [JsonPropertyName("html_url")]
            public string HtmlUrl { get; set; }

            [JsonPropertyName("assets")]
            public List<GitHubAsset> Assets { get; set; }
        }

        /// <summary>
        /// DTO for deserializing a release asset from the GitHub API.
        /// </summary>
        private class GitHubAsset
        {
            [JsonPropertyName("browser_download_url")]
            public string DownloadUrl { get; set; }
        }
    }
}