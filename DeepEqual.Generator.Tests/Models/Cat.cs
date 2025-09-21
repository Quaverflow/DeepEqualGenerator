using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class Cat : IAnimal
{
    public string Name { get; init; } = "";
    public int Age { get; init; }
}