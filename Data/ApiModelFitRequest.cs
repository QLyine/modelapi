using System.Collections.Generic;
using Newtonsoft.Json;

namespace out_ai.Data
{
    public class ApiModelFitRequest
    {
        public ApiModelFitRequest()
        {
        }

        public ApiModelFitRequest(long modelVersion, List<float> data)
        {
            ModelVersion = modelVersion;
            Data = data;
        }

        [JsonProperty("modelVersion")]
        public long ModelVersion { get; set; }
        [JsonProperty("dta")]
        public List<float> Data { get; set; }
    }
}