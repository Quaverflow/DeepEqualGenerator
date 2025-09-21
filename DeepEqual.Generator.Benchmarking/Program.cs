using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using DeepEqual.Generator.Shared;
using Perfolizer.Horology;
using System.Buffers;
using System.Dynamic;
using System.Text;

namespace DeepEqual.Generator.Benchmarking;

internal class Program
{
    static void Main() => BenchmarkRunner.Run<MidGraphBenchmarks>();
}

public enum Region { NA, EU, APAC }

// Enable both Diff & Delta across the graph for full coverage
[DeepComparable(CycleTracking = false, GenerateDiff = true, GenerateDelta = true)]
public sealed class Address
{
    public string Line1 { get; set; } = "";
    public string City { get; set; } = "";
    public string Postcode { get; set; } = "";
    public string Country { get; set; } = "";
    public ExpandoObject? Countr3y { get; set; }
}

[DeepComparable(CycleTracking = false, GenerateDiff = true, GenerateDelta = true)]
public sealed class OrderLine
{
    public string Sku { get; set; } = "";
    public int Qty { get; set; }
    public decimal LineTotal { get; set; }
}

[DeepComparable(CycleTracking = false, GenerateDiff = true, GenerateDelta = true)]
public sealed class Order
{
    public Guid Id { get; set; }
    public DateTimeOffset Created { get; set; }
    public List<OrderLine> Lines { get; set; } = [];
    public Dictionary<string, string> Meta { get; set; } = new(StringComparer.Ordinal);
}

[DeepComparable(CycleTracking = false, GenerateDiff = true, GenerateDelta = true)]
public sealed class Customer
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = "";
    public Region Region { get; set; }
    public Address ShipTo { get; set; } = new();
    public List<Order> Orders { get; set; } = [];
}

[DeepComparable(CycleTracking = false, GenerateDiff = true, GenerateDelta = true)]
public sealed class MidGraph
{
    public string Title { get; set; } = "";
    public List<Customer> Customers { get; set; } = [];
    public Dictionary<string, decimal> PriceIndex { get; set; } = new(StringComparer.Ordinal);
    public object? Polymorph { get; set; }
    public IDictionary<string, object?> Extra { get; set; } = new ExpandoObject();
}

public static class MidGraphFactory
{
    public static MidGraph Create(int customers = 40, int ordersPerCustomer = 3, int linesPerOrder = 4, int seed = 123)
    {
        var g = new MidGraph
        {
            Title = $"MidGraph-{seed}",
            Polymorph = (seed % 2 == 0)
                ? (object)$"poly-{seed}"
                : new Address { Line1 = "1 High St", City = "London", Postcode = "E1 1AA", Country = "UK" }
        };

        for (var i = 0; i < 50; i++)
            g.PriceIndex[$"SKU-{i:D4}"] = 10 + (i % 7);

        var ex = (IDictionary<string, object?>)g.Extra;
        ex["build"] = seed;
        ex["flags"] = new[] { "x", "y", "z" };
        ex["meta"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["channel"] = "web",
            ["ab"] = new[] { "A", "B" }
        };

        for (var c = 0; c < customers; c++)
        {
            var cust = new Customer
            {
                Id = GuidFrom($"C{seed}-{c}"),
                FullName = $"Customer {c}",
                Region = (Region)(c % 3),
                ShipTo = new Address
                {
                    Line1 = $"{c} Road",
                    City = (c % 2 == 0) ? "London" : "Paris",
                    Postcode = $"PC{c:000}",
                    Country = (c % 2 == 0) ? "UK" : "FR"
                }
            };

            for (var o = 0; o < ordersPerCustomer; o++)
            {
                var order = new Order
                {
                    Id = GuidFrom($"C{c}-O{o}-{seed}"),
                    Created = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero).AddMinutes(o)
                };

                for (var l = 0; l < linesPerOrder; l++)
                {
                    var sku = $"SKU-{(c + o + l) % 50:D4}";
                    var price = g.PriceIndex[sku];
                    order.Lines.Add(new OrderLine
                    {
                        Sku = sku,
                        Qty = 1 + (l % 3),
                        LineTotal = price * (1 + (l % 3))
                    });
                }

                order.Meta["channel"] = (o % 2 == 0) ? "web" : "app";
                order.Meta["bucket"] = ((c + o) % 5).ToString();
                cust.Orders.Add(order);
            }

            g.Customers.Add(cust);
        }

        return g;

        static Guid GuidFrom(string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            Span<byte> g = stackalloc byte[16];
            for (var i = 0; i < 16; i++) g[i] = (byte)(bytes[i % bytes.Length] + i * 31);
            return new Guid(g);
        }
    }
}

// ------------------ Manual comparers (unchanged) ------------------

public static class ManualNonLinq
{
    public static bool AreEqual(MidGraph? a, MidGraph? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;

        if (!string.Equals(a.Title, b.Title, StringComparison.Ordinal)) return false;
        if (!DictEqual(a.PriceIndex, b.PriceIndex)) return false;
        if (!ObjectEqual(a.Polymorph, b.Polymorph)) return false;
        if (!DynamicEqual(a.Extra, b.Extra)) return false;

        if (a.Customers.Count != b.Customers.Count) return false;
        for (var i = 0; i < a.Customers.Count; i++)
            if (!CustomerEqual(a.Customers[i], b.Customers[i])) return false;

        return true;
    }
    private static bool CustomerEqual(Customer? a, Customer? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;

        if (!string.Equals(a.FullName, b.FullName, StringComparison.Ordinal)) return false;
        if (a.Region != b.Region) return false;
        if (!AddressEqual(a.ShipTo, b.ShipTo)) return false;

        if (a.Orders.Count != b.Orders.Count) return false;
        for (var i = 0; i < a.Orders.Count; i++)
            if (!OrderEqual(a.Orders[i], b.Orders[i])) return false;

        return true;
    }
    private static bool OrderEqual(Order? a, Order? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;

        if (a.Id != b.Id) return false;
        if (a.Created != b.Created) return false;
        if (!DictEqual(a.Meta, b.Meta)) return false;

        if (a.Lines.Count != b.Lines.Count) return false;
        for (var i = 0; i < a.Lines.Count; i++)
            if (!LineEqual(a.Lines[i], b.Lines[i])) return false;

        return true;
    }
    private static bool LineEqual(OrderLine? a, OrderLine? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        return a.Sku == b.Sku && a.Qty == b.Qty && a.LineTotal == b.LineTotal;
    }
    private static bool AddressEqual(Address? a, Address? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        return a.Line1 == b.Line1 && a.City == b.City && a.Postcode == b.Postcode && a.Country == b.Country;
    }
    private static bool DictEqual<TKey, TValue>(Dictionary<TKey, TValue>? a, Dictionary<TKey, TValue>? b)
        where TKey : notnull
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Count != b.Count) return false;

        foreach (var kv in a)
        {
            if (!b.TryGetValue(kv.Key, out var bv)) return false;
            if (!Equals(kv.Value, bv)) return false;
        }
        return true;
    }
    private static bool ObjectEqual(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.GetType() != b.GetType()) return false;

        if (a is string sa) return string.Equals(sa, (string)b, StringComparison.Ordinal);
        if (a is Address aa) return AddressEqual(aa, (Address)b);
        return a.Equals(b);
    }
    private static bool DynamicEqual(IDictionary<string, object?> a, IDictionary<string, object?> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var kv in a)
        {
            if (!b.TryGetValue(kv.Key, out var bv)) return false;
            if (!ObjectEqual(kv.Value, bv)) return false;
        }
        return true;
    }
}

public static class ManualLinqy
{
    public static bool AreEqual(MidGraph? a, MidGraph? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;

        return a.Title == b.Title
               && DictEqual(a.PriceIndex, b.PriceIndex)
               && ObjectEqual(a.Polymorph, b.Polymorph)
               && DynamicEqual(a.Extra, b.Extra)
               && a.Customers.SequenceEqual(b.Customers, new CustomerEq());
    }

    private sealed class CustomerEq : IEqualityComparer<Customer>
    {
        public bool Equals(Customer? x, Customer? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;

            return x.FullName == y.FullName
                   && x.Region == y.Region
                   && AddressEqual(x.ShipTo, y.ShipTo)
                   && x.Orders.SequenceEqual(y.Orders, new OrderEq());
        }
        public int GetHashCode(Customer obj) => 0;
    }

    private sealed class OrderEq : IEqualityComparer<Order>
    {
        public bool Equals(Order? x, Order? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;

            return x.Id == y.Id
                   && x.Created.Equals(y.Created)
                   && DictEqual(x.Meta, y.Meta)
                   && x.Lines.SequenceEqual(y.Lines, new LineEq());
        }
        public int GetHashCode(Order obj) => 0;
    }

    private sealed class LineEq : IEqualityComparer<OrderLine>
    {
        public bool Equals(OrderLine? x, OrderLine? y)
            => x!.Sku == y!.Sku && x.Qty == y!.Qty && x.LineTotal == y!.LineTotal;
        public int GetHashCode(OrderLine obj) => 0;
    }

    private static bool AddressEqual(Address? a, Address? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        return a.Line1 == b.Line1 && a.City == b.City && a.Postcode == b.Postcode && a.Country == b.Country;
    }
    private static bool DictEqual<TKey, TValue>(Dictionary<TKey, TValue> a, Dictionary<TKey, TValue> b)
        where TKey : notnull
        => a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var bv) && Equals(kv.Value, bv));
    private static bool ObjectEqual(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.GetType() != b.GetType()) return false;

        return a switch
        {
            string sa => sa == (string)b,
            Address aa => AddressEqual(aa, (Address)b),
            _ => a.Equals(b)
        };
    }
    private static bool DynamicEqual(IDictionary<string, object?> a, IDictionary<string, object?> b)
        => a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var bv) && ObjectEqual(kv.Value, bv));
}

// ------------------ Benchmarks ------------------

[MemoryDiagnoser]
[PlainExporter]
[Config(typeof(DefaultJobConfig))]
public class MidGraphBenchmarks
{
    private class DefaultJobConfig : ManualConfig
    {
        public DefaultJobConfig()
        {
            AddJob(Job.Default
                .WithId("DefaultJob")
                .WithUnrollFactor(16)
                .WithIterationTime(TimeInterval.FromMilliseconds(100)));
        }
    }

    [Params(500)] public int Customers;
    [Params(3)] public int OrdersPerCustomer;
    [Params(4)] public int LinesPerOrder;

    // Equality inputs
    private MidGraph _eqA = null!;
    private MidGraph _eqB = null!;
    private MidGraph _neqShallowA = null!;
    private MidGraph _neqShallowB = null!;
    private MidGraph _neqDeepA = null!;
    private MidGraph _neqDeepB = null!;

    // Delta inputs
    private BinaryDeltaOptions _bin = null!;
    private DeltaDocument _deltaShallow = null!;
    private DeltaDocument _deltaDeep = null!;
    private byte[] _deltaShallowBin = null!;
    private byte[] _deltaDeepBin = null!;
    private DeltaDocument _deltaShallowDecoded = null!;
    private DeltaDocument _deltaDeepDecoded = null!;

    // Apply targets (one per iteration)
    private MidGraph _shallowTarget = null!;
    private MidGraph _deepTarget = null!;
    private MidGraph _shallowTargetBin = null!;
    private MidGraph _deepTargetBin = null!;

    // Encode buffers (reset per iteration)
    private ArrayBufferWriter<byte> _bufShallow = null!;
    private ArrayBufferWriter<byte> _bufDeep = null!;

    [GlobalSetup]
    public void Setup()
    {
        _eqA = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, seed: 11);
        _eqB = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, seed: 11);

        _neqShallowA = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, seed: 22);
        _neqShallowB = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, seed: 22);
        _neqShallowB.Title += "-DIFF";

        _neqDeepA = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, seed: 33);
        _neqDeepB = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, seed: 33);
        var lastC = _neqDeepB.Customers[^1];
        var lastO = lastC.Orders[^1];
        lastO.Lines[^1].Qty += 1;

        _bin = new BinaryDeltaOptions { IncludeHeader = false };

        // Precompute deltas and binary
        {
            var ctx = new ComparisonContext();
            _deltaShallow = MidGraphDeepOps.ComputeDelta(_neqShallowA, _neqShallowB, ctx);
            var tmp = new ArrayBufferWriter<byte>();
            BinaryDeltaCodec.Write(_deltaShallow, tmp, _bin);
            _deltaShallowBin = tmp.WrittenSpan.ToArray();
            _deltaShallowDecoded = BinaryDeltaCodec.Read(_deltaShallowBin, _bin);
        }

        {
            var ctx = new ComparisonContext();
            _deltaDeep = MidGraphDeepOps.ComputeDelta(_neqDeepA, _neqDeepB, ctx);
            var tmp = new ArrayBufferWriter<byte>();
            BinaryDeltaCodec.Write(_deltaDeep, tmp, _bin);
            _deltaDeepBin = tmp.WrittenSpan.ToArray();
            _deltaDeepDecoded = BinaryDeltaCodec.Read(_deltaDeepBin, _bin);
        }

        // Initial targets for apply benches (reset per iteration)
        _shallowTarget = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, seed: 22);
        _deepTarget = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, seed: 33);
        _shallowTargetBin = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, seed: 22);
        _deepTargetBin = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, seed: 33);

        // Initial encode buffers (capacity hint)
        _bufShallow = new ArrayBufferWriter<byte>(_deltaShallowBin.Length + 64);
        _bufDeep = new ArrayBufferWriter<byte>(_deltaDeepBin.Length + 64);
    }

    // ---------- Per-target resets outside measurement ----------

    [IterationSetup(Target = nameof(Apply_InMemory_Shallow_Delta))]
    public void Reset_ShallowTarget_ForInMemoryApply()
        => _shallowTarget = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, seed: 22);

    [IterationSetup(Target = nameof(Apply_InMemory_Deep_Delta))]
    public void Reset_DeepTarget_ForInMemoryApply()
        => _deepTarget = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, seed: 33);

    [IterationSetup(Target = nameof(Binary_Apply_Shallow_Delta))]
    public void Reset_ShallowTarget_ForBinaryApply()
        => _shallowTargetBin = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, seed: 22);

    [IterationSetup(Target = nameof(Binary_Apply_Deep_Delta))]
    public void Reset_DeepTarget_ForBinaryApply()
        => _deepTargetBin = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, seed: 33);

    [IterationSetup(Targets = new[] { nameof(Binary_Encode_Shallow_Delta_Size) })]
    public void Reset_Shallow_Buffer() => _bufShallow = new ArrayBufferWriter<byte>(_deltaShallowBin.Length + 64);

    [IterationSetup(Targets = new[] { nameof(Binary_Encode_Deep_Delta_Size) })]
    public void Reset_Deep_Buffer() => _bufDeep = new ArrayBufferWriter<byte>(_deltaDeepBin.Length + 64);

    // -------------------- Equality --------------------

    [Benchmark] public bool Generated_NotEqual_Deep() => MidGraphDeepEqual.AreDeepEqual(_neqDeepA, _neqDeepB);
    [Benchmark] public bool Generated_NotEqual_Shallow() => MidGraphDeepEqual.AreDeepEqual(_neqShallowA, _neqShallowB);
    [Benchmark(Baseline = true)] public bool Generated_Equal() => MidGraphDeepEqual.AreDeepEqual(_eqA, _eqB);
    [Benchmark] public bool Manual_NonLinq_Equal() => ManualNonLinq.AreEqual(_eqA, _eqB);
    [Benchmark] public bool Manual_Linqy_Equal() => ManualLinqy.AreEqual(_eqA, _eqB);
    [Benchmark] public bool Manual_NonLinq_NotEqual_Shallow() => ManualNonLinq.AreEqual(_neqShallowA, _neqShallowB);
    [Benchmark] public bool Manual_Linqy_NotEqual_Shallow() => ManualLinqy.AreEqual(_neqShallowA, _neqShallowB);
    [Benchmark] public bool Manual_NonLinq_NotEqual_Deep() => ManualNonLinq.AreEqual(_neqDeepA, _neqDeepB);
    [Benchmark] public bool Manual_Linqy_NotEqual_Deep() => ManualLinqy.AreEqual(_neqDeepA, _neqDeepB);

    // -------------------- Delta compute / diff --------------------

    [Benchmark]
    public (bool hasDiff, Diff<MidGraph> diff) Generated_Diff_NoChange_HasDiff()
        => MidGraphDeepOps.GetDiff(_eqA, _eqB);

    [Benchmark]
    public (bool hasDiff, Diff<MidGraph> diff) Generated_Diff_Deep_Change_MemberCount()
        => MidGraphDeepOps.GetDiff(_neqDeepA, _neqDeepB);

    [Benchmark]
    public DeltaDocument Generated_ComputeDelta_Shallow_OpCount()
        => MidGraphDeepOps.ComputeDelta(_neqShallowA, _neqShallowB);

    [Benchmark]
    public DeltaDocument Generated_ComputeDelta_Deep_OpCount()
        => MidGraphDeepOps.ComputeDelta(_neqDeepA, _neqDeepB);

    // -------------------- In-memory apply (document already built) --------------------

    [Benchmark]
    public bool Apply_InMemory_Shallow_Delta()
    {
        MidGraphDeepOps.ApplyDelta(ref _shallowTarget, _deltaShallow);
        return true;
    }

    [Benchmark]
    public bool Apply_InMemory_Deep_Delta()
    {
        MidGraphDeepOps.ApplyDelta(ref _deepTarget, _deltaDeep);
        return true;
    }

    // -------------------- Binary encode/decode/apply --------------------

    [Benchmark]
    public int Binary_Encode_Shallow_Delta_Size()
    {
        BinaryDeltaCodec.Write(_deltaShallow, _bufShallow, _bin);
        return _bufShallow.WrittenCount;
    }

    [Benchmark]
    public int Binary_Encode_Deep_Delta_Size()
    {
        BinaryDeltaCodec.Write(_deltaDeep, _bufDeep, _bin);
        return _bufDeep.WrittenCount;
    }

    [Benchmark]
    public DeltaDocument Binary_Decode_Shallow_Delta_OpCount()
        => BinaryDeltaCodec.Read(_deltaShallowBin, _bin);

    [Benchmark]
    public int Binary_Decode_Deep_Delta_OpCount()
        => BinaryDeltaCodec.Read(_deltaDeepBin, _bin).Operations.Count;

    [Benchmark]
    public bool Binary_Apply_Shallow_Delta()
    {
        MidGraphDeepOps.ApplyDelta(ref _shallowTargetBin, _deltaShallowDecoded);
        return true;
    }

    [Benchmark]
    public bool Binary_Apply_Deep_Delta()
    {
        MidGraphDeepOps.ApplyDelta(ref _deepTargetBin, _deltaDeepDecoded);
        return true;
    }
}
