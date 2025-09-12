using DeepEqual.Generator.Attributes;

namespace Tests.ValueType.Models;

[DeepComparable]
public partial struct StructWithNullable
{
    public int? MaybeInt { get; set; }
    public DateTime? MaybeTime { get; set; }
    public SRole? MaybeRole { get; set; }
}