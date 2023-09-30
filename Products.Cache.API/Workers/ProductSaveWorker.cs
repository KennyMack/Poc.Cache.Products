using System.Globalization;
using Products.Cache.API.Repositories;
using Products.Cache.API.Services;

namespace Products.Cache.API.Workers;

public class ProductSaveWorker: BackgroundService
{
    private readonly ILogger<ProductSaveWorker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ProductSaveWorker(
        ILogger<ProductSaveWorker> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(1));

        do
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var redisStream = scope.ServiceProvider.GetRequiredService<IRedisStreamHelper>();
            var redisCache = scope.ServiceProvider.GetRequiredService<IRedisCacheHelper>();
            var productRepository = scope.ServiceProvider.GetRequiredService<ProductRepository>();
            var productService = scope.ServiceProvider.GetRequiredService<ProductService>();
        
            await redisStream.InitializeStreamAsync();
            
            try
            {
                var messages = await redisStream.Get();

                _logger.LogInformation("{Date} Received {MessagesCount} messages [{Thread}] Machine {MachineName}",
                    DateTime.Now, messages.Length, Environment.CurrentManagedThreadId, Environment.MachineName);

                if (messages.Length <= 0)
                    continue;

                _logger.LogInformation("{Date} Processing {MessagesCount} messages [{Thread}] Machine {MachineName}",
                    DateTime.Now, messages.Length, Environment.CurrentManagedThreadId, Environment.MachineName);

                var products = (from message in messages
                    let values = message.Values.ToDictionary(x => x.Name.ToString(), x => x.Value)
                    let product = new Product()
                    {
                        ID = Guid.Parse(values[nameof(Product.ID)].ToString()),
                        SKU = values[nameof(Product.SKU)].ToString(),
                        TITLE = values[nameof(Product.TITLE)].ToString(),
                        DESCRIPTION = values[nameof(Product.DESCRIPTION)].ToString(),
                        REDUCEDTITLE = values[nameof(Product.REDUCEDTITLE)].ToString(),
                        PRICE = double.Parse(values[nameof(Product.PRICE)].ToString(), new CultureInfo("pt-BR"))
                    }
                    select new NewProductMessage(message.Id, product)).ToList();

                var invalidProducts = await productService.ValidateProductsAsync(products.Select(x => x.Product).ToArray(), stoppingToken);
                
                // remove from product list by sku
                var productsSave = products.Where(x => 
                    !invalidProducts.Select(s => s.Product?.ID)
                        .Contains(x.Product.ID))
                    .ToList();
                var productsFailed = products.Where(x => 
                    invalidProducts.Select(s => s.Product?.ID)
                        .Contains(x.Product.ID))
                    .ToList();
                
                await productRepository.Insert(productsSave.Select(x => x.Product), stoppingToken);
                
                foreach (var message in productsSave)
                    await redisCache.Set($"PROCESSED-{message.Product.ID}", message.Product.ID, TimeSpan.FromMinutes(15));

                foreach (var message in productsFailed)
                    await redisCache.Set($"FAILED-{message.Product.ID}", message, TimeSpan.FromMinutes(15));
                
                foreach (var message in products)
                {
                    await redisStream.Ack(message.Id);
                    await redisCache.Remove($"PENDING-{message.Product.ID}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"{DateTime.Now} Failed to save {ex.Message} Inner: {ex.InnerException?.Message}");
                await Task.Delay(1000, stoppingToken);
                // Console.WriteLine();
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}