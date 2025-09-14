using System.Globalization;
using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable(CycleTracking = true)]
public sealed class CultureCycleNode
{
    public CultureInfo? Culture { get; init; }
    public CultureCycleNode? Next { get; set; }
}