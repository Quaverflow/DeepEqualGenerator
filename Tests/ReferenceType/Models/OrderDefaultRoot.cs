using DeepEqual.Generator.Shared;

namespace Tests;

[DeepComparable(OrderInsensitiveCollections = true)]
public class OrderDefaultRoot { public List<int> Ordered { get; set; } = []; }