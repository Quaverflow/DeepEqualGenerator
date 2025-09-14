using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class EnumHolder
{
    public Color Shade { get; set; }
}