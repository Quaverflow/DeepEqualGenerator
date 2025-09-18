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
        BenchmarkRunner.Run<DiffDeltaBenchmarks>(DefaultConfig.Instance);
    }
}

// ======================= MODELS (opted-in) =======================

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
public class Order
{
    public int Id { get; set; }

    public Customer? Customer { get; set; }

    // IList<T> → granular Seq* ops
    public List<OrderItem>? Items { get; set; }

    // Dictionary → granular Dict* ops
    public Dictionary<string, string>? Meta { get; set; }

    public string? Notes { get; set; }
}

// Polymorphic payloads for interface scenarios
public interface IAnimal { string? Name { get; set; } }

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class Dog : IAnimal { public string? Name { get; set; } public int Bones { get; set; } }

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class ZooOrder : Order
{
    public IAnimal? Pet { get; set; }
}

// ======================= DATASET BUILDERS =======================

public static class OrderDataset
{
    public enum Scenario
    {
        NoOp,               // identical; sanity + no-op
        ScalarChange,       // just Id/Notes changes
        ListReplaceSome,    // replace a few existing elements (SeqReplaceAt)
        ListAddRemove,      // add/remove a few elements (SeqAddAt/SeqRemoveAt)
        DictEdits,          // DictSet/DictRemove
        Polymorphic,        // interface member change (runtime dispatch)
        ArraysFallback      // not benchmarked by competitors; SetMember fallback
    }

    public static List<Order> BuildBase(int customers, int ordersPerCustomer, int linesPerOrder, bool polymorphic = false)
    {
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
                    items.Add(new OrderItem { Sku = "SKU" + (l % 15), Qty = 1 + (l % 5) });

                var meta = new Dictionary<string, string>
                {
                    ["env"] = "prod",
                    ["source"] = "bench"
                };

                if (polymorphic)
                {
                    orders.Add(new ZooOrder
                    {
                        Id = id++,
                        Customer = cust,
                        Items = items,
                        Meta = meta,
                        Notes = "n/a",
                        Pet = new Dog { Name = "fido", Bones = 1 }
                    });
                }
                else
                {
                    orders.Add(new Order
                    {
                        Id = id++,
                        Customer = cust,
                        Items = items,
                        Meta = meta,
                        Notes = "n/a"
                    });
                }
            }
        }
        return orders;
    }

    public static List<Order> Mutate(List<Order> src, Scenario scenario)
    {
        // Deep clone base
        var dst = src.Select(CloneOrder).ToList();
        switch (scenario)
        {
            case Scenario.NoOp:
                return dst;

            case Scenario.ScalarChange:
                foreach (var o in dst)
                {
                    o.Notes = "changed";
                    if ((o.Id % 10) == 0) o.Id += 1;
                }
                return dst;

            case Scenario.ListReplaceSome:
                foreach (var o in dst)
                {
                    if (o.Items is null) continue;
                    for (int i = 0; i < o.Items.Count; i++)
                        if (i % 5 == 0) o.Items[i].Qty += 1;
                }
                return dst;

            case Scenario.ListAddRemove:
                foreach (var o in dst)
                {
                    if (o.Items is null) continue;
                    // remove one in the middle, add one at tail
                    if (o.Items.Count > 4) o.Items.RemoveAt(o.Items.Count / 2);
                    o.Items.Add(new OrderItem { Sku = "ADDED", Qty = 7 });
                }
                return dst;

            case Scenario.DictEdits:
                foreach (var o in dst)
                {
                    if (o.Meta is null) continue;
                    o.Meta["source"] = "sync";
                    if ((o.Id % 7) == 0) o.Meta.Remove("env");
                    o.Meta["k" + (o.Id % 3)] = "v" + (o.Id % 5);
                }
                return dst;

            case Scenario.Polymorphic:
                foreach (var o in dst)
                {
                    if (o is ZooOrder z && z.Pet is Dog d)
                        d.Bones += 1; // nested field changes under interface
                }
                return dst;

            case Scenario.ArraysFallback:
                // Not used directly in main benchmarks; included for completeness if you extend models
                return dst;

            default:
                return dst;
        }
    }

    public static Order CloneOrder(Order s) => new()
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
        Items = s.Items is null ? null : s.Items.Select(i => new OrderItem { Sku = i.Sku, Qty = i.Qty }).ToList(),
        Meta = s.Meta is null ? null : new Dictionary<string, string>(s.Meta)
    };
}

// ======================= BENCHMARKS =======================

[MemoryDiagnoser]
[HideColumns("Median", "Min", "Max")]
public class DiffDeltaBenchmarks
{
    // Scale and shape
    [Params(5)] public int Customers { get; set; }
    [Params(5)] public int OrdersPerCustomer { get; set; }
    [Params(10)] public int LinesPerOrder { get; set; }

    // Scenario controls what changes exist (and stresses list/dict/polymorphic)
    [Params(
        OrderDataset.Scenario.ScalarChange,
        OrderDataset.Scenario.ListReplaceSome,
        OrderDataset.Scenario.ListAddRemove,
        OrderDataset.Scenario.DictEdits,
        OrderDataset.Scenario.Polymorphic,
        OrderDataset.Scenario.NoOp
    )]
    public OrderDataset.Scenario Shape { get; set; }

    private List<Order> _before = default!;
    private List<Order> _after = default!;
    private List<DeltaDocument> _patches = default!;

    // Competitors
    private CompareLogic _compareNetObjects = default!;
    private ObjectsComparer.Comparer<Order> _objectsComparer = default!;
    private JsonDiffPatchDotNet.JsonDiffPatch _jdp = default!;

    // ---------------- Manual baselines ----------------

    private static bool ManualEqual_Order(Order? a, Order? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Id != b.Id) return false;
        if (!ManualEqual_Customer(a.Customer, b.Customer)) return false;
        if (!ManualEqual_Items(a.Items, b.Items)) return false;
        if (!ManualEqual_Dict(a.Meta, b.Meta)) return false;
        if (!string.Equals(a.Notes, b.Notes, StringComparison.Ordinal)) return false;
        return true;
    }

    private static bool ManualEqual_Customer(Customer? a, Customer? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Id != b.Id) return false;
        if (!string.Equals(a.Name, b.Name, StringComparison.Ordinal)) return false;
        return ManualEqual_Address(a.Home, b.Home);
    }

    private static bool ManualEqual_Address(Address? a, Address? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (!string.Equals(a.Street, b.Street, StringComparison.Ordinal)) return false;
        if (!string.Equals(a.City, b.City, StringComparison.Ordinal)) return false;
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
            if (!string.Equals(ai.Sku, bi.Sku, StringComparison.Ordinal)) return false;
            if (ai.Qty != bi.Qty) return false;
        }
        return true;
    }

    private static bool ManualEqual_Dict(Dictionary<string, string>? a, Dictionary<string, string>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Count != b.Count) return false;
        foreach (var kv in a)
        {
            if (!b.TryGetValue(kv.Key, out var bv)) return false;
            if (!string.Equals(kv.Value, bv, StringComparison.Ordinal)) return false;
        }
        return true;
    }

    private static void ManualComputeDelta_Order(Order? left, Order? right, ref DeltaWriter writer)
    {
        if (ReferenceEquals(left, right)) return;
        if (left is null || right is null) { writer.WriteReplaceObject(right); return; }

        if (left.Id != right.Id)
            writer.WriteSetMember(StableIndex(typeof(Order), nameof(Order.Id)), right.Id);

        // nested customer
        {
            var sub = new DeltaDocument();
            var w = new DeltaWriter(sub);
            ManualComputeDelta_Customer(left.Customer, right.Customer, ref w);
            if (!sub.IsEmpty)
                writer.WriteNestedMember(StableIndex(typeof(Order), nameof(Order.Customer)), sub);
        }

        if (!ManualEqual_Items(left.Items, right.Items))
            writer.WriteSetMember(StableIndex(typeof(Order), nameof(Order.Items)), right.Items);

        if (!ManualEqual_Dict(left.Meta, right.Meta))
            writer.WriteSetMember(StableIndex(typeof(Order), nameof(Order.Meta)), right.Meta);

        if (!string.Equals(left.Notes, right.Notes, StringComparison.Ordinal))
            writer.WriteSetMember(StableIndex(typeof(Order), nameof(Order.Notes)), right.Notes);
    }

    private static void ManualComputeDelta_Customer(Customer? left, Customer? right, ref DeltaWriter writer)
    {
        if (ReferenceEquals(left, right)) return;
        if (left is null || right is null) { writer.WriteReplaceObject(right); return; }

        if (left.Id != right.Id)
            writer.WriteSetMember(StableIndex(typeof(Customer), nameof(Customer.Id)), right.Id);

        if (!string.Equals(left.Name, right.Name, StringComparison.Ordinal))
            writer.WriteSetMember(StableIndex(typeof(Customer), nameof(Customer.Name)), right.Name);

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

        if (!string.Equals(left.Street, right.Street, StringComparison.Ordinal))
            writer.WriteSetMember(StableIndex(typeof(Address), nameof(Address.Street)), right.Street);
        if (!string.Equals(left.City, right.City, StringComparison.Ordinal))
            writer.WriteSetMember(StableIndex(typeof(Address), nameof(Address.City)), right.City);
    }

    private static int StableIndex(Type owner, string memberName)
    {
        unchecked
        {
            int h = 17;
            var ownerName = owner.FullName ?? owner.Name;
            foreach (var ch in ownerName) h = h * 31 + ch;
            foreach (var ch in memberName) h = h * 31 + ch;
            return (h & 0x7FFFFFFF);
        }
    }

    // ---------------- Setup ----------------

    [GlobalSetup]
    public void Setup()
    {
        // Ensure generated helpers are live (module initializer should do this; belt & braces)
        GeneratedHelperRegistry.WarmUp(typeof(Order));
        GeneratedHelperRegistry.WarmUp(typeof(Customer));
        GeneratedHelperRegistry.WarmUp(typeof(Address));
        GeneratedHelperRegistry.WarmUp(typeof(OrderItem));
        GeneratedHelperRegistry.WarmUp(typeof(ZooOrder));
        GeneratedHelperRegistry.WarmUp(typeof(Dog));

        bool poly = Shape == OrderDataset.Scenario.Polymorphic;
        _before = OrderDataset.BuildBase(Customers, OrdersPerCustomer, LinesPerOrder, polymorphic: poly);
        _after = OrderDataset.Mutate(_before, Shape);
        _patches = new List<DeltaDocument>(_before.Count);

        // Competitors config
        _compareNetObjects = new CompareLogic(new ComparisonConfig
        {
            IgnoreCollectionOrder = false,
            MaxDifferences = int.MaxValue
        });

        _objectsComparer = new ObjectsComparer.Comparer<Order>(new ComparisonSettings());
        _jdp = new JsonDiffPatchDotNet.JsonDiffPatch();
    }

    // ===================== Diff =====================

    [Benchmark(Baseline = true, Description = "Generated_Diff_TryGetDiff")]
    public int Generated_Diff_TryGetDiff()
    {
        int changed = 0;
        for (int i = 0; i < _before.Count; i++)
            if (OrderDeepOps.TryGetDiff(_before[i], _after[i], out var diff) && diff.HasChanges) changed++;
        return changed;
    }

    [Benchmark(Description = "Generated_Diff_GetDiff")]
    public int Generated_Diff_GetDiff()
    {
        int changed = 0;
        for (int i = 0; i < _before.Count; i++)
        {
            OrderDeepOps.TryGetDiff(_before[i], _after[i], out var diff);
            if (diff.HasChanges) changed++;
        }
        return changed;
    }

    [Benchmark(Description = "Manual_Diff")]
    public int Manual_Diff()
    {
        int changed = 0;
        for (int i = 0; i < _before.Count; i++)
            if (!ManualEqual_Order(_before[i], _after[i])) changed++;
        return changed;
    }

    //[Benchmark(Description = "CompareNetObjects_Diff")]
    //public int CompareNetObjects_Diff()
    //{
    //    int changed = 0;
    //    for (int i = 0; i < _before.Count; i++)
    //        if (!_compareNetObjects.Compare(_before[i], _after[i]).AreEqual) changed++;
    //    return changed;
    //}

    //[Benchmark(Description = "ObjectsComparer_Diff")]
    //public int ObjectsComparer_Diff()
    //{
    //    int changed = 0;
    //    for (int i = 0; i < _before.Count; i++)
    //        if (!_objectsComparer.Compare(_before[i], _after[i], out _)) changed++;
    //    return changed;
    //}

    //[Benchmark(Description = "JsonDiffPatch_Diff")]
    //public int JsonDiffPatch_Diff()
    //{
    //    int changed = 0;
    //    for (int i = 0; i < _before.Count; i++)
    //    {
    //        var a = JToken.Parse(JsonConvert.SerializeObject(_before[i]));
    //        var b = JToken.Parse(JsonConvert.SerializeObject(_after[i]));
    //        if (_jdp.Diff(a, b) != null) changed++;
    //    }
    //    return changed;
    //}

    //// ===================== Delta Compute =====================

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

    //[Benchmark(Description = "JsonDiffPatch_Compute")]
    //public int JsonDiffPatch_Compute()
    //{
    //    int produced = 0;
    //    for (int i = 0; i < _before.Count; i++)
    //    {
    //        var a = JToken.Parse(JsonConvert.SerializeObject(_before[i]));
    //        var b = JToken.Parse(JsonConvert.SerializeObject(_after[i]));
    //        var patch = _jdp.Diff(a, b);
    //        if (patch != null) produced++;
    //    }
    //    return produced;
    //}

    //// ===================== Delta Apply =====================

    //[Benchmark(Description = "Generated_Delta_Apply")]
    //public int Generated_Delta_Apply()
    //{
    //    if (_patches.Count == 0) Generated_Delta_Compute();

    //    int applied = 0;
    //    for (int i = 0; i < _before.Count; i++)
    //    {
    //        var doc = _patches[i];
    //        var reader = new DeltaReader(doc);
    //        var target = OrderDataset.CloneOrder(_before[i]);
    //        OrderDeepOps.ApplyDelta(ref target, ref reader);
    //        applied += (target is not null) ? 1 : 0;
    //    }
    //    return applied;
    //}

    //[Benchmark(Description = "JsonDiffPatch_Apply")]
    //[A(typeof(Order), nameof(Order.Items), nameof(OrderItem.Qty))]
    //public int JsonDiffPatch_Apply()
    //{
    //    int applied = 0;
    //    for (int i = 0; i < _before.Count; i++)
    //    {
    //        var a = JToken.Parse(JsonConvert.SerializeObject(_before[i]));
    //        var b = JToken.Parse(JsonConvert.SerializeObject(_after[i]));
    //        var patch = _jdp.Diff(a, b);
    //        if (patch == null) continue;
    //        var patched = _jdp.Patch(a, patch);
    //        if (!JToken.DeepEquals(patched, b)) { /* ignore mismatch */ }
    //        applied++;
    //    }
    //    return applied;
    //}
}

