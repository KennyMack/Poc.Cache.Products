using Npgsql;
using Products.Cache.API;
using Products.Cache.API.Repositories;
using Products.Cache.API.Services;
using Products.Cache.API.Workers;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var dbPass = Environment.GetEnvironmentVariable("DB_PASS");
var dbHost = Environment.GetEnvironmentVariable("DB_HOST");
var dbUser = Environment.GetEnvironmentVariable("DB_USER");
var dbName = Environment.GetEnvironmentVariable("DB_NAME");

Settings.ConnStr = new NpgsqlConnectionStringBuilder
{
    Host = dbHost,
    Database = dbName,
    Username = dbUser,
    Password = dbPass,
    IncludeErrorDetail = true,
    Pooling = true,
    MaxPoolSize = 30,
    CommandTimeout = 100,
    
};

// Add services to the container.
/*
 builder.Services.AddNpgsqlDataSource(
   Environment.GetEnvironmentVariable(
   "DB_CONNECTION_STRING") ??
   "ERRO de connection string!!!", dataSourceBuilderAction: a => { a.UseLoggerFactory(NullLoggerFactory.Instance); });
   
 */

//localhost,port: 6379,password=Redis2019!
var redisConn = $"{Environment.GetEnvironmentVariable("REDIS_HOST")},port:{Environment.GetEnvironmentVariable("REDIS_PORT")},password={Environment.GetEnvironmentVariable("REDIS_PASSWORD")}";
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConn;
    options.InstanceName = "api-products";
});

builder.Services.AddControllers();

builder.Services.Configure<RouteHandlerOptions>(opt =>
{
    opt.ThrowOnBadRequest = false;
});

builder.Services.AddOutputCache(opt =>
{
    opt.DefaultExpirationTimeSpan = TimeSpan.FromSeconds(5);
    opt.AddBasePolicy(b =>
        b.Cache().SetLocking(false)
    );
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMemoryCache();

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(redisConn)
);

builder.Services.AddScoped<IRedisStreamHelper, RedisStreamHelper>();
builder.Services.AddScoped<IRedisCacheHelper, RedisCacheHelper>();
builder.Services.AddScoped<IMemoryCacheHelper, MemoryCacheHelper>();
builder.Services.AddScoped<ProductRepository>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<IDatabaseHelper, DatabaseHelper>();


//builder.Services.AddHostedService<ProductSaveWorker>();
builder.Services.AddHostedService<ProductPageWorker>();

var app = builder.Build();

Settings.Env = app.Environment.IsDevelopment();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseOutputCache();
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// create redis stream
// var redisStream = app.Services.GetRequiredService<IRedisStreamHelper>();
// await redisStream.InitializeStreamAsync();

app.Run();