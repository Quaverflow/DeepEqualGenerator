using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class ZooRoList
{
    public IReadOnlyList<IAnimal> Animals { get; init; } = new List<IAnimal>();
}