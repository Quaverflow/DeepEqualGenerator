using System.Dynamic;

namespace DeepEqual.Generator.Benchmarking;

public class Product
{
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public DateTime Introduced { get; set; }
    public IDictionary<string, object?> Attributes { get; set; } = new ExpandoObject();
}