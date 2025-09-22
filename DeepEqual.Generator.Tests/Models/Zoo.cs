using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class Zoo
{
    public IAnimal? Animal { get; init; }
}