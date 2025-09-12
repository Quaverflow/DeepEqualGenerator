using DeepEqual.Generator.Attributes;

namespace Tests;

[DeepComparable(OrderInsensitiveCollections = true)]
public class RootUnordered
{
    public List<int> Items { get; set; } = [];
    [DeepCompare(OrderInsensitive = false)]
    public List<int> Ordered { get; set; } = [];
}