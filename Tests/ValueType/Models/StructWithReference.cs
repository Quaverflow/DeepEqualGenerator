using DeepEqual.Generator.Shared;

namespace Tests.ValueType.Models;

[DeepComparable]
public partial struct StructWithReference
{
    public string? Name { get; set; }
    public string Code { get; set; }
}