using DeepEqual.Generator.Shared;

namespace Tests.ValueType.Models;

[DeepCompare(Members = [nameof(X), nameof(Y)])]
public class SampleA
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
}