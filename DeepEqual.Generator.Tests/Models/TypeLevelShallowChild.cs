using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepCompare(Kind = CompareKind.Shallow)]
[DeepComparable]
public sealed class TypeLevelShallowChild
{
    public int V { get; set; }
}