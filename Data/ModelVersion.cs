using System.Text.Json.Serialization;

namespace out_ai.Data
{
    public class ModelVersion
    {
        public ModelVersion()
        {
        }

        public ModelVersion(long version, long timeStamp)
        {
            Version = version;
            TimeStamp = timeStamp;
        }

        [JsonPropertyName("version")] public long Version { get; set; }
        [JsonPropertyName("timestamp")] public long TimeStamp { get; set; }

        public override string ToString()
        {
            return $"{nameof(Version)}: {Version}, {nameof(TimeStamp)}: {TimeStamp}";
        }
    }
}