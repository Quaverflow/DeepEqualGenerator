using DeepEqual.Generator.Shared;

namespace Tests;

[DeepComparable(OrderInsensitiveCollections = false)]
public class BigAggregate
{
    public string Title { get; set; } = "";
    public int Version { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }

    public Person Root { get; set; } = new();

    public List<Person> People { get; set; } = [];

    [DeepCompare(OrderInsensitive = true)]
    public List<string> Tags { get; set; } = [];

    public Dictionary<string, Person> ByName { get; set; } = new(StringComparer.Ordinal);

    public int[] Measurements { get; set; } = [];

    public Uri? Endpoint { get; set; }

    [DeepCompare(Kind = CompareKind.Skip)]
    public object? Ignored { get; set; }

    [DeepCompare(Kind = CompareKind.Reference)]
    public byte[]? Blob { get; set; }
}