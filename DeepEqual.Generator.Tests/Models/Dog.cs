using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class Dog : IAnimal
{
    public string Name { get; init; } = "";
    public int Age { get; init; }
}