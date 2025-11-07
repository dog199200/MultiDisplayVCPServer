using System;
using System.Collections.Generic;
using System.Management;
using System.Text;

namespace MultiDisplayVCPServer
{
    /// <summary>
    /// A static helper class for querying WMI to get monitor hardware IDs.
    /// This class bridges the gap between the DDC/CI monitor name and the stable PnP (Plug and Play) ID.
    /// </summary>
    public static class MonitorWmiHelper
    {
        /// <summary>
        /// Parses a full PnP Device ID string to extract the short Model ID.
        /// </summary>
        /// <param name="fullId">The full PnP ID (e.g., "DISPLAY\ACR0D1D\4&...").</param>
        /// <returns>The short Model ID (e.g., "ACR0D1D"), or null if parsing fails.</returns>
        private static string ParsePnP_ID(string fullId)
        {
            if (string.IsNullOrEmpty(fullId)) return null;
            string[] parts = fullId.Split('\\');
            if (parts.Length > 1)
            {
                return parts[1]; // The second part is the Model ID
            }
            return null;
        }

        /// <summary>
        /// Gets a map of [DDC/CI Description] -> [Short Model ID].
        /// This is used by GET_CAPS to associate a stable ID with a monitor.
        /// </summary>
        /// <returns>A dictionary mapping names like "NVIDIA GeForce..." to "ACR0D1D".</returns>
        public static Dictionary<string, string> GetMonitorPnPMap()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                // Win32_DesktopMonitor is the key WMI class that links the PnP ID (hardware)
                // to the "Name" property (what DDC/CI reports as the description).
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    @"root\cimv2",
                    "SELECT * FROM Win32_DesktopMonitor");

                foreach (ManagementObject mo in searcher.Get())
                {
                    string pnpId = mo["PNPDeviceID"]?.ToString();
                    string name = mo["Name"]?.ToString(); // This is the DDC/CI Description
                    string shortId = ParsePnP_ID(pnpId);

                    if (!string.IsNullOrEmpty(shortId) && !string.IsNullOrEmpty(name))
                    {
                        if (!map.ContainsKey(name))
                        {
                            map[name] = shortId;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error but continue, so the server doesn't crash if WMI fails.
                Console.WriteLine($"WMI Query Error (GetMonitorPnPMap): {ex.Message}");
            }
            return map;
        }

        /// <summary>
        /// Gets a map of [Short Model ID] -> [DDC/CI Description].
        /// This is used by SET commands to find a monitor's handle from its stable ID.
        /// </summary>
        /// <returns>A dictionary mapping names like "ACR0D1D" to "NVIDIA GeForce...".</returns>
        public static Dictionary<string, string> GetPnPMonitorMap()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    @"root\cimv2",
                    "SELECT * FROM Win32_DesktopMonitor");

                foreach (ManagementObject mo in searcher.Get())
                {
                    string pnpId = mo["PNPDeviceID"]?.ToString();
                    string name = mo["Name"]?.ToString();
                    string shortId = ParsePnP_ID(pnpId);

                    if (!string.IsNullOrEmpty(shortId) && !string.IsNullOrEmpty(name))
                    {
                        if (!map.ContainsKey(shortId))
                        {
                            map[shortId] = name;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WMI Query Error (GetPnPMonitorMap): {ex.Message}");
            }
            return map;
        }

        /// <summary>
        /// Helper function to convert a raw byte array from WMI into a clean string.
        /// WMI often stores friendly names as byte arrays with trailing nulls.
        /// </summary>
        /// <param name="data">The byte array from the WMI query.</param>
        /// <returns>A trimmed ASCII string.</returns>
        private static string GetStringFromByteArray(byte[] data)
        {
            if (data == null) return string.Empty;
            // GetString handles the conversion, TrimEnd cleans up trailing null characters
            return Encoding.ASCII.GetString(data).TrimEnd('\0');
        }
    }
}