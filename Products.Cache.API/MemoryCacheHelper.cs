using Microsoft.Extensions.Caching.Memory;

namespace Products.Cache.API;

public class MemoryCacheHelper : IMemoryCacheHelper
{
    private readonly IMemoryCache _memoryCache;

    public MemoryCacheHelper(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public Task Set<T>(string key, T data)
    {
        _memoryCache.Set(key, data);
        
        return Task.CompletedTask;
    }
    
    public Task Set<T>(string key, T data, TimeSpan expirationTime)
    {
        _memoryCache.Set(key, data, expirationTime);
        
        return Task.CompletedTask;
    }

    public Task<T?> Get<T>(string key)
    {
        var valueExists = _memoryCache.TryGetValue(key, out T? value);

        return Task.FromResult(!valueExists ? default(T) : value);
    }

    public Task<bool> Exists(string key)
    {
        var valueExists = _memoryCache.TryGetValue(key, out _);

        return Task.FromResult(valueExists);
    }
    
    public Task Remove(string key)
    {
        _memoryCache.Remove(key);

        return Task.CompletedTask;
    }
}

public interface IMemoryCacheHelper
{
    Task Remove(string key);
    Task Set<T>(string key, T data);
    Task Set<T>(string key, T data, TimeSpan expirationTime);
    Task<T?> Get<T>(string key);
    Task<bool> Exists(string key);
}