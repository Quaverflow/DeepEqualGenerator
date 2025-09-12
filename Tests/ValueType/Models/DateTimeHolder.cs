using DeepEqual.Generator.Attributes;

namespace Tests.ValueType.Models;

[DeepComparable]
public class DateTimeHolder
{
    public DateTime When { get; set; }
}