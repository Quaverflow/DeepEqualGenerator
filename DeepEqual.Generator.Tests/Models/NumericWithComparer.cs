using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class NumericWithComparer
{
    [DeepCompare(ComparerType = typeof(DoubleEpsComparer))]
    public double Value { get; set; }
}