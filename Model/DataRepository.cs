using System;
using System.Collections;
using System.Collections.Generic;
using Cassandra;
using out_ai.Data;

namespace out_ai.Model
{
    public class DataRepository : IDataRepository
    {
        private const string KEY_SPACE = "test";
        private const string TABLE = "test";
        private const string SENSOR_ID = "sensorId";
        private const string TS = "time";
        private const string TEMP = "temp";
        private readonly ICluster _cluster;
        private readonly ISession _session;
        private readonly PreparedStatement _prepared_select;
        private readonly PreparedStatement _prepared_insert;

        public DataRepository(ICluster cluster)
        {
            _cluster = cluster;
            this._session = _cluster.Connect(KEY_SPACE);
            var select = string.Format(
                "SELECT * FROM {0}.{1} WHERE  {2} = ? AND {3} >= ? AND {3} <= ?", KEY_SPACE,
                TABLE, SENSOR_ID, TS);
            this._prepared_select = _session.Prepare(select);
            this._prepared_insert =
                _session.Prepare(String.Format("INSERT INTO {0}.{1} ({2}, {3}, {4})  VALUES (?, ?, ?)", KEY_SPACE,
                    TABLE, SENSOR_ID, TS, TEMP));
        }

        public IEnumerable<TempData> getDataFrom(string senorId, long from, long to)
        {
            var fromDT = new DateTime(@from);
            var toDT = new DateTime(@to);
            var boundStatement = _prepared_select.Bind(senorId, @from, to);
            var list = new List<TempData>();
            var rowSet = _session.Execute(boundStatement);
            foreach (var row in rowSet.GetRows())
            {
                var sensorId = row.GetValue<string>(SENSOR_ID);
                var ts = row.GetValue<long>(TS);
                var temp = row.GetValue<float>(TEMP);
                list.Add(new TempData(senorId, ts, temp));
            }

            return list;
        }

        public void insertData(TempData data)
        {
            var boundStatement = _prepared_insert.Bind(data.SensorId, new DateTime(data.Ts), data.Temp);
            _session.Execute(boundStatement);
        }
    }
}