using DeepEqual.Generator.Shared;
using DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class BadKeyMembersBag
{
    [DeepCompare(OrderInsensitive = true, KeyMembers = new[] { "Nope", "AlsoNope" })]
    public List<Item> Items { get; init; } = new();
}