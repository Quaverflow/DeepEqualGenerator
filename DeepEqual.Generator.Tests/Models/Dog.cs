using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class Dog : IAnimal
{
    public int Age { get; init; }
    public string Name { get; init; } = "";
}