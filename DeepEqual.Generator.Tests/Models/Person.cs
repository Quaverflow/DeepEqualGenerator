using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class Person
{
    public string? Name { get; set; }
    public int Age { get; set; }
}