using System.Buffers;
using DeepEqual.Generator.Shared;

namespace DeepEqualGenerator.MockApp;

public class TrackedSubItem
{
    public string Label { get; set; } = "";
    public int Level { get; set; }
}

[DeepComparable(GenerateDelta = true, GenerateDiff = true)]
[DeltaTrack(AccessTrack = AccessMode.Write, AccessGranularity = AccessGranularity.CountsAndLast, AccessLogCapacity = 256)]
public partial class DemoOrder
{
    private int _id;
    public int Id
    {
        get => _id;
        set { _id = value; __MarkDirty(__Bit_Id); }
    }

    private string _name = "";
    public string Name
    {
        get => _name;
        set { _name = value; __MarkDirty(__Bit_Name); }
    }

    private DateTime _when;
    public DateTime When
    {
        get => _when;
        set { _when = value; __MarkDirty(__Bit_When); }
    }

    private decimal _price;
    public decimal Price
    {
        get => _price;
        set { _price = value; __MarkDirty(__Bit_Price); }
    }

    private List<int> _lines = new();
    public List<int> Lines
    {
        get => _lines;
        set { _lines = value; __MarkDirty(__Bit_Lines); }
    }

    private int[] _scores = Array.Empty<int>();
    public int[] Scores
    {
        get => _scores;
        set { _scores = value; __MarkDirty(__Bit_Scores); }
    }

    private Dictionary<string, int> _map = new();
    public Dictionary<string, int> Map
    {
        get => _map;
        set { _map = value; __MarkDirty(__Bit_Map); }
    }

    private TrackedSubItem _item = new();
    public TrackedSubItem Item
    {
        get => _item;
        set { _item = value; __MarkDirty(__Bit_Item); }
    }

    private Guid _key;
    public Guid Key
    {
        get => _key;
        set { _key = value; __MarkDirty(__Bit_Key); }
    }

    private byte[] _blob = Array.Empty<byte>();
    public byte[] Blob
    {
        get => _blob;
        set { _blob = value; __MarkDirty(__Bit_Blob); }
    }
}

internal static class Mutations
{
    public static void Init(DemoOrder o)
    {
        using var _ = AccessTracking.PushScope(label: "Init");
        o.Id = 1001;
        o.Name = "order-1001";
        o.When = DateTime.UtcNow;
        o.Price = 12.34m;

        o.Lines = new List<int> { 1, 2, 3 };
        o.Scores = new[] { 10, 20, 30 };
        o.Map = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        o.Item = new TrackedSubItem { Label = "new", Level = 1 };
        o.Key = Guid.NewGuid();
        o.Blob = new byte[] { 1, 2, 3, 4 };
    }

    public static void ApplyDiscount(DemoOrder o, decimal pct)
    {
        using var _ = AccessTracking.PushScope(label: "ApplyDiscount");
        o.Price = Math.Round(o.Price * (1 - pct), 2);
    }

    public static void Rename(DemoOrder o, string name)
    {
        using var _ = AccessTracking.PushScope(label: "Rename");
        o.Name = name;
    }

    // Replace-on-change (setter hit + brand new collections)
    public static void ReplaceLines(DemoOrder o, int n)
    {
        using var _ = AccessTracking.PushScope(label: "ReplaceLines");
        var list = new List<int>(n);
        for (int i = 0; i < n; i++) list.Add(i);
        o.Lines = list;

        var arr = new int[n];
        for (int i = 0; i < n; i++) arr[i] = i * 2;
        o.Scores = arr;

        var map = new Dictionary<string, int>(n);
        for (int i = 0; i < n; i++) map["k" + i] = i;
        o.Map = map;

        var blob = ArrayPool<byte>.Shared.Rent(Math.Max(0, n) * 4);
        try
        {
            new Random(42).NextBytes(blob.AsSpan(0, Math.Max(0, n) * 4));
            o.Blob = blob[..(Math.Max(0, n) * 4)];
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(blob);
        }
    }

    // Mutate-in-place (setter hit + snapshot must copy current content)
    public static void MutateInPlace(DemoOrder o, int addCount)
    {
        using var _ = AccessTracking.PushScope(label: "MutateInPlace");

        // Lines
        o.Lines.Capacity = Math.Max(o.Lines.Capacity, o.Lines.Count + addCount);
        for (int i = 0; i < addCount; i++) o.Lines.Add(1000 + i);

        // Scores (ensure length)
        var newLen = o.Scores.Length + addCount;
        var dst = new int[newLen];
        Array.Copy(o.Scores, dst, o.Scores.Length);
        for (int i = o.Scores.Length; i < newLen; i++) dst[i] = i;
        o.Scores = dst;

        // Map
        for (int i = 0; i < addCount; i++) o.Map["m" + i] = i * 3;

        // Blob
        var newBlob = new byte[o.Blob.Length + addCount];
        if (o.Blob.Length > 0) Buffer.BlockCopy(o.Blob, 0, newBlob, 0, o.Blob.Length);
        for (int i = 0; i < addCount; i++) newBlob[o.Blob.Length + i] = (byte)(i & 0xFF);
        o.Blob = newBlob;
    }

    public static void PromoteItem(DemoOrder o)
    {
        using var _ = AccessTracking.PushScope(label: "PromoteItem");
        o.Item = new TrackedSubItem { Label = o.Item.Label + "-pro", Level = o.Item.Level + 1 };
    }
}

public static class Program
{
    public static void Main()
    {
        // Enable tracking + event logging + value snapshots
        AccessTracking.Configure(
            defaultMode: AccessMode.Write,
            defaultGranularity: AccessGranularity.CountsAndLast,
            defaultLogCapacity: 256,   // events on
            trackingEnabled: true,
            countsEnabled: true,
            lastEnabled: true,
            logEnabled: true,
            callersEnabled: true);

        // Keep defaults: Scalars+Strings+Collections snapshots; you can force Full if you want:
        // AccessTracking.ConfigureSnapshots(
        //   snapshotModeDefault: ValueSnapshotMode.ScalarsAndStrings | ValueSnapshotMode.Collections | ValueSnapshotMode.Full,
        //   collectionFullThreshold: int.MaxValue);

        var order = new DemoOrder();

        Mutations.Init(order);
        Mutations.ApplyDiscount(order, 0.10m);
        Mutations.Rename(order, "order-1001-updated");
        Mutations.ReplaceLines(order, 8);     // replace
        Mutations.MutateInPlace(order, 4);    // mutate
        Mutations.PromoteItem(order);

        var json = order.__DumpAccessJson(reset: false);
        var txt = order.__DumpAccessText(reset: false);
        Console.WriteLine(json);
        Console.WriteLine();
        Console.WriteLine(txt);
    }
}
