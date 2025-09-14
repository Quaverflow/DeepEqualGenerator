using DeepEqual.Generator.Shared;
using DeepEqual.Generator.Tests.Models;

[DeepComparable(CycleTracking = true)]
public sealed class PolyCycleNode
{
    public int Id { get; init; }
    public IAnimal? Animal { get; init; }
    public PolyCycleNode? Next { get; set; }
}