using Products.Cache.API.Repositories;

namespace Products.Cache.API.Services;

public class ProductService
{
    readonly ProductRepository _repository; 
    public ProductService(ProductRepository repository)
    {
        _repository = repository;
    }
    
    // check if product SKU already exists in repository
    public async Task<ProductFailed[]> ValidateProductsAsync(Product[] products, CancellationToken cancellationToken)
    {
        var skusAlreadyExists = await _repository.ExistsSkuList(products.Select(x => x.SKU).ToArray(), cancellationToken);

        var productsFailed = skusAlreadyExists.Select(product => 
            new ProductFailed()
            {
                MessageError = $"Product {product.SKU} already exists",
                Product = product
            }).ToList();

        return productsFailed.ToArray();
    }
}