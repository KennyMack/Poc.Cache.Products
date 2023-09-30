using System.Globalization;
using MassTransit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using NetSwissTools.Utils;
using Products.Cache.API.Repositories;
using Products.Cache.API.Services;
using StackExchange.Redis;

namespace Products.Cache.API.Controllers;

[ApiController]
[Route("[controller]")]
public class ProductController : ControllerBase
{
    private readonly IRedisStreamHelper _redisStream;
    private readonly IRedisCacheHelper _redisCache;
    private readonly IMemoryCacheHelper _memoryCache;
    private readonly ProductRepository _productRepository;
    private readonly ProductService _productService;
    
    
    public ProductController(IRedisCacheHelper redisCache, 
        IMemoryCacheHelper memoryCache, 
        ProductRepository productRepository, 
        IRedisStreamHelper redisStream, 
        ProductService productService)
    {
        _redisCache = redisCache;
        _memoryCache = memoryCache;
        _productRepository = productRepository;
        _redisStream = redisStream;
        _productService = productService;
    }
    
    // Create product from dto and validate in service and save 
    [HttpPost("Create/db", Name = "CreateProductDb")]
    public async Task<IActionResult> CreateProductDb(CreateProductDTO productDto, CancellationToken stoppingToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var id = NewId.NextSequentialGuid();

        var product = new Product
        {
            ID = id,
            SKU = productDto.SKU,
            TITLE = productDto.TITLE,
            DESCRIPTION = productDto.DESCRIPTION,
            REDUCEDTITLE = productDto.REDUCEDTITLE,
            PRICE = productDto.PRICE
        };

        var productFailed = await _productService.ValidateProductsAsync(new[] { product }, stoppingToken);

        if (productFailed.Length > 0)
            return BadRequest(productFailed);

        await _productRepository.Insert(product, stoppingToken);

        return Ok(product);
    }


    // Create product from dto and save to memory cache and redis
    [HttpPost("Create", Name = "CreateProduct")]
    public async Task<IActionResult> CreateProduct(CreateProductDTO productDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var id = NewId.NextSequentialGuid();
        
        var product = new Product
        {
            ID = id,
            SKU = productDto.SKU,
            TITLE = productDto.TITLE,
            DESCRIPTION = productDto.DESCRIPTION,
            REDUCEDTITLE = productDto.REDUCEDTITLE,
            PRICE = productDto.PRICE
        };

        await _redisStream.Push<Product>(new []
        {
            new NameValueEntry(nameof(product.ID), product.ID.ToString()),
            new NameValueEntry(nameof(product.SKU), product.SKU),
            new NameValueEntry(nameof(product.TITLE), product.TITLE),
            new NameValueEntry(nameof(product.DESCRIPTION), product.DESCRIPTION),
            new NameValueEntry(nameof(product.REDUCEDTITLE), product.REDUCEDTITLE),
            new NameValueEntry(nameof(product.PRICE), product.PRICE.ToString("n3", new CultureInfo("pt-BR")))
        });
        
        await _redisCache.Set($"PENDING-{id}", product.ID, TimeSpan.FromMinutes(15));

        return AcceptedAtAction("GetStatusProcess", 
            new { id = product.ID },
            product); // CreatedAtAction() Accepted($"Product/Status/{id}", product);
    }

    [HttpGet("Status/{id}", Name = "GetStatusProcess")]
    public async Task<IActionResult> GetStatusProcessAsync(string id, CancellationToken cancellationToken)
    {
        if (id.IsEmpty())
            return BadRequest("id is required");

        var exists = await _redisCache.Exists($"PENDING-{id}");

        if (exists) return Ok("Processing");
        
        
        exists = await _redisCache.Exists($"FAILED-{id}");
        if (exists) return new RedirectToActionResult("GetFailedStatus",
            "Product", new { id }, true);
        
        
        exists = await _redisCache.Exists($"PROCESSED-{id}");
        // Response.Headers.Add("Location", $"http://localhost:9999/Product/Id/{id}");
        if (exists) return new RedirectToActionResult("GetProductById",
            "Product", new { id }, true); 
        
        // StatusCode((int)HttpStatusCode.MovedPermanently, "Processed"); 
        // RedirectPermanent($"http://localhost:9999/Product/Id/{id}"); 
        
        // StatusCode((int)HttpStatusCode.MovedPermanently, "Processed"); 
        //return new RedirectToActionResult("GetProductById", 
        //"Product", new { id = id }, true); 
        // RedirectPermanent($"http://localhost:9999/Product/Id/{id}");
        

        return NotFound();
    }
    
    [HttpGet("Failed/{id}", Name = "GetFailedStatus")]
    public async Task<IActionResult> GetFailedStatus(string id, CancellationToken cancellationToken)
    {
        if (id.IsEmpty())
            return BadRequest("id is required");
        
        var product = await _redisCache.Get<ProductFailed>($"FAILED-{id}");

        // if not found, return 404
        if (product == null)
            return NotFound();
        
        return UnprocessableEntity(product);
    }
    
    [HttpGet("Id/{id}", Name = "GetProductById")]
    public async Task<IActionResult> GetProductById(string id, CancellationToken cancellationToken)
    {
        if (id.IsEmpty())
            return BadRequest("id is required");
        
        if (!Guid.TryParse(id, out var guid))
            return BadRequest("id is invalid");
        
        // try to get from memory cache
        // if not found then try to get from redis
        // if not found then try to get from database
        var product = (await _memoryCache.Get<Product>(guid.ToString()) ?? 
                       await _redisCache.Get<Product>(guid.ToString())) ?? 
                      await _productRepository.GetById(guid.ToString(), cancellationToken);

        // if not found, return 404
        if (product == null)
            return NotFound();
        
        // save the product to memory cache
        await _memoryCache.Set(product.ID.ToString(), product, TimeSpan.FromMinutes(5));
        // save the product to redis cache
        await _redisCache.Set(product.ID.ToString(), product, TimeSpan.FromMinutes(10));

        return Ok(product);
    }


    [HttpGet("Test", Name = "GetTest")]
    public async Task<IActionResult> GetTest(CancellationToken cancellationToken)
    {
        var key = $"PRODUCTS-1-1";

        var result = await _redisCache.GetKeyExpireTime(key); 
        return Ok(new
        {
            result,
            total =result.Value.TotalSeconds, 
            sec = (result.Value.TotalSeconds / 10000) / 60
        });
    }

    [HttpGet("Page/{page}/{pageSize}", Name = "GetPage")]
    public async Task<IActionResult> GetPage(int page, int pageSize, CancellationToken cancellationToken)
    {
        var key = $"PRODUCTS-{page}-{pageSize}";
        var keyPages = "PRODUCTS-PAGES";

        var products = (await _memoryCache.Get<Product[]>(key) ??
                        await _redisCache.Get<Product[]>(key));

        if (products != null)
            return Ok(products);

        products = await _productRepository.GetByPage(page, pageSize, cancellationToken);

        if (!(products?.Any() ?? false))
            return NoContent();

        await _memoryCache.Set(key, products, TimeSpan.FromMinutes(10));
        await _redisCache.Set(key, products, TimeSpan.FromMinutes(10));

        var pagesCached = await _redisCache.Get<List<string>>(keyPages) ?? new List<string>();
        // add key to pagesCached if not exists
        if (!pagesCached.Contains(key))
            pagesCached.Add(key);

        await _redisCache.Remove(keyPages);
        await _redisCache.Set(keyPages, pagesCached);
        
        return Ok(products);
    }
}