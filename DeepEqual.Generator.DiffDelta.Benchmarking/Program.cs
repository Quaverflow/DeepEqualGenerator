using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using DeepEqual.Generator.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DeepEqual.Generator.DiffDelta.Benchmarking;

public static class Program_ManualVsGenerated
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<ManualVsGeneratedBenchmarks>(
            DefaultConfig.Instance.AddJob(Job.ShortRun));
    }
}

// -----------------------------------------------------------------------------
// POCOs (decorated so generated DeepOps exist)
// -----------------------------------------------------------------------------
[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
[DeltaTrack(ThreadSafe = false)]
public partial class DirtyOrder
{
    private int _Id;
    public int Id { get => _Id; set { _Id = value; __MarkDirty(__Bit_Id); } }

    private string? _Notes;
    public string? Notes { get => _Notes; set { _Notes = value; __MarkDirty(__Bit_Notes); } }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class Address { public string? Street { get; set; } public string? City { get; set; } }

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class Customer { public int Id { get; set; } public string? Name { get; set; } public Address? Home { get; set; } }

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class OrderItem { public string? Sku { get; set; } public int Qty { get; set; } }

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class Order
{
    public int Id { get; set; }
    public Customer? Customer { get; set; }
    public List<OrderItem>? Items { get; set; }
    public string? Notes { get; set; }
}

// -----------------------------------------------------------------------------
// Dataset builders (inline instead of separate OrderDataset/DirtyOrderDataset)
// -----------------------------------------------------------------------------
public static class BenchData
{
    public static List<DirtyOrder> BuildDirtyBase(int count)
        => Enumerable.Range(1, count).Select(i => new DirtyOrder { Id = i, Notes = "n/a" }).ToList();

    public static List<DirtyOrder> CloneDirty(List<DirtyOrder> src)
        => src.Select(o => new DirtyOrder { Id = o.Id, Notes = o.Notes }).ToList();

    public static void MutateDirty_OneScalar(List<DirtyOrder> dst)
    {
        foreach (var o in dst) o.Notes = "changed";
    }

    public static void MutateDirty_TwoScalars(List<DirtyOrder> dst)
    {
        foreach (var o in dst)
        {
            o.Notes = "changed";
            if ((o.Id & 1) == 0) o.Id = o.Id + 1;
        }
    }

    public static List<Order> BuildOrders(int n, int linesPerOrder)
    {
        var list = new List<Order>(n);
        for (int i = 0; i < n; i++)
        {
            list.Add(new Order
            {
                Id = i + 1,
                Notes = "n/a",
                Customer = new Customer
                {
                    Id = 1000 + i,
                    Name = "C" + i,
                    Home = new Address { Street = "S" + i, City = "City" + (i % 5) }
                },
                Items = Enumerable.Range(0, linesPerOrder)
                    .Select(j => new OrderItem { Sku = "SKU" + (j % 10), Qty = 1 + (j % 3) })
                    .ToList()
            });
        }
        return list;
    }

    public static List<Order> Mutate_ScalarChange(List<Order> src)
        => src.Select(s => new Order
        {
            Id = ((s.Id % 10) == 0) ? s.Id + 1 : s.Id,
            Notes = "changed",
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
            Items = s.Items?.Select(i => new OrderItem { Sku = i.Sku, Qty = i.Qty }).ToList()
        }).ToList();

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
        Items = s.Items?.Select(i => new OrderItem { Sku = i.Sku, Qty = i.Qty }).ToList()
    };
}

public static class ManualDeepOps
{
    // ---- Address ----------------------------------------------------------------
    // Indices: 0 = Street, 1 = City
    public static void ComputeDelta(Address? a, Address? b, ComparisonContext ctx, ref DeltaWriter w)
    {
        if (ReferenceEquals(a, b)) return;
        if (a is null || b is null)
        {
            w.WriteReplaceObject(b);
            return;
        }

        if (!string.Equals(a.Street, b.Street, StringComparison.Ordinal))
            w.WriteSetMember(0, b.Street);

        if (!string.Equals(a.City, b.City, StringComparison.Ordinal))
            w.WriteSetMember(1, b.City);
    }

    // ---- Customer ---------------------------------------------------------------
    // Indices: 0 = Id, 1 = Name, 2 = Home (nested)
    public static void ComputeDelta(Customer? a, Customer? b, ComparisonContext ctx, ref DeltaWriter w)
    {
        if (ReferenceEquals(a, b)) return;
        if (a is null || b is null)
        {
            w.WriteReplaceObject(b);
            return;
        }

        if (a.Id != b.Id) w.WriteSetMember(0, b.Id);

        if (!string.Equals(a.Name, b.Name, StringComparison.Ordinal))
            w.WriteSetMember(1, b.Name);

        // Nested: Address
        if (!ReferenceEquals(a.Home, b.Home))
        {
            if (a.Home is null || b.Home is null)
            {
                w.WriteSetMember(2, b.Home);
            }
            else
            {
                var scope = w.BeginNestedMember(2, out var nw);
                ComputeDelta(a.Home, b.Home, ctx, ref nw);
                scope.Dispose(); // will emit only if nested doc non-empty
            }
        }
    }

    // ---- OrderItem --------------------------------------------------------------
    // Indices: 0 = Sku, 1 = Qty
    public static void ComputeDelta(OrderItem? a, OrderItem? b, ComparisonContext ctx, ref DeltaWriter w)
    {
        if (ReferenceEquals(a, b)) return;
        if (a is null || b is null)
        {
            w.WriteReplaceObject(b);
            return;
        }

        if (!string.Equals(a.Sku, b.Sku, StringComparison.Ordinal))
            w.WriteSetMember(0, b.Sku);

        if (a.Qty != b.Qty)
            w.WriteSetMember(1, b.Qty);
    }

    // ---- Order (top level) ------------------------------------------------------
    // Indices: 0 = Id, 1 = Customer (nested), 2 = Items (list), 3 = Notes
    public static void ComputeDelta(Order? a, Order? b, ComparisonContext ctx, ref DeltaWriter w)
    {
        if (ReferenceEquals(a, b)) return;
        if (a is null || b is null)
        {
            w.WriteReplaceObject(b);
            return;
        }

        if (a.Id != b.Id) w.WriteSetMember(0, b.Id);

        // Nested: Customer
        if (!ReferenceEquals(a.Customer, b.Customer))
        {
            if (a.Customer is null || b.Customer is null)
            {
                w.WriteSetMember(1, b.Customer);
            }
            else
            {
                var scope = w.BeginNestedMember(1, out var nw);
                ComputeDelta(a.Customer, b.Customer, ctx, ref nw);
                scope.Dispose();
            }
        }

        // List: Items (IList<OrderItem>)
        if (!ReferenceEquals(a.Items, b.Items))
        {
            if (a.Items is null || b.Items is null)
            {
                w.WriteSetMember(2, b.Items);
            }
            else
            {
                ComputeListDelta_OrderItems(a.Items, b.Items, memberIndex: 2, ref w, ctx);
            }
        }

        if (!string.Equals(a.Notes, b.Notes, StringComparison.Ordinal))
            w.WriteSetMember(3, b.Notes);
    }

    // A tiny list delta similar to your generator’s windowed approach
    private static void ComputeListDelta_OrderItems(
        IList<OrderItem> left, IList<OrderItem> right, int memberIndex, ref DeltaWriter w, ComparisonContext ctx)
    {
        var la = left.Count;
        var lb = right.Count;

        // Common prefix
        var prefix = 0;
        var maxP = Math.Min(la, lb);
        while (prefix < maxP && AreEqual_Item(left[prefix], right[prefix]))
            prefix++;

        // Common suffix (avoid overlap)
        var suffix = 0;
        var maxS = Math.Min(la - prefix, lb - prefix);
        while (suffix < maxS && AreEqual_Item(left[la - 1 - suffix], right[lb - 1 - suffix]))
            suffix++;

        var ra = la - prefix - suffix; // replace/remove range in left
        var rb = lb - prefix - suffix; // replace/add range in right
        var common = Math.Min(ra, rb);

        // Replaces within the window
        for (int i = 0; i < common; i++)
        {
            var ai = prefix + i;
            if (!AreEqual_Item(left[ai], right[ai]))
                w.WriteSeqReplaceAt(memberIndex, ai, right[ai]);
        }

        // Removes
        if (ra > rb)
        {
            for (int i = ra - 1; i >= rb; i--)
                w.WriteSeqRemoveAt(memberIndex, prefix + i);
        }
        // Adds
        else if (rb > ra)
        {
            for (int i = ra; i < rb; i++)
                w.WriteSeqAddAt(memberIndex, prefix + i, right[prefix + i]);
        }

        static bool AreEqual_Item(OrderItem a, OrderItem b)
            => a.Qty == b.Qty && string.Equals(a.Sku, b.Sku, StringComparison.Ordinal);
    }

    // ---- DirtyOrder (bitfield) --------------------------------------------------
    // Indices: 0 = Id, 1 = Notes
    // This "manual" version assumes the same semantics you use in generated dirty mode:
    // if a bit is dirty, emit SetMember for that member; otherwise skip.
    public static void ComputeDelta_Dirty(DirtyOrder a, DirtyOrder b, ComparisonContext ctx, ref DeltaWriter w)
    {
        if (!ReferenceEquals(a, b))
        {
            // For this manual benchmark, just compare values (mimicking “dirty knows best”).
            if (a.Id != b.Id) w.WriteSetMember(0, b.Id);
            if (!string.Equals(a.Notes, b.Notes, StringComparison.Ordinal)) w.WriteSetMember(1, b.Notes);
        }
    }
}


// -----------------------------------------------------------------------------
// Benchmarks
// -----------------------------------------------------------------------------
[MemoryDiagnoser]
[HideColumns("Median", "Min", "Max")]
public class ManualVsGeneratedBenchmarks
{
    [Params(200)] public int N { get; set; }
    [Params(10)] public int LinesPerOrder { get; set; }

    private List<Order> _before = default!;
    private List<Order> _after_equal = default!;
    private List<Order> _after_scalar = default!;
    private List<DirtyOrder> _dirtyBefore = default!;
    private List<DirtyOrder> _dirtyAfter_1bit = default!;
    private List<DirtyOrder> _dirtyAfter_2bits = default!;

    private ComparisonContext _ctx = default!;

    [GlobalSetup]
    public void Setup()
    {
        GeneratedHelperRegistry.WarmUp(typeof(DirtyOrder));
        GeneratedHelperRegistry.WarmUp(typeof(Order));
        GeneratedHelperRegistry.WarmUp(typeof(Customer));
        GeneratedHelperRegistry.WarmUp(typeof(Address));
        GeneratedHelperRegistry.WarmUp(typeof(OrderItem));

        _ctx = new ComparisonContext(new ComparisonOptions());

        _before = BenchData.BuildOrders(N, LinesPerOrder);
        _after_equal = _before.Select(BenchData.CloneOrder).ToList();
        _after_scalar = BenchData.Mutate_ScalarChange(_before);

        _dirtyBefore = BenchData.BuildDirtyBase(N);
        _dirtyAfter_1bit = BenchData.CloneDirty(_dirtyBefore);
        BenchData.MutateDirty_OneScalar(_dirtyAfter_1bit);

        _dirtyAfter_2bits = BenchData.CloneDirty(_dirtyBefore);
        BenchData.MutateDirty_TwoScalars(_dirtyAfter_2bits);
    }

    [Benchmark] public int Generated_Order_ScalarChange() => RunGenerated(_before, _after_scalar);
    [Benchmark] public int Manual_Order_ScalarChange() => RunManual(_before, _after_scalar);

    [Benchmark] public int Generated_Order_Equal() => RunGenerated(_before, _after_equal);
    [Benchmark] public int Manual_Order_Equal() => RunManual(_before, _after_equal);

    [Benchmark] public int Generated_Dirty_1bit() => RunGeneratedDirty(_dirtyBefore, _dirtyAfter_1bit);
    [Benchmark] public int Manual_Dirty_1bit() => RunManualDirty(_dirtyBefore, _dirtyAfter_1bit);

    [Benchmark] public int Generated_Dirty_2bits() => RunGeneratedDirty(_dirtyBefore, _dirtyAfter_2bits);
    [Benchmark] public int Manual_Dirty_2bits() => RunManualDirty(_dirtyBefore, _dirtyAfter_2bits);

    private int RunGenerated(List<Order> a, List<Order> b)
    {
        int ops = 0;
        for (int i = 0; i < a.Count; i++)
            ops += OrderDeepOps.ComputeDelta(a[i], b[i], _ctx).Operations.Count;
        return ops;
    }

    private int RunManual(List<Order> a, List<Order> b)
    {
        int ops = 0;
        for (int i = 0; i < a.Count; i++)
        {
            var dd = new DeltaDocument();
            var w = new DeltaWriter(dd);
            ManualDeepOps.ComputeDelta(a[i], b[i], _ctx, ref w);
            ops += dd.Operations.Count;
        }
        return ops;
    }

    private int RunGeneratedDirty(List<DirtyOrder> a, List<DirtyOrder> b)
    {
        int ops = 0;
        for (int i = 0; i < a.Count; i++)
            ops += DirtyOrderDeepOps.ComputeDelta(a[i], b[i], _ctx).Operations.Count;
        return ops;
    }

    private int RunManualDirty(List<DirtyOrder> a, List<DirtyOrder> b)
    {
        int ops = 0;
        for (int i = 0; i < a.Count; i++)
        {
            var dd = new DeltaDocument();
            var w = new DeltaWriter(dd);
            ManualDeepOps.ComputeDelta_Dirty(a[i], b[i], _ctx, ref w);
            ops += dd.Operations.Count;
        }
        return ops;
    }
}
