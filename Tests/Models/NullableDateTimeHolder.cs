using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class NullableDateTimeHolder
{
    public DateTime? When { get; set; }
}