using System.Dynamic;

namespace DeepEqual.Generator.Benchmarking;

public class Order
{
    public Guid Id { get; set; }
    public DateTimeOffset Created { get; set; }
    public List<OrderLine> Lines { get; set; } = new();
    public Dictionary<string, string> Meta { get; set; } = new(StringComparer.Ordinal);
    public IDictionary<string, object?> Extra { get; set; } = new ExpandoObject();
}