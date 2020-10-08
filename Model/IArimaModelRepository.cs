using System.Collections.Generic;
using LanguageExt.Common;

namespace out_ai.Model
{
    public interface IArimaModelRepository
    {
        void fit_model(List<float> data);
        Result<List<float>> forecast(int numSteps);
    }
}