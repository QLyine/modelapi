using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using out_ai.Data;

namespace out_ai.Model
{
    public interface ICoordinator
    {
        ModelVersion incrementAndPublishNewModelVersion();
        void publishModelVersion(ModelVersion modelVersion);
        ImmutableList<APIHostPort> getApiHostPorts();
        APIHostPort? getRandomApiHostPort();
        
        ModelVersion getCurrentModelVersion();

    }
}