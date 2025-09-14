using System.Dynamic;
using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Benchmarking;

[DeepComparable(CycleTracking = false)]
public partial class BigGraph
{
    public string Title { get; set; } = "";
    public OrgNode Org { get; set; } = new();
    public Dictionary<string, OrgNode> OrgIndex { get; set; } = new(StringComparer.Ordinal);
    public List<Product> Catalog { get; set; } = new();
    public List<Customer> Customers { get; set; } = new();
    public IDictionary<string, object?> Meta { get; set; } = new ExpandoObject();
}