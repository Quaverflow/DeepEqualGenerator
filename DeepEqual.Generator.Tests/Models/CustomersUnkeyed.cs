using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class CustomersUnkeyed
{
    [DeepCompare(OrderInsensitive = true)] public List<Person> People { get; set; } = [];
}