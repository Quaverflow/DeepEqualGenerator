using DeepEqual.Generator.Shared;

namespace Tests.ValueType.Models;

[DeepComparable]
public class CollectionsOfTimeHolder
{
    public DateTime[] Snapshots { get; set; } = [];
    public List<DateTime> Events { get; set; } = [];
    public Dictionary<string, DateTimeOffset> Index { get; set; } = new(StringComparer.Ordinal);
}