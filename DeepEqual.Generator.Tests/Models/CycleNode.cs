using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable(CycleTracking = true)]
public sealed class CycleNode
{
    public int Id { get; set; }
    public CycleNode? Next { get; set; }
}