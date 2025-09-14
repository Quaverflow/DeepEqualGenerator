using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class ObjectHolder
{
    public object? Any { get; set; }
    public ChildRef Known { get; set; } = new();
}