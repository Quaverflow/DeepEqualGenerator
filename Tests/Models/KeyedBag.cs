using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class KeyedBag
{
    [DeepCompare(OrderInsensitive = true, KeyMembers = new[] { nameof(Item.Name) })]
    public List<Item> Items { get; init; } = new();
}