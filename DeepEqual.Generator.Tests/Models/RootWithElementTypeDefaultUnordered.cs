using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class RootWithElementTypeDefaultUnordered
{
    public List<TagAsElementDefaultUnordered> Tags { get; set; } = [];
}