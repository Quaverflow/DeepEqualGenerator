using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using DeepEqual.Generator.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DeepEqual.Generator.DiffDelta.Benchmarking;

public static class Program_RerunOnly
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<RerunOnlyBenchmarks>(DefaultConfig.Instance);
    }
}

// ======================= Dirty-tracked models (minimal) =======================

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
[DeltaTrack(ThreadSafe = false)]
public partial class DirtyOrder
{
    private int _Id;
    public int Id { get => _Id; set { _Id = value; __MarkDirty(__Bit_Id); } }

    private string? _Notes;
    public string? Notes { get => _Notes; set { _Notes = value; __MarkDirty(__Bit_Notes); } }
}

public static class DirtyOrderDataset
{
    public static List<DirtyOrder> BuildBase(int count)
        => Enumerable.Range(1, count).Select(i => new DirtyOrder { Id = i, Notes = "n/a" }).ToList();

    public static List<DirtyOrder> Clone(List<DirtyOrder> src)
        => src.Select(o => new DirtyOrder { Id = o.Id, Notes = o.Notes }).ToList();

    public static void Mutate_OneScalar(List<DirtyOrder> dst)
    {
        foreach (var o in dst) o.Notes = "changed";
    }

    public static void Mutate_TwoScalars(List<DirtyOrder> dst)
    {
        foreach (var o in dst)
        {
            o.Notes = "changed";
            if ((o.Id & 1) == 0) o.Id = o.Id + 1;
        }
    }
}

// ======================= Minimal Order models for Apply benchmark =======================

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

public static class OrderDataset
{
    public static List<Order> BuildBase(int n, int linesPerOrder)
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
            Customer = new Customer
            {
                Id = s.Customer!.Id,
                Name = s.Customer.Name,
                Home = s.Customer.Home is null ? null : new Address { Street = s.Customer.Home.Street, City = s.Customer.Home.City }
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
            Home = s.Customer.Home is null ? null : new Address { Street = s.Customer.Home.Street, City = s.Customer.Home.City }
        },
        Items = s.Items?.Select(i => new OrderItem { Sku = i.Sku, Qty = i.Qty }).ToList()
    };
}

// ======================= Benchmarks: only the ones to rerun =======================
[MemoryDiagnoser]
[HideColumns("Median", "Min", "Max")]
public class RerunOnlyBenchmarks
{
    [Params(200000)] public int N { get; set; }                 // #orders for dirty suites & apply
    [Params(10)] public int LinesPerOrder { get; set; }     // size for apply dataset

    private List<DirtyOrder> _dirtyBefore = default!;
    private List<DirtyOrder> _dirtyAfter_1bit = default!;
    private List<DirtyOrder> _dirtyAfter_2bits = default!;

    private ComparisonContext _ctxFast = default!;
    private ComparisonContext _ctxValidate = default!;

    private List<Order> _before = default!;
    private List<Order> _after = default!;
    private List<DeltaDocument> _patches = default!;

    [GlobalSetup]
    public void Setup()
    {
        // Warm up generated helpers
        GeneratedHelperRegistry.WarmUp(typeof(DirtyOrder));
        GeneratedHelperRegistry.WarmUp(typeof(Order));
        GeneratedHelperRegistry.WarmUp(typeof(Customer));
        GeneratedHelperRegistry.WarmUp(typeof(Address));
        GeneratedHelperRegistry.WarmUp(typeof(OrderItem));

        _ctxFast = new ComparisonContext(new ComparisonOptions { ValidateDirtyOnEmit = false });
        _ctxValidate = new ComparisonContext(new ComparisonOptions { ValidateDirtyOnEmit = true });

        // Dirty datasets
        _dirtyBefore = DirtyOrderDataset.BuildBase(N);
        _dirtyAfter_1bit = DirtyOrderDataset.Clone(_dirtyBefore);
        DirtyOrderDataset.Mutate_OneScalar(_dirtyAfter_1bit);

        _dirtyAfter_2bits = DirtyOrderDataset.Clone(_dirtyBefore);
        DirtyOrderDataset.Mutate_TwoScalars(_dirtyAfter_2bits);

        // Apply datasets
        _before = OrderDataset.BuildBase(N, LinesPerOrder);
        _after = OrderDataset.Mutate_ScalarChange(_before);
        _patches = new List<DeltaDocument>(_before.Count);
    }

    // Re-dirty before each iteration so fast path stays O(#dirty)
    [IterationSetup(Targets = new[] { nameof(Dirty_ComputeDelta_1bit_fast), nameof(Dirty_ComputeDelta_1bit_validate) })]
    public void IterSetup_Dirty_1bit()
    {
        foreach (var o in _dirtyAfter_1bit) o.Notes = o.Notes; // re-mark dirty
    }

    [IterationSetup(Targets = new[] { nameof(Dirty_ComputeDelta_2bits_fast), nameof(Dirty_ComputeDelta_2bits_validate) })]
    public void IterSetup_Dirty_2bits()
    {
        foreach (var o in _dirtyAfter_2bits)
        {
            o.Notes = o.Notes; // dirty Notes
            o.Id = o.Id;       // dirty Id
        }
    }

    // Ensure patches exist for Apply in each iteration
    //[IterationSetup(Targets = new[] { nameof(Generated_Delta_Apply) })]
    //public void IterSetup_Apply()
    //{
    //    _patches.Clear();
    //    _patches.Capacity = Math.Max(_patches.Capacity, _before.Count);
    //    for (int i = 0; i < _before.Count; i++)
    //        _patches.Add(OrderDeepOps.ComputeDelta(_before[i], _after[i]));
    //}

    // ---- RERUN: Dirty fast/validate ----

    [Benchmark(Description = "Dirty_ComputeDelta_1bit_fast")]
    [InvocationCount(2048)]
    public int Dirty_ComputeDelta_1bit_fast()
    {
        int produced = 0;
        for (int i = 0; i < _dirtyBefore.Count; i++)
        {
            var doc = DirtyOrderDeepOps.ComputeDelta(_dirtyBefore[i], _dirtyAfter_1bit[i], _ctxFast);
            if (!doc.IsEmpty) produced++;
        }
        return produced;
    }

    [Benchmark(Description = "Dirty_ComputeDelta_1bit_validate")]
    [InvocationCount(2048)]
    public int Dirty_ComputeDelta_1bit_validate()
    {
        int produced = 0;
        for (int i = 0; i < _dirtyBefore.Count; i++)
        {
            var doc = DirtyOrderDeepOps.ComputeDelta(_dirtyBefore[i], _dirtyAfter_1bit[i], _ctxValidate);
            if (!doc.IsEmpty) produced++;
        }
        return produced;
    }

    [Benchmark(Description = "Dirty_ComputeDelta_2bits_fast")]
    [InvocationCount(2048)]
    public int Dirty_ComputeDelta_2bits_fast()
    {
        int produced = 0;
        for (int i = 0; i < _dirtyBefore.Count; i++)
        {
            var doc = DirtyOrderDeepOps.ComputeDelta(_dirtyBefore[i], _dirtyAfter_2bits[i], _ctxFast);
            if (!doc.IsEmpty) produced++;
        }
        return produced;
    }

    [Benchmark(Description = "Dirty_ComputeDelta_2bits_validate")]
    [InvocationCount(2048)]
    public int Dirty_ComputeDelta_2bits_validate()
    {
        int produced = 0;
        for (int i = 0; i < _dirtyBefore.Count; i++)
        {
            var doc = DirtyOrderDeepOps.ComputeDelta(_dirtyBefore[i], _dirtyAfter_2bits[i], _ctxValidate);
            if (!doc.IsEmpty) produced++;
        }
        return produced;
    }

    // ---- RERUN: Generated_Delta_Apply (with patches rebuilt per iteration) ----

    //[Benchmark(Description = "Generated_Delta_Apply")]
    //[InvocationCount(2048)]
    //public int Generated_Delta_Apply()
    //{
    //    int applied = 0;
    //    for (int i = 0; i < _before.Count; i++)
    //    {
    //        var target = OrderDataset.CloneOrder(_before[i]); // fresh clone avoids compounding
    //        OrderDeepOps.ApplyDelta(ref target, _patches[i]);
    //        if (target is not null) applied++;
    //    }
    //    return applied;
    //}
}
