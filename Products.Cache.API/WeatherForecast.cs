namespace Products.Cache.API;

public class WeatherForecast
{
    public DateOnly Date { get; set; }

    public int TemperatureC { get; set; }

    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

    public string? Summary { get; set; }
    public bool? Env { get; set; }
    
    public string ConnStr { get; set; }
    
    public string? Products { get; set; }
    
    public string? Hostname { get; set; }
}