using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class Item
{
    public int X { get; set; }
    public string? Name { get; set; }
}