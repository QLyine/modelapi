using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace out_ai.Data
{
    
    public class BrokerModelData
    {
        public BrokerModelData()
        {
            Data = new List<float>();
        }

        public BrokerModelData(List<float> data)
        {
            Data = data;
        }
        public List<float> Data { get; set; }
    }
}