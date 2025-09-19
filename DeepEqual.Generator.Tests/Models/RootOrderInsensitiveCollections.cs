using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable(OrderInsensitiveCollections = true)]
public sealed class RootOrderInsensitiveCollections
{
    public List<string> Names { get; set; } = [];
    public List<Person> People { get; set; } = [];
    [DeepCompare(OrderInsensitive = false)]
    public List<int> ForcedOrdered { get; set; } = [];
}