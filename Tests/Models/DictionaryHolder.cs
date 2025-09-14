using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class DictionaryHolder
{
    public Dictionary<int, Person> Map { get; set; } = new();
}