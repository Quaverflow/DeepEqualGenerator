using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class DateOnlyTimeOnlyHolder
{
    public DateOnly D { get; set; }
    public TimeOnly T { get; set; }
}