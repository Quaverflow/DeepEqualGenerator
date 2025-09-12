using DeepEqual.Generator.Shared;

namespace DeepEqual.Tests;

[DeepComparable]
public class SchemaRoot
{
    public SchemaChild Child { get; set; } = new();
}