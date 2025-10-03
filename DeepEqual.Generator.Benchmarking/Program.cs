// Program.cs
#nullable enable
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using DeepEqual;
using DeepEqual.Generator.Shared;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Perfolizer.Horology;
using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

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
[DeepComparable(GenerateDelta = true)]
public sealed class RoHolder { public IReadOnlyList<ValueLine_Extra> Lines { get; set; } = Array.AsReadOnly(Array.Empty<ValueLine_Extra>()); }

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

    //[Benchmark(Description = "ApplyDelta -> List<T> (in-place path, pre-sized)")]
    //public int Apply_To_List()
    //{
    //    var t = new WithList_Extra { Lines = new List<ValueLine_Extra>() };
    //    t = t.ApplyDeepDelta(_addsDoc);
    //    return t.Lines.Count;
    //}
    [Benchmark(Description = "ApplyDelta -> IReadOnlyList<T> (clone path)")]
    public int Apply_To_ReadOnlyList()
    {
        object? target = _targetRo; // IReadOnlyList<ValueLine_Extra>
        var ops = new DeltaReader(_addsDoc).AsSpan();

        // This helper handles RO → clone-to-List<T> and replays seq ops correctly
        for (int i = 0; i < ops.Length; i++)
            DeltaHelpers.ApplyListOpCloneIfNeeded<ValueLine_Extra>(ref target, in ops[i]);

        var ro = (IReadOnlyList<ValueLine_Extra>)target!;
        if (ro.Count != Adds) throw new InvalidOperationException("Delta not applied");
        return ro.Count;
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

/// <summary>
/// Apples-to-apples competitors for "apply N adds to a list" — no pre-serialization anywhere.
/// Compares DeepEqual delta vs JsonPatchDocument vs Manual in-memory ops vs Immutable list path.
/// </summary>
[MemoryDiagnoser]
public class Competitors_Adds_Benches
{
    [Params(100, 1_000, 5_000)]
    public int Adds;

    // Reuse your element type for parity
    [DeepCompare]
    public sealed class VLine { public int V { get; set; } }

    // Simple strongly-typed holder used by all competitors
    [DeepComparable(GenerateDiff = true, GenerateDelta = true)]
    public sealed class Holder
    {
        public List<VLine> Lines { get; set; } = new();
    }

    // -------------------------
    // Artifacts prebuilt in GlobalSetup (no serialization)
    // -------------------------

    // DeepEqual
    public DeltaDocument _deepeqAddsDoc = default!;
    public ComparisonContext _ctx = default!;

    // JsonPatch
    public JsonPatchDocument<Holder> _jsonPatchAdds = default!;

    // Manual (naïve) ops
    public struct AddOp { public int Index; public VLine Value; }
    public List<AddOp> _manualAdds = default!;

    // Immutable (builder-friendly form of the same ops)
    private List<VLine> _immutableAddsValues = default!; // values in insertion order

    [GlobalSetup]
    public void Setup()
    {
        // DeepEqual delta: build SeqAddAt ops in-memory
        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        for (int i = 0; i < Adds; i++)
            w.WriteSeqAddAt(memberIndex: 0, index: i, value: new VLine { V = i });
        _deepeqAddsDoc = doc;
        _ctx = new ComparisonContext();

        // JsonPatch: build RFC6902-style "add" operations (no serialization)
        var patch = new JsonPatchDocument<Holder>();
        for (int i = 0; i < Adds; i++)
        {
            patch.Operations.Add(new Operation<Holder>(
                op: "add",
                path: $"/Lines/{i}",
                from: null,
                value: new VLine { V = i }
            ));
        }
        _jsonPatchAdds = patch;

        // Manual ops: strongly-typed list of (index, value)
        _manualAdds = new List<AddOp>(Adds);
        for (int i = 0; i < Adds; i++)
            _manualAdds.Add(new AddOp { Index = i, Value = new VLine { V = i } });

        // Immutable: just the values (we’ll Insert at index each time to mirror others)
        _immutableAddsValues = new List<VLine>(Adds);
        for (int i = 0; i < Adds; i++)
            _immutableAddsValues.Add(new VLine { V = i });
    }

    // -------------------------
    // BUILD (how expensive it is to AUTHOR the change set)
    // -------------------------

    [Benchmark(Description = "DeepEqual Build (adds doc)")]
    public DeltaDocument DeepEqual_Build_AddsDoc()
    {
        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        for (int i = 0; i < Adds; i++)
            w.WriteSeqAddAt(memberIndex: 0, index: i, value: new VLine { V = i });
        return doc;
    }

    [Benchmark(Description = "JsonPatch Build (adds patch)")]
    public JsonPatchDocument<Holder> JsonPatch_Build_AddsPatch()
    {
        var patch = new JsonPatchDocument<Holder>();
        for (int i = 0; i < Adds; i++)
        {
            patch.Operations.Add(new Operation<Holder>(
                op: "add",
                path: $"/Lines/{i}",
                from: null,
                value: new VLine { V = i }
            ));
        }
        return patch;
    }

    [Benchmark(Description = "Manual Build (adds ops)")]
    public List<AddOp> Manual_Build_Adds()
    {
        var ops = new List<AddOp>(Adds);
        for (int i = 0; i < Adds; i++)
            ops.Add(new AddOp { Index = i, Value = new VLine { V = i } });
        return ops;
    }

    [Benchmark(Description = "Immutable Build (values only)")]
    public List<VLine> Immutable_Build_Values()
    {
        var vals = new List<VLine>(Adds);
        for (int i = 0; i < Adds; i++)
            vals.Add(new VLine { V = i });
        return vals;
    }

    // -------------------------
    // APPLY (apply the prebuilt change set to an empty target)
    // -------------------------

    [Benchmark(Description = "DeepEqual Apply (adds doc)")]
    public int DeepEqual_Apply_AddsDoc()
    {
        var target = new Holder { Lines = new List<VLine>() };
        target = target.ApplyDeepDelta(_deepeqAddsDoc);
        return target.Lines.Count;
    }

    [Benchmark(Description = "JsonPatch Apply (adds patch)")]
    public int JsonPatch_Apply_AddsPatch()
    {
        var target = new Holder { Lines = new List<VLine>() };
        _jsonPatchAdds.ApplyTo(target);
        return target.Lines.Count;
    }

    [Benchmark(Description = "Manual Apply (adds ops)")]
    public int Manual_Apply_Adds()
    {
        var list = new List<VLine>(Adds);
        // Insert at specified index to match the semantics of the other patches
        // (here all indices are i, so this is effectively Append for our scenario)
        for (int i = 0; i < _manualAdds.Count; i++)
        {
            var op = _manualAdds[i];
            int idx = op.Index;
            if ((uint)idx > (uint)list.Count) idx = list.Count; // clamp like others
            if (idx == list.Count) list.Add(op.Value);
            else list.Insert(idx, op.Value);
        }
        return list.Count;
    }

    [Benchmark(Description = "Immutable Apply (Insert)")]
    public int Immutable_Apply_Insert()
    {
        var im = ImmutableList<VLine>.Empty;
        for (int i = 0; i < _immutableAddsValues.Count; i++)
        {
            // Insert at i to mirror other competitors; this is intentionally expensive
            im = im.Insert(i, _immutableAddsValues[i]);
        }
        return im.Count;
    }
}

[MemoryDiagnoser]
public class AccessTrackingSetterBenchmarks
{
    private readonly BaselineModel _baseline = new();
    private readonly BitsBenchModel _bits = new();
    private readonly CountsBenchModel _counts = new();

    [GlobalSetup]
    public void Setup()
    {
        AccessTracking.Configure(
            defaultMode: AccessMode.None,
            defaultGranularity: AccessGranularity.Bits,
            defaultLogCapacity: 0,
            trackingEnabled: true,
            countsEnabled: true,
            lastEnabled: true,
            logEnabled: true,
            callersEnabled: true);
    }

    [Benchmark(Baseline = true, Description = "Setter (no tracking)")]
    public int Baseline()
    {
        _baseline.Value++;
        return _baseline.Value;
    }

    [Benchmark(Description = "Setter (bits)")]
    public int Bits()
    {
        _bits.Value++;
        return _bits.Value;
    }

    [Benchmark(Description = "Setter (counts+last)")]
    public int Counts()
    {
        _counts.Value++;
        return _counts.Value;
    }

    private sealed class BaselineModel
    {
        public int Value { get; set; }
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        var config = DefaultConfig.Instance.WithArtifactsPath(@"C:\Users\mirko\Downloads\benc");
        if (args is { Length: > 0 })
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
            return;
        }

        BenchmarkRunner.Run(
        [
            //typeof(Competitors_Adds_Benches),
            //typeof(ExtraApplyClonePathBenches),
            typeof(AccessTrackingComplexBenchmarks),
            //typeof(AccessTrackingSetterBenchmarks),
            //typeof(AccessTrackingListBenchmarks),
            //typeof(ExtraArrayVsListBenches),
            //typeof(ExtraCodecBenches),
            //typeof(ExtraDirtyBitBenches),
            //typeof(ExtraEqualityBenches),
            //typeof(ExtraPolymorphicBenches),
        ], config);
    }
}




[DeepComparable(GenerateDiff = true, GenerateDelta = true, StableMemberIndex = StableMemberIndexMode.Auto)]
[DeltaTrack(AccessTrack = AccessMode.Write, AccessGranularity = AccessGranularity.Bits)]
public partial class BitsBenchModel
{
    private int _value;
    public int Value
    {
        get => _value;
        set
        {
            _value = value;
            __MarkDirty(__Bit_Value);
        }
    }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true, StableMemberIndex = StableMemberIndexMode.Auto)]
[DeltaTrack(AccessTrack = AccessMode.Write, AccessGranularity = AccessGranularity.CountsAndLast)]
public partial class CountsBenchModel
{
    private int _value;
    public int Value
    {
        get => _value;
        set
        {
            _value = value;
            __MarkDirty(__Bit_Value);
        }
    }
}
// Tracked models (generator will inject the tracking hooks)
[DeltaTrack(AccessTrack = AccessMode.Write, AccessGranularity = AccessGranularity.Bits)]
public partial class TrackedBitsOrder
{
    // Tracking triggers on the SET; internal List<T>.Add wouldn't trigger tracking
    public List<int> Lines { get; set; } = new();
}

[DeltaTrack(AccessTrack = AccessMode.Write, AccessGranularity = AccessGranularity.CountsAndLast)]
public partial class TrackedCountsLastOrder
{
    public List<int> Lines { get; set; } = new();
}

// Plain baseline (no tracking)
public class PlainOrder
{
    public List<int> Lines { get; set; } = new();
}

[MemoryDiagnoser]
[HideColumns("Median", "Min", "Max")]
public class AccessTrackingListBenchmarks
{
    // Size of the list we build/assign each iteration (replace-on-change pattern)
    [Params(0, 8, 64, 512)]
    public int N;

    private PlainOrder _plain = default!;
    private TrackedBitsOrder _bits = default!;
    private TrackedCountsLastOrder _countsLast = default!;

    [GlobalSetup]
    public void Setup()
    {
        _plain = new PlainOrder();
        _bits = new TrackedBitsOrder();
        _countsLast = new TrackedCountsLastOrder();

        // Optional: if your runtime needs callers enabled globally, uncomment:
        // AccessTracking.Configure(trackingEnabled: true, callersEnabled: true);
    }

    // Helper: build a fresh list with N elements to simulate real work/allocations
    private List<int> BuildList(int n)
    {
        var list = new List<int>(n);
        for (int i = 0; i < n; i++) list.Add(i);
        return list;
    }

    [Benchmark(Baseline = true, Description = "List replace (no tracking)")]
    public void Plain_ListReplace()
    {
        _plain.Lines = BuildList(N);
    }

    [Benchmark(Description = "List replace (bits)")]
    public void TrackedBits_ListReplace()
    {
        _bits.Lines = BuildList(N);
    }

    [Benchmark(Description = "List replace (counts+last)")]
    public void TrackedCountsLast_ListReplace()
    {
        _countsLast.Lines = BuildList(N);
    }

    [Benchmark(Description = "List replace (counts+last + caller scope)")]
    public void TrackedCountsLast_WithCallerScope_ListReplace()
    {
        using (AccessTracking.PushScope(label: "Benchmark.ReplaceList",
               member: nameof(TrackedCountsLast_WithCallerScope_ListReplace),
               file: "Benchmarks/AccessTrackingListBenchmarks.cs",
               line: 0))
        {
            _countsLast.Lines = BuildList(N);
        }
    }
}
// -----------------------------
// Baseline (no tracking)
// -----------------------------
public class PlainSubItem
{
    public string Label { get; set; } = string.Empty;
    public int Level { get; set; }
}

public class PlainComplexModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime When { get; set; }
    public decimal Price { get; set; }
    public List<int> Lines { get; set; } = new();
    public int[] Scores { get; set; } = Array.Empty<int>();
    public Dictionary<string, int> Map { get; set; } = new();
    public PlainSubItem Item { get; set; } = new();
    public Guid Key { get; set; }
    public byte[] Blob { get; set; } = Array.Empty<byte>();
}

// -----------------------------
// Tracked (write-only; generator will inject hooks)
// -----------------------------
public class TrackedSubItem
{
    public string Label { get; set; } = string.Empty;
    public int Level { get; set; }
}

// Counts+Last to stress the heavier path (bitset-only is cheaper; feel free to add that variant too)
[DeltaTrack(AccessTrack = AccessMode.Write, AccessGranularity = AccessGranularity.CountsAndLast)]
public partial class TrackedComplexModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime When { get; set; }
    public decimal Price { get; set; }
    public List<int> Lines { get; set; } = new();
    public int[] Scores { get; set; } = Array.Empty<int>();
    public Dictionary<string, int> Map { get; set; } = new();
    public TrackedSubItem Item { get; set; } = new();
    public Guid Key { get; set; }
    public byte[] Blob { get; set; } = Array.Empty<byte>();
}

[MemoryDiagnoser]
[HideColumns("Median", "Min", "Max")]
public class AccessTrackingComplexBenchmarks
{
    // Controls size of collections/blobs to simulate small vs large updates
    [Params(0, 8, 64, 512)]
    public int N;

    private PlainComplexModel _plain = default!;
    private TrackedComplexModel _tracked = default!;

    [GlobalSetup]
    public void Setup()
    {
        _plain = new PlainComplexModel();
        _tracked = new TrackedComplexModel();

        // If you want caller attribution enabled globally, configure at runtime:
        // AccessTracking.Configure(trackingEnabled: true, callersEnabled: true);
    }

    // Build a fresh set of values to assign to all 10 properties.
    // Intent: replace-on-change to exercise the property setters (and thus write-tracking hook).
    private (int id, string name, DateTime when, decimal price,
             List<int> lines, int[] scores, Dictionary<string, int> map,
             object item /*PlainSubItem|TrackedSubItem*/, Guid key, byte[] blob)
    BuildData(int n, bool forTracked)
    {
        var id = Environment.TickCount; // cheap non-deterministic
        var name = "order-" + id.ToString();
        var when = DateTime.UtcNow;
        var price = (decimal)(id & 0xFFFF) / 100m;

        var lines = new List<int>(n);
        for (int i = 0; i < n; i++) lines.Add(i);

        var scores = new int[n];
        for (int i = 0; i < n; i++) scores[i] = i ^ 0x5A;

        var map = new Dictionary<string, int>(n);
        for (int i = 0; i < n; i++) map["k" + i] = i * 3;

        var key = Guid.NewGuid();

        var blob = new byte[Math.Max(n, 0) * 4]; // 0, 32, 256, 2048 bytes
        if (blob.Length > 0) new Random(id).NextBytes(blob);

        object item = forTracked
            ? new TrackedSubItem { Label = "L" + id, Level = (id & 7) }
            : new PlainSubItem { Label = "L" + id, Level = (id & 7) };

        return (id, name, when, price, lines, scores, map, item, key, blob);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AssignAll(PlainComplexModel m,
        int id, string name, DateTime when, decimal price,
        List<int> lines, int[] scores, Dictionary<string, int> map,
        PlainSubItem item, Guid key, byte[] blob)
    {
        // 10 property writes
        m.Id = id;
        m.Name = name;
        m.When = when;
        m.Price = price;
        m.Lines = lines;
        m.Scores = scores;
        m.Map = map;
        m.Item = item;
        m.Key = key;
        m.Blob = blob;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AssignAll(TrackedComplexModel m,
        int id, string name, DateTime when, decimal price,
        List<int> lines, int[] scores, Dictionary<string, int> map,
        TrackedSubItem item, Guid key, byte[] blob)
    {
        // 10 property writes (each should hit the write-tracker hook)
        m.Id = id;
        m.Name = name;
        m.When = when;
        m.Price = price;
        m.Lines = lines;
        m.Scores = scores;
        m.Map = map;
        m.Item = item;
        m.Key = key;
        m.Blob = blob;
    }

    // -----------------------------
    // Benchmarks
    // -----------------------------

    [Benchmark(Baseline = true, Description = "Set 10 props (no tracking)")]
    public void Plain_SetAll()
    {
        var (id, name, when, price, lines, scores, map, item, key, blob) = BuildData(N, forTracked: false);
        AssignAll(_plain, id, name, when, price, lines, scores, map, (PlainSubItem)item, key, blob);
    }

    [Benchmark(Description = "Set 10 props (tracking: counts+last)")]
    public void Tracked_SetAll()
    {
        var (id, name, when, price, lines, scores, map, item, key, blob) = BuildData(N, forTracked: true);
        AssignAll(_tracked, id, name, when, price, lines, scores, map, (TrackedSubItem)item, key, blob);
    }

    [Benchmark(Description = "Set 10 props (tracking + caller scope)")]
    public void Tracked_WithCallerScope_SetAll()
    {
        using (AccessTracking.PushScope(
            label: "Benchmark.SetAll",
            member: nameof(Tracked_WithCallerScope_SetAll),
            file: "Benchmarks/AccessTrackingComplexBenchmarks.cs",
            line: 0))
        {
            var (id, name, when, price, lines, scores, map, item, key, blob) = BuildData(N, forTracked: true);
            AssignAll(_tracked, id, name, when, price, lines, scores, map, (TrackedSubItem)item, key, blob);
        }
    }
}