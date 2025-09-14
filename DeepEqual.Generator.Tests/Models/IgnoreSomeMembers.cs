using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepCompare(IgnoreMembers = new[] { "C" })]
[DeepComparable]
public sealed class IgnoreSomeMembers
{
    public int A { get; set; }
    public int B { get; set; }
    public int C { get; set; }
}