using DeepEqual.Generator.Shared;

[DeepComparable]
public sealed class IntSetHolder
{
    [DeepCompare(OrderInsensitive = true)]
    public HashSet<int> Set { get; init; } = new();
}