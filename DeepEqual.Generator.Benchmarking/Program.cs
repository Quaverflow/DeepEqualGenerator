using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using DeepEqual.Generator.Shared;
using KellermanSoftware.CompareNetObjects;
using System.Text.Json;
using Newtonsoft.Json.Linq;

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
}

public class OrgNode
{
    public string Name { get; set; } = "";
    public Role Role { get; set; }
    public List<OrgNode> Reports { get; set; } = new();
}

public class Product
{
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public DateTime Introduced { get; set; }
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
}

public class Customer
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = "";
    public List<Order> Orders { get; set; } = new();
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
            catalog.Add(new Product
            {
                Sku = $"SKU-{i:D6}",
                Name = $"Product {i}",
                Price = 10 + i,
                Introduced = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i)
            });
        }

        var custs = new List<Customer>(customers);
        for (int c = 0; c < customers; c++)
        {
            var cust = new Customer
            {
                Id = DeterministicGuid($"C{c}"),
                FullName = $"Customer {c}"
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
                    }
                };

                for (int l = 0; l < linesPerOrder; l++)
                {
                    var p = catalog[(l + o) % catalog.Count];
                    order.Lines.Add(new OrderLine
                    {
                        Sku = p.Sku,
                        Qty = 1 + (l % 3),
                        LineTotal = p.Price * (1 + (l % 3))
                    });
                }

                cust.Orders.Add(order);
            }

            custs.Add(cust);
        }

        return new BigGraph
        {
            Title = "BigGraph Bench",
            Org = root,
            OrgIndex = index,
            Catalog = catalog,
            Customers = custs
        };
    }

    private static void BuildOrg(OrgNode parent, int breadth, int maxDepth, int depth, Random rng)
    {
        if (depth >= maxDepth)
        {
            return;
        }

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
        {
            IndexOrg(r, index);
        }
    }

    private static Guid DeterministicGuid(string s)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(s);
        Span<byte> g = stackalloc byte[16];
        for (int i = 0; i < 16; i++)
        {
            g[i] = (byte)(bytes[i % bytes.Length] + i * 31);
        }

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

    private JToken _eqA_J = null!;
    private JToken _eqB_J = null!;
    private JToken _neqDeepA_J = null!;
    private JToken _neqDeepB_J = null!;

    private string _eqA_Ser = null!;
    private string _eqB_Ser = null!;
    private string _neqDeepA_Ser = null!;
    private string _neqDeepB_Ser = null!;

    private static readonly JsonSerializerOptions _stjOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

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

        _eqA_J = JToken.FromObject(_eqA);
        _eqB_J = JToken.FromObject(_eqB);
        _neqDeepA_J = JToken.FromObject(_neqDeepA);
        _neqDeepB_J = JToken.FromObject(_neqDeepB);

        // STJ precompute
        _eqA_Ser = JsonSerializer.Serialize(_eqA, _stjOptions);
        _eqB_Ser = JsonSerializer.Serialize(_eqB, _stjOptions);
        _neqDeepA_Ser = JsonSerializer.Serialize(_neqDeepA, _stjOptions);
        _neqDeepB_Ser = JsonSerializer.Serialize(_neqDeepB, _stjOptions);
    }

    [Benchmark(Baseline = true)]
    public bool Generated_Equal() =>
        BigGraphDeepEqual.AreDeepEqual(_eqA, _eqB);

    [Benchmark]
    public bool Generated_NotEqual_Shallow() =>
        BigGraphDeepEqual.AreDeepEqual(_neqShallowA, _neqShallowB);

    [Benchmark]
    public bool Generated_NotEqual_Deep() =>
        BigGraphDeepEqual.AreDeepEqual(_neqDeepA, _neqDeepB);

    [Benchmark]
    public bool CNO_Equal() =>
        _cno.Compare(_eqA, _eqB).AreEqual;

    [Benchmark]
    public bool CNO_NotEqual_Shallow() =>
        _cno.Compare(_neqShallowA, _neqShallowB).AreEqual;

    [Benchmark]
    public bool CNO_NotEqual_Deep() =>
        _cno.Compare(_neqDeepA, _neqDeepB).AreEqual;

    [Benchmark]
    public bool JToken_FromObject_Equal() =>
        JToken.DeepEquals(JToken.FromObject(_eqA), JToken.FromObject(_eqB));

    [Benchmark]
    public bool JToken_FromObject_NotEqual_Deep() =>
        JToken.DeepEquals(JToken.FromObject(_neqDeepA), JToken.FromObject(_neqDeepB));

    [Benchmark]
    public bool JToken_Precomputed_Equal() =>
        JToken.DeepEquals(_eqA_J, _eqB_J);

    [Benchmark]
    public bool JToken_Precomputed_NotEqual_Deep() =>
        JToken.DeepEquals(_neqDeepA_J, _neqDeepB_J);

    [Benchmark]
    public bool STJ_Serialize_StringEquals_Equal() =>
        JsonSerializer.Serialize(_eqA, _stjOptions) == JsonSerializer.Serialize(_eqB, _stjOptions);

    [Benchmark]
    public bool STJ_Serialize_StringEquals_NotEqual_Deep() =>
        JsonSerializer.Serialize(_neqDeepA, _stjOptions) == JsonSerializer.Serialize(_neqDeepB, _stjOptions);

    [Benchmark]
    public bool STJ_Precomputed_StringEquals_Equal() =>
        _eqA_Ser == _eqB_Ser;

    [Benchmark]
    public bool STJ_Precomputed_StringEquals_NotEqual_Deep() =>
        _neqDeepA_Ser == _neqDeepB_Ser;
}