using System.Collections.Generic;
using Failsafe;
using LanguageExt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using out_ai.Data;
using out_ai.Model;

namespace out_ai.Controllers
{
    [ApiController]
    public class ModelController : ControllerBase
    {
        private readonly IArimaModelRepository _iRepository;

        private readonly ILogger<WeatherForecastController> _logger;

        public ModelController(ILogger<WeatherForecastController> logger, IArimaModelRepository iRepository)
        {
            _logger = logger;
            _iRepository = iRepository;
        }

        [HttpPost]
        [Route("api/fit_model")]
        public ActionResult InsertData(BrokerModelData data)
        {
            if (!data.IsNull() && data.Data.Count > 0)
            {
                _iRepository.fit_model(data.Data);
            }

            return Ok();
        }


        [HttpGet]
        [Route("api/forecast/{num_steps}")]
        public ActionResult<BrokerModelData> Get(int num_steps)
        {
            var maybeForecast = _iRepository.forecast(num_steps);
            List<float> values = maybeForecast.Match<List<float>>(value => value, exception => new List<float>());
            return new BrokerModelData(values);
        }
    }
}