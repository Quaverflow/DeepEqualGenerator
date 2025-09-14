using System.Dynamic;
using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Benchmarking;

[DeepComparable(OrderInsensitiveCollections = true)]
public sealed class MegaRoot
{
    public string Title { get; set; } = "";
    public EverythingBagel Bagel { get; set; } = new();
    public BigGraph Graph { get; set; } = new();
    public List<EverythingBagel> Bagels { get; set; } = new();
    public Dictionary<string, EverythingBagel> BagelIndex { get; set; } = new(StringComparer.Ordinal);
    public int[][] Jaggy { get; set; } = Array.Empty<int[]>();
    public Dictionary<string, object?> Meta { get; set; } = new(StringComparer.Ordinal);
    public IDictionary<string, object?> Expando { get; set; } = new ExpandoObject();
    public object? Polymorph { get; set; }
    public Memory<byte> Data { get; set; }
    public ReadOnlyMemory<byte> RData { get; set; }
    public List<object> Mixed { get; set; } = new();
    [DeepCompare(OrderInsensitive = false)]
    public List<int> ForcedOrdered { get; set; } = new();
}