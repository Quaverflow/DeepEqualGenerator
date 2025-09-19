using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepCompare(Members = ["A", "B"])]
[DeepComparable]
public sealed class OnlySomeMembers
{
    public int A { get; set; }
    public int B { get; set; }
    public int C { get; set; }
}