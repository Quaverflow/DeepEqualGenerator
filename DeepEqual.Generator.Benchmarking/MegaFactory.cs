using System.Dynamic;

namespace DeepEqual.Generator.Benchmarking;

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
        for (var i = 0; i < bagelsCount; i++)
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