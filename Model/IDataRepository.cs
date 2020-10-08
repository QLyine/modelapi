using System.Collections.Generic;
using out_ai.Data;

namespace out_ai.Model
{
    public interface IDataRepository
    {
        IEnumerable<TempData> getDataFrom(string senorId, long from, long to);
        void insertData(TempData data);
    }
}