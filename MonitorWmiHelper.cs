using System;
using System.Collections.Generic;
using System.Management;
using System.Text;
using System.Diagnostics;
using Microsoft.Win32;

namespace MultiDisplayVCPServer
{
    /// <summary>
    /// A static helper class for querying WMI to get monitor hardware IDs.
    /// This class bridges the gap between the DDC/CI monitor name and the stable PnP (Plug and Play) ID.
    /// </summary>
    public static class MonitorWmiHelper
    {
        private static Dictionary<string, string> _pnpToDescMapCache = null;
        private static Dictionary<string, string> _descToPnpMapCache = null;
        private static readonly object _wmiLock = new object();

        /// <summary>
        /// Parses a full PnP Device ID string to extract the short Model ID.
        /// </summary>
        /// <param name="fullId">The full PnP ID (e.g., "DISPLAY\ACR0D1D\4&...").</param>
        /// <returns>The short Model ID (e.g., "ACR0D1D"), or null if parsing fails.</returns>
        private static string ParsePnP_ID(string fullId)
        {
            Debug.WriteLine($"ParsePnP_ID() called with: {fullId}");
            if (string.IsNullOrEmpty(fullId))
            {
                Debug.WriteLine("ParsePnP_ID: fullId is null or empty, returning null.");
                return null;
            }
            string[] parts = fullId.Split('\\');
            if (parts.Length > 1)
            {
                Debug.WriteLine($"ParsePnP_ID: Found {parts.Length} parts. Returning part 1: {parts[1]}");
                return parts[1]; // The second part is the Model ID
            }
            Debug.WriteLine("ParsePnP_ID: fullId did not contain '\\', returning null.");
            return null;
        }

        public static void ClearWmiCache()
        {
            Debug.WriteLine("ClearWmiCache() started.");
            lock (_wmiLock)
            {
                Debug.WriteLine("WMI lock acquired.");
                _pnpToDescMapCache = null;
                _descToPnpMapCache = null;
                Debug.WriteLine("WMI cache cleared (_pnpToDescMapCache = null, _descToPnpMapCache = null).");
            }
            Debug.WriteLine("WMI lock released. ClearWmiCache() finished.");
        }

        /// <summary>
        /// Populates *both* WMI maps at the same time.
        /// </summary>
        private static void PopulateWmiCaches()
        {
            Debug.WriteLine("PopulateWmiCaches() started.");
            // Double-check lock to ensure only one thread populates
            if (_pnpToDescMapCache != null)
            {
                Debug.WriteLine("Cache is already populated. Skipping.");
                return;
            }

            Debug.WriteLine("Cache is null, acquiring WMI lock...");
            lock (_wmiLock)
            {
                Debug.WriteLine("WMI lock acquired.");
                // Check again inside the lock
                if (_pnpToDescMapCache != null)
                {
                    Debug.WriteLine("Cache was populated by another thread. Skipping.");
                    Debug.WriteLine("WMI lock released.");
                    return;
                }

                Debug.WriteLine("Initializing new cache dictionaries.");
                var pnpToDesc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var descToPnp = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    Debug.WriteLine("Executing WMI query: 'SELECT * FROM Win32_DesktopMonitor'...");
                    ManagementObjectSearcher searcher = new(
                                            @"root\cimv2",
                                            "SELECT * FROM Win32_DesktopMonitor");

                    foreach (ManagementObject mo in searcher.Get().Cast<ManagementObject>())
                    {
                        string pnpId = mo["PNPDeviceID"]?.ToString();
                        string name = mo["Name"]?.ToString();
                        Debug.WriteLine($"WMI Result: Name='{name}', PnPDeviceID='{pnpId}'");

                        string shortId = ParsePnP_ID(pnpId);

                        if (!string.IsNullOrEmpty(shortId) && !string.IsNullOrEmpty(name))
                        {
                            if (!pnpToDesc.ContainsKey(shortId))
                            {
                                pnpToDesc[shortId] = name;
                                Debug.WriteLine($"Added to pnpToDesc map: [{shortId}] = {name}");
                            }
                            if (!descToPnp.ContainsKey(name))
                            {
                                descToPnp[name] = shortId;
                                Debug.WriteLine($"Added to descToPnp map: [{name}] = {shortId}");
                            }
                        }
                        else
                        {
                            Debug.WriteLine("Skipping WMI result (missing name or shortId).");
                        }
                    }
                    Debug.WriteLine("WMI query finished.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"WMI Query Error (PopulateWmiCaches): {ex.Message}");
                }

                _pnpToDescMapCache = pnpToDesc;
                _descToPnpMapCache = descToPnp;
                Debug.WriteLine("Cache population complete. Assigning new dictionaries to static cache fields.");
            }
            Debug.WriteLine("WMI lock released. PopulateWmiCaches() finished.");
        }

        /// <summary>
        /// Gets a map of [DDC/CI Description] -> [Short Model ID].
        /// </summary>
        public static Dictionary<string, string> GetMonitorPnPMap()
        {
            Debug.WriteLine("GetMonitorPnPMap() called.");
            // --- MODIFIED: Use the cache ---
            if (_descToPnpMapCache == null)
            {
                Debug.WriteLine("Cache miss. Calling PopulateWmiCaches().");
                PopulateWmiCaches();
            }
            else
            {
                Debug.WriteLine("Cache hit. Returning existing _descToPnpMapCache.");
            }
            return _descToPnpMapCache;
            // --- END MODIFIED ---
        }

        /// <summary>
        /// Gets a map of [Short Model ID] -> [DDC/CI Description].
        /// </summary>
        public static Dictionary<string, string> GetPnPMonitorMap()
        {
            Debug.WriteLine("GetPnPMonitorMap() called.");
            // --- MODIFIED: Use the cache ---
            if (_pnpToDescMapCache == null)
            {
                Debug.WriteLine("Cache miss. Calling PopulateWmiCaches().");
                PopulateWmiCaches();
            }
            else
            {
                Debug.WriteLine("Cache hit. Returning existing _pnpToDescMapCache.");
            }
            return _pnpToDescMapCache;
            // --- END MODIFIED ---
        }
    }
}