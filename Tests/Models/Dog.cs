using DeepEqual.Generator.Shared;
using DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class Dog : IAnimal
{
    public int Age { get; init; }
    public string Name { get; init; } = "";
}