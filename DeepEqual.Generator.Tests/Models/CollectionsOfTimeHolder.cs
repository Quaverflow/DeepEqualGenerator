using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class CollectionsOfTimeHolder
{
    public DateTime[] Snapshots { get; set; } = [];
    public List<DateTime> Events { get; set; } = [];
    public Dictionary<string, DateTimeOffset> Index { get; set; } = new();
}