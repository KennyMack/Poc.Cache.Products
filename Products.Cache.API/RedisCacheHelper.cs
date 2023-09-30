using System.Text.Json;
using StackExchange.Redis;

namespace Products.Cache.API;

public class RedisCacheHelper: IRedisCacheHelper
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    public RedisCacheHelper(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task Remove(string key)
    {
        var redisDatabase = _connectionMultiplexer.GetDatabase();
        
        if (await redisDatabase.KeyExistsAsync(key))
            await redisDatabase.KeyDeleteAsync(key);
    }

    public async Task Set<T>(string key, T data)
    {
        var redisDatabase = _connectionMultiplexer.GetDatabase();
        await redisDatabase.StringSetAsync(key, JsonSerializer.Serialize(data));
    }

    public async Task Set<T>(string key, T data, TimeSpan? expireTime)
    {
        var redisDatabase = _connectionMultiplexer.GetDatabase();
        await redisDatabase.StringSetAsync(key, JsonSerializer.Serialize(data), expireTime);
    }

    public async Task<T?> Get<T>(string key)
    {
        var redisDatabase = _connectionMultiplexer.GetDatabase();

        var stringValue = await redisDatabase.StringGetAsync(key);

        if (stringValue.IsNullOrEmpty || !stringValue.HasValue)
            return default(T);
        
        return JsonSerializer.Deserialize<T>(stringValue.ToString());
    }

    public async Task SetTime(string key, TimeSpan timeout)
    {
        var redisDatabase = _connectionMultiplexer.GetDatabase();

        if (await redisDatabase.KeyExistsAsync(key))
            await redisDatabase.KeyExpireAsync(key, timeout);
    }
    
    public async Task<TimeSpan?> GetKeyExpireTime(string key)
    {
        var redisDatabase = _connectionMultiplexer.GetDatabase();

        if (!await redisDatabase.KeyExistsAsync(key))
            return null;
        
        var dtExpire = await redisDatabase.KeyExpireTimeAsync(key);

        if (dtExpire == null)
            return null;
        
        //return new TimeSpan(0, 
        //    dtExpire.Value.Hour, 
        //    dtExpire.Value.Minute, 
        //    dtExpire.Value.Second,
        // dtExpire.Value.Millisecond);

        var dtBase = DateTime.Now;  //new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dtTime = dtExpire - dtBase; // ((DateTime)dtExpire).Subtract(new TimeSpan(dtBase.Ticks));

        return dtTime; //TimeSpan.FromMilliseconds((long)(dtTime.Ticks / 10000));
    }

    public async Task<bool> Exists(string key)
    {
        var redisDatabase = _connectionMultiplexer.GetDatabase();

        return await redisDatabase.KeyExistsAsync(key);
    }
}

public interface IRedisCacheHelper
{
    Task Remove(string key);
    Task Set<T>(string key, T data);
    Task Set<T>(string key, T data, TimeSpan? expireTime);
    Task<T?> Get<T>(string key);
    Task SetTime(string key, TimeSpan timeout);
    Task<TimeSpan?> GetKeyExpireTime(string key);
    Task<bool> Exists(string key);
}