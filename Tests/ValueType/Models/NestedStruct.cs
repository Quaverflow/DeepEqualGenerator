using DeepEqual.Generator.Attributes;

namespace Tests.ValueType.Models;

[DeepComparable]
public partial struct NestedStruct
{
    public SimpleStruct Inner { get; set; }
    public long Id { get; set; }
}