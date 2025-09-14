using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable(OrderInsensitiveCollections = true)]
public sealed class TagAsElementDefaultUnordered
{
    public string Label { get; set; } = "";
}