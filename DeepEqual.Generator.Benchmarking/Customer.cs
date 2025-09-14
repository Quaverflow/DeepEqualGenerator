using System.Dynamic;

namespace DeepEqual.Generator.Benchmarking;

public class Customer
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = "";
    public List<Order> Orders { get; set; } = new();
    public IDictionary<string, object?> Profile { get; set; } = new ExpandoObject();
}