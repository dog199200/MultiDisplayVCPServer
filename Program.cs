using Microsoft.Win32;
using MultiDisplayVCPServer.Properties;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MultiDisplayVCPServer
{
    /// <summary>
    /// Main application class. Handles server lifecycle, client connections, and command execution.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// A unique GUID to ensure only one instance of the application can run.
        /// </summary>
        private const string AppGuid = "MultiDisplayVCPServer.0D77BA1D-F890-48F0-975F-5E8A91F2681E";

        /// <summary>
        /// Mutex to enforce the single-instance-only rule.
        /// </summary>
        private static Mutex appMutex = null;

        /// <summary>
        /// The main TCP listener for incoming client connections.
        /// </summary>
        private static TcpListener listener;

        /// <summary>
        /// CancellationTokenSource to gracefully shut down the server.
        /// </summary>
        private static CancellationTokenSource cts;

        /// <summary>
        /// The background task that runs the server's listener loop.
        /// </summary>
        private static Task listenerTask;

        /// <summary>
        /// Event that fires when the server's state (Stopped, Running, Restarting) changes.
        /// </summary>
        public static event EventHandler<int> ServerStateChanged;

        /// <summary>
        /// Event that fires to send a log message to the UI.
        /// </summary>
        public static event EventHandler<string> LogMessageReceived;

        /// <summary>
        /// Logs a message to the console and to the main UI form if it's open.
        /// </summary>
        /// <param name="message">The message to log.</param>
        private static void Log(string message)
        {
            string logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            Console.WriteLine(logEntry);
            if (Application.OpenForms.Cast<Form>().Any(f => f is MainForm))
            {
                LogMessageReceived?.Invoke(null, logEntry);
            }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {
            bool createdNew = false;
            try
            {
                // Ensure only one instance of the app is running
                appMutex = new Mutex(true, $"Global\\{AppGuid}", out createdNew);
            }
            catch (Exception)
            {
                createdNew = false;
            }

            if (!createdNew)
            {
                MessageBox.Show("Another instance of Multi-Connection Monitor Server is already running.", "Application Already Running", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            appMutex.ReleaseMutex();
        }

        /// <summary>
        /// Starts the TCP listener background task.
        /// </summary>
        public static void StartServerLoop()
        {
            if (listenerTask != null && !listenerTask.IsCompleted)
            {
                Log("Attempted to start server, but it is already running or busy.");
                return;
            }
            cts = new CancellationTokenSource();
            listenerTask = Task.Run(() => ListenerLoopAsync(cts.Token));
            Log("Server startup initiated...");
        }

        /// <summary>
        /// Signals the TCP listener to stop and cancels the background task.
        /// </summary>
        public static void ShutdownServer()
        {
            cts?.Cancel();
            listener?.Stop();
        }

        /// <summary>
        /// Shuts down the server, waits, and then starts it again.
        /// </summary>
        public static void RestartServer()
        {
            Settings.Default.ServerState = 2; // Set state to "Restarting"
            Settings.Default.Save();
            ServerStateChanged?.Invoke(null, 2);
            ShutdownServer();
            Thread.Sleep(150);
            StartServerLoop();
            Log("Server restart sequence completed.");
        }

        /// <summary>
        /// The main server loop that runs on a background thread.
        /// </summary>
        /// <param name="token">The cancellation token to signal shutdown.</param>
        private static async Task ListenerLoopAsync(CancellationToken token)
        {
            bool serverStartedSuccessfully = false;
            try
            {
                int port = Settings.Default.Port;
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                serverStartedSuccessfully = true;
                Settings.Default.ServerState = 1; // "Running"
                Settings.Default.Save();
                Log($"Server started on port {port}. Waiting for connections...");
                ServerStateChanged?.Invoke(null, 1);

                while (!token.IsCancellationRequested)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync(token);
                    if (client != null)
                    {
                        Log($"Client connected: {client.Client.RemoteEndPoint}");
                    }
                    // Handle each client on its own task
                    Task.Run(() => HandleClientAsync(client, token), token);
                }
            }
            catch (OperationCanceledException)
            {
                Log("Server listener stopped gracefully via token cancellation.");
            }
            catch (SocketException ex) when (ex.ErrorCode == 10004)
            {
                Log("Server listener stopped gracefully.");
            }
            catch (Exception ex)
            {
                Log($"Server failed to start or crashed: {ex.Message}");
                if (!serverStartedSuccessfully)
                {
                    throw;
                }
            }
            finally
            {
                listener?.Stop();
                Settings.Default.ServerState = 0; // "Stopped"
                Settings.Default.Save();
                if (!token.IsCancellationRequested)
                {
                    ServerStateChanged?.Invoke(null, 0);
                }
            }
        }

        /// <summary>
        /// Handles a single connected client.
        /// </summary>
        /// <param name="client">The connected TcpClient.</param>
        /// <param name="token">The cancellation token.</param>
        private static async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            string remoteEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown Client";
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                try
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    string receivedData = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
                    Log($"Received request from {remoteEndPoint}: '{receivedData}'");
                    if (string.IsNullOrEmpty(receivedData)) return;

                    // Protocol: timestamp|hash_base64|command
                    string[] parts = receivedData.Split(new[] { '|' }, 3);
                    string responseMessage;
                    bool isJson = false;

                    if (parts.Length == 3)
                    {
                        string timestampStr = parts[0];
                        string hashBase64 = parts[1];
                        string command = parts[2];
                        string requiredPassword = Settings.Default.Password;

                        if (ValidateHash(timestampStr, hashBase64, command, requiredPassword))
                        {
                            responseMessage = ExecuteDdcCiCommand(command, out isJson);
                        }
                        else
                        {
                            responseMessage = "ERROR: Invalid Hash.";
                            Log($"Authentication failed for {remoteEndPoint}. Hash mismatch or stale timestamp.");
                        }
                    }
                    else
                    {
                        responseMessage = "ERROR: Invalid request format.";
                        Log($"Authentication failed for {remoteEndPoint}. Invalid format.");
                    }

                    await SendResponseAsync(stream, responseMessage, isJson, token);
                }
                catch (OperationCanceledException) { }
                catch (IOException ex) when (ex.InnerException is SocketException se && se.ErrorCode == 10054)
                {
                    Log($"Client {remoteEndPoint} disconnected abruptly.");
                }
                catch (Exception ex)
                {
                    Log($"Client handling error for {remoteEndPoint}: {ex.Message}");
                    if (stream.CanWrite)
                    {
                        await SendResponseAsync(stream, "SERVER ERROR: An unexpected server error occurred.", false, token);
                    }
                }
                finally
                {
                    Log($"Client connection closed: {remoteEndPoint}.");
                }
            }
        }

        /// <summary>
        /// Validates an incoming request's HMAC hash and timestamp.
        /// </summary>
        /// <param name="timestampStr">The client-provided Unix timestamp string.</param>
        /// <param name="hashBase64">The client-provided HMAC-SHA256 hash.</param>
        /// <param name="command">The command being executed.</param>
        /// <param name="password">The shared secret (password) used to generate the hash.</param>
        /// <returns>True if the hash and timestamp are valid, otherwise false.</returns>
        private static bool ValidateHash(string timestampStr, string hashBase64, string command, string password)
        {
            try
            {
                // 1. Check timestamp
                if (!long.TryParse(timestampStr, NumberStyles.None, CultureInfo.InvariantCulture, out long timestamp))
                    return false;

                var requestTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
                var now = DateTime.UtcNow;

                // Allow a 30-second window
                if (requestTime < now.AddSeconds(-30) || requestTime > now.AddSeconds(30))
                {
                    Log("Hash validation failed: Stale timestamp.");
                    return false;
                }

                // 2. Check hash
                using (var hmac = new HMACSHA256(Encoding.ASCII.GetBytes(password)))
                {
                    string messageToHash = command + timestampStr;
                    byte[] computedHashBytes = hmac.ComputeHash(Encoding.ASCII.GetBytes(messageToHash));
                    string computedHashBase64 = Convert.ToBase64String(computedHashBytes);

                    if (computedHashBase64 != hashBase64)
                    {
                        Log("Hash validation failed: Hash mismatch.");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Log($"Error during hash validation: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Parses and executes a validated command from a client.
        /// </summary>
        /// <param name="command">The command to execute (e.g., "GET_CAPS" or "SET:ID:CODE:VALUE").</param>
        /// <param name="isJson">Output parameter; set to true if the response is JSON.</param>
        /// <returns>A string response for the client.</returns>
        private static string ExecuteDdcCiCommand(string command, out bool isJson)
        {
            isJson = false;

            if (command.Equals("GET_CAPS", StringComparison.OrdinalIgnoreCase))
            {
                isJson = true;
                return GetMonitorCapabilitiesJson();
            }
            else if (command.StartsWith("SET:", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = command.Split(':');

                if (parts.Length == 4 &&
                    uint.TryParse(parts[2], out uint vcpCode) &&
                    uint.TryParse(parts[3], out uint vcpValue))
                {
                    string targetPnP_ID = parts[1]; // This is the stable Model ID
                    IntPtr targetHandle = FindMonitorHandle(targetPnP_ID);

                    if (targetHandle != (IntPtr)(-1))
                    {
                        try
                        {
                            bool success = MonitorController.SetVCPFeature(targetHandle, (byte)vcpCode, vcpValue);
                            return success
                                ? $"OK: VCP Code 0x{vcpCode:X2} set to {vcpValue} on {targetPnP_ID}."
                                : "ERROR: DDC/CI command failed.";
                        }
                        finally
                        {
                            MonitorController.DestroyPhysicalMonitor(targetHandle);
                        }
                    }
                    else
                    {
                        return $"ERROR: Monitor ID {targetPnP_ID} not found.";
                    }
                }
                else
                {
                    return "ERROR: Invalid SET command format. Use SET:ID:CODE:VALUE.";
                }
            }
            else
            {
                return "ERROR: Invalid Command. Send GET_CAPS or SET:ID:CODE:VALUE.";
            }
        }

        #region Monitor_DDC/CI_Helpers

        /// <summary>
        /// Finds the DDC/CI handle (hMonitor) for a monitor based on its stable PnP Model ID.
        /// </summary>
        /// <param name="targetPnP_ID">The stable Model ID (e.g., "ACR0D1D").</param>
        /// <returns>A handle to the physical monitor, or -1 if not found.</returns>
        static IntPtr FindMonitorHandle(string targetPnP_ID)
        {
            Log($"Finding handle for PnP_ID: {targetPnP_ID}");
            if (string.IsNullOrEmpty(targetPnP_ID)) return (IntPtr)(-1);

            // 1. Get the WMI map of PnP_ID -> DDC/CI Description
            var pnpMap = MonitorWmiHelper.GetPnPMonitorMap();
            if (!pnpMap.TryGetValue(targetPnP_ID, out string targetDescription))
            {
                Log($"Error: Could not find PnP_ID {targetPnP_ID} in WMI map.");
                return (IntPtr)(-1);
            }
            Log($"Target DDC/CI Description is: {targetDescription}");

            // 2. Enumerate all monitors
            var monitors = MonitorController.EnumeratePhysicalMonitors(new Dictionary<string, string>());
            IntPtr foundHandle = (IntPtr)(-1);

            // 3. Find the monitor with the matching DDC/CI Description
            foreach (var monitor in monitors)
            {
                if (monitor.Description.Equals(targetDescription, StringComparison.OrdinalIgnoreCase))
                {
                    foundHandle = monitor.Handle;
                    Log($"Found handle {foundHandle} for {targetDescription}");
                    break;
                }
            }

            // 4. Clean up other handles
            foreach (var monitor in monitors)
            {
                if (monitor.Handle != foundHandle)
                {
                    MonitorController.DestroyPhysicalMonitor(monitor.Handle);
                }
            }

            return foundHandle;
        }

        /// <summary>
        /// Gathers capabilities from all DDC/CI-compliant monitors and returns them as a JSON string.
        /// </summary>
        /// <returns>A JSON string representing the ServerStatus object.</returns>
        static string GetMonitorCapabilitiesJson()
        {
            // 1. Get the map of DDC/CI Description -> PnP Model ID
            var pnpMap = MonitorWmiHelper.GetMonitorPnPMap();
            Log($"Found {pnpMap.Count} monitors in WMI.");

            List<MonitorInfo> monitorList = new List<MonitorInfo>();

            // 2. Enumerate all physical monitors
            var physicalMonitors = MonitorController.EnumeratePhysicalMonitors(pnpMap);
            Log($"Found {physicalMonitors.Count} DDC/CI monitors.");

            foreach (var pMon in physicalMonitors)
            {
                IntPtr hMonitor = pMon.Handle;

                // 3. Check for stable PnP_ID (if it's missing, we can't use this monitor)
                if (string.IsNullOrEmpty(pMon.PnP_ID))
                {
                    Log($"Skipping monitor {pMon.Description}: Could not find matching PnP_ID in WMI.");
                    MonitorController.DestroyPhysicalMonitor(hMonitor);
                    continue;
                }

                // 4. Get capabilities string
                uint length = 0;
                if (!MonitorController.GetCapabilitiesStringLength(hMonitor, ref length))
                {
                    Log($"Skipping monitor {pMon.Description}: Failed to get capabilities string length.");
                    MonitorController.DestroyPhysicalMonitor(hMonitor);
                    continue;
                }

                StringBuilder sb = new StringBuilder((int)length);
                if (!MonitorController.CapabilitiesRequestAndCapabilitiesReply(hMonitor, sb, length))
                {
                    Log($"Skipping monitor {pMon.Description}: Failed to get capabilities string.");
                    MonitorController.DestroyPhysicalMonitor(hMonitor);
                    continue;
                }

                // 5. Discover all VCP features
                List<VcpFeature> features = DiscoverAllVcpFeatures(hMonitor, sb.ToString());

                monitorList.Add(new MonitorInfo
                {
                    DeviceID = pMon.PnP_ID, // The stable Model ID
                    Description = pMon.Description, // The DDC/CI Name
                    Capabilities = features
                });

                MonitorController.DestroyPhysicalMonitor(hMonitor);
            }

            ServerStatus status = new ServerStatus
            {
                Monitors = monitorList,
                Message = $"OK: Found {monitorList.Count} DDC/CI compliant monitors."
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Serialize(status, options);
        }

        /// <summary>
        /// Helper to parse a hex string to a uint.
        /// </summary>
        static bool TryParseVcpHex(string hex, out uint result)
        {
            return uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out result);
        }

        /// <summary>
        /// Iterates through all possible VCP codes (0-255) to discover a monitor's features.
        /// </summary>
        /// <param name="hMonitor">The monitor's handle.</param>
        /// <param name="capString">The monitor's capabilities string (for parsing non-continuous values).</param>
        /// <returns>A list of VcpFeature objects.</returns>
        static List<VcpFeature> DiscoverAllVcpFeatures(IntPtr hMonitor, string capString)
        {
            List<VcpFeature> features = new List<VcpFeature>();

            // List of VCP codes to ignore (reserved, table, or buggy)
            var exclusionList = new HashSet<byte>
            {
                0x04, 0x05, 0x08, 0xAC, 0xAE, 0xB6, 0xC0, 0xC8, 0xC9, 0xDF, 0x02, 0x52, 0x82,
                0xB2, 0xC6, 0xCA, 0xCC, 0xDC,
            };
            for (int i = 224; i <= 255; i++) { exclusionList.Add((byte)i); } // 0xE0-0xFF

            // Known non-continuous VCP codes
            var nonContinuousList = new HashSet<byte> { 0x14, 0x60, 0x8D, 0xD6, };

            // Parse non-continuous value maps from the capability string (e.g., "60(01 02 0F)")
            Dictionary<byte, string> nonContinuousMap = new Dictionary<byte, string>();
            Match vcpMatch = Regex.Match(capString, @"vcp\((.*?)\)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (vcpMatch.Success)
            {
                string vcpContent = vcpMatch.Groups[1].Value.Trim();
                string[] vcpTokens = vcpContent.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string token in vcpTokens)
                {
                    int nonContStart = token.IndexOf('(');
                    if (nonContStart != -1)
                    {
                        if (TryParseVcpHex(token.Substring(0, nonContStart), out uint vcpCode))
                        {
                            nonContinuousMap[(byte)vcpCode] = token.Substring(nonContStart + 1).TrimEnd(')');
                        }
                    }
                }
            }

            // Brute-force check all 256 VCP codes
            for (int vcpCode = 0; vcpCode <= 255; vcpCode++)
            {
                uint currentValue = 0;
                uint maximumValue = 0;
                MonitorController.MONITOR_CAPABILITIES_REQUEST_TYPE type = 0;

                if (MonitorController.GetVCPFeatureAndVCPFeatureReply(hMonitor, (byte)vcpCode, ref type, ref currentValue, ref maximumValue))
                {
                    if (exclusionList.Contains((byte)vcpCode))
                    {
                        continue;
                    }

                    VcpFeature feature = new VcpFeature
                    {
                        Code = (byte)vcpCode,
                        Name = GetVcpFeatureName((byte)vcpCode),
                        ReadWrite = true, // We assume Read/Write if GetVCPFeature succeeded
                        CurrentValue = currentValue,
                        MaximumValue = maximumValue,
                    };

                    if (nonContinuousList.Contains((byte)vcpCode))
                    {
                        feature.Type = "Non-Continuous";
                        if (nonContinuousMap.TryGetValue((byte)vcpCode, out string valuesStr))
                        {
                            string[] values = valuesStr.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string valHex in values)
                            {
                                if (TryParseVcpHex(valHex, out uint val))
                                {
                                    if (feature.NonContinuousValues == null) feature.NonContinuousValues = new Dictionary<uint, string>();
                                    feature.NonContinuousValues[val] = val.ToString(); // TODO: Add friendly names if available
                                }
                            }
                        }
                    }
                    else
                    {
                        feature.Type = "Continuous";
                    }
                    features.Add(feature);
                }
            }
            return features;
        }

        /// <summary>
        /// Provides a friendly name for a known VCP code.
        /// </summary>
        static string GetVcpFeatureName(byte code)
        {
            return code switch
            {
                0x02 => "New Control Value",
                0x04 => "Restore Factory Defaults",
                0x05 => "Restore Factory Luminance/Contrast",
                0x08 => "Restore Factory Color Defaults",
                0x0B => "Color Temperature Increment",
                0x0C => "Color Temperature Request",
                0x10 => "Brightness",
                0x12 => "Contrast",
                0x14 => "Select Color Preset",
                0x16 => "Video Gain (Drive): Red",
                0x18 => "Video Gain (Drive): Green",
                0x1A => "Video Gain (Drive): Blue",
                0x59 => "6 Axis Saturation Control: Red",
                0x5A => "6 Axis Saturation Control: Yellow",
                0x5B => "6 Axis Saturation Control: Green",
                0x5C => "6 Axis Saturation Control: Cyan",
                0x5D => "6 Axis Saturation Control: Blue",
                0x5E => "6 Axis Saturation Control: Magenta",
                0x60 => "Input Select",
                0x62 => "Audio: Speaker Volume",
                0x6C => "Video Black Level: Red",
                0x6E => "Video Black Level: Green",
                0x70 => "Video Black Level: Blue",
                0x8D => "Audio Mute / Screen Blank",
                0x9B => "6 Axis Color Control: Red",
                0x9C => "6 Axis Color Control: Yellow",
                0x9D => "6 Axis Color Control: Green",
                0x9E => "6 Axis Color Control: Cyan",
                0x9F => "6 Axis Color Control: Blue",
                0xA0 => "6 Axis Color Control: Magenta",
                0xAC => "Horizontal Frequency",
                0xAE => "Vertical Frequency",
                0xB6 => "Display Technology Type",
                0xC0 => "Display Usage Time",
                0xC6 => "Application Enable Key",
                0xC8 => "Display Controller ID",
                0xC9 => "Display Firmware Level",
                0xCC => "OSD Language",
                0xD6 => "Power Mode",
                0xDF => "VCP Version",
                _ => $"VCP Code 0x{code:X2}",
            };
        }
        #endregion

        /// <summary>
        /// Sends a response back to the client over the network stream.
        /// </summary>
        private static async Task SendResponseAsync(NetworkStream stream, string message, bool isJson, CancellationToken token)
        {
            byte[] response = Encoding.ASCII.GetBytes(message);
            await stream.WriteAsync(response, 0, response.Length, token);
            string logMessage = isJson ? "JSON Data" : message;
            Log($"Sent response: {logMessage}");
        }

        /// <summary>
        /// Saves the current application settings to disk.
        /// </summary>
        public static void SaveSettings()
        {
            try
            {
                Settings.Default.Save();
                Log("Application settings saved successfully.");
            }
            catch (Exception ex)
            {
                Log($"Error saving settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Enables or disables the application's automatic startup via the Windows Registry.
        /// </summary>
        /// <param name="enable">If true, adds to startup; otherwise, removes it.</param>
        public static void SetStartup(bool enable)
        {
            const string runKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
            const string appName = "MultiDisplayVCPServer";

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(runKey, true))
                {
                    if (key == null) return;

                    if (enable)
                    {
                        string executablePath = Environment.ProcessPath;

                        if (string.IsNullOrEmpty(executablePath))
                        {
                            Log("Error: Could not determine executable path for startup.");
                            return;
                        }

                        // Add quotes to handle spaces in the path
                        key.SetValue(appName, $"\"{executablePath}\"");
                        Log("Application added to Windows startup.");
                    }
                    else
                    {
                        if (key.GetValue(appName) != null)
                        {
                            key.DeleteValue(appName);
                            Log("Application removed from Windows startup.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error modifying registry for startup: {ex.Message}");
            }
        }
    }
}