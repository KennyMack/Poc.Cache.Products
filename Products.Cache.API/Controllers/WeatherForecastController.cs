using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace Products.Cache.API.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private readonly IRedisCacheHelper _redisCache;
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly ILogger<WeatherForecastController> _logger;

    public WeatherForecastController(ILogger<WeatherForecastController> logger,
        IRedisCacheHelper redis)
    {
        _logger = logger;
        _redisCache = redis;
    }

    [HttpGet("Cache", Name = "GetCache")]
    public async Task<IActionResult> GetCache()
    {
        var product = new Product
        {
            ID = NewId.NextSequentialGuid(),
            SKU = $"{DateTime.Now:hh:mm:ss t z}",
            TITLE = $"Product-{DateTime.Now:hh:mm:ss t z}",
            DESCRIPTION = "Prod",
            REDUCEDTITLE = $"Prod-{DateTime.Now:hh:mm:ss t z}",
            PRICE = 100
        };
        await _redisCache.Set(product.ID.ToString(), product);

        return Ok();
    }

    [HttpGet("Weather", Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        using var conn = new Npgsql.NpgsqlConnection(Settings.ConnStr.ConnectionString);
        using var cmd = new Npgsql.NpgsqlCommand();
        conn.Open();
        cmd.CommandText = @"SELECT * FROM public.products";
        cmd.Connection = conn;

        using var reader = cmd.ExecuteReader();

        StringBuilder str = new StringBuilder();
        while (reader.Read())
        {
            str.Append($"{reader["SKU"]}, ");
        }

        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)],
                Env = Settings.Env,
                ConnStr = Settings.ConnStr.ConnectionString,
                Products = str.ToString(),
                Hostname = Environment.GetEnvironmentVariable("HOSTNAME")
            })
            .ToArray();
    }
}