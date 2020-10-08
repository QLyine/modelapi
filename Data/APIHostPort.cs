using System;
using System.Text.Json.Serialization;

namespace out_ai.Data
{
    public class APIHostPort
    {
        public APIHostPort()
        {
        }

        public APIHostPort(string host, int port, long modelVersion)
        {
            ModelVersion = modelVersion;
            Host = host;
            Port = port;
        }

        [JsonPropertyName("host")] public string Host { get; set; }
        [JsonPropertyName("port")] public int Port { get; set; }
        [JsonPropertyName("model")] public long ModelVersion { get; set; }

        protected bool Equals(APIHostPort other)
        {
            return Host == other.Host && Port == other.Port && ModelVersion == other.ModelVersion;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((APIHostPort) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Host, Port, ModelVersion);
        }

        public override string ToString()
        {
            return $"{nameof(Host)}: {Host}, {nameof(Port)}: {Port}, {nameof(ModelVersion)}: {ModelVersion}";
        }
    }
}