using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class ZooList
{
    public List<IAnimal?> Animals { get; init; } = [];
}