using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class DateTimeHolder
{
    public DateTime When { get; set; }
}