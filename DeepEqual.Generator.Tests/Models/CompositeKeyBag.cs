using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class CompositeKeyBag
{
    [DeepCompare(OrderInsensitive = true, KeyMembers = [nameof(Item.Name), nameof(Item.X)])]
    public List<Item> Items { get; init; } = [];
}