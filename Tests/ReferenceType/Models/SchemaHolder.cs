using DeepEqual.Generator.Attributes;
using Tests.ValueType.Models;

namespace Tests.ReferenceType.Models;

[DeepComparable]
public class SchemaHolder
{
    public SampleA A1 { get; set; } = new();
    public SampleB B1 { get; set; } = new();
}