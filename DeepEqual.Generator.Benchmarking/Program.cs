using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using DeepEqual.Generator.Shared;
using System.Dynamic;

namespace DeepEqual.Generator.Benchmarking;

internal class Program
{
    static void Main()
    {
        _ = BenchmarkRunner.Run<MegaBenchmarks>();
    }
}

public enum TinyEnum { None, A, B, C }

public struct MiniPoint { public int X; public int Y; }

public sealed class Leaf
{
    public string Name { get; set; } = "";
    public int Score { get; set; }
}

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

public static class EverythingFactory
{
    public static EverythingBagel Create(int seed, bool mutateShallow = false, bool mutateDeep = false)
    {
        var e = new EverythingBagel
        {
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
            NI32 = (seed % 3 == 0) ? null : seed * 17,
            NEnum = (seed % 4 == 0) ? null : TinyEnum.B,
            NPoint = (seed % 5 == 0) ? null : new MiniPoint { X = seed, Y = seed * 2 },
            E = (TinyEnum)(seed % 4),
            P = new MiniPoint { X = seed % 100, Y = (seed % 100) * 2 },
            When = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddMinutes(seed % 500),
            WhenOff = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero).AddMinutes(seed % 500),
            HowLong = TimeSpan.FromSeconds(seed % 10_000),
#if NET6_0_OR_GREATER
            Day = new DateOnly(2025, 1, 1).AddDays(seed % 365),
            Clock = new TimeOnly((seed % 24), (seed % 60), (seed % 60)),
#endif
            Id = DeterministicGuid($"E-{seed}"),
            Blob = new Memory<byte>(MakeBytes(seed, 64)),
            RBlob = new ReadOnlyMemory<byte>(MakeBytes(seed + 1, 64)),
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
            LInts = Enumerable.Range(0, 64).Select(i => i * 3 + seed).ToList(),
            RListStrings = Enumerable.Range(0, 16).Select(i => $"s{i + seed}").ToList(),
            Tags = new HashSet<string>(new[] { "x", "y", $"t-{seed}" }, StringComparer.Ordinal),
            ByName = Enumerable.Range(0, 16).ToDictionary(i => $"k{i}", i => i + seed, StringComparer.Ordinal),
            ByKey = Enumerable.Range(0, 8).ToDictionary(i => $"id{i}", i => new Leaf { Name = $"L{i}", Score = i + seed }),
            Left = new Leaf { Name = "left", Score = 10 + (seed % 5) },
            Right = new Leaf { Name = "right", Score = 20 + (seed % 5) },
            Pair = (seed % 100, $"pair-{seed}"),
            Kvp = new KeyValuePair<string, int>($"kvp-{seed}", seed % 123),
            Boxed = (seed % 2 == 0) ? (object)($"box-{seed}") : (object)(seed % 999),
            Dyn = MakeExpando(seed),
            RefBlob = (seed % 2 == 0) ? new byte[] { 1, 2, 3 } : new byte[] { 1, 2, 3 }
        };

        if (mutateShallow) e.S = $"DIFF-{seed}";
        if (mutateDeep)
        {
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

public enum Role { None, Dev, Lead, Manager }

[DeepComparable(CycleTracking = false)]
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

        static void BuildOrg(OrgNode parent, int breadth, int maxDepth, int depth, Random rng)
        {
            if (depth >= maxDepth) return;
            for (int i = 0; i < breadth; i++)
            {
                var n = new OrgNode { Name = $"{parent.Name}-{depth}-{i}", Role = (Role)(i % 3) };
                parent.Reports.Add(n);
                BuildOrg(n, breadth, maxDepth, depth + 1, rng);
            }
        }

        static void IndexOrg(OrgNode node, Dictionary<string, OrgNode> index)
        {
            index[node.Name] = node;
            foreach (var r in node.Reports) IndexOrg(r, index);
        }

        static void FillOrgExpandos(OrgNode node, Random rng)
        {
            node.Extra = MakeExpando(rng, node.Name, depth: 1);
            foreach (var r in node.Reports) FillOrgExpandos(r, rng);
        }

        static IDictionary<string, object?> MakeExpando(Random rng, string id, int depth)
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

        static Guid DeterministicGuid(string s)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(s);
            Span<byte> g = stackalloc byte[16];
            for (int i = 0; i < 16; i++) g[i] = (byte)(bytes[i % bytes.Length] + i * 31);
            return new Guid(g);
        }
    }
}

static class ManualValueComparer
{
    public static bool AreEqual(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.GetType() != b.GetType()) return false;

        switch (a)
        {
            case string sa: return sa == (string)b!;
            case bool va: return va == (bool)b!;
            case char ca: return ca == (char)b!;
            case sbyte sb: return sb == (sbyte)b!;
            case byte by: return by == (byte)b!;
            case short sh: return sh == (short)b!;
            case ushort ush: return ush == (ushort)b!;
            case int ia: return ia == (int)b!;
            case uint uia: return uia == (uint)b!;
            case long la: return la == (long)b!;
            case ulong ula: return ula == (ulong)b!;
            case float fa: return fa == (float)b!;
            case double da: return da == (double)b!;
            case decimal de: return de == (decimal)b!;
            case Guid ga: return ga == (Guid)b!;
            case DateTime dt: return dt == (DateTime)b!;
            case DateTimeOffset dto: return dto == (DateTimeOffset)b!;
            case TimeSpan tsp: return tsp == (TimeSpan)b!;
#if NET6_0_OR_GREATER
            case DateOnly don: return don == (DateOnly)b!;
            case TimeOnly ton: return ton == (TimeOnly)b!;
#endif
            case TinyEnum ee: return ee == (TinyEnum)b!;
            case MiniPoint mp: return ((MiniPoint)b!).X == mp.X && ((MiniPoint)b!).Y == mp.Y;
            case byte[] arrA: return ReferenceEquals(arrA, b);
            case Array aa: return ArrayEqual(aa, (Array)b!);
            case IDictionary<string, object?> da1: return DictObjectEqual(da1, (IDictionary<string, object?>)b!);
            case Leaf la1: return LeafEqual(la1, (Leaf)b!);
            default: return a.Equals(b);
        }
    }

    private static bool ArrayEqual(Array a, Array b)
    {
        if (a.Rank != b.Rank) return false;
        for (int d = 0; d < a.Rank; d++)
            if (a.GetLength(d) != b.GetLength(d)) return false;

        if (a is int[] ai && b is int[] bi) return SequenceEqual(ai, bi);
        if (a is string[] as1 && b is string[] bs1) return SequenceEqual(as1, bs1);
        if (a is int[][] aj && b is int[][] bj)
        {
            if (aj.Length != bj.Length) return false;
            for (int i = 0; i < aj.Length; i++)
                if (!SequenceEqual(aj[i], bj[i])) return false;
            return true;
        }

        var idx = new int[a.Rank];
        return WalkRect(a, b, 0, idx);

        static bool WalkRect(Array a, Array b, int dim, int[] idx)
        {
            if (dim == a.Rank)
            {
                var va = a.GetValue(idx);
                var vb = b.GetValue(idx);
                return Equals(va, vb);
            }
            for (int i = 0; i < a.GetLength(dim); i++)
            {
                idx[dim] = i;
                if (!WalkRect(a, b, dim + 1, idx)) return false;
            }
            return true;
        }

        static bool SequenceEqual<T>(T[] x, T[] y)
        {
            if (x.Length != y.Length) return false;
            for (int i = 0; i < x.Length; i++)
                if (!Equals(x[i], y[i])) return false;
            return true;
        }
    }

    private static bool DictObjectEqual(IDictionary<string, object?> a, IDictionary<string, object?> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var kv in a)
        {
            if (!b.TryGetValue(kv.Key, out var bv)) return false;
            if (!AreEqual(kv.Value, bv)) return false;
        }
        return true;
    }

    public static bool LeafEqual(Leaf? a, Leaf? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        return a.Name == b.Name && a.Score == b.Score;
    }
}

static class ManualEverythingComparer
{
    public static bool AreEqual(EverythingBagel? a, EverythingBagel? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;

        if (a.B != b.B || a.U8 != b.U8 || a.I8 != b.I8 || a.I16 != b.I16 || a.U16 != b.U16 ||
            a.I32 != b.I32 || a.U32 != b.U32 || a.I64 != b.I64 || a.U64 != b.U64 ||
            a.F32 != b.F32 || a.F64 != b.F64 || a.M128 != b.M128 || a.C != b.C ||
            a.S != b.S) return false;

        if (a.NI32 != b.NI32 || a.NEnum != b.NEnum ||
            !(a.NPoint is null ? b.NPoint is null :
              (b.NPoint is not null && a.NPoint.Value.X == b.NPoint.Value.X && a.NPoint.Value.Y == b.NPoint.Value.Y)))
            return false;

        if (a.E != b.E || a.P.X != b.P.X || a.P.Y != b.P.Y) return false;

        if (a.When != b.When || a.WhenOff != b.WhenOff || a.HowLong != b.HowLong) return false;
#if NET6_0_OR_GREATER
        if (a.Day != b.Day || a.Clock != b.Clock) return false;
#endif
        if (a.Id != b.Id) return false;

        if (!a.Blob.Span.SequenceEqual(b.Blob.Span)) return false;
        if (!a.RBlob.Span.SequenceEqual(b.RBlob.Span)) return false;

        if (!ArrayEqual(a.Numbers, b.Numbers)) return false;
        if (!ArrayEqual(a.Words, b.Words)) return false;
        if (!JaggedEqual(a.Jagged, b.Jagged)) return false;
        if (!RectEqual(a.Rect, b.Rect)) return false;

        if (!ListEqual(a.LInts, b.LInts)) return false;
        if (!ListEqual(a.RListStrings, b.RListStrings)) return false;

        if (!SetEqual(a.Tags, b.Tags)) return false;

        if (!DictEqual(a.ByName, b.ByName)) return false;
        if (!DictEqualLeaf(a.ByKey, b.ByKey)) return false;

        if (!ManualValueComparer.LeafEqual(a.Left, b.Left)) return false;
        if (!ManualValueComparer.LeafEqual(a.Right, b.Right)) return false;

        if (a.Pair != b.Pair) return false;
        if (a.Kvp.Key != b.Kvp.Key || a.Kvp.Value != b.Kvp.Value) return false;

        if (!ManualValueComparer.AreEqual(a.Boxed, b.Boxed)) return false;
        if (!DictObjEqual(a.Dyn, b.Dyn)) return false;
        if (!ReferenceEquals(a.RefBlob, b.RefBlob)) return false;

        return true;

        static bool ArrayEqual<T>(T[]? x, T[]? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            if (x.Length != y.Length) return false;
            for (int i = 0; i < x.Length; i++) if (!Equals(x[i], y[i])) return false;
            return true;
        }

        static bool JaggedEqual<T>(T[][]? a, T[][]? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (!ArrayEqual(a[i], b[i])) return false;
            return true;
        }

        static bool RectEqual<T>(T[,]? a, T[,]? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            if (a.GetLength(0) != b.GetLength(0) || a.GetLength(1) != b.GetLength(1)) return false;
            for (int i = 0; i < a.GetLength(0); i++)
                for (int j = 0; j < a.GetLength(1); j++)
                    if (!Equals(a[i, j], b[i, j])) return false;
            return true;
        }

        static bool ListEqual<T>(IReadOnlyList<T>? a, IReadOnlyList<T>? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!Equals(a[i], b[i])) return false;
            return true;
        }

        static bool SetEqual(HashSet<string>? a, HashSet<string>? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            return a.SetEquals(b);
        }

        static bool DictEqual(Dictionary<string, int>? a, Dictionary<string, int>? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            if (a.Count != b.Count) return false;
            foreach (var (k, v) in a)
                if (!b.TryGetValue(k, out var bv) || v != bv) return false;
            return true;
        }

        static bool DictEqualLeaf(IReadOnlyDictionary<string, Leaf>? a, IReadOnlyDictionary<string, Leaf>? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            if (a.Count != b.Count) return false;
            foreach (var (k, v) in a)
                if (!b.TryGetValue(k, out var bv) || !ManualValueComparer.LeafEqual(v, bv)) return false;
            return true;
        }

        static bool DictObjEqual(IDictionary<string, object?> a, IDictionary<string, object?> b)
            => ManualValueComparer.AreEqual(a, b);
    }
}

static class ManualBigGraphComparer
{
    public static bool AreEqual(BigGraph? a, BigGraph? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (!string.Equals(a.Title, b.Title, StringComparison.Ordinal)) return false;
        if (!OrgEqual(a.Org, b.Org)) return false;
        if (!ListEqual(a.Catalog, b.Catalog, ProductEqual)) return false;
        if (!ListEqual(a.Customers, b.Customers, CustomerEqual)) return false;
        if (!DictOrgEqual(a.OrgIndex, b.OrgIndex)) return false;
        if (!ManualValueComparer.AreEqual(a.Meta, b.Meta)) return false;
        return true;

        static bool OrgEqual(OrgNode? a, OrgNode? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            if (!string.Equals(a.Name, b.Name, StringComparison.Ordinal)) return false;
            if (a.Role != b.Role) return false;
            if (!ListEqual(a.Reports, b.Reports, OrgEqual)) return false;
            if (!ManualValueComparer.AreEqual(a.Extra, b.Extra)) return false;
            return true;
        }

        static bool ProductEqual(Product? a, Product? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            if (a.Sku != b.Sku || a.Name != b.Name) return false;
            if (a.Price != b.Price || a.Introduced != b.Introduced) return false;
            if (!ManualValueComparer.AreEqual(a.Attributes, b.Attributes)) return false;
            return true;
        }

        static bool OrderLineEqual(OrderLine? a, OrderLine? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            return a.Sku == b.Sku && a.Qty == b.Qty && a.LineTotal == b.LineTotal;
        }

        static bool OrderEqual(Order? a, Order? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            if (a.Id != b.Id || a.Created != b.Created) return false;
            if (!ListEqual(a.Lines, b.Lines, OrderLineEqual)) return false;
            if (!DictEqual(a.Meta, b.Meta)) return false;
            if (!ManualValueComparer.AreEqual(a.Extra, b.Extra)) return false;
            return true;
        }

        static bool CustomerEqual(Customer? a, Customer? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            if (a.Id != b.Id || a.FullName != b.FullName) return false;
            if (!ListEqual(a.Orders, b.Orders, OrderEqual)) return false;
            if (!ManualValueComparer.AreEqual(a.Profile, b.Profile)) return false;
            return true;
        }

        static bool DictEqual(Dictionary<string, string>? a, Dictionary<string, string>? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            if (a.Count != b.Count) return false;
            foreach (var (k, v) in a)
                if (!b.TryGetValue(k, out var bv) || !string.Equals(v, bv, StringComparison.Ordinal)) return false;
            return true;
        }

        static bool DictOrgEqual(Dictionary<string, OrgNode>? a, Dictionary<string, OrgNode>? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            if (a.Count != b.Count) return false;
            foreach (var (k, v) in a)
                if (!b.TryGetValue(k, out var bv) || !OrgEqual(v, bv)) return false;
            return true;
        }

        static bool ListEqual<T>(List<T>? a, List<T>? b, Func<T?, T?, bool> eq)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!eq(a[i], b[i])) return false;
            return true;
        }
    }
}

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

public static class MegaFactory
{
    public static MegaRoot Create(int orgBreadth, int orgDepth, int products, int customers, int ordersPerCustomer, int linesPerOrder, int bagelsCount, int seed)
    {
        var rng = new Random(seed);
        var mr = new MegaRoot
        {
            Title = $"Mega-{seed}",
            Bagel = EverythingFactory.Create(seed),
            Graph = BigGraphFactory.Create(orgBreadth, orgDepth, products, customers, ordersPerCustomer, linesPerOrder, seed),
            Jaggy = Enumerable.Range(0, 8).Select(i => Enumerable.Range(0, i + 4).Select(j => (i + j + seed) % 100).ToArray()).ToArray(),
            Data = new Memory<byte>(MakeBytes(seed ^ 0xABC, 256)),
            RData = new ReadOnlyMemory<byte>(MakeBytes(seed ^ 0xDEF, 256)),
            Polymorph = (seed % 2 == 0) ? (object)new Leaf { Name = $"poly-{seed}", Score = 7 + seed % 5 } : (object)$"poly-s-{seed}",
            Mixed = new List<object> { "m1", 123, new Leaf { Name = "mleaf", Score = 9 }, new[] { 1, 2, 3 }, MakeExpando(seed) },
            ForcedOrdered = Enumerable.Range(0, 32).Select(i => i + seed).ToList()
        };

        mr.Bagels = new List<EverythingBagel>(bagelsCount);
        for (int i = 0; i < bagelsCount; i++)
            mr.Bagels.Add(EverythingFactory.Create(seed + i));

        mr.BagelIndex = mr.Bagels.Select((b, i) => (b, i)).ToDictionary(t => $"bk{t.i:000}", t => t.b, StringComparer.Ordinal);

        mr.Meta = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["a"] = new[] { "x", "y", "z" },
            ["b"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["i"] = 1,
                ["list"] = new[] { 3, 4, 5 }
            }
        };

        mr.Expando = MakeExpando(seed + 999);
        return mr;

        static IDictionary<string, object?> MakeExpando(int s)
        {
            dynamic eo = new ExpandoObject();
            eo.k = $"k-{s}";
            eo.arr = new[] { 1, 2, 3 + (s % 3) };
            eo.map = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["x"] = s % 10,
                ["y"] = new[] { "p", "q" },
                ["z"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["u"] = $"v-{s}" }
            };
            return eo;
        }

        static byte[] MakeBytes(int s, int n)
        {
            var rng2 = new Random(s);
            var arr = new byte[n];
            rng2.NextBytes(arr);
            return arr;
        }
    }

    public static void MutateShallow(MegaRoot r) => r.Title = r.Title + "-diff";

    public static void MutateDeep(MegaRoot r)
    {
        r.Bagel.Right!.Score += 1;
        r.Graph.Customers[^1].Orders[^1].Lines[^1].Qty += 1;
        r.Bagels[^1].ByName!["k7"] += 1;
        r.ForcedOrdered[^1] += 1;
        ((IDictionary<string, object?>)r.Expando)["k"] = r.Title;
    }
}

static class ManualMegaComparer
{
    public static bool AreEqual(MegaRoot? a, MegaRoot? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (!string.Equals(a.Title, b.Title, StringComparison.Ordinal)) return false;
        if (!ManualEverythingComparer.AreEqual(a.Bagel, b.Bagel)) return false;
        if (!ManualBigGraphComparer.AreEqual(a.Graph, b.Graph)) return false;

        if (a.Bagels.Count != b.Bagels.Count) return false;
        var matched = new bool[b.Bagels.Count];
        for (int i = 0; i < a.Bagels.Count; i++)
        {
            bool ok = false;
            for (int j = 0; j < b.Bagels.Count; j++)
            {
                if (matched[j]) continue;
                if (ManualEverythingComparer.AreEqual(a.Bagels[i], b.Bagels[j])) { matched[j] = true; ok = true; break; }
            }
            if (!ok) return false;
        }

        if (a.BagelIndex.Count != b.BagelIndex.Count) return false;
        foreach (var (k, v) in a.BagelIndex)
            if (!b.BagelIndex.TryGetValue(k, out var bv) || !ManualEverythingComparer.AreEqual(v, bv)) return false;

        if (a.Jaggy.Length != b.Jaggy.Length) return false;
        for (int i = 0; i < a.Jaggy.Length; i++)
        {
            var ax = a.Jaggy[i];
            var bx = b.Jaggy[i];
            if (ax.Length != bx.Length) return false;
            for (int j = 0; j < ax.Length; j++)
                if (ax[j] != bx[j]) return false;
        }

        if (a.Data.Length != b.Data.Length) return false;
        if (!a.Data.Span.SequenceEqual(b.Data.Span)) return false;
        if (a.RData.Length != b.RData.Length) return false;
        if (!a.RData.Span.SequenceEqual(b.RData.Span)) return false;

        if (a.Mixed.Count != b.Mixed.Count) return false;
        for (int i = 0; i < a.Mixed.Count; i++)
            if (!ManualValueComparer.AreEqual(a.Mixed[i], b.Mixed[i])) return false;

        if (!DictObjEqual(a.Meta, b.Meta)) return false;
        if (!ManualValueComparer.AreEqual(a.Expando, b.Expando)) return false;

        if (!ManualValueComparer.AreEqual(a.Polymorph, b.Polymorph)) return false;

        if (a.ForcedOrdered.Count != b.ForcedOrdered.Count) return false;
        for (int i = 0; i < a.ForcedOrdered.Count; i++)
            if (a.ForcedOrdered[i] != b.ForcedOrdered[i]) return false;

        return true;

        static bool DictObjEqual(Dictionary<string, object?> a, Dictionary<string, object?> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var (k, v) in a)
                if (!b.TryGetValue(k, out var bv) || !ManualValueComparer.AreEqual(v, bv)) return false;
            return true;
        }
    }
}

[MemoryDiagnoser]
[PlainExporter]
public class MegaBenchmarks
{
    [Params(3)] public int OrgBreadth;
    [Params(3)] public int OrgDepth;
    [Params(300)] public int Products;
    [Params(300)] public int Customers;
    [Params(4)] public int OrdersPerCustomer;
    [Params(6)] public int LinesPerOrder;
    [Params(384)] public int EB_Count;

    [Params(128)] public int BagelsCount;

    private MegaRoot _eqA = null!;
    private MegaRoot _eqB = null!;
    private MegaRoot _neqShallowA = null!;
    private MegaRoot _neqShallowB = null!;
    private MegaRoot _neqDeepA = null!;
    private MegaRoot _neqDeepB = null!;

    [GlobalSetup]
    public void Setup()
    {
        _eqA = MegaFactory.Create(OrgBreadth, OrgDepth, Products, Customers, OrdersPerCustomer, LinesPerOrder, BagelsCount, seed: 11);
        _eqB = MegaFactory.Create(OrgBreadth, OrgDepth, Products, Customers, OrdersPerCustomer, LinesPerOrder, BagelsCount, seed: 11);

        _neqShallowA = MegaFactory.Create(OrgBreadth, OrgDepth, Products, Customers, OrdersPerCustomer, LinesPerOrder, BagelsCount, seed: 22);
        _neqShallowB = MegaFactory.Create(OrgBreadth, OrgDepth, Products, Customers, OrdersPerCustomer, LinesPerOrder, BagelsCount, seed: 22);
        MegaFactory.MutateShallow(_neqShallowB);

        _neqDeepA = MegaFactory.Create(OrgBreadth, OrgDepth, Products, Customers, OrdersPerCustomer, LinesPerOrder, BagelsCount, seed: 33);
        _neqDeepB = MegaFactory.Create(OrgBreadth, OrgDepth, Products, Customers, OrdersPerCustomer, LinesPerOrder, BagelsCount, seed: 33);
        MegaFactory.MutateDeep(_neqDeepB);
    }

    [Benchmark(Baseline = true)] public bool Generated_Mega_Equal() => MegaRootDeepEqual.AreDeepEqual(_eqA, _eqB);
    [Benchmark] public bool Manual_Mega_Equal() => ManualMegaComparer.AreEqual(_eqA, _eqB);

    [Benchmark] public bool Generated_Mega_NotEqual_Shallow() => MegaRootDeepEqual.AreDeepEqual(_neqShallowA, _neqShallowB);
    [Benchmark] public bool Manual_Mega_NotEqual_Shallow() => ManualMegaComparer.AreEqual(_neqShallowA, _neqShallowB);

    [Benchmark] public bool Generated_Mega_NotEqual_Deep() => MegaRootDeepEqual.AreDeepEqual(_neqDeepA, _neqDeepB);
    [Benchmark] public bool Manual_Mega_NotEqual_Deep() => ManualMegaComparer.AreEqual(_neqDeepA, _neqDeepB);
}
