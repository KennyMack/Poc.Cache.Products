using System.ComponentModel.DataAnnotations;

namespace Products.Cache.API;

public class CreateProductDTO
{
    [Required]
    public string SKU { get; set; }
    [Required]
    public string TITLE { get; set; }
    [Required]
    public string REDUCEDTITLE { get; set; }
    public string DESCRIPTION { get; set; }
    [Required]
    [Range(0.01, int.MaxValue, ErrorMessage = "Price must be greater than 0")]
    public double PRICE { get; set; }
}