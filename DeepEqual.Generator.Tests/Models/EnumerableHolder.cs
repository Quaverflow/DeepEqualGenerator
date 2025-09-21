using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class EnumerableHolder
{
    public IEnumerable<int> Seq { get; init; } = Array.Empty<int>();
}