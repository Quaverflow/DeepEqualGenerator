using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class DateTimeOffsetHolder
{
    public DateTimeOffset When { get; set; }
}