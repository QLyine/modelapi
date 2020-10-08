

namespace out_ai.Data
{
    public class TempData
    {
        public TempData()
        {
        }

        public TempData(string sensorId, long ts, float temp)
        {
            SensorId = sensorId;
            Ts = ts;
            Temp = temp;
        }

        public string SensorId { get; set; }
        public long Ts { get; set; }

        public float Temp
        {
            get;
            set;

        }
    }
}