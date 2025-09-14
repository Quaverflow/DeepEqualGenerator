using System.Dynamic;
using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Benchmarking;

[DeepComparable(CycleTracking = false)]
public sealed class EverythingBagel
{
    public bool B { get; set; }
    public byte U8 { get; set; }
    public sbyte I8 { get; set; }
    public short I16 { get; set; }
    public ushort U16 { get; set; }
    public int I32 { get; set; }
    public uint U32 { get; set; }
    public long I64 { get; set; }
    public ulong U64 { get; set; }
    public float F32 { get; set; }
    public double F64 { get; set; }
    public decimal M128 { get; set; }
    public char C { get; set; }
    public string? S { get; set; }
    public int? NI32 { get; set; }
    public TinyEnum? NEnum { get; set; }
    public MiniPoint? NPoint { get; set; }
    public TinyEnum E { get; set; }
    public MiniPoint P { get; set; }
    public DateTime When { get; set; }
    public DateTimeOffset WhenOff { get; set; }
    public TimeSpan HowLong { get; set; }
#if NET6_0_OR_GREATER
    public DateOnly Day { get; set; }
    public TimeOnly Clock { get; set; }
#endif
    public Guid Id { get; set; }
    public Memory<byte> Blob { get; set; }
    public ReadOnlyMemory<byte> RBlob { get; set; }
    public int[]? Numbers { get; set; }
    public string[]? Words { get; set; }
    public int[][]? Jagged { get; set; }
    public int[,]? Rect { get; set; }
    public List<int>? LInts { get; set; }
    public IReadOnlyList<string>? RListStrings { get; set; }
    [DeepCompare(OrderInsensitive = true)]
    public HashSet<string>? Tags { get; set; }
    public Dictionary<string, int>? ByName { get; set; }
    public IReadOnlyDictionary<string, Leaf>? ByKey { get; set; }
    public Leaf? Left { get; set; }
    public Leaf? Right { get; set; }
    public (int, string) Pair { get; set; }
    public KeyValuePair<string, int> Kvp { get; set; }
    public object? Boxed { get; set; }
    public IDictionary<string, object?> Dyn { get; set; } = new ExpandoObject();
    [DeepCompare(Kind = CompareKind.Reference)]
    public byte[]? RefBlob { get; set; }
}