using StackExchange.Redis;

namespace Products.Cache.API;

public class RedisStreamHelper: IRedisStreamHelper
{
    const string StreamName = "products-changed";
    const string GroupName = $"{StreamName}-consumers";
    
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    public RedisStreamHelper(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task InitializeStreamAsync()
    {
        var redisDatabase = _connectionMultiplexer.GetDatabase();

        if (!await redisDatabase.KeyExistsAsync(StreamName) ||
            (await redisDatabase.StreamGroupInfoAsync(StreamName))
            .All(x => x.Name is not GroupName))
            await redisDatabase.StreamCreateConsumerGroupAsync(StreamName, GroupName, "0-0");
    }
    
    public async Task Push<T>(NameValueEntry[] data)
    {
        var redisDatabase = _connectionMultiplexer.GetDatabase();

        await redisDatabase.StreamAddAsync(StreamName, data);
    }

    public async Task<StreamEntry[]> Get()
    {
        var redisDatabase = _connectionMultiplexer.GetDatabase();

        var entries = await redisDatabase.StreamReadGroupAsync(StreamName, 
            GroupName, Environment.MachineName, ">", 50, true);

        return entries;
    }

    public async Task<bool> HasMessages()
    {
        var redisDatabase = _connectionMultiplexer.GetDatabase();
        
        var pending = await redisDatabase.StreamPendingAsync(StreamName, GroupName);

        return pending.PendingMessageCount > 0;
    }

    public async Task Ack(RedisValue id)
    {
        var redisDatabase = _connectionMultiplexer.GetDatabase();

        await redisDatabase.StreamAcknowledgeAsync(StreamName, GroupName, id);
    }
}

public interface IRedisStreamHelper
{
    Task InitializeStreamAsync();
    Task Push<T>(NameValueEntry[] data);
    Task<StreamEntry[]> Get();
    Task<bool> HasMessages();
    Task Ack(RedisValue id);
}