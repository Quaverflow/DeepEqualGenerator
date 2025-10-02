// Program.cs
#nullable enable
using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using DeepEqual;
using DeepEqual.Generator.Shared;
using Perfolizer.Horology;

namespace DeepEqual.Generator.Benchmarking;
// ===============================================================
// === YOUR EXISTING BENCHES GO HERE (UNCHANGED) =================
// ===============================================================
//
// Paste the full content of your current benchmark classes/models
// in this region. Do not paste your old Program/Main here.
// Keep exactly as-is; nothing else in this file depends on it.
//
// ===============================================================


// ===============================================================
// === EXTRA BENCHMARK MODELS & FACTORY (appended) ===============
// ===============================================================

// ----- Ordered vs Unordered (with keys) -----

[DeepComparable(IncludeBaseMembers = true, GenerateDiff = true, GenerateDelta = true, StableMemberIndex = StableMemberIndexMode.Auto)]
public sealed class CustOrdered_Extra
{
    public string Id { get; set; } = "";
    public List<OrderOrdered_Extra> Orders { get; set; } = new();
}

[DeepComparable(IncludeBaseMembers = true, GenerateDiff = true, GenerateDelta = true)]
public sealed class OrderOrdered_Extra
{
    public string Number { get; set; } = "";
    public List<ItemOrdered_Extra> Lines { get; set; } = new();
}

[DeepCompare] // default ordered
public sealed class ItemOrdered_Extra
{
    public string Sku { get; set; } = "";
    public int Qty { get; set; }
    public decimal Price { get; set; }
}

// Unordered variant that matches by key (Sku)
[DeepComparable(IncludeBaseMembers = true, GenerateDiff = true, GenerateDelta = true)]
public sealed class CustUnordered_Extra
{
    public string Id { get; set; } = "";
    public List<OrderUnordered_Extra> Orders { get; set; } = new();
}

[DeepComparable(IncludeBaseMembers = true, GenerateDiff = true, GenerateDelta = true)]
public sealed class OrderUnordered_Extra
{
    public string Number { get; set; } = "";
    public List<ItemUnordered_Extra> Lines { get; set; } = new();
}

[DeepCompare(OrderInsensitive = true, KeyMembers = new[] { "Sku" })]
public sealed class ItemUnordered_Extra
{
    public string Sku { get; set; } = "";
    public int Qty { get; set; }
    public decimal Price { get; set; }
}

// ----- Arrays vs Lists -----

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class WithList_Extra
{
    public List<ValueLine_Extra> Lines { get; set; } = new();
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class WithArray_Extra
{
    public ValueLine_Extra[] Lines { get; set; } = Array.Empty<ValueLine_Extra>();
}

[DeepCompare]
public sealed class ValueLine_Extra
{
    public int V { get; set; }
}

// ----- Polymorphic member -----

public interface IPolymorph_Extra { int Tag { get; } }
[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
[DeltaTrack] 
public partial class PolyA_Extra : IPolymorph_Extra { public int Tag => 1; public string A = "a"; }

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
[DeltaTrack] 
public partial class PolyB_Extra : IPolymorph_Extra { public int Tag => 2; public int B = 42; }

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
[DeltaTrack] 
public partial class PolyC_Extra : IPolymorph_Extra { public int Tag => 3; public Guid C = Guid.NewGuid(); }

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class WithPolymorph_Extra
{
    public List<IPolymorph_Extra> Payload { get; set; } = new();
}

// ----- Big strings / extras -----

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class WithBigStrings_Extra
{
    public List<BigStr_Extra> Lines { get; set; } = new();
}

[DeepCompare]
public sealed class BigStr_Extra
{
    public string Key { get; set; } = "";
    public string Extra { get; set; } = "";
}

// ----- DeltaTrack fast-path emission toggle -----

[DeepComparable(GenerateDiff = true, GenerateDelta = true, StableMemberIndex = StableMemberIndexMode.Auto)]
[DeltaTrack(ThreadSafe = false)]
public partial class DirtyTracked_Extra
{
    public int A { get; set; }
    public int B { get; set; }
    public List<int> L { get; set; } = new();
}

public static class GenX_Extra
{
    public static CustOrdered_Extra MakeOrdered(int customers, int ordersPerCustomer, int linesPerOrder)
        => new CustOrdered_Extra
        {
            Id = "root",
            Orders = Enumerable.Range(0, ordersPerCustomer).Select(c => new OrderOrdered_Extra
            {
                Number = $"O{c}",
                Lines = Enumerable.Range(0, linesPerOrder).Select(i => new ItemOrdered_Extra
                {
                    Sku = $"SKU{i}",
                    Qty = 1 + (i % 3),
                    Price = 10 + i
                }).ToList()
            }).ToList()
        };

    public static CustUnordered_Extra MakeUnordered(int ordersPerCustomer, int linesPerOrder, bool shuffleRight)
    {
        var root = new CustUnordered_Extra { Id = "root" };
        for (int c = 0; c < ordersPerCustomer; c++)
        {
            var o = new OrderUnordered_Extra { Number = $"O{c}" };
            o.Lines = Enumerable.Range(0, linesPerOrder).Select(i => new ItemUnordered_Extra
            {
                Sku = $"SKU{i}",
                Qty = 1 + (i % 5),
                Price = 10 + i
            }).ToList();
            root.Orders.Add(o);
        }
        if (shuffleRight) foreach (var o in root.Orders) o.Lines.Reverse();
        return root;
    }

    public static WithList_Extra MakeList(int n) => new WithList_Extra
    {
        Lines = Enumerable.Range(0, n).Select(i => new ValueLine_Extra { V = i }).ToList()
    };

    public static WithArray_Extra MakeArray(int n) => new WithArray_Extra
    {
        Lines = Enumerable.Range(0, n).Select(i => new ValueLine_Extra { V = i }).ToArray()
    };

    public static WithPolymorph_Extra MakePoly(int n, int variantCount)
    {
        var w = new WithPolymorph_Extra();
        for (int i = 0; i < n; i++)
            w.Payload.Add((i % variantCount) switch
            {
                0 => new PolyA_Extra { A = "a" + i },
                1 => new PolyB_Extra { B = i },
                _ => new PolyC_Extra { C = Guid.Empty }
            });
        return w;
    }

    public static WithBigStrings_Extra MakeBig(int n, int bytesPerString)
    {
        string big = new string('x', Math.Max(1, bytesPerString));
        return new WithBigStrings_Extra
        {
            Lines = Enumerable.Range(0, n).Select(i => new BigStr_Extra
            {
                Key = "K" + i,
                Extra = big + i
            }).ToList()
        };
    }

    public static DirtyTracked_Extra MakeDirty(int nList)
        => new DirtyTracked_Extra { A = 1, B = 2, L = Enumerable.Range(0, nList).ToList() };
}


// ===============================================================
// === EXTRA BENCHMARKS (appended) ===============================
// ===============================================================

[MemoryDiagnoser]
public class ExtraEqualityBenches
{
    [Params(50, 200, 500)] public int Customers;
    [Params(1, 3, 8)] public int LinesPerOrder;

    private CustOrdered_Extra _orderedA = default!;
    private CustOrdered_Extra _orderedB = default!;
    private CustUnordered_Extra _unorderedA = default!;
    private CustUnordered_Extra _unorderedB = default!;
    private ComparisonContext _ctx = default!;

    [GlobalSetup]
    public void Setup()
    {
        _orderedA = GenX_Extra.MakeOrdered(Customers, ordersPerCustomer: 1, linesPerOrder: LinesPerOrder);
        _orderedB = GenX_Extra.MakeOrdered(Customers, ordersPerCustomer: 1, linesPerOrder: LinesPerOrder);

        _unorderedA = GenX_Extra.MakeUnordered(ordersPerCustomer: 1, linesPerOrder: LinesPerOrder, shuffleRight: false);
        _unorderedB = GenX_Extra.MakeUnordered(ordersPerCustomer: 1, linesPerOrder: LinesPerOrder, shuffleRight: true);

        _ctx = new ComparisonContext(new ComparisonOptions
        {
            StringComparison = StringComparison.Ordinal,
            TreatNaNEqual = true
        });

        // pre-touch to avoid first-call warmup spikes
        _ = _orderedA.AreDeepEqual(_orderedB, _ctx);
        _ = _unorderedA.ComputeDeepDelta(_unorderedB, _ctx);
    }

    [Benchmark(Description = "Ordered seq equality (equal fast path)")]
    public bool Ordered_AreDeepEqual()
        => _orderedA.AreDeepEqual(_orderedB, _ctx);

    [Benchmark(Description = "Unordered seq equality w/ keys (same set, different order)")]
    public bool UnorderedWithKeys_AreDeepEqual()
        => _unorderedA.AreDeepEqual(_unorderedB, _ctx);
}

[MemoryDiagnoser]
public class ExtraArrayVsListBenches
{
    [Params(100, 1_000, 10_000)] public int N;

    private WithList_Extra _listA = default!;
    private WithList_Extra _listB = default!;
    private WithArray_Extra _arrA = default!;
    private WithArray_Extra _arrB = default!;
    private ComparisonContext _ctx = default!;

    [GlobalSetup]
    public void Setup()
    {
        _listA = GenX_Extra.MakeList(N);
        _listB = GenX_Extra.MakeList(N);
        _arrA = GenX_Extra.MakeArray(N);
        _arrB = GenX_Extra.MakeArray(N);
        if (N > 0) _arrB.Lines[N / 2].V++; // small change
        _ctx = new ComparisonContext();

        _ = _listA.AreDeepEqual(_listB, _ctx);
        _ = _arrA.ComputeDeepDelta(_arrB, _ctx);
    }

    [Benchmark(Description = "List equality (ordered)")]
    public bool List_Equal() => _listA.AreDeepEqual(_listB, _ctx);

    [Benchmark(Description = "Array equality (ordered)")]
    public bool Array_Equal() => _arrA.AreDeepEqual(_arrB, _ctx);

    [Benchmark(Description = "List ComputeDelta (granular seq ops)")]
    public DeltaDocument List_ComputeDelta()
        => _listA.ComputeDeepDelta(_listB, _ctx);

    [Benchmark(Description = "Array ComputeDelta (replace-on-change)")]
    public DeltaDocument Array_ComputeDelta()
        => _arrA.ComputeDeepDelta(_arrB, _ctx);
}

[MemoryDiagnoser]
public class ExtraPolymorphicBenches
{
    [Params(100, 1_000, 5_000)] public int N;
    [Params(2, 3)] public int VariantCount;

    private WithPolymorph_Extra _left = default!;
    private WithPolymorph_Extra _right = default!;
    private ComparisonContext _ctx = default!;

    [GlobalSetup]
    public void Setup()
    {
        _left = GenX_Extra.MakePoly(N, VariantCount);
        _right = GenX_Extra.MakePoly(N, VariantCount);
        if (_right.Payload.Count > 0) _right.Payload[0] = new PolyC_Extra { C = Guid.NewGuid() };
        _ctx = new ComparisonContext();

        _ = _left.AreDeepEqual(_right, _ctx);
        _ = _left.ComputeDeepDelta(_right, _ctx);
    }

    [Benchmark(Description = "Polymorphic equality (same type shapes)")]
    public bool Poly_Equal() => _left.AreDeepEqual(_right, _ctx);

    [Benchmark(Description = "Polymorphic ComputeDelta")]
    public DeltaDocument Poly_ComputeDelta() => _left.ComputeDeepDelta(_right, _ctx);
}

[MemoryDiagnoser]
public class ExtraDirtyBitBenches
{
    [Params(100, 1_000)] public int ListCount;

    private DirtyTracked_Extra _before = default!;
    private DirtyTracked_Extra _after = default!;
    private ComparisonContext _ctxFast = default!;
    private ComparisonContext _ctxValidate = default!;

    [GlobalSetup]
    public void Setup()
    {
        _before = GenX_Extra.MakeDirty(ListCount);
        _after = GenX_Extra.MakeDirty(ListCount);

        _after.A += 1;
        for (int i = 0; i < Math.Min((int)10, (int)_after.L.Count); i++) _after.L[i]++;

        _ctxFast = new ComparisonContext(new ComparisonOptions { ValidateDirtyOnEmit = false });
        _ctxValidate = new ComparisonContext(new ComparisonOptions { ValidateDirtyOnEmit = true });

        _ = _before.ComputeDeepDelta(_after, _ctxFast);
    }

    [Benchmark(Description = "ComputeDelta (DirtyTrack, fast emit)")]
    public DeltaDocument Dirty_Compute_Fast()
        => _before.ComputeDeepDelta(_after, _ctxFast);

    [Benchmark(Description = "ComputeDelta (DirtyTrack, validate-on-emit)")]
    public DeltaDocument Dirty_Compute_Validate()
        => _before.ComputeDeepDelta(_after, _ctxValidate);
}

[MemoryDiagnoser]
public class ExtraCodecBenches
{
    [Params(100, 1_000)] public int N;
    [Params(16, 1024, 8192)] public int BytesPerString;

    private WithBigStrings_Extra _left = default!;
    private WithBigStrings_Extra _right = default!;
    private DeltaDocument _doc = default!;
    private ComparisonContext _ctx = default!;

    private byte[] _bufHeaderless = Array.Empty<byte>();
    private byte[] _bufHeaderful = Array.Empty<byte>();

    [GlobalSetup]
    public void Setup()
    {
        _left = GenX_Extra.MakeBig(N, BytesPerString);
        _right = GenX_Extra.MakeBig(N, BytesPerString);
        for (int i = 0; i < Math.Min(10, _right.Lines.Count); i++)
            _right.Lines[i].Extra += "Δ";

        _ctx = new ComparisonContext();
        _doc = _left.ComputeDeepDelta(_right, _ctx);

        var w1 = new ArrayBufferWriter<byte>(1024);
        BinaryDeltaCodec.Write(_doc, w1, new BinaryDeltaOptions { IncludeHeader = false });
        _bufHeaderless = w1.WrittenSpan.ToArray();

        var w2 = new ArrayBufferWriter<byte>(1024);
        BinaryDeltaCodec.Write(_doc, w2, new BinaryDeltaOptions { IncludeHeader = true });
        _bufHeaderful = w2.WrittenSpan.ToArray();
    }

    [Benchmark(Description = "Binary Encode (headerless)")]
    public int Encode_Headerless()
    {
        var writer = new ArrayBufferWriter<byte>(1024);
        BinaryDeltaCodec.Write(_doc, writer, new BinaryDeltaOptions { IncludeHeader = false });
        return writer.WrittenCount;
    }

    [Benchmark(Description = "Binary Encode (headerful + tables)")]
    public int Encode_Headerful()
    {
        var writer = new ArrayBufferWriter<byte>(1024);
        BinaryDeltaCodec.Write(_doc, writer, new BinaryDeltaOptions
        {
            IncludeHeader = true,
            UseStringTable = true,
            UseTypeTable = true,
            IncludeEnumTypeIdentity = true
        });
        return writer.WrittenCount;
    }

    [Benchmark(Description = "Binary Decode (headerless)")]
    public DeltaDocument Decode_Headerless()
        => BinaryDeltaCodec.Read(_bufHeaderless, new BinaryDeltaOptions { IncludeHeader = false });

    [Benchmark(Description = "Binary Decode (headerful)")]
    public DeltaDocument Decode_Headerful()
        => BinaryDeltaCodec.Read(_bufHeaderful, new BinaryDeltaOptions { IncludeHeader = true });
}

[MemoryDiagnoser]
public class ExtraApplyClonePathBenches
{
    [Params(1_000, 5_000)] public int Adds;

    private WithList_Extra _targetList = default!;
    private IReadOnlyList<ValueLine_Extra> _targetRo = default!;
    private DeltaDocument _addsDoc = default!;

    [GlobalSetup]
    public void Setup()
    {
        _targetList = GenX_Extra.MakeList(0);
        _targetRo = Array.AsReadOnly(Array.Empty<ValueLine_Extra>());

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        for (int i = 0; i < Adds; i++)
            w.WriteSeqAddAt(memberIndex: 0, index: i, value: new ValueLine_Extra { V = i });
        _addsDoc = doc;

        _targetList = _targetList.ApplyDeepDelta(_addsDoc); // touch paths
    }

    [Benchmark(Description = "ApplyDelta -> List<T> (in-place path, pre-sized)")]
    public int Apply_To_List()
    {
        var t = new WithList_Extra { Lines = new List<ValueLine_Extra>() };
        t = t.ApplyDeepDelta(_addsDoc);
        return t.Lines.Count;
    }

    [Benchmark(Description = "ApplyDelta -> IReadOnlyList<T> (clone path)")]
    public int Apply_To_ReadOnlyList()
    {
        object? boxed = _targetRo;
        var reader = new DeltaReader(_addsDoc);
        GeneratedHelperRegistry.TryApplyDeltaSameType(boxed!.GetType(), ref boxed, ref reader);
        var roList = (IReadOnlyList<ValueLine_Extra>)boxed;
        return roList.Count;
    }
}


// ===============================================================
// === DEV / QUICK CONFIG + ENTRYPOINT ===========================
// ===============================================================

public sealed class DevConfig : ManualConfig
{
    public DevConfig()
    {
        // Super-fast developer run to prevent “workload warmup” from dragging
        AddJob(Job.ShortRun
            .WithWarmupCount(1)
            .WithIterationCount(5)
            .WithLaunchCount(1)
            .WithUnrollFactor(1)
            .WithMinIterationTime(TimeInterval.FromMilliseconds(50))
            .WithMaxRelativeError(0.05)
            .WithId("DevShortRun"));

        // Optional: in-process toolchain for super fast inner-loop (DEV ONLY!)
        // Comment this out for final measurements.
        AddJob(Job.ShortRun
            .WithWarmupCount(1)
            .WithIterationCount(5)
            .WithLaunchCount(1)
            .WithUnrollFactor(1)
            .WithMinIterationTime(TimeInterval.FromMilliseconds(50))
            .WithMaxRelativeError(0.05)
            .WithToolchain(InProcessNoEmitToolchain.Instance)
            .WithId("DevInProc"));
    }
}


public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run(
        [
            typeof(ExtraApplyClonePathBenches),
            typeof(ExtraArrayVsListBenches),
            typeof(ExtraCodecBenches),
            typeof(ExtraDirtyBitBenches),
            typeof(ExtraEqualityBenches),
            typeof(ExtraPolymorphicBenches),
        ], DefaultConfig.Instance.WithArtifactsPath(@"C:\Users\mirko\Downloads\benc"));
    }
}



