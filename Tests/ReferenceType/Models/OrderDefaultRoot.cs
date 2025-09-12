using DeepEqual.Generator.Attributes;

namespace Tests;

[DeepComparable(OrderInsensitiveCollections = true)]
public class OrderDefaultRoot { public List<int> Ordered { get; set; } = []; }