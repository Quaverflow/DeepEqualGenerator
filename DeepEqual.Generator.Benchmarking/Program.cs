using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using DeepEqual.Generator.Shared;
using KellermanSoftware.CompareNetObjects;
using System.Dynamic;

namespace DeepEqual.Generator.Benchmarking;

internal class Program
{
    static void Main()
    {
        _ = BenchmarkRunner.Run<DeepGraphBenchmarks>();
    }
}

// -------------------------------------------
// EverythingBagel: a “kitchen sink” model
// -------------------------------------------

public enum TinyEnum { None, A, B, C }

public struct MiniPoint
{
    public int X;
    public int Y;
}

[DeepComparable]
public sealed class Leaf
{
    public string Name { get; set; } = "";
    public int Score { get; set; }
}

[DeepComparable]
public sealed class EverythingBagel
{
    // Primitives
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

    // Nullable primitives/enums/structs
    public int? NI32 { get; set; }
    public TinyEnum? NEnum { get; set; }
    public MiniPoint? NPoint { get; set; }

    // Enums / Structs
    public TinyEnum E { get; set; }
    public MiniPoint P { get; set; }

    // Time / Guid
    public DateTime When { get; set; }
    public DateTimeOffset WhenOff { get; set; }
    public TimeSpan HowLong { get; set; }
#if NET6_0_OR_GREATER
    public DateOnly Day { get; set; }
    public TimeOnly Clock { get; set; }
#endif
    public Guid Id { get; set; }

    // Memory blocks
    public Memory<byte> Blob { get; set; }
    public ReadOnlyMemory<byte> RBlob { get; set; }

    // Arrays
    public int[]? Numbers { get; set; }
    public string[]? Words { get; set; }
    public int[][]? Jagged { get; set; }                // array of arrays
    public int[,]? Rect { get; set; }                   // small rectangular

    // Collections (ordered)
    public List<int>? LInts { get; set; }
    public IReadOnlyList<string>? RListStrings { get; set; }

    // Collections (unordered)
    [DeepCompare(OrderInsensitive = true)]
    public HashSet<string>? Tags { get; set; }

    // Dictionaries
    public Dictionary<string, int>? ByName { get; set; }
    public IReadOnlyDictionary<string, Leaf>? ByKey { get; set; }

    // Nested class graphs
    public Leaf? Left { get; set; }
    public Leaf? Right { get; set; }

    // Tuple / KVP
    public (int, string) Pair { get; set; }
    public KeyValuePair<string, int> Kvp { get; set; }

    // Object / dynamic
    public object? Boxed { get; set; }
    public IDictionary<string, object?> Dyn { get; set; } = new ExpandoObject();

    // Reference-kind example (if you want to force reference equality):
    [DeepCompare(Kind = CompareKind.Reference)]
    public byte[]? RefBlob { get; set; }                // reference-only
}

public static class EverythingFactory
{
    public static EverythingBagel Create(int seed, bool mutateShallow = false, bool mutateDeep = false)
    {
        var rng = new Random(seed);

        var e = new EverythingBagel
        {
            // primitives
            B = (seed & 1) == 0,
            U8 = (byte)(seed % 256),
            I8 = (sbyte)((seed % 200) - 100),
            I16 = (short)(seed % 1000),
            U16 = (ushort)(seed % 1000),
            I32 = seed * 31,
            U32 = (uint)(seed * 31),
            I64 = (long)seed * 1_000_003L,
            U64 = (ulong)(seed * 1_000_003L),
            F32 = (float)(seed * 0.123),
            F64 = seed * 0.123456789,
            M128 = (decimal)(seed * 0.987654321m),
            C = (char)('A' + (seed % 26)),
            S = $"S-{seed:000000}",

            // nullable
            NI32 = (seed % 3 == 0) ? null : seed * 17,
            NEnum = (seed % 4 == 0) ? null : TinyEnum.B,
            NPoint = (seed % 5 == 0) ? null : new MiniPoint { X = seed, Y = seed * 2 },

            // enums / struct
            E = (TinyEnum)(seed % 4),
            P = new MiniPoint { X = seed % 100, Y = (seed % 100) * 2 },

            // time / guid
            When = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddMinutes(seed % 500),
            WhenOff = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero).AddMinutes(seed % 500),
            HowLong = TimeSpan.FromSeconds(seed % 10_000),
#if NET6_0_OR_GREATER
            Day = new DateOnly(2025, 1, 1).AddDays(seed % 365),
            Clock = new TimeOnly((seed % 24), (seed % 60), (seed % 60)),
#endif
            Id = DeterministicGuid($"E-{seed}"),

            // memory
            Blob = new Memory<byte>(MakeBytes(seed, 64)),
            RBlob = new ReadOnlyMemory<byte>(MakeBytes(seed + 1, 64)),

            // arrays
            Numbers = Enumerable.Range(0, 32).Select(i => (i + seed) % 1000).ToArray(),
            Words = new[] { "alpha", "beta", $"w-{seed}" },
            Jagged = new[]
            {
                new[] { 1, 2, 3 },
                new[] { 5 + seed%5, 6 + seed%7 }
            },
            Rect = new int[,]
            {
                { 1, 2, 3 },
                { 4, 5, (6 + seed % 3) }
            },

            // collections
            LInts = Enumerable.Range(0, 64).Select(i => i * 3 + seed).ToList(),
            RListStrings = Enumerable.Range(0, 16).Select(i => $"s{i + seed}").ToList(),

            // unordered set
            Tags = new HashSet<string>(new[] { "x", "y", $"t-{seed}" }, StringComparer.Ordinal),

            // dictionaries
            ByName = Enumerable.Range(0, 16).ToDictionary(i => $"k{i}", i => i + seed, StringComparer.Ordinal),
            ByKey = Enumerable.Range(0, 8).ToDictionary(i => $"id{i}", i => new Leaf { Name = $"L{i}", Score = i + seed }),

            // nested leaves
            Left = new Leaf { Name = "left", Score = 10 + (seed % 5) },
            Right = new Leaf { Name = "right", Score = 20 + (seed % 5) },

            // tuple / kvp
            Pair = (seed % 100, $"pair-{seed}"),
            Kvp = new KeyValuePair<string, int>($"kvp-{seed}", seed % 123),

            // object/dynamic
            Boxed = (seed % 2 == 0) ? (object)($"box-{seed}") : (object)(seed % 999),
            Dyn = MakeExpando(seed),

            // reference-only
            RefBlob = (seed % 2 == 0) ? new byte[] { 1, 2, 3 } : new byte[] { 1, 2, 3 } // same content, different reference
        };

        if (mutateShallow)
        {
            e.S = $"DIFF-{seed}";
        }

        if (mutateDeep)
        {
            // deep change: mutate nested leaf and dictionary value
            e.Right!.Score += 1;
            e.ByName!["k7"] += 1;
        }

        return e;

        static byte[] MakeBytes(int s, int n)
        {
            var rng = new Random(s);
            var arr = new byte[n];
            rng.NextBytes(arr);
            return arr;
        }

        static Guid DeterministicGuid(string s)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(s);
            Span<byte> g = stackalloc byte[16];
            for (int i = 0; i < 16; i++) g[i] = (byte)(bytes[i % bytes.Length] + i * 31);
            return new Guid(g);
        }

        static IDictionary<string, object?> MakeExpando(int seed)
        {
            dynamic eo = new ExpandoObject();
            eo.id = seed;
            eo.name = $"dyn-{seed}";
            eo.arr = new[] { 1, 2, 3 + (seed % 3) };
            eo.map = new Dictionary<string, object?>
            {
                ["a"] = 1,
                ["b"] = new[] { "p", "q" },
                ["c"] = new Dictionary<string, object?> { ["z"] = seed % 10 }
            };
            return eo;
        }
    }
}

// -------------------------------------------
// Your existing BigGraph model & benchmarks
// -------------------------------------------

public enum Role { None, Dev, Lead, Manager }

[DeepComparable]
public partial class BigGraph
{
    public string Title { get; set; } = "";
    public OrgNode Org { get; set; } = new();
    public Dictionary<string, OrgNode> OrgIndex { get; set; } = new(StringComparer.Ordinal);
    public List<Product> Catalog { get; set; } = new();
    public List<Customer> Customers { get; set; } = new();
    public IDictionary<string, object?> Meta { get; set; } = new ExpandoObject();
}

public class OrgNode
{
    public string Name { get; set; } = "";
    public Role Role { get; set; }
    public List<OrgNode> Reports { get; set; } = new();
    public IDictionary<string, object?> Extra { get; set; } = new ExpandoObject();
}

public class Product
{
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public DateTime Introduced { get; set; }
    public IDictionary<string, object?> Attributes { get; set; } = new ExpandoObject();
}

public class OrderLine
{
    public string Sku { get; set; } = "";
    public int Qty { get; set; }
    public decimal LineTotal { get; set; }
}

public class Order
{
    public Guid Id { get; set; }
    public DateTimeOffset Created { get; set; }
    public List<OrderLine> Lines { get; set; } = new();
    public Dictionary<string, string> Meta { get; set; } = new(StringComparer.Ordinal);
    public IDictionary<string, object?> Extra { get; set; } = new ExpandoObject();
}

public class Customer
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = "";
    public List<Order> Orders { get; set; } = new();
    public IDictionary<string, object?> Profile { get; set; } = new ExpandoObject();
}

public static class BigGraphFactory
{
    public static BigGraph Create(
        int orgBreadth,
        int orgDepth,
        int products,
        int customers,
        int ordersPerCustomer,
        int linesPerOrder,
        int seed = 123)
    {
        var rng = new Random(seed);

        var root = new OrgNode { Name = "CEO", Role = Role.Manager };
        BuildOrg(root, orgBreadth, orgDepth, 0, rng);

        var index = new Dictionary<string, OrgNode>(StringComparer.Ordinal);
        IndexOrg(root, index);

        var catalog = new List<Product>(products);
        for (int i = 0; i < products; i++)
        {
            var p = new Product
            {
                Sku = $"SKU-{i:D6}",
                Name = $"Product {i}",
                Price = 10 + i,
                Introduced = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i),
                Attributes = MakeExpando(rng, $"P{i}", depth: 2)
            };
            catalog.Add(p);
        }

        var custs = new List<Customer>(customers);
        for (int c = 0; c < customers; c++)
        {
            var cust = new Customer
            {
                Id = DeterministicGuid($"C{c}"),
                FullName = $"Customer {c}",
                Profile = MakeExpando(rng, $"C{c}", depth: 2)
            };

            for (int o = 0; o < ordersPerCustomer; o++)
            {
                var order = new Order
                {
                    Id = DeterministicGuid($"C{c}-O{o}"),
                    Created = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero).AddMinutes(o),
                    Meta = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["channel"] = (o % 2 == 0) ? "web" : "app"
                    },
                    Extra = MakeExpando(rng, $"C{c}-O{o}", depth: 1)
                };

                for (int l = 0; l < linesPerOrder; l++)
                {
                    var prod = catalog[(l + o) % catalog.Count];
                    order.Lines.Add(new OrderLine
                    {
                        Sku = prod.Sku,
                        Qty = 1 + (l % 3),
                        LineTotal = prod.Price * (1 + (l % 3))
                    });
                }

                cust.Orders.Add(order);
            }

            custs.Add(cust);
        }

        var graph = new BigGraph
        {
            Title = "BigGraph Bench",
            Org = root,
            OrgIndex = index,
            Catalog = catalog,
            Customers = custs,
            Meta = MakeExpando(rng, "ROOT", depth: 2)
        };

        FillOrgExpandos(graph.Org, rng);
        return graph;
    }

    private static void BuildOrg(OrgNode parent, int breadth, int maxDepth, int depth, Random rng)
    {
        if (depth >= maxDepth) return;
        for (int i = 0; i < breadth; i++)
        {
            var n = new OrgNode { Name = $"{parent.Name}-{depth}-{i}", Role = (Role)(i % 3) };
            parent.Reports.Add(n);
            BuildOrg(n, breadth, maxDepth, depth + 1, rng);
        }
    }

    private static void IndexOrg(OrgNode node, Dictionary<string, OrgNode> index)
    {
        index[node.Name] = node;
        foreach (var r in node.Reports) IndexOrg(r, index);
    }

    private static void FillOrgExpandos(OrgNode node, Random rng)
    {
        node.Extra = MakeExpando(rng, node.Name, depth: 1);
        foreach (var r in node.Reports) FillOrgExpandos(r, rng);
    }

    private static IDictionary<string, object?> MakeExpando(Random rng, string id, int depth)
    {
        var exp = new ExpandoObject();
        var d = (IDictionary<string, object?>)exp;

        d["id"] = id;
        d["flag"] = (id.GetHashCode() & 1) == 0;
        d["nums"] = new[] { 1, 2, (3 + id.Length % 3) };
        d["tags"] = new[] { "alpha", "beta", id };
        d["map"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["x"] = new[] { 1, 2, 3 },
            ["y"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["z"] = id.Length,
                ["u"] = new[] { "p", "q" }
            }
        };

        if (depth > 0) d["child"] = MakeExpando(rng, id + "-" + depth.ToString(), depth - 1);
        return d;
    }

    private static Guid DeterministicGuid(string s)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(s);
        Span<byte> g = stackalloc byte[16];
        for (int i = 0; i < 16; i++) g[i] = (byte)(bytes[i % bytes.Length] + i * 31);
        return new Guid(g);
    }
}

// -------------------------------------------
// Benchmarks
// -------------------------------------------

[MemoryDiagnoser]
[PlainExporter, RPlotExporter]
public class DeepGraphBenchmarks
{
    // BigGraph params
    [Params(3)] public int OrgBreadth;
    [Params(3)] public int OrgDepth;
    [Params(2000)] public int Products;
    [Params(2000)] public int Customers;
    [Params(3)] public int OrdersPerCustomer;
    [Params(4)] public int LinesPerOrder;

    // EverythingBagel workload sizes (small to keep runtime reasonable)
    [Params(2048)] public int EB_Count; // number of small leaves/collection sizes inside bagel

    private BigGraph _eqA = null!;
    private BigGraph _eqB = null!;
    private BigGraph _neqShallowA = null!;
    private BigGraph _neqShallowB = null!;
    private BigGraph _neqDeepA = null!;
    private BigGraph _neqDeepB = null!;

    private EverythingBagel _ebEqA = null!;
    private EverythingBagel _ebEqB = null!;
    private EverythingBagel _ebNeqShallowA = null!;
    private EverythingBagel _ebNeqShallowB = null!;
    private EverythingBagel _ebNeqDeepA = null!;
    private EverythingBagel _ebNeqDeepB = null!;

    private CompareLogic _cno = null!;

    [GlobalSetup]
    public void Setup()
    {
        // BigGraph
        _eqA = BigGraphFactory.Create(OrgBreadth, OrgDepth, Products, Customers, OrdersPerCustomer, LinesPerOrder, seed: 1);
        _eqB = BigGraphFactory.Create(OrgBreadth, OrgDepth, Products, Customers, OrdersPerCustomer, LinesPerOrder, seed: 1);

        _neqShallowA = BigGraphFactory.Create(OrgBreadth, OrgDepth, Products, Customers, OrdersPerCustomer, LinesPerOrder, seed: 2);
        _neqShallowB = BigGraphFactory.Create(OrgBreadth, OrgDepth, Products, Customers, OrdersPerCustomer, LinesPerOrder, seed: 2);
        _neqShallowB.Title = "DIFFERENT";

        _neqDeepA = BigGraphFactory.Create(OrgBreadth, OrgDepth, Products, Customers, OrdersPerCustomer, LinesPerOrder, seed: 3);
        _neqDeepB = BigGraphFactory.Create(OrgBreadth, OrgDepth, Products, Customers, OrdersPerCustomer, LinesPerOrder, seed: 3);
        var c = _neqDeepB.Customers[^1];
        var o = c.Orders[^1];
        o.Lines[^1].Qty += 1;

        // EverythingBagel
        _ebEqA = EverythingFactory.Create(seed: 100);
        _ebEqB = EverythingFactory.Create(seed: 100);

        _ebNeqShallowA = EverythingFactory.Create(seed: 200, mutateShallow: false);
        _ebNeqShallowB = EverythingFactory.Create(seed: 200, mutateShallow: true);  // shallow change: string

        _ebNeqDeepA = EverythingFactory.Create(seed: 300, mutateDeep: false);
        _ebNeqDeepB = EverythingFactory.Create(seed: 300, mutateDeep: true);        // deep change: nested + dict

        _cno = new CompareLogic(new ComparisonConfig
        {
            Caching = true,
            MaxDifferences = 1,
            ComparePrivateFields = false,
            ComparePrivateProperties = false,
            IgnoreObjectTypes = false,
            TreatStringEmptyAndNullTheSame = false,
            IgnoreCollectionOrder = false
        });
    }

    // ----- Existing BigGraph benchmarks -----

    [Benchmark(Baseline = true)]
    public bool Generated_Equal() =>
        BigGraphDeepEqual.AreDeepEqual(_eqA, _eqB);

    [Benchmark]
    public bool CNO_Equal() =>
        _cno.Compare(_eqA, _eqB).AreEqual;

    [Benchmark]
    public bool Generated_NotEqual_Shallow() =>
        BigGraphDeepEqual.AreDeepEqual(_neqShallowA, _neqShallowB);

    [Benchmark]
    public bool CNO_NotEqual_Shallow() =>
        _cno.Compare(_neqShallowA, _neqShallowB).AreEqual;

    [Benchmark]
    public bool Generated_NotEqual_Deep() =>
        BigGraphDeepEqual.AreDeepEqual(_neqDeepA, _neqDeepB);

    [Benchmark]
    public bool CNO_NotEqual_Deep() =>
        _cno.Compare(_neqDeepA, _neqDeepB).AreEqual;

    // ----- EverythingBagel “kitchen sink” -----

    [Benchmark]
    public bool EB_Generated_Equal() =>
        EverythingBagelDeepEqual.AreDeepEqual(_ebEqA, _ebEqB);

    [Benchmark]
    public bool EB_CNO_Equal() =>
        _cno.Compare(_ebEqA, _ebEqB).AreEqual;

    [Benchmark]
    public bool EB_Generated_NotEqual_Shallow() =>
        EverythingBagelDeepEqual.AreDeepEqual(_ebNeqShallowA, _ebNeqShallowB);

    [Benchmark]
    public bool EB_Generated_NotEqual_Deep() =>
        EverythingBagelDeepEqual.AreDeepEqual(_ebNeqDeepA, _ebNeqDeepB);

    [Benchmark]
    public bool EB_CNO_NotEqual_Deep() =>
        _cno.Compare(_ebNeqDeepA, _ebNeqDeepB).AreEqual;
}
