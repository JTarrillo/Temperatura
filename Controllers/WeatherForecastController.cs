using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Options;
using System.Security;

namespace Temperatura.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger)
        {
            _logger = logger;
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public IActionResult GetWeatherForecast(string username, string password)
        {
            try
            {
                _logger.LogInformation("Attempting to retrieve hardware temperatures...");

                string computer = Environment.MachineName;
                string domain = Environment.UserDomainName;

                SecureString securepassword = new SecureString();
                foreach (char c in password)
                {
                    securepassword.AppendChar(c);
                }

                // create Credentials
                CimCredential Credentials = new CimCredential(PasswordAuthenticationMechanism.Default,
                                                              domain,
                                                              username,
                                                              securepassword);

                // create SessionOptions using Credentials
                WSManSessionOptions SessionOptions = new WSManSessionOptions();
                SessionOptions.AddDestinationCredentials(Credentials);

                // create Session using computer, SessionOptions
                CimSession Session = CimSession.Create(computer, SessionOptions);

                _logger.LogInformation("CIM session created successfully.");

                // Query temperatures
                var cpuTemperature = GetTemperature(Session, "SELECT * FROM Win32_TemperatureProbe WHERE Name LIKE '%CPU%'");
                var gpuTemperature = GetTemperature(Session, "SELECT * FROM Win32_TemperatureProbe WHERE Name LIKE '%GPU%'");
                var hddTemperature = GetTemperature(Session, "SELECT * FROM Win32_TemperatureProbe WHERE Name LIKE '%HDD%'");

                _logger.LogInformation("Hardware temperatures retrieved successfully.");

                return Ok(new
                {
                    CPUTemperature = cpuTemperature,
                    GPUTemperature = gpuTemperature,
                    HDDTemperature = hddTemperature
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting hardware temperatures: {0}", ex.Message);
                return StatusCode(500, "Internal server error");
            }
        }

        private double GetTemperature(CimSession session, string query)
        {
            var temperatureInstances = session.QueryInstances("root\\cimv2", "WQL", query);
            var temperatureProperty = temperatureInstances.FirstOrDefault()?.CimInstanceProperties["CurrentTemperature"];
            if (temperatureProperty != null && temperatureProperty.Value != null)
            {
                return Convert.ToDouble(temperatureProperty.Value) / 10.0; // Convert from tenths of degrees Celsius to degrees Celsius
            }
            return double.NaN; // Indicate temperature couldn't be retrieved
        }
    }
}
