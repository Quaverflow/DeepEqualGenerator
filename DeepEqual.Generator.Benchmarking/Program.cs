using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using DeepEqual.Generator.Shared;
using KellermanSoftware.CompareNetObjects;
using System.Dynamic;
using System.Text.Json;
using Newtonsoft.Json.Linq;

// James Foster's DeepEqual
using DeepEqual;
using DeepEqual.Syntax;

namespace DeepEqual.Generator.Benchmarking;

internal class Program
{
    static void Main()
    {
        _ = BenchmarkRunner.Run<DeepGraphBenchmarks>();
    }
}

public enum Role { None, Dev, Lead, Manager }

[DeepComparable]
public partial class BigGraph
{
    public string Title { get; set; } = "";
    public OrgNode Org { get; set; } = new();
    public Dictionary<string, OrgNode> OrgIndex { get; set; } = new(StringComparer.Ordinal);
    public List<Product> Catalog { get; set; } = new();
    public List<Customer> Customers { get; set; } = new();

    // NEW: heterogeneous, JSON-like metadata on the root
    public IDictionary<string, object?> Meta { get; set; } = new ExpandoObject();
}

public class OrgNode
{
    public string Name { get; set; } = "";
    public Role Role { get; set; }
    public List<OrgNode> Reports { get; set; } = new();

    // NEW: Expando “extra” blob on each org node
    public IDictionary<string, object?> Extra { get; set; } = new ExpandoObject();
}

public class Product
{
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public DateTime Introduced { get; set; }

    // NEW: Expando attributes with nested arrays/maps/objects
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

    // NEW: Expando per-order blob (heterogeneous values)
    public IDictionary<string, object?> Extra { get; set; } = new ExpandoObject();
}

public class Customer
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = "";
    public List<Order> Orders { get; set; } = new();

    // NEW: Expando profile for customers
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

        // Root graph
        var graph = new BigGraph
        {
            Title = "BigGraph Bench",
            Org = root,
            OrgIndex = index,
            Catalog = catalog,
            Customers = custs,
            Meta = MakeExpando(rng, "ROOT", depth: 2)
        };

        // Fill org node expandos deterministically
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
        foreach (var r in node.Reports)
            IndexOrg(r, index);
    }

    private static void FillOrgExpandos(OrgNode node, Random rng)
    {
        node.Extra = MakeExpando(rng, node.Name, depth: 1);
        foreach (var r in node.Reports)
            FillOrgExpandos(r, rng);
    }

    /// <summary>
    /// Builds a deterministic Expando graph with a mix of primitives, arrays, nested maps and a child expando.
    /// Kept deterministic by the provided seed & id.
    /// </summary>
    private static IDictionary<string, object?> MakeExpando(Random rng, string id, int depth)
    {
        var exp = new ExpandoObject();
        var d = (IDictionary<string, object?>)exp;

        // primitives
        d["id"] = id;
        d["flag"] = (id.GetHashCode() & 1) == 0;

        // arrays (ints + strings)
        d["nums"] = new[] { NextRange(rng, id, 0), NextRange(rng, id, 1), NextRange(rng, id, 2) };
        d["tags"] = new[] { "alpha", "beta", id };

        // nested map with mixed values (including an array leaf)
        d["map"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["x"] = new[] { 1, 2, 3 },
            ["y"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["z"] = id.Length,
                ["u"] = new[] { "p", "q" }
            }
        };

        // nested expando (recurse)
        if (depth > 0)
        {
            d["child"] = MakeExpando(rng, id + "-" + depth.ToString(), depth - 1);
        }

        return d;

        static int NextRange(Random r, string salt, int k)
            => Math.Abs((salt.GetHashCode() + 31 * k)) % 10 + r.Next(0, 3);
    }

    private static Guid DeterministicGuid(string s)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(s);
        Span<byte> g = stackalloc byte[16];
        for (int i = 0; i < 16; i++)
            g[i] = (byte)(bytes[i % bytes.Length] + i * 31);
        return new Guid(g);
    }
}

[MemoryDiagnoser]
[PlainExporter, RPlotExporter]
public class DeepGraphBenchmarks
{
    [Params(3)] public int OrgBreadth;
    [Params(3)] public int OrgDepth;
    [Params(200)] public int Products;
    [Params(200)] public int Customers;
    [Params(3)] public int OrdersPerCustomer;
    [Params(4)] public int LinesPerOrder;

    private BigGraph _eqA = null!;
    private BigGraph _eqB = null!;
    private BigGraph _neqShallowA = null!;
    private BigGraph _neqShallowB = null!;
    private BigGraph _neqDeepA = null!;
    private BigGraph _neqDeepB = null!;

    private CompareLogic _cno = null!;

    [GlobalSetup]
    public void Setup()
    {
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

    // ---------------- Generated ----------------

    [Benchmark(Baseline = true)]
    public bool Generated_Equal() =>
        BigGraphDeepEqual.AreDeepEqual(_eqA, _eqB);

    [Benchmark]
    public bool Generated_NotEqual_Shallow() =>
        BigGraphDeepEqual.AreDeepEqual(_neqShallowA, _neqShallowB);

    [Benchmark]
    public bool Generated_NotEqual_Deep() =>
        BigGraphDeepEqual.AreDeepEqual(_neqDeepA, _neqDeepB);

    // ---------------- Compare-NET-Objects ----------------

    [Benchmark]
    public bool CNO_NotEqual_Shallow() =>
        _cno.Compare(_neqShallowA, _neqShallowB).AreEqual;
}
