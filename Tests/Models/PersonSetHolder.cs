using DeepEqual.Generator.Shared;
using DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class PersonSetHolder
{
    [DeepCompare(OrderInsensitive = true)]
    public ISet<Person> People { get; init; } = new HashSet<Person>();
}