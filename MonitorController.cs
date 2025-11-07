using System.Runtime.InteropServices;
using System.Text;

namespace MultiDisplayVCPServer
{
    public class PhysicalMonitorData
    {
        public IntPtr Handle { get; set; }
        public string Description { get; set; }
        public string DeviceID { get; set; } 
        public string PnP_ID { get; set; } 
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public char[] szPhysicalMonitorDescription;
    }

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

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left, top, right, bottom;
    }

    public delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    public static class MonitorController
    {
        public enum MONITOR_CAPABILITIES_REQUEST_TYPE : uint
        {
            MC_MOMENTARY = 0x00000001,
            MC_SET_PARAMETER = 0x00000002,
            MC_GET_PARAMETER = 0x00000004,
            MC_CAPABILITIES_STRING = 0x00000008,
            MC_SUPPORT_VSM_METHODS = 0x00000010,
            MC_USER_PREFERRED_SETTINGS = 0x00000020
        }

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor,
            ref uint pdwNumberOfPhysicalMonitors
        );

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor,
            uint dwPhysicalMonitorArraySize,
            [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray
        );

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr hmon, ref MONITORINFOEX mi);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool SetVCPFeature(
            IntPtr hMonitor,
            byte bVCPCode,
            uint dwNewValue
        );

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetVCPFeatureAndVCPFeatureReply(
            IntPtr hMonitor,
            byte bVCPCode,
            ref MONITOR_CAPABILITIES_REQUEST_TYPE pvct,
            ref uint pdwCurrentValue,
            ref uint pdwMaximumValue
        );

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetCapabilitiesStringLength(
            IntPtr hMonitor,
            ref uint pdwCapabilitiesStringLengthInCharacters
        );

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool CapabilitiesRequestAndCapabilitiesReply(
            IntPtr hMonitor,
            [Out] StringBuilder pszASCIICapabilitiesString,
            uint dwCapabilitiesStringLengthInCharacters
        );

        public static List<PhysicalMonitorData> EnumeratePhysicalMonitors(Dictionary<string, string> pnpMap)
        {
            List<PhysicalMonitorData> allMonitors = new List<PhysicalMonitorData>();

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                delegate (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
                {
                    uint physicalMonitorCount = 0;
                    if (GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, ref physicalMonitorCount) && physicalMonitorCount > 0)
                    {
                        PHYSICAL_MONITOR[] pMonitors = new PHYSICAL_MONITOR[physicalMonitorCount];
                        if (GetPhysicalMonitorsFromHMONITOR(hMonitor, physicalMonitorCount, pMonitors))
                        {
                            string devicePath = GetDevicePathFromMonitorHandle(hMonitor);

                            for (int i = 0; i < physicalMonitorCount; i++)
                            {
                                var pMonitor = pMonitors[i];
                                string description = new string(pMonitor.szPhysicalMonitorDescription).Trim('\0');

                                pnpMap.TryGetValue(description, out string pnpId);

                                allMonitors.Add(new PhysicalMonitorData
                                {
                                    Handle = pMonitor.hPhysicalMonitor,
                                    Description = description,
                                    DeviceID = $"{devicePath}\\Monitor{i}",
                                    PnP_ID = pnpId ?? string.Empty
                                });
                            }
                        }
                    }
                    return true;
                },
                IntPtr.Zero
            );

            return allMonitors;
        }

        private static string GetDevicePathFromMonitorHandle(IntPtr hMonitor)
        {
            MONITORINFOEX info = new MONITORINFOEX();
            info.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));

            if (GetMonitorInfo(hMonitor, ref info))
            {
                return info.szDevice.Trim('\0');
            }
            return "Unknown Device";
        }
    }
}