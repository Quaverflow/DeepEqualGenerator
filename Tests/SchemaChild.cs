using DeepEqual.Generator.Shared;

namespace DeepEqual.Tests;

[DeepCompare(Members = new[] { "Name" })]
public class SchemaChild
{
    public string Name { get; set; } = "";
    public int Ignored { get; set; }
}