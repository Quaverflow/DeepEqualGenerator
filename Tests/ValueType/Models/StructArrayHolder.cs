using DeepEqual.Generator.Shared;

namespace Tests.ValueType.Models;

[DeepComparable]
public partial class StructArrayHolder
{
    public SimpleStruct[] Items { get; set; } = [];
}