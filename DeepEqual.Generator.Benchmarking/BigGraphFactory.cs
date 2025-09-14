using System.Dynamic;

namespace DeepEqual.Generator.Benchmarking;

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
        for (var i = 0; i < products; i++)
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
        for (var c = 0; c < customers; c++)
        {
            var cust = new Customer
            {
                Id = DeterministicGuid($"C{c}"),
                FullName = $"Customer {c}",
                Profile = MakeExpando(rng, $"C{c}", depth: 2)
            };

            for (var o = 0; o < ordersPerCustomer; o++)
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

                for (var l = 0; l < linesPerOrder; l++)
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
            for (var i = 0; i < breadth; i++)
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
            for (var i = 0; i < 16; i++) g[i] = (byte)(bytes[i % bytes.Length] + i * 31);
            return new Guid(g);
        }
    }
}