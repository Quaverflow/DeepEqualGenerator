using DeepEqual.Generator.Attributes;

namespace Tests.ValueType.Models;

[DeepComparable]
public class NullableDateTimeHolder
{
    public DateTime? When { get; set; }
}