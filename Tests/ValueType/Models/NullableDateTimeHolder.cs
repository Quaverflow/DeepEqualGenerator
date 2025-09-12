using DeepEqual.Generator.Shared;

namespace Tests.ValueType.Models;

[DeepComparable]
public class NullableDateTimeHolder
{
    public DateTime? When { get; set; }
}