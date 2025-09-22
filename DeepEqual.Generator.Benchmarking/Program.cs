using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using DeepEqual.Generator.Shared;
// Cloning (kept in its own benchmark group)
using FastDeepCloner;
using KellermanSoftware.CompareNetObjects;
using Newtonsoft.Json.Linq;
using Perfolizer.Horology;
using System.Buffers;
using System.Dynamic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DeepEqual.Generator.Benchmarking;

internal class Program
{
    private static void Main()
    {
        BenchmarkRunner.Run<MidGraphBenchmarks>();
    }
}

public enum Region
{
    NA,
    EU,
    APAC
}

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
            Polymorph = seed % 2 == 0
                ? (object)$"poly-{seed}"
                : new Address { Line1 = "1 High St", City = "London", Postcode = "E1 1AA", Country = "UK" }
        };

        for (var i = 0; i < 50; i++)
            g.PriceIndex[$"SKU-{i:D4}"] = 10 + i % 7;

        var ex = (IDictionary<string, object?>)g.Extra;
        ex["build"] = seed;
        ex["flags"] = new[] { "x", "y", "z" };

        var rnd = new Random(seed);
        for (var c = 0; c < customers; c++)
        {
            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                FullName = $"Customer {c}",
                Region = (Region)(c % 3),
                ShipTo = new Address
                {
                    Line1 = $"{c} Any St",
                    City = "London",
                    Postcode = $"E1 {c % 10}AA",
                    Country = "UK"
                }
            };

            for (var o = 0; o < ordersPerCustomer; o++)
            {
                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    Created = DateTimeOffset.UtcNow.AddDays(-o),
                };
                order.Meta["channel"] = (o % 2 == 0) ? "web" : "app";

                for (var l = 0; l < linesPerOrder; l++)
                {
                    var skuIndex = rnd.Next(0, 50);
                    order.Lines.Add(new OrderLine
                    {
                        Sku = $"SKU-{skuIndex:D4}",
                        Qty = 1 + (l % 3),
                        LineTotal = 9.99m + skuIndex % 7
                    });
                }

                customer.Orders.Add(order);
            }

            g.Customers.Add(customer);
        }

        return g;
    }
}

// ------------------ Manual comparers used in existing baselines ------------------

public static class ManualNonLinq
{
    public static bool AreEqual(MidGraph a, MidGraph b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Title != b.Title) return false;
        if (!DictEqual(a.PriceIndex, b.PriceIndex)) return false;
        if (a.Customers.Count != b.Customers.Count) return false;

        for (int i = 0; i < a.Customers.Count; i++)
            if (!new CustomerEq().Equals(a.Customers[i], b.Customers[i])) return false;

        return DynamicEqual((IDictionary<string, object?>)a.Extra, (IDictionary<string, object?>)b.Extra);
    }

    private static bool AddressEqual(Address? a, Address? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        return a.Line1 == b.Line1 && a.City == b.City && a.Postcode == b.Postcode && a.Country == b.Country;
    }

    private static bool DictEqual<TKey, TValue>(Dictionary<TKey, TValue> a, Dictionary<TKey, TValue> b)
        where TKey : notnull
    {
        return a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var bv) && Equals(kv.Value, bv));
    }

    private static bool ObjectEqual(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.GetType() != b.GetType()) return false;

        return a switch
        {
            string sa => sa == (string)b!,
            Address aa => AddressEqual(aa, (Address)b!),
            _ => a.Equals(b)
        };
    }

    private static bool DynamicEqual(IDictionary<string, object?> a, IDictionary<string, object?> b)
    {
        return a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var bv) && ObjectEqual(kv.Value, bv));
    }

    private sealed class CustomerEq : IEqualityComparer<Customer>
    {
        public bool Equals(Customer? x, Customer? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;

            return x.FullName == y.FullName
                   && x.Region == y.Region
                   && new AddressEq().Equals(x.ShipTo, y.ShipTo)
                   && x.Orders.SequenceEqual(y.Orders, new OrderEq());
        }

        public int GetHashCode(Customer obj) => 0;
    }

    private sealed class AddressEq : IEqualityComparer<Address>
    {
        public bool Equals(Address? x, Address? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            return x.Line1 == y.Line1 && x.City == y.City && x.Postcode == y.Postcode && x.Country == y.Country;
        }

        public int GetHashCode(Address obj) => 0;
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
        {
            return x!.Sku == y!.Sku && x.Qty == y!.Qty && x.LineTotal == y!.LineTotal;
        }

        public int GetHashCode(OrderLine obj) => 0;
    }
}

public static class ManualLinqy
{
    public static bool AreEqual(MidGraph a, MidGraph b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;

        return a.Title == b.Title
            && a.PriceIndex.Count == b.PriceIndex.Count
            && a.PriceIndex.All(kv => b.PriceIndex.TryGetValue(kv.Key, out var v) && v == kv.Value)
            && a.Customers.SequenceEqual(b.Customers, new CustomerEq())
            && ((IDictionary<string, object?>)a.Extra).OrderBy(kv => kv.Key)
                 .SequenceEqual(((IDictionary<string, object?>)b.Extra).OrderBy(kv => kv.Key), new DynEq());
    }

    private sealed class DynEq : IEqualityComparer<KeyValuePair<string, object?>>
    {
        public bool Equals(KeyValuePair<string, object?> x, KeyValuePair<string, object?> y)
            => x.Key == y.Key && Equals(x.Value, y.Value);
        public int GetHashCode(KeyValuePair<string, object?> obj) => 0;
    }

    private sealed class CustomerEq : IEqualityComparer<Customer>
    {
        public bool Equals(Customer? x, Customer? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;

            return x.FullName == y.FullName
                   && x.Region == y.Region
                   && new AddressEq().Equals(x.ShipTo, y.ShipTo)
                   && x.Orders.SequenceEqual(y.Orders, new OrderEq());
        }

        public int GetHashCode(Customer obj) => 0;
    }

    private sealed class AddressEq : IEqualityComparer<Address>
    {
        public bool Equals(Address? x, Address? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            return x.Line1 == y.Line1 && x.City == y.City && x.Postcode == y.Postcode && x.Country == y.Country;
        }

        public int GetHashCode(Address obj) => 0;
    }

    private sealed class OrderEq : IEqualityComparer<Order>
    {
        public bool Equals(Order? x, Order? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;

            return x.Id == y.Id
                   && x.Created.Equals(y.Created)
                   && x.Meta.Count == y.Meta.Count
                   && x.Meta.All(kv => y.Meta.TryGetValue(kv.Key, out var v) && v == kv.Value)
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
}

// ------------------ Benchmarks ------------------

[MemoryDiagnoser]
[PlainExporter]
[MarkdownExporterAttribute.GitHub]
[CsvExporter(CsvSeparator.Comma)]
[Config(typeof(DefaultJobConfig))]
public class MidGraphBenchmarks
{
    // binary delta setup (existing)
    private BinaryDeltaOptions _bin = null!;
    private ArrayBufferWriter<byte> _bufDeep = null!;
    private ArrayBufferWriter<byte> _bufShallow = null!;
    private MidGraph _deepTarget = null!;
    private MidGraph _deepTargetBin = null!;
    private DeltaDocument _deltaDeep = null!;
    private byte[] _deltaDeepBin = null!;
    private DeltaDocument _deltaDeepDecoded = null!;
    private DeltaDocument _deltaShallow = null!;
    private byte[] _deltaShallowBin = null!;
    private DeltaDocument _deltaShallowDecoded = null!;

    // datasets
    private MidGraph _eqA = null!;
    private MidGraph _eqB = null!;
    private MidGraph _neqDeepA = null!;
    private MidGraph _neqDeepB = null!;
    private MidGraph _neqShallowA = null!;
    private MidGraph _neqShallowB = null!;

    private MidGraph _shallowTarget = null!;
    private MidGraph _shallowTargetBin = null!;

    // -------- Precomputed JSON DOMs for equality-only baselines --------
    private JToken _eqA_J = null!, _eqB_J = null!, _neqShallowA_J = null!, _neqShallowB_J = null!, _neqDeepA_J = null!, _neqDeepB_J = null!;
    private JsonNode _eqA_N = null!, _eqB_N = null!, _neqShallowA_N = null!, _neqShallowB_N = null!, _neqDeepA_N = null!, _neqDeepB_N = null!;
    private JsonSerializerOptions _stjOpts = null!;

    // -------- Compare-NET-Objects --------
    private CompareLogic _compareLogic = null!;

    [Params(500)] public int Customers;
    [Params(4)] public int LinesPerOrder;
    [Params(3)] public int OrdersPerCustomer;

    [GlobalSetup]
    public void Setup()
    {
        _eqA = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, 11);
        _eqB = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, 11);

        _neqShallowA = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, 22);
        _neqShallowB = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, 22);
        _neqShallowB.Title += "-DIFF";

        _neqDeepA = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, 33);
        _neqDeepB = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, 33);
        var lastC = _neqDeepB.Customers[^1];
        var lastO = lastC.Orders[^1];
        lastO.Lines[^1].Qty += 1;

        _bin = new BinaryDeltaOptions { IncludeHeader = false };

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

        _shallowTarget = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, 22);
        _deepTarget = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, 33);
        _shallowTargetBin = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, 22);
        _deepTargetBin = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, 33);

        _bufShallow = new ArrayBufferWriter<byte>(_deltaShallowBin.Length + 64);
        _bufDeep = new ArrayBufferWriter<byte>(_deltaDeepBin.Length + 64);

        // ---------- Baselines set-up ----------
        _stjOpts = new JsonSerializerOptions { PropertyNamingPolicy = null, WriteIndented = false };

        // Newtonsoft.Json (JToken) — precomputed DOMs for "Equality" group
        _eqA_J = JToken.FromObject(_eqA);
        _eqB_J = JToken.FromObject(_eqB);
        _neqShallowA_J = JToken.FromObject(_neqShallowA);
        _neqShallowB_J = JToken.FromObject(_neqShallowB);
        _neqDeepA_J = JToken.FromObject(_neqDeepA);
        _neqDeepB_J = JToken.FromObject(_neqDeepB);

        // System.Text.Json (JsonNode) — precomputed DOMs for "Equality" group
        _eqA_N = JsonSerializer.SerializeToNode(_eqA, _stjOpts)!;
        _eqB_N = JsonSerializer.SerializeToNode(_eqB, _stjOpts)!;
        _neqShallowA_N = JsonSerializer.SerializeToNode(_neqShallowA, _stjOpts)!;
        _neqShallowB_N = JsonSerializer.SerializeToNode(_neqShallowB, _stjOpts)!;
        _neqDeepA_N = JsonSerializer.SerializeToNode(_neqDeepA, _stjOpts)!;
        _neqDeepB_N = JsonSerializer.SerializeToNode(_neqDeepB, _stjOpts)!;

        // Compare-NET-Objects
        _compareLogic = new CompareLogic(new ComparisonConfig
        {
            CaseSensitive = true,
            IgnoreObjectTypes = false,
            MaxDifferences = 1
        });
    }

    [IterationSetup(Target = nameof(Binary_Apply_Shallow_Delta))]
    public void Reset_ShallowTarget_ForBinaryApply()
    {
        _shallowTargetBin = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, 22);
    }

    [IterationSetup(Target = nameof(Binary_Apply_Deep_Delta))]
    public void Reset_DeepTarget_ForBinaryApply()
    {
        _deepTargetBin = MidGraphFactory.Create(Customers, OrdersPerCustomer, LinesPerOrder, 33);
    }

    [IterationSetup(Targets = new[] { nameof(Binary_Encode_Shallow_Delta_Size) })]
    public void Reset_Shallow_Buffer()
    {
        _bufShallow = new ArrayBufferWriter<byte>(_deltaShallowBin.Length + 64);
    }

    [IterationSetup(Targets = new[] { nameof(Binary_Encode_Deep_Delta_Size) })]
    public void Reset_Deep_Buffer()
    {
        _bufDeep = new ArrayBufferWriter<byte>(_deltaDeepBin.Length + 64);
    }

    // ----------------- Your generated equality (baseline=true) -----------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Equality")]
    public bool Generated_Equal()
        => MidGraphDeepEqual.AreDeepEqual(_eqA, _eqB);

    [Benchmark]
    [BenchmarkCategory("Equality")]
    public bool Generated_NotEqual_Shallow()
        => MidGraphDeepEqual.AreDeepEqual(_neqShallowA, _neqShallowB);

    [Benchmark]
    [BenchmarkCategory("Equality")]
    public bool Generated_NotEqual_Deep()
        => MidGraphDeepEqual.AreDeepEqual(_neqDeepA, _neqDeepB);

    // ----------------- Manual baselines -----------------

    [Benchmark]
    [BenchmarkCategory("Equality")]
    public bool Manual_NonLinq_Equal() => ManualNonLinq.AreEqual(_eqA, _eqB);

    [Benchmark]
    [BenchmarkCategory("Equality")]
    public bool Manual_Linqy_Equal() => ManualLinqy.AreEqual(_eqA, _eqB);

    [Benchmark]
    [BenchmarkCategory("Equality")]
    public bool Manual_NonLinq_NotEqual_Shallow() => ManualNonLinq.AreEqual(_neqShallowA, _neqShallowB);

    [Benchmark]
    [BenchmarkCategory("Equality")]
    public bool Manual_Linqy_NotEqual_Shallow() => ManualLinqy.AreEqual(_neqShallowA, _neqShallowB);

    [Benchmark]
    [BenchmarkCategory("Equality")]
    public bool Manual_NonLinq_NotEqual_Deep() => ManualNonLinq.AreEqual(_neqDeepA, _neqDeepB);

    [Benchmark]
    [BenchmarkCategory("Equality")]
    public bool Manual_Linqy_NotEqual_Deep() => ManualLinqy.AreEqual(_neqDeepA, _neqDeepB);

    // ----------------- Equality only: Newtonsoft JToken.DeepEquals -----------------

    [Benchmark]
    [BenchmarkCategory("Equality")]
    public bool NewtonsoftJToken_Equal() => JToken.DeepEquals(_eqA_J, _eqB_J);

    [Benchmark]
    [BenchmarkCategory("Equality")]
    public bool NewtonsoftJToken_NotEqual_Shallow() => JToken.DeepEquals(_neqShallowA_J, _neqShallowB_J);

    [Benchmark]
    [BenchmarkCategory("Equality")]
    public bool NewtonsoftJToken_NotEqual_Deep() => JToken.DeepEquals(_neqDeepA_J, _neqDeepB_J);

    // ----------------- Equality only: STJ JsonNode.DeepEquals -----------------

    [Benchmark]
    [BenchmarkCategory("Equality")]
    public bool STJ_JsonNode_Equal() => JsonNode.DeepEquals(_eqA_N, _eqB_N);

    [Benchmark]
    [BenchmarkCategory("Equality")]
    public bool STJ_JsonNode_NotEqual_Shallow() => JsonNode.DeepEquals(_neqShallowA_N, _neqShallowB_N);

    [Benchmark]
    [BenchmarkCategory("Equality")]
    public bool STJ_JsonNode_NotEqual_Deep() => JsonNode.DeepEquals(_neqDeepA_N, _neqDeepB_N);

    // ----------------- Compare-NET-Objects -----------------

    [Benchmark]
    [BenchmarkCategory("Equality")]
    public bool CompareNetObjects_Equal() => _compareLogic.Compare(_eqA, _eqB).AreEqual;

    [Benchmark]
    [BenchmarkCategory("Equality")]
    public bool CompareNetObjects_NotEqual_Shallow() => _compareLogic.Compare(_neqShallowA, _neqShallowB).AreEqual;

    [Benchmark]
    [BenchmarkCategory("Equality")]
    public bool CompareNetObjects_NotEqual_Deep() => _compareLogic.Compare(_neqDeepA, _neqDeepB).AreEqual;

    // ================= NEW GROUP: Equality + Serialization =================
    // These benchmarks serialize to a JSON DOM inside each iteration, then DeepEquals.

    // --- Newtonsoft (serialize then compare) ---
    [Benchmark]
    [BenchmarkCategory("Equality+Serialization")]
    public bool NewtonsoftJToken_Equal_WithSerialization()
    {
        var a = JToken.FromObject(_eqA);
        var b = JToken.FromObject(_eqB);
        return JToken.DeepEquals(a, b);
    }

    [Benchmark]
    [BenchmarkCategory("Equality+Serialization")]
    public bool NewtonsoftJToken_NotEqual_Shallow_WithSerialization()
    {
        var a = JToken.FromObject(_neqShallowA);
        var b = JToken.FromObject(_neqShallowB);
        return JToken.DeepEquals(a, b);
    }

    [Benchmark]
    [BenchmarkCategory("Equality+Serialization")]
    public bool NewtonsoftJToken_NotEqual_Deep_WithSerialization()
    {
        var a = JToken.FromObject(_neqDeepA);
        var b = JToken.FromObject(_neqDeepB);
        return JToken.DeepEquals(a, b);
    }

    // --- System.Text.Json (serialize then compare) ---
    [Benchmark]
    [BenchmarkCategory("Equality+Serialization")]
    public bool STJ_JsonNode_Equal_WithSerialization()
    {
        var a = JsonSerializer.SerializeToNode(_eqA, _stjOpts)!;
        var b = JsonSerializer.SerializeToNode(_eqB, _stjOpts)!;
        return JsonNode.DeepEquals(a, b);
    }

    [Benchmark]
    [BenchmarkCategory("Equality+Serialization")]
    public bool STJ_JsonNode_NotEqual_Shallow_WithSerialization()
    {
        var a = JsonSerializer.SerializeToNode(_neqShallowA, _stjOpts)!;
        var b = JsonSerializer.SerializeToNode(_neqShallowB, _stjOpts)!;
        return JsonNode.DeepEquals(a, b);
    }

    [Benchmark]
    [BenchmarkCategory("Equality+Serialization")]
    public bool STJ_JsonNode_NotEqual_Deep_WithSerialization()
    {
        var a = JsonSerializer.SerializeToNode(_neqDeepA, _stjOpts)!;
        var b = JsonSerializer.SerializeToNode(_neqDeepB, _stjOpts)!;
        return JsonNode.DeepEquals(a, b);
    }

    // ----------------- Generated diff & delta (existing) -----------------

    [Benchmark]
    [BenchmarkCategory("Diff")]
    public (bool hasDiff, Diff<MidGraph> diff) Generated_Diff_NoChange_HasDiff()
        => MidGraphDeepOps.GetDiff(_eqA, _eqB);

    [Benchmark]
    [BenchmarkCategory("Diff")]
    public (bool hasDiff, Diff<MidGraph> diff) Generated_Diff_Deep_Change_MemberCount()
        => MidGraphDeepOps.GetDiff(_neqDeepA, _neqDeepB);

    [Benchmark]
    [BenchmarkCategory("Delta")]
    public DeltaDocument Generated_ComputeDelta_Shallow_OpCount()
        => MidGraphDeepOps.ComputeDelta(_neqShallowA, _neqShallowB);

    [Benchmark]
    [BenchmarkCategory("Delta")]
    public DeltaDocument Generated_ComputeDelta_Deep_OpCount()
        => MidGraphDeepOps.ComputeDelta(_neqDeepA, _neqDeepB);

    [Benchmark]
    [BenchmarkCategory("Delta")]
    public bool Apply_InMemory_Shallow_Delta()
    {
        MidGraphDeepOps.ApplyDelta(ref _shallowTarget, _deltaShallow);
        return true;
    }

    [Benchmark]
    [BenchmarkCategory("Delta")]
    public bool Apply_InMemory_Deep_Delta()
    {
        MidGraphDeepOps.ApplyDelta(ref _deepTarget, _deltaDeep);
        return true;
    }

    [Benchmark]
    [BenchmarkCategory("Delta-Binary")]
    public int Binary_Encode_Shallow_Delta_Size()
    {
        BinaryDeltaCodec.Write(_deltaShallow, _bufShallow, _bin);
        return _bufShallow.WrittenCount;
    }

    [Benchmark]
    [BenchmarkCategory("Delta-Binary")]
    public int Binary_Encode_Deep_Delta_Size()
    {
        BinaryDeltaCodec.Write(_deltaDeep, _bufDeep, _bin);
        return _bufDeep.WrittenCount;
    }

    [Benchmark]
    [BenchmarkCategory("Delta-Binary")]
    public DeltaDocument Binary_Decode_Shallow_Delta_OpCount()
        => BinaryDeltaCodec.Read(_deltaShallowBin, _bin);

    [Benchmark]
    [BenchmarkCategory("Delta-Binary")]
    public int Binary_Decode_Deep_Delta_OpCount()
        => BinaryDeltaCodec.Read(_deltaDeepBin, _bin).Operations.Count;

    [Benchmark]
    [BenchmarkCategory("Delta-Binary")]
    public bool Binary_Apply_Shallow_Delta()
    {
        MidGraphDeepOps.ApplyDelta(ref _shallowTargetBin, _deltaShallowDecoded);
        return true;
    }

    [Benchmark]
    [BenchmarkCategory("Delta-Binary")]
    public bool Binary_Apply_Deep_Delta()
    {
        MidGraphDeepOps.ApplyDelta(ref _deepTargetBin, _deltaDeepDecoded);
        return true;
    }

    // ----------------- Optional: cloning group (NOT equality) -----------------

    [Benchmark]
    [BenchmarkCategory("Clone")]
    public MidGraph FastDeepCloner_Clone_EqA()
        => (MidGraph)DeepCloner.Clone(_eqA);

    private class DefaultJobConfig : ManualConfig
    {
        public DefaultJobConfig()
        {
            AddJob(Job.Default
                .WithId("DefaultJob")
                .WithUnrollFactor(16)
                .WithIterationTime(TimeInterval.FromMilliseconds(100)));

            WithOptions(ConfigOptions.DisableOptimizationsValidator);

            // tip: set artifacts path if you want fixed output location
            // WithArtifactsPath(@"C:\Users\mirko\Downloads");
        }
    }
}
