using StackExchange.Redis;

namespace Products.Cache.API;

public class Product
{
    public Guid ID { get; set; }
    public string SKU { get; set; }
    public string TITLE { get; set; }
    public string REDUCEDTITLE { get; set; }
    public string DESCRIPTION { get; set; }
    public double PRICE { get; set; }
    public string SEARCHTEXT { get; set; }
}

public class ProductFailed
{
    public string MessageError { get; set; }
    public Product? Product { get; set; }
}

public readonly record struct NewProductMessage(RedisValue Id, Product Product); 