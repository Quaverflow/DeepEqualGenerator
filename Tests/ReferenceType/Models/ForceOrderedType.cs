using DeepEqual.Generator.Attributes;

namespace Tests;

[DeepCompare(OrderInsensitive = false)]
[DeepComparable]
public class ForceOrderedType { public List<int> Items { get; set; } = []; }