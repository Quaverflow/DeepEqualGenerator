using DeepEqual.Generator.Shared;

namespace Tests.ValueType.Models;

[DeepComparable]
public class DateTimeHolder
{
    public DateTime When { get; set; }
}