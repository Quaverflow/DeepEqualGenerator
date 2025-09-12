using DeepEqual.Generator.Shared;

namespace Tests.ValueType.Models;

[DeepComparable]
public partial struct NestedStruct
{
    public SimpleStruct Inner { get; set; }
    public long Id { get; set; }
}