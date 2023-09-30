using Products.Cache.API.Repositories;

namespace Products.Cache.API.Workers;

public class ProductPageWorker: BackgroundService
{
    private const string PageKey = "PRODUCTS-PAGES";
    private readonly ILogger<ProductSaveWorker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ProductPageWorker(
        ILogger<ProductSaveWorker> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(5));
        await using var scope = _serviceProvider.CreateAsyncScope();
        var redisCache = scope.ServiceProvider.GetRequiredService<IRedisCacheHelper>();
        var memoryCache = scope.ServiceProvider.GetRequiredService<IMemoryCacheHelper>();
        var productRepository = scope.ServiceProvider.GetRequiredService<ProductRepository>();

        do
        {
            try
            {
                _logger.LogInformation("Updating product page cache.");

                var exists = await redisCache.Exists(PageKey);

                if (!exists)
                    continue;

                var pages = await redisCache.Get<string[]>(PageKey);

                if (pages == null)
                    continue;

                _logger.LogInformation($"{pages.Length} pages found.");

                var pagesList = pages.ToList();
                var pagesRows = pages.ToArray();
                foreach (var page in pagesRows)
                {
                    var pageCached = await redisCache.Exists(page);

                    if (!pageCached)
                    {
                        pagesList.Remove(page);
                        continue;
                    }

                    var pageNumber = Convert.ToInt32(page.Split('-')[1]);
                    var pageSize = Convert.ToInt32(page.Split('-')[2]);

                    var products = await productRepository.GetByPage(pageNumber, pageSize, stoppingToken);

                    var keyProductPage = $"PRODUCTS-{pageNumber}-{pageSize}";

                    _logger.LogInformation($"Page {keyProductPage} updated.");
                    var expireTime = await redisCache.GetKeyExpireTime(keyProductPage);
                    
                    var expire = TimeSpan.FromMinutes(10);
                    if (expireTime != null)
                        expire = TimeSpan.FromSeconds(expireTime.Value.TotalSeconds);

                    await memoryCache.Remove(keyProductPage);
                    await redisCache.Remove(keyProductPage);

                    await redisCache.Set(keyProductPage, products, expire);
                    await memoryCache.Set(keyProductPage, products, expire);
                }

                await redisCache.Remove(PageKey);
                await redisCache.Set(PageKey, pagesList);
                
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex,
                    $"{DateTime.Now} Failed to update product page cache {ex.Message} Inner: {ex.InnerException?.Message}");
                await Task.Delay(1000, stoppingToken);
            }

        } while (await timer.WaitForNextTickAsync(stoppingToken));

    }
}