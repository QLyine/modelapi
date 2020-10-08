using System.Collections.Generic;
using Newtonsoft.Json;

namespace out_ai.Data
{
    public class ApiModelForecastResponse
    {
        public ApiModelForecastResponse()
        {
        }

        public ApiModelForecastResponse(List<float> data)
        {
            Data = data;
        }

        [JsonProperty("data")]
        public List<float> Data { get; set; }
    }
}