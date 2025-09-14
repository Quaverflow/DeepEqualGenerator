using System.Globalization;
using DeepEqual.Generator.Shared;

[DeepComparable(CycleTracking = true)]
public sealed class CultureCycleNode
{
    public CultureInfo? Culture { get; init; }
    public CultureCycleNode? Next { get; set; }
}