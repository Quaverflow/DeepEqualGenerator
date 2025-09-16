using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using DeepEqual.Generator.Shared;
using KellermanSoftware.CompareNetObjects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObjectsComparer;

namespace DeepEqual.Generator.DiffDelta.Benchmarking;

public static class Program
{
    public static void Main(string[] args)
    {
        // Run with default config; you can add columns/exporters as you like.
        BenchmarkRunner.Run<DiffDeltaBenchmarks>(DefaultConfig.Instance);
    }
}
[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class Address
{
    public string? Street { get; set; }
    public string? City { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class Customer
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public Address? Home { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class OrderItem
{
    public string? Sku { get; set; }
    public int Qty { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class Order
{
    public int Id { get; set; }

    public Customer? Customer { get; set; }

    // Ordered sequence: v1 shallow set on change
    public List<OrderItem>? Items { get; set; }

    // Dictionary: v1 shallow set on change
    public Dictionary<string, string>? Meta { get; set; }

    public string? Notes { get; set; }
}
public static class OrderDataset
{
    public static List<Order> Build(int customers, int ordersPerCustomer, int linesPerOrder)
    {
        var rand = new Random(123);
        var orders = new List<Order>(customers * ordersPerCustomer);

        int id = 1;
        for (int c = 0; c < customers; c++)
        {
            var cust = new Customer
            {
                Id = 1000 + c,
                Name = "C" + c,
                Home = new Address { Street = "S" + c, City = "City" + (c % 7) }
            };

            for (int o = 0; o < ordersPerCustomer; o++)
            {
                var items = new List<OrderItem>(linesPerOrder);
                for (int l = 0; l < linesPerOrder; l++)
                {
                    items.Add(new OrderItem { Sku = "SKU" + (l % 15), Qty = 1 + (l % 5) });
                }

                orders.Add(new Order
                {
                    Id = id++,
                    Customer = cust,
                    Items = items,
                    Meta = new Dictionary<string, string>
                    {
                        ["env"] = "prod",
                        ["source"] = "bench"
                    },
                    Notes = "n/a"
                });
            }
        }

        return orders;
    }

    // Make a copy with *small* changes to simulate typical deltas
    public static List<Order> Mutate(List<Order> src)
    {
        var dst = new List<Order>(src.Count);
        foreach (var o in src)
        {
            dst.Add(new Order
            {
                Id = o.Id,
                Notes = o.Notes, // unchanged
                Customer = o.Customer is null ? null : new Customer
                {
                    Id = o.Customer.Id,
                    Name = o.Customer.Name, // unchanged
                    Home = o.Customer.Home is null ? null : new Address
                    {
                        Street = o.Customer.Home.Street,
                        City = o.Customer.Home.City // unchanged
                    }
                },
                Items = o.Items is null ? null : new List<OrderItem>(o.Items.Count)
            });
            var d = dst[^1];

            if (o.Items is not null)
            {
                for (int i = 0; i < o.Items.Count; i++)
                {
                    var it = o.Items[i];
                    // bump every 3rd line’s Qty by +1 (causes change in collection)
                    var q = (i % 3 == 0) ? it.Qty + 1 : it.Qty;
                    (d.Items!).Add(new OrderItem { Sku = it.Sku, Qty = q });
                }
            }

            // Meta: flip one key occasionally (causes dict change)
            d.Meta = o.Meta is null ? null : new Dictionary<string, string>(o.Meta);
            if (d.Meta is not null && d.Id % 5 == 0) d.Meta["source"] = "sync";
        }

        return dst;
    }
}
[MemoryDiagnoser]
[HideColumns("Median", "Min", "Max")]
public class DiffDeltaBenchmarks
{
    [Params(1500)]
    public int Customers { get; set; } = 40;

    [Params(10)]
    public int OrdersPerCustomer { get; set; } = 3;

    [Params(10)]
    public int LinesPerOrder { get; set; } = 4;

    private List<Order> _before = default!;
    private List<Order> _after = default!;
    private List<DeltaDocument> _patches = default!;

    // Competitors
    private CompareLogic _compareNetObjects = default!;
    private ObjectsComparer.Comparer<Order> _objectsComparer = default!;
    private JsonDiffPatchDotNet.JsonDiffPatch _jdp = default!;

    private static bool ManualEqual_Order(Order? a, Order? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;

        if (a.Id != b.Id) return false;
        if (!ManualEqual_Customer(a.Customer, b.Customer)) return false;
        if (!ManualEqual_Items(a.Items, b.Items)) return false;
        if (!ManualEqual_Dict(a.Meta, b.Meta)) return false;
        if (!string.Equals(a.Notes, b.Notes, System.StringComparison.Ordinal)) return false;

        return true;
    }

    private static bool ManualEqual_Customer(Customer? a, Customer? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;

        if (a.Id != b.Id) return false;
        if (!string.Equals(a.Name, b.Name, System.StringComparison.Ordinal)) return false;
        if (!ManualEqual_Address(a.Home, b.Home)) return false;

        return true;
    }

    private static bool ManualEqual_Address(Address? a, Address? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;

        if (!string.Equals(a.Street, b.Street, System.StringComparison.Ordinal)) return false;
        if (!string.Equals(a.City, b.City, System.StringComparison.Ordinal)) return false;

        return true;
    }

    private static bool ManualEqual_Items(List<OrderItem>? a, List<OrderItem>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Count != b.Count) return false;

        for (int i = 0; i < a.Count; i++)
        {
            var ai = a[i]; var bi = b[i];
            if (!string.Equals(ai.Sku, bi.Sku, System.StringComparison.Ordinal)) return false;
            if (ai.Qty != bi.Qty) return false;
        }
        return true;
    }

    private static bool ManualEqual_Dict(Dictionary<string, string>? a, Dictionary<string, string>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Count != b.Count) return false;

        // order-insensitive key/value compare
        foreach (var kv in a)
        {
            if (!b.TryGetValue(kv.Key, out var bv)) return false;
            if (!string.Equals(kv.Value, bv, System.StringComparison.Ordinal)) return false;
        }
        return true;
    }

    // ---- Manual delta compute (builds a DeltaDocument; v1 semantics) ----
    // NOTE: we don't need to match the generator's member indices; these are
    // internal to the manual baseline. We compute stable ints locally.
    private static void ManualComputeDelta_Order(Order? left, Order? right, ref DeltaWriter writer)
    {
        if (ReferenceEquals(left, right)) return;
        if (left is null || right is null) { writer.WriteReplaceObject(right); return; }

        // Id (value-like) -> Set on change
        if (left.Id != right.Id)
            writer.WriteSetMember(StableIndex(typeof(Order), nameof(Order.Id)), right.Id);

        // Customer (nested)
        {
            var sub = new DeltaDocument();
            var w = new DeltaWriter(sub);
            ManualComputeDelta_Customer(left.Customer, right.Customer, ref w);
            if (!sub.IsEmpty)
                writer.WriteNestedMember(StableIndex(typeof(Order), nameof(Order.Customer)), sub);
        }

        // Items (collection) -> v1 shallow replace on any change
        if (!ManualEqual_Items(left.Items, right.Items))
            writer.WriteSetMember(StableIndex(typeof(Order), nameof(Order.Items)), right.Items);

        // Meta (dict) -> v1 shallow replace on any change
        if (!ManualEqual_Dict(left.Meta, right.Meta))
            writer.WriteSetMember(StableIndex(typeof(Order), nameof(Order.Meta)), right.Meta);

        // Notes (string)
        if (!string.Equals(left.Notes, right.Notes, System.StringComparison.Ordinal))
            writer.WriteSetMember(StableIndex(typeof(Order), nameof(Order.Notes)), right.Notes);
    }

    private static void ManualComputeDelta_Customer(Customer? left, Customer? right, ref DeltaWriter writer)
    {
        if (ReferenceEquals(left, right)) return;
        if (left is null || right is null) { writer.WriteReplaceObject(right); return; }

        if (left.Id != right.Id)
            writer.WriteSetMember(StableIndex(typeof(Customer), nameof(Customer.Id)), right.Id);

        if (!string.Equals(left.Name, right.Name, System.StringComparison.Ordinal))
            writer.WriteSetMember(StableIndex(typeof(Customer), nameof(Customer.Name)), right.Name);

        // Address nested
        var sub = new DeltaDocument();
        var w = new DeltaWriter(sub);
        ManualComputeDelta_Address(left.Home, right.Home, ref w);
        if (!sub.IsEmpty)
            writer.WriteNestedMember(StableIndex(typeof(Customer), nameof(Customer.Home)), sub);
    }

    private static void ManualComputeDelta_Address(Address? left, Address? right, ref DeltaWriter writer)
    {
        if (ReferenceEquals(left, right)) return;
        if (left is null || right is null) { writer.WriteReplaceObject(right); return; }

        if (!string.Equals(left.Street, right.Street, System.StringComparison.Ordinal))
            writer.WriteSetMember(StableIndex(typeof(Address), nameof(Address.Street)), right.Street);

        if (!string.Equals(left.City, right.City, System.StringComparison.Ordinal))
            writer.WriteSetMember(StableIndex(typeof(Address), nameof(Address.City)), right.City);
    }

    // Stable, deterministic member index (local to manual baseline)
    private static int StableIndex(System.Type owner, string memberName)
    {
        unchecked
        {
            int h = 17;
            var ownerName = owner.FullName ?? owner.Name; // e.g. "SyncBin.DiffDelta.Benchmarks.Order"
            foreach (var ch in ownerName) h = h * 31 + ch;
            foreach (var ch in memberName) h = h * 31 + ch;
            return (h & 0x7FFFFFFF); // no need to mod; just keep non-negative
        }
    }
    [GlobalSetup]
    public void Setup()
    {
        // Ensure generated static ctors run (also handled by module initializer, but safe here)
        GeneratedHelperRegistry.WarmUp(typeof(Order));
        GeneratedHelperRegistry.WarmUp(typeof(Customer));
        GeneratedHelperRegistry.WarmUp(typeof(Address));
        GeneratedHelperRegistry.WarmUp(typeof(OrderItem));

        _before = OrderDataset.Build(Customers, OrdersPerCustomer, LinesPerOrder);
        _after = OrderDataset.Mutate(_before);

        _patches = new List<DeltaDocument>(_before.Count);

        // Compare-NET-Objects
        _compareNetObjects = new CompareLogic(new ComparisonConfig
        {
            IgnoreCollectionOrder = false,
            MaxDifferences = int.MaxValue
        });

        // ObjectsComparer
        _objectsComparer = new ObjectsComparer.Comparer<Order>(new ComparisonSettings());
        // Json Diff/Patch
        _jdp = new JsonDiffPatchDotNet.JsonDiffPatch();
    }

    // ===================== Diff =====================

    [Benchmark(Baseline = true, Description = "Generated_Diff")]
    public int Generated_Diff()
    {
        int totalChanged = 0;
        for (int i = 0; i < _before.Count; i++)
        {
            if (OrderDeepOps.TryGetDiff(_before[i], _after[i], out var diff) && diff.HasChanges)
                totalChanged++;
        }
        return totalChanged;
    }

    [Benchmark(Description = "Manual_Diff")]
    public int Manual_Diff()
    {
        int totalChanged = 0;
        for (int i = 0; i < _before.Count; i++)
        {
            if (!ManualEqual_Order(_before[i], _after[i])) totalChanged++;
        }
        return totalChanged;
    }

    [Benchmark(Description = "CompareNetObjects_Diff")]
    public int CompareNetObjects_Diff()
    {
        int totalChanged = 0;
        for (int i = 0; i < _before.Count; i++)
        {
            var res = _compareNetObjects.Compare(_before[i], _after[i]);
            if (!res.AreEqual) totalChanged++;
        }
        return totalChanged;
    }

    [Benchmark(Description = "ObjectsComparer_Diff")]
    public int ObjectsComparer_Diff()
    {
        int totalChanged = 0;
        for (int i = 0; i < _before.Count; i++)
        {
            bool equal = _objectsComparer.Compare(_before[i], _after[i], out _);
            if (!equal) totalChanged++;
        }
        return totalChanged;
    }

    [Benchmark(Description = "JsonDiffPatch_Diff")]
    public int JsonDiffPatch_Diff()
    {
        int totalChanged = 0;
        for (int i = 0; i < _before.Count; i++)
        {
            var a = JToken.Parse(JsonConvert.SerializeObject(_before[i]));
            var b = JToken.Parse(JsonConvert.SerializeObject(_after[i]));
            var patch = _jdp.Diff(a, b);
            if (patch != null) totalChanged++;
        }
        return totalChanged;
    }

    // ===================== Delta Compute =====================

    [Benchmark(Description = "Generated_Delta_Compute")]
    public int Generated_Delta_Compute()
    {
        _patches.Clear();
        for (int i = 0; i < _before.Count; i++)
        {
            var doc = new DeltaDocument();
            var w = new DeltaWriter(doc);
            OrderDeepOps.ComputeDelta(_before[i], _after[i], ref w);
            _patches.Add(doc);
        }
        return _patches.Count;
    }

    [Benchmark(Description = "Manual_Delta_Compute")]
    public int Manual_Delta_Compute()
    {
        int produced = 0;
        for (int i = 0; i < _before.Count; i++)
        {
            var doc = new DeltaDocument();
            var w = new DeltaWriter(doc);
            ManualComputeDelta_Order(_before[i], _after[i], ref w);
            if (!doc.IsEmpty) produced++;
        }
        return produced;
    }

    // For JSON competitor, "compute delta" = produce a JsonDiffPatch patch token
    [Benchmark(Description = "JsonDiffPatch_Compute")]
    public int JsonDiffPatch_Compute()
    {
        int produced = 0;
        for (int i = 0; i < _before.Count; i++)
        {
            var a = JToken.Parse(JsonConvert.SerializeObject(_before[i]));
            var b = JToken.Parse(JsonConvert.SerializeObject(_after[i]));
            var patch = _jdp.Diff(a, b);
            if (patch != null) produced++;
        }
        return produced;
    }

    // ===================== Delta Apply =====================

    [Benchmark(Description = "Generated_Delta_Apply")]
    public int Generated_Delta_Apply()
    {
        // Ensure patches exist
        if (_patches.Count == 0) Generated_Delta_Compute();

        int applied = 0;
        for (int i = 0; i < _before.Count; i++)
        {
            var doc = _patches[i];
            var reader = new DeltaReader(doc);
            var target = CloneOrder(_before[i]);
            OrderDeepOps.ApplyDelta(ref target, ref reader);
            applied += (target is not null) ? 1 : 0;
        }
        return applied;
    }

    [Benchmark(Description = "JsonDiffPatch_Apply")]
    public int JsonDiffPatch_Apply()
    {
        int applied = 0;
        for (int i = 0; i < _before.Count; i++)
        {
            var a = JToken.Parse(JsonConvert.SerializeObject(_before[i]));
            var b = JToken.Parse(JsonConvert.SerializeObject(_after[i]));

            var patch = _jdp.Diff(a, b);
            if (patch == null) continue;

            var patched = _jdp.Patch(a, patch);
            // We don't deserialize back (that would measure JSON deserialize cost)
            if (!JToken.DeepEquals(patched, b)) { /* sanity fail; still count */ }
            applied++;
        }
        return applied;
    }

    // -------- helpers --------

    private static Order CloneOrder(Order s) => new()
    {
        Id = s.Id,
        Notes = s.Notes,
        Customer = s.Customer is null ? null : new Customer
        {
            Id = s.Customer.Id,
            Name = s.Customer.Name,
            Home = s.Customer.Home is null ? null : new Address
            {
                Street = s.Customer.Home.Street,
                City = s.Customer.Home.City
            }
        },
        Items = s.Items is null ? null : new List<OrderItem>(s.Items.Count)
        {
            Capacity = s.Items.Count
        }
    };

}