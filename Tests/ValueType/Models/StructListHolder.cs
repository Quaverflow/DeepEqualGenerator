using DeepEqual.Generator.Shared;

namespace Tests.ValueType.Models;

[DeepComparable]
public partial class StructListHolder
{
    public List<SimpleStruct> Items { get; set; } = [];
}