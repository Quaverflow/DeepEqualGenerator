using DeepEqual.Generator.Attributes;

namespace Tests.ValueType.Models;

[DeepComparable]
public class DateTimeOffsetHolder
{
    public DateTimeOffset When { get; set; }
}