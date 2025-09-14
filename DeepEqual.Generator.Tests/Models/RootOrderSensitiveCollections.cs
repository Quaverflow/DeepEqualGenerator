using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class RootOrderSensitiveCollections
{
    public List<string> Names { get; set; } = new();
}