using System.Collections.Generic;
using System.Text.Json.Serialization; 

namespace MultiDisplayVCPServer
{
    public class ServerStatus
    {
        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("monitors")]
        public List<MonitorInfo> Monitors { get; set; } = new List<MonitorInfo>();
    }

    public class MonitorInfo
    {
        [JsonPropertyName("id")]
        public string DeviceID { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("capabilities")]
        public List<VcpFeature> Capabilities { get; set; } = new List<VcpFeature>();
    }

    public class VcpFeature
    {
        [JsonPropertyName("code")]
        public byte Code { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } 

        [JsonPropertyName("readWrite")]
        public bool ReadWrite { get; set; }

        [JsonPropertyName("current")]
        public uint CurrentValue { get; set; }

        [JsonPropertyName("max")]
        public uint MaximumValue { get; set; }

        [JsonPropertyName("nonContinuousValues")]
        public Dictionary<uint, string> NonContinuousValues { get; set; } = new Dictionary<uint, string>();
    }
}