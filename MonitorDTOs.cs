using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MultiDisplayVCPServer
{
    /// <summary>
    /// Represents the top-level status object sent to the client.
    /// This object is serialized into JSON.
    /// </summary>
    public class ServerStatus
    {
        /// <summary>
        /// A status message for the client (e.g., "OK: Found 2 monitors." or "ERROR: ...").
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; }

        /// <summary>
        /// A list of all DDC/CI compliant monitors discovered by the server.
        /// </summary>
        [JsonPropertyName("monitors")]
        public List<MonitorInfo> Monitors { get; set; } = new();
    }

    /// <summary>
    /// Represents all the information for a single physical monitor.
    /// </summary>
    public class MonitorInfo
    {
        /// <summary>
        /// The stable, unique Plug-and-Play Model ID for the monitor (e.g., "ACR0D1D").
        /// This is used by the client as the primary identifier for SET commands.
        /// </summary>
        [JsonPropertyName("id")]
        public string DeviceID { get; set; }

        /// <summary>
        /// The monitor's DDC/CI description name (e.g., "NVIDIA GeForce RTX 5070 Ti").
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// A list of all VCP features (e.g., Brightness) discovered for this monitor.
        /// </summary>
        [JsonPropertyName("capabilities")]
        public List<VcpFeature> Capabilities { get; set; } = new();
    }

    /// <summary>
    /// Represents a single VCP (Virtual Control Panel) feature of a monitor.
    /// </summary>
    public class VcpFeature
    {
        /// <summary>
        /// The 1-byte hex code for the feature (e.g., 0x10 for Brightness).
        /// </summary>
        [JsonPropertyName("code")]
        public byte Code { get; set; }

        /// <summary>
        /// The human-readable name for the feature (e.g., "Brightness").
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// The type of feature, either "Continuous" (like Brightness) or "Non-Continuous" (like Input Select).
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// Indicates if the feature is read/write.
        /// (Note: Currently assumed true if GetVCPFeatureAndVCPFeatureReply succeeds).
        /// </summary>
        [JsonPropertyName("readWrite")]
        public bool ReadWrite { get; set; }

        /// <summary>
        /// The value of the feature at the time it was discovered.
        /// </summary>
        [JsonPropertyName("current")]
        public uint CurrentValue { get; set; }

        /// <summary>
        /// The maximum possible value for this feature.
        /// </summary>
        [JsonPropertyName("max")]
        public uint MaximumValue { get; set; }

        /// <summary>
        /// For "Non-Continuous" features, a dictionary of possible values.
        /// (e.g., [1, "HDMI 1"], [3, "DisplayPort"]).
        /// </summary>
        [JsonPropertyName("nonContinuousValues")]
        public Dictionary<uint, string> NonContinuousValues { get; set; } = new();
    }
}