using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Failsafe;
using LanguageExt;
using LanguageExt.Common;
using out_ai.Data;
using Polly;

namespace out_ai.Model
{
    public class ArimaModelRepository : IArimaModelRepository
    {
        private const int MAX_RETRY = 15;
        private const int MAX_RETRY_FORECAST_CALL = 3;
        private ICoordinator _coordinator;
        private HttpClient _client;
        private JsonSerializerOptions _jsonSerializerOptions;
        private Random _random = new Random();
        private string FIT_MODEL_ENDPOINT = "api/fit_model";
        private string FORECAST = "api/forecast";

        public ArimaModelRepository(ICoordinator coordinator)
        {
            _coordinator = coordinator;
            _client = new HttpClient();
            this._jsonSerializerOptions = new JsonSerializerOptions();
            _jsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        }

        public void fit_model(List<float> data)
        {
            var apiHostPorts = _coordinator.getApiHostPorts();
            var currentModelVersion = _coordinator.getCurrentModelVersion();
            var newModelVersion = _coordinator.incrementAndPublishNewModelVersion();
            try
            {
                var httpResponseMessage = doPostCall(apiHostPorts, FIT_MODEL_ENDPOINT,
                    new ApiModelFitRequest(newModelVersion.Version, data));
                if (!httpResponseMessage.IsSuccessStatusCode)
                {
                    Console.WriteLine("failed to install new model");
                    // We can rollback by simply publishing the "currentModelVersion" which refers to the old model version
                    // Which will make the api models reload to and old model
                    // It is not needed since it is not a requirement
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public Result<List<float>> forecast(int numSteps)
        {
            // Hold the requests in case we don't have any updated or simply any API to receive the requests
            // Retry a specified number of times, using a function to 
            // calculate the duration to wait between retries based on 
            // the current retry attempt (allows for exponential backoff)
            // In this case will wait for
            //  2 ^ 1 = 2 seconds then
            //  2 ^ 2 = 4 seconds then
            //  2 ^ 3 = 8 seconds then
            //  2 ^ 4 = 16 seconds then
            Policy<bool>.HandleResult(false).WaitAndRetry(4, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
            ).Execute(() => _coordinator.getApiHostPorts().Count > 0);
            var endpoint = string.Format("{0}/{1}", FORECAST, numSteps);
            try
            {
                var jsonBodyResp = Retry.Create()
                    .CatchAnyException()
                    .WithDelay(i => TimeSpan.FromSeconds(i * 0.5))
                    .WithMaxTryCount(MAX_RETRY_FORECAST_CALL)
                    .Execute<string>(() =>
                        _client.GetStringAsync(createUri(_coordinator.getApiHostPorts(), endpoint)).Result);

                return deserialize<ApiModelForecastResponse>(jsonBodyResp)
                    .Map(value => value.Data);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return new Result<List<float>>(e);
            }
        }

        private HttpResponseMessage doPostCall<T>(ImmutableList<APIHostPort> apiHostPorts, string endpoint, T objBody)
        {
            var json = serializeJson(objBody);
            var jsonBody = new StringContent(json, Encoding.UTF8, "application/json");
            return Retry.Create()
                .CatchAnyException()
                .WithDelay(i => TimeSpan.FromSeconds(i * 0.5))
                .WithMaxTryCount(MAX_RETRY)
                .Execute<HttpResponseMessage>(() =>
                    _client.PostAsync(createUri(apiHostPorts, endpoint), jsonBody).Result);
        }

        private string createUri(ImmutableList<APIHostPort> apiHostPorts, string endpoint)
        {
            var next = _random.Next(apiHostPorts.Count);
            var elementAt = apiHostPorts.ElementAt(next);
            return string.Format("http://{0}:{1}/{2}", elementAt.Host, elementAt.Port, endpoint);
        }

        private string serializeJson<T>(T @object)
        {
            return JsonSerializer.Serialize(@object, _jsonSerializerOptions);
        }

        private Result<T> deserialize<T>(string json)
        {
            try
            {
                var value = JsonSerializer.Deserialize<T>(json, _jsonSerializerOptions);
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
    }
}