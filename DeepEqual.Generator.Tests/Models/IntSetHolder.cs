using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class IntSetHolder
{
    [DeepCompare(OrderInsensitive = true)]
    public HashSet<int> Set { get; init; } = [];
}