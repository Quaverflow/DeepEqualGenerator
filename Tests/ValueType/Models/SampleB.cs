using DeepEqual.Generator.Shared;

namespace Tests.ValueType.Models;

[DeepCompare(IgnoreMembers = [nameof(Z)])]
public class SampleB
{
    public int X { get; set; }
    public int Z { get; set; }
}