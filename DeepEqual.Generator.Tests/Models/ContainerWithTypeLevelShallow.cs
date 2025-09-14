using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class ContainerWithTypeLevelShallow
{
    public TypeLevelShallowChild? Child { get; set; }
}