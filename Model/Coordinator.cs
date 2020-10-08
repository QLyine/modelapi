#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Security.AccessControl;
using System.Text.Json;
using System.Threading.Tasks;
using dotnet_etcd;
using Etcdserverpb;
using Google.Protobuf;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.SomeHelp;
using Mvccpb;
using org.apache.zookeeper;
using org.apache.zookeeper.client;
using out_ai.Data;

namespace out_ai.Model
{
    public class Coordinator : ICoordinator
    {
        private const string NODES_KEY = "nodes";
        private const string MODEL_VERSION = "modelVersion";
        private const string MODEL_VERSION_LOCK = "modelVersion_lock";
        private readonly ModelVersion DEFAULT_MODEL_VERSION = new ModelVersion(0, 0);
        private EtcdClient client;
        private ModelVersion _modelVersion;
        private List<APIHostPort> _apiHostPorts;
        private readonly Random _random = new Random();
        private JsonSerializerOptions _jsonSerializerOptions;

        private readonly object model_version_lock = new object();
        private readonly object apiHostsList_lock = new object();

        public Coordinator(string etcdHost)
        {
            this._jsonSerializerOptions = new JsonSerializerOptions();
            _jsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;


            client = new EtcdClient(etcdHost);

            _modelVersion = getModelVersion();

            this._apiHostPorts = new List<APIHostPort>();
            listAndAddNodesBootStrap();

            addWatches();
        }

        private ModelVersion getModelVersion()
        {
            var response = client.Get(MODEL_VERSION);
            if (response.Kvs.Count > 0 && response.Kvs[0].Value != null)
            {
                var json = response.Kvs[0].Value.ToStringUtf8();
                return deserialize<ModelVersion>(json).Match(v => v, exception => DEFAULT_MODEL_VERSION);
            }

            return DEFAULT_MODEL_VERSION;
        }

        private void addWatches()
        {
            Task.Run(() => client.WatchRange(NODES_KEY, nodesWatch));
            Task.Run(() => client.Watch(MODEL_VERSION, watchModelVersion));
            Console.WriteLine("Added Watches");
        }

        public ModelVersion? incrementAndPublishNewModelVersion()
        {
            try
            {
                var lockResponse = client.Lock(name: MODEL_VERSION_LOCK, deadline: DateTime.UtcNow.AddMilliseconds(500));
                var modelVersion = getModelVersion();
                if (modelVersion.Version <= _modelVersion.Version)
                {
                    _modelVersion.Version++;
                    _modelVersion.TimeStamp = millis();
                    var json = serializeJson(_modelVersion);
                    client.Put(MODEL_VERSION, json);
                }

                client.Unlock(MODEL_VERSION_LOCK);
                filterOutModelVersions();
                return _modelVersion;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            return null;
        }

        public void publishModelVersion(ModelVersion modelVersion)
        {
            try
            {
                var lockResponse = client.Lock(name: MODEL_VERSION_LOCK, deadline: DateTime.UtcNow.AddSeconds(5));
                client.Put(MODEL_VERSION, serializeJson(modelVersion));
                client.Unlock(MODEL_VERSION_LOCK);
                filterOutModelVersions();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public ImmutableList<APIHostPort> getApiHostPorts()
        {
            ImmutableList<APIHostPort> result = null;
            lock (apiHostsList_lock)
            {
                result = _apiHostPorts.ToImmutableList();
            }

            return result ?? ImmutableList<APIHostPort>.Empty;
        }

        public APIHostPort? getRandomApiHostPort()
        {
            lock (_apiHostPorts)
            {
                if (_apiHostPorts.Count <= 0)
                {
                    return null;
                }

                var next = _random.Next(_apiHostPorts.Count);
                return _apiHostPorts.ElementAt(next);
            }
        }

        public ModelVersion getCurrentModelVersion()
        {
            return new ModelVersion(_modelVersion.Version, _modelVersion.TimeStamp);
        }

        private void filterOutModelVersions()
        {
            lock (apiHostsList_lock)
            {
                _apiHostPorts.RemoveAll(port => port.ModelVersion != _modelVersion.Version);
            }
        }

        private void listAndAddNodesBootStrap()
        {
            var rangeResponse = client.GetRange(NODES_KEY);
            if (!rangeResponse.IsNull() && !rangeResponse.Kvs.IsNull() && rangeResponse.Kvs.Count > 0)
            {
                foreach (var kv in rangeResponse.Kvs)
                {
                    var keyStr = kv.Key.ToStringUtf8();
                    var valueStr = kv.Value.ToStringUtf8();
                    consumeAndAddApiHostPort(deserialize<APIHostPort>(valueStr));
                }
            }
        }

        // Print function that prints key and value from the watch response
        private void nodesWatch(WatchResponse response)
        {
            if (response.Events.Count == 0)
            {
                Console.WriteLine(response);
            }
            else
            {
                Console.WriteLine(
                    $"{response.Events[0].Kv.Key.ToStringUtf8()}:{response.Events[0].Kv.Value.ToStringUtf8()}");
            }

            foreach (var responseEvent in response.Events)
            {
                if (responseEvent != null && responseEvent.Kv.Key != null)
                {
                    var key = responseEvent.Kv.Key.ToStringUtf8();
                    if (responseEvent.Kv.Value != null)
                    {
                        Result<APIHostPort> value = deserialize<APIHostPort>(responseEvent.Kv.Value.ToStringUtf8());
                        Console.WriteLine("Got new apiHostPort - {0}", value);
                        consumeAndAddApiHostPort(value);
                    }
                }
            }
        }

        private void consumeAndAddApiHostPort(Result<APIHostPort> value)
        {
            value.IfSucc(value =>
            {
                if (value.ModelVersion == _modelVersion.Version)
                {
                    lock (_apiHostPorts)
                    {
                        _apiHostPorts.Add(value);
                    }

                    Console.WriteLine("Got new apiHostPort with current ModelVersion - {0}", value);
                }
            });
        }

        private void watchModelVersion(WatchResponse watchResponse)
        {
            Console.WriteLine("Fire Watch");
            if (watchResponse.Events.Count > 0 && watchResponse.Events[0].Kv != null)
            {
                Console.WriteLine(
                    $"{watchResponse.Events[0].Kv.Key.ToStringUtf8()}:{watchResponse.Events[0].Kv.Value.ToStringUtf8()}");
                var json = watchResponse.Events[0].Kv.Value.ToStringUtf8();
                var maybeModelVersion = deserialize<ModelVersion>(json);
                if (maybeModelVersion.IsSuccess)
                {
                    maybeModelVersion.IfSucc(version =>
                    {
                        lock (model_version_lock)
                        {
                            if (version.TimeStamp != _modelVersion.TimeStamp)
                            {
                                _modelVersion = version;
                            }
                        }

                        filterOutModelVersions();
                    });
                }

                Console.WriteLine("Got new modelversion - {0}", _modelVersion);
            }
        }

        private string serializeJson<T>(T @object)
        {
            return JsonSerializer.Serialize(@object, _jsonSerializerOptions);
        }

        private Result<T> deserialize<T>(string json)
        {
            try
            {
                var value = JsonSerializer.Deserialize<T>(json);
                return new Result<T>(value);
            }
            catch (Exception e)
            {
                if (!json.IsNull())
                {
                    Console.WriteLine(json);
                }

                Console.WriteLine(e);
                return new Result<T>(e);
            }
        }

        public long millis()
        {
            return (long.MaxValue + DateTime.UtcNow.ToBinary()) / 10000;
        }
    }
}