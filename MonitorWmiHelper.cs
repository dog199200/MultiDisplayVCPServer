using System.Management;

namespace MultiDisplayVCPServer
{
    public static class MonitorWmiHelper
    {
        private static string ParsePnP_ID(string fullId)
        {
            if (string.IsNullOrEmpty(fullId)) return null;
            string[] parts = fullId.Split('\\');
            if (parts.Length > 1)
            {
                return parts[1];
            }
            return null;
        }

        public static Dictionary<string, string> GetMonitorPnPMap()
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
                        if (!map.ContainsKey(name))
                        {
                            map[name] = shortId;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WMI Query Error (GetMonitorPnPMap): {ex.Message}");
            }
            return map;
        }

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
    }
}