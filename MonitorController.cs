using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using Microsoft.Win32;

namespace MultiDisplayVCPServer
{
    /// <summary>
    /// A data class to hold combined information about a physical monitor.
    /// </summary>
    public class PhysicalMonitorData
    {
        /// <summary>
        /// The handle to the physical monitor (hMonitor). Used for DDC/CI calls.
        /// </summary>
        public IntPtr Handle { get; set; }

        /// <summary>
        /// The DDC/CI description of the monitor (e.g., "NVIDIA GeForce...").
        /// This is used as the key to find the PnP_ID.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The unstable Windows device path (e.g., "\\.\DISPLAY1\Monitor0").
        /// </summary>
        public string DeviceID { get; set; }

        /// <summary>
        /// The stable PnP (Plug and Play) Model ID (e.g., "ACR0D1D").
        /// This is the primary ID used by the client.
        /// </summary>
        public string PnP_ID { get; set; }
    }

    /// <summary>
    /// A structure used for P/Invoke (Platform Invoke) with dxva2.dll.
    /// Represents a physical monitor handle and its Unicode description.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public char[] szPhysicalMonitorDescription;
    }

    /// <summary>
    /// A P/Invoke structure containing information about a display monitor.
    /// Used by GetMonitorInfo to find the device path.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    /// <summary>
    /// A P/Invoke structure that defines the coordinates of a rectangle.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left, top, right, bottom;
    }

    /// <summary>
    /// A delegate (callback function) used by EnumDisplayMonitors.
    /// </summary>
    public delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    /// <summary>
    /// Static class that wraps low-level Windows API calls (P/Invoke)
    /// for enumerating monitors and interacting with them via DDC/CI.
    /// </summary>
    public static class MonitorController
    {
        /// <summary>
        /// Defines constants for DDC/CI (Display Data Channel Command Interface) requests.
        /// </summary>
        public enum MONITOR_CAPABILITIES_REQUEST_TYPE : uint
        {
            MC_MOMENTARY = 0x00000001,
            MC_SET_PARAMETER = 0x00000002,
            MC_GET_PARAMETER = 0x00000004,
            MC_CAPABILITIES_STRING = 0x00000008,
            MC_SUPPORT_VSM_METHODS = 0x00000010,
            MC_USER_PREFERRED_SETTINGS = 0x00000020
        }

        #region P/Invoke DllImports

        // --- FIX: Reverted all to [DllImport] ---

        /// <summary>
        /// Enumerates display monitors (including virtual monitors that mirror part of the desktop).
        /// </summary>
        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        /// <summary>
        /// Retrieves the number of physical monitors associated with an HMONITOR (a display monitor handle).
        /// </summary>
        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor,
            ref uint pdwNumberOfPhysicalMonitors
        );

        /// <summary>
        /// Retrieves the physical monitor handles from an HMONITOR.
        /// </summary>
        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor,
            uint dwPhysicalMonitorArraySize,
            [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray
        );

        /// <summary>
        /// Retrieves info for a display monitor, including the device name (e.g., "\\.\DISPLAY1").
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr hmon, ref MONITORINFOEX mi);

        /// <summary>
        /// Destroys a handle to a physical monitor. Must be called to free resources.
        /// </summary>
        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);

        /// <summary>
        /// Sets the value of a VCP (Virtual Control Panel) feature on a monitor.
        /// </summary>
        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool SetVCPFeature(
            IntPtr hMonitor,
            byte bVCPCode,
            uint dwNewValue
        );

        /// <summary>
        /// Gets the current and maximum value of a VCP feature.
        /// </summary>
        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetVCPFeatureAndVCPFeatureReply(
            IntPtr hMonitor,
            byte bVCPCode,
            ref MONITOR_CAPABILITIES_REQUEST_TYPE pvct,
            ref uint pdwCurrentValue,
            ref uint pdwMaximumValue
        );

        /// <summary>
        /// Gets the length of the DDC/CI capabilities string.
        /// </summary>
        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetCapabilitiesStringLength(
            IntPtr hMonitor,
            ref uint pdwCapabilitiesStringLengthInCharacters
        );

        /// <summary>
        /// Retrieves the DDC/CI capabilities string from a monitor.
        /// </summary>
        [DllImport("dxva2.dll", SetLastError = true, CharSet = CharSet.Ansi)] // Added CharSet.Ansi for StringBuilder
        public static extern bool CapabilitiesRequestAndCapabilitiesReply(
            IntPtr hMonitor,
            [Out] StringBuilder pszASCIICapabilitiesString,
            uint dwCapabilitiesStringLengthInCharacters
        );

        #endregion

        /// <summary>
        /// Enumerates all physical monitors attached to the system that respond to DDC/CI.
        /// </summary>
        /// <param name="pnpMap">A WMI-generated dictionary mapping DDC/CI Descriptions to PnP Model IDs.</param>
        /// <returns>A list of PhysicalMonitorData objects, one for each valid monitor.</returns>
        public static List<PhysicalMonitorData> EnumeratePhysicalMonitors(Dictionary<string, string> pnpMap)
        {
            Debug.WriteLine("EnumeratePhysicalMonitors() started.");
            List<PhysicalMonitorData> allMonitors = new();

            Debug.WriteLine("Calling EnumDisplayMonitors...");
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                delegate (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
                {
                    Debug.WriteLine($"EnumDisplayMonitors callback triggered for hMonitor: {hMonitor}");
                    uint physicalMonitorCount = 0;
                    if (GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, ref physicalMonitorCount) && physicalMonitorCount > 0)
                    {
                        Debug.WriteLine($"Found {physicalMonitorCount} physical monitor(s) for this hMonitor.");
                        PHYSICAL_MONITOR[] pMonitors = new PHYSICAL_MONITOR[physicalMonitorCount];
                        if (GetPhysicalMonitorsFromHMONITOR(hMonitor, physicalMonitorCount, pMonitors))
                        {
                            Debug.WriteLine("Successfully retrieved physical monitor array.");
                            // Get the base device path (e.g., \\.\DISPLAY1)
                            Debug.WriteLine("Getting device path from monitor handle...");
                            string devicePath = GetDevicePathFromMonitorHandle(hMonitor);
                            Debug.WriteLine($"Device path: {devicePath}");

                            for (int i = 0; i < physicalMonitorCount; i++)
                            {
                                var pMonitor = pMonitors[i];
                                // Get the DDC/CI description name
                                string description = new string(pMonitor.szPhysicalMonitorDescription).Trim('\0');
                                Debug.WriteLine($"Processing physical monitor {i}: Handle={pMonitor.hPhysicalMonitor}, Description='{description}'");

                                // Use the description to look up the stable PnP_ID from our WMI map
                                pnpMap.TryGetValue(description, out string pnpId);
                                if (string.IsNullOrEmpty(pnpId))
                                {
                                    Debug.WriteLine($"PnP_ID not found in WMI map for '{description}'.");
                                }
                                else
                                {
                                    Debug.WriteLine($"Found matching PnP_ID: {pnpId}");
                                }

                                allMonitors.Add(new PhysicalMonitorData
                                {
                                    Handle = pMonitor.hPhysicalMonitor,
                                    Description = description,
                                    DeviceID = $"{devicePath}\\Monitor{i}", // The unstable ID
                                    PnP_ID = pnpId ?? string.Empty // The stable ID
                                });
                            }
                        }
                        else
                        {
                            Debug.WriteLine("GetPhysicalMonitorsFromHMONITOR failed.");
                        }
                    }
                    else
                    {
                        Debug.WriteLine("GetNumberOfPhysicalMonitorsFromHMONITOR failed or returned 0.");
                    }
                    return true; // Continue enumeration
                },
                IntPtr.Zero
            );
            Debug.WriteLine($"EnumDisplayMonitors finished. Found {allMonitors.Count} total physical monitors.");
            Debug.WriteLine("EnumeratePhysicalMonitors() finished.");
            return allMonitors;
        }

        /// <summary>
        /// Gets the Windows device path (e.g., "\\.\DISPLAY1") from an HMONITOR handle.
        /// </summary>
        /// <param name="hMonitor">The monitor handle.</param>
        /// <returns>The device path string, or "Unknown Device" if it fails.</returns>
private static string GetDevicePathFromMonitorHandle(IntPtr hMonitor)
        {
            Debug.WriteLine($"GetDevicePathFromMonitorHandle() started for hMonitor: {hMonitor}");
            // --- FIX: (IDE0090) 'new' expression can be simplified ---
            MONITORINFOEX info = new();
            info.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));

            if (GetMonitorInfo(hMonitor, ref info))
            {
                string deviceName = info.szDevice.Trim('\0');
                Debug.WriteLine($"GetMonitorInfo succeeded. Device path: {deviceName}");
                return deviceName;
            }
            Debug.WriteLine("GetMonitorInfo failed. Returning 'Unknown Device'.");
            return "Unknown Device";
        }
    }
}