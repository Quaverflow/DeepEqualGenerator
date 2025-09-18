using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DeepEqual.Generator.Shared;
using Xunit;

namespace DeepEqual.Generator.Tests.DiffDeltaTests;
// ======= Models used by the tests (same core set, plus a few hosts for special cases) =======

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

    // IList<T> → granular Seq* ops
    public List<OrderItem>? Items { get; set; }

    // Dictionary → granular Dict* ops
    public Dictionary<string, string>? Meta { get; set; }

    public string? Notes { get; set; }
}

public interface IAnimal { string? Name { get; set; } }

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class Dog : IAnimal { public string? Name { get; set; } public int Bones { get; set; } }

// For type-change fallback
[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class Cat : IAnimal { public string? Name { get; set; } public int Mice { get; set; } }

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class Zoo
{
    public IAnimal? Pet { get; set; } // polymorphic
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class ShallowWrap
{
    [DeepCompare(DeltaShallow = true)]
    public Address? Addr { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class SkipWrap
{
    [DeepCompare(DeltaSkip = true)]
    public string? Ignored { get; set; }

    public string? Tracked { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class WithArray
{
    public int[]? Values { get; set; } // arrays fallback to SetMember (not granular)
}

// ---- Hosts for special cases ----

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class DictHost
{
    public Dictionary<string, Address>? Map { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class ReadOnlyListHost
{
    // IReadOnlyList<T> — compute should fallback to SetMember; apply replaces value (no granular ops)
    public IReadOnlyList<OrderItem>? Items { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class ReadOnlyDictHost
{
    // IReadOnlyDictionary<K,V> — compute should fallback to SetMember; apply replaces value
    public IReadOnlyDictionary<string, string>? Meta { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class CaseDictHost
{
    public Dictionary<string, string>? Meta { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class PolyDictHost
{
    public Dictionary<string, IAnimal>? Pets { get; set; }
}

public sealed class Parrot : IAnimal { public string? Name { get; set; } public int Seeds { get; set; } } // unregistered

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class ShallowCollectionWrap
{
    [DeepCompare(DeltaShallow = true)]
    public List<OrderItem>? Items { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class SkipCollectionWrap
{
    [DeepCompare(DeltaSkip = true)]
    public List<OrderItem>? Items { get; set; }
    [DeepCompare(DeltaSkip = true)]
    public Dictionary<string, string>? Meta { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class IntListHost
{
    public List<int>? Values { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class ModuleInitFoo
{
    public string? V { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class SealedThing // sealed class correctness (we can’t assert optimization, but assert correctness)
{
    public int A { get; set; }
    public Address? Addr { get; set; }
}

// ======= Tests =======

public class DiffDeltaFullSuite
{
    public DiffDeltaFullSuite()
    {
        // Ensure helpers are initialized for most types (we intentionally do not warm ModuleInitFoo or Parrot)
        GeneratedHelperRegistry.WarmUp(typeof(Address));
        GeneratedHelperRegistry.WarmUp(typeof(Customer));
        GeneratedHelperRegistry.WarmUp(typeof(OrderItem));
        GeneratedHelperRegistry.WarmUp(typeof(Order));
        GeneratedHelperRegistry.WarmUp(typeof(Dog));
        GeneratedHelperRegistry.WarmUp(typeof(Cat));
        GeneratedHelperRegistry.WarmUp(typeof(Zoo));
        GeneratedHelperRegistry.WarmUp(typeof(ShallowWrap));
        GeneratedHelperRegistry.WarmUp(typeof(SkipWrap));
        GeneratedHelperRegistry.WarmUp(typeof(WithArray));
        GeneratedHelperRegistry.WarmUp(typeof(DictHost));
        GeneratedHelperRegistry.WarmUp(typeof(ReadOnlyListHost));
        GeneratedHelperRegistry.WarmUp(typeof(ReadOnlyDictHost));
        GeneratedHelperRegistry.WarmUp(typeof(CaseDictHost));
        GeneratedHelperRegistry.WarmUp(typeof(PolyDictHost));
        GeneratedHelperRegistry.WarmUp(typeof(ShallowCollectionWrap));
        GeneratedHelperRegistry.WarmUp(typeof(SkipCollectionWrap));
        GeneratedHelperRegistry.WarmUp(typeof(IntListHost));
        GeneratedHelperRegistry.WarmUp(typeof(SealedThing));
    }

    // ---------- Utility builders ----------

    private static Order NewOrder()
    {
        return new Order
        {
            Id = 42,
            Notes = "init",
            Customer = new Customer
            {
                Id = 1,
                Name = "A",
                Home = new Address { Street = "S1", City = "C1" }
            },
            Items = new List<OrderItem>
            {
                new OrderItem { Sku = "X", Qty = 3 },
                new OrderItem { Sku = "Y", Qty = 1 },
                new OrderItem { Sku = "Z", Qty = 2 },
            },
            Meta = new Dictionary<string, string>
            {
                ["env"] = "test",
                ["who"] = "user"
            }
        };
    }

    private static Order Clone(Order s) => new()
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
        Items = s.Items?.Select(i => new OrderItem { Sku = i.Sku, Qty = i.Qty }).ToList(),
        Meta = s.Meta is null ? null : new Dictionary<string, string>(s.Meta)
    };

    private static List<OrderItem> MakeItems(params (string sku, int qty)[] xs)
        => xs.Select(x => new OrderItem { Sku = x.sku, Qty = x.qty }).ToList();
    // Find the MemberIndex for Order.Items by probing ops
    private static int ProbeItemsMemberIndex()
    {
        var a = new Order { Items = new List<OrderItem> { new() { Sku = "A", Qty = 1 }, new() { Sku = "B", Qty = 2 } } };
        var b = new Order { Items = new List<OrderItem> { new() { Sku = "A", Qty = 1 }, new() { Sku = "B", Qty = 9 } } }; // change forces diff

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(a, b, ref w);

        // Prefer any Seq* op (Value is an OrderItem) first
        var seq = doc.Operations.FirstOrDefault(op =>
            (op.Kind == DeltaKind.SeqReplaceAt || op.Kind == DeltaKind.SeqAddAt || op.Kind == DeltaKind.SeqRemoveAt));
        if (seq.Kind == DeltaKind.SeqReplaceAt || seq.Kind == DeltaKind.SeqAddAt || seq.Kind == DeltaKind.SeqRemoveAt)
            return seq.MemberIndex;

        // Fallback: SetMember where Value is a List<OrderItem>
        var set = doc.Operations.First(op => op.Kind == DeltaKind.SetMember && op.Value is List<OrderItem>);
        return set.MemberIndex;
    }

    // Find the MemberIndex for Order.Meta by probing ops
    private static int ProbeMetaMemberIndex()
    {
        var a = new Order { Meta = new Dictionary<string, string> { ["k"] = "1" } };
        var b = new Order { Meta = new Dictionary<string, string> { ["k"] = "2" } };

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(a, b, ref w);

        // Prefer Dict* ops when present
        var dict = doc.Operations.FirstOrDefault(op =>
            op.Kind == DeltaKind.DictSet || op.Kind == DeltaKind.DictRemove || op.Kind == DeltaKind.DictNested);
        if (dict.Kind == DeltaKind.DictSet || dict.Kind == DeltaKind.DictRemove || dict.Kind == DeltaKind.DictNested)
            return dict.MemberIndex;

        // Fallback: SetMember where Value is Dictionary<string,string>
        var set = doc.Operations.First(op => op.Kind == DeltaKind.SetMember && op.Value is Dictionary<string, string>);
        return set.MemberIndex;
    }

    // ---------- DIFF basics (kept) ----------

    [Fact]
    public void Diff_ValueLike_NoChange_NoDiff()
    {
        var a = new Address { Street = "S", City = "C" };
        var b = new Address { Street = "S", City = "C" };

        Assert.False(AddressDeepOps.TryGetDiff(a, b, out var diff));
        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void Diff_ValueLike_Change_Records_Set()
    {
        var a = new Address { Street = "S", City = "C1" };
        var b = new Address { Street = "S", City = "C2" };

        Assert.True(AddressDeepOps.TryGetDiff(a, b, out var diff));
        Assert.True(diff.HasChanges);
        Assert.Contains(diff.MemberChanges!, mc => mc.Kind == MemberChangeKind.Set);
    }

    [Fact]
    public void Diff_NestedObject_Records_Nested()
    {
        var a = new Customer { Id = 1, Name = "A", Home = new Address { Street = "S1", City = "C" } };
        var b = new Customer { Id = 1, Name = "A", Home = new Address { Street = "S2", City = "C" } };

        Assert.True(CustomerDeepOps.TryGetDiff(a, b, out var diff));
        Assert.Contains(diff.MemberChanges!, mc => mc.Kind == MemberChangeKind.Nested);
    }

    [Fact]
    public void Diff_Collections_Granular_Still_Registers_Change()
    {
        var a = NewOrder();
        var b = Clone(a);
        b.Items![1].Qty++;

        Assert.True(OrderDeepOps.TryGetDiff(a, b, out var diff));
        Assert.True(diff.HasChanges);
    }

    // ---------- Delta: basic replace-object ----------

    [Fact]
    public void Delta_ReplaceObject_NullToObj()
    {
        Address? left = null, right = new Address { Street = "S", City = "C" };
        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        AddressDeepOps.ComputeDelta(left, right, ref w);

        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.ReplaceObject && op.Value is Address);

        var reader = new DeltaReader(doc);
        Address? target = null;
        AddressDeepOps.ApplyDelta(ref target, ref reader);

        Assert.True(AddressDeepEqual.AreDeepEqual(right, target));
    }

    [Fact]
    public void Delta_ReplaceObject_ObjToNull()
    {
        Address? left = new Address { Street = "S", City = "C" };
        Address? right = null;

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        AddressDeepOps.ComputeDelta(left, right, ref w);

        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.ReplaceObject && op.Value is null);

        var reader = new DeltaReader(doc);
        Address? target = new Address { Street = "X", City = "Y" };
        AddressDeepOps.ApplyDelta(ref target, ref reader);

        Assert.Null(target);
    }

    // ---------- Lists granular ops: single/multiple & edges ----------

    [Fact]
    public void List_One_Element_Changed_Emits_Single_SeqReplaceAt_And_Applies()
    {
        var a = NewOrder();
        var b = Clone(a);
        b.Items![1].Qty++;

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(a, b, ref w);

        var repl = doc.Operations.Single(op => op.Kind == DeltaKind.SeqReplaceAt);
        Assert.Equal(1, repl.Index);

        var reader = new DeltaReader(doc);
        var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, ref reader);

        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void List_Add_In_Middle_Emits_SeqAddAt_And_Applies()
    {
        var a = NewOrder();
        var b = Clone(a);
        b.Items!.Insert(1, new OrderItem { Sku = "ADD", Qty = 9 });

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(a, b, ref w);

        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.SeqAddAt && op.Index == 1);

        var reader = new DeltaReader(doc);
        var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, ref reader);

        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void List_Remove_In_Middle_Emits_SeqRemoveAt_And_Applies()
    {
        var a = NewOrder();
        var b = Clone(a);
        b.Items!.RemoveAt(1);

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(a, b, ref w);

        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.SeqRemoveAt && op.Index == 1);

        var reader = new DeltaReader(doc);
        var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, ref reader);

        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void List_Add_Head_And_Tail_Then_Apply()
    {
        var a = NewOrder();
        var b = Clone(a);
        b.Items!.Insert(0, new OrderItem { Sku = "HEAD", Qty = 7 });
        b.Items!.Add(new OrderItem { Sku = "TAIL", Qty = 8 });

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(a, b, ref w);
        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.SeqAddAt && op.Index == b.Items!.Count - 1);

        // Head insertion may appear as SeqAddAt(0) OR as a replace at index 0 (both valid with this diff)
        Assert.True(
            doc.Operations.Any(op => op.Kind == DeltaKind.SeqAddAt && op.Index == 0) ||
            doc.Operations.Any(op => op.Kind == DeltaKind.SeqReplaceAt && op.Index == 0)
        );

        // Apply should still produce b
        var reader = new DeltaReader(doc);
        var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, ref reader);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void List_Remove_Head_And_Tail_Then_Apply()
    {
        var a = NewOrder();
        var b = Clone(a);
        b.Items!.RemoveAt(0);
        b.Items!.RemoveAt(b.Items!.Count - 1);

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(NewOrder(), b, ref w);

        Assert.True(doc.Operations.Count(op => op.Kind == DeltaKind.SeqRemoveAt) >= 2);

        var reader = new DeltaReader(doc);
        var target = NewOrder();
        OrderDeepOps.ApplyDelta(ref target, ref reader);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void List_Duplicate_Values_Replaces_By_Index()
    {
        var a = NewOrder();
        a.Items = MakeItems(("X", 1), ("X", 1), ("X", 1));
        var b = Clone(a);
        b.Items![1].Qty = 99; // change middle duplicate

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(a, b, ref w);

        var ops = doc.Operations.Where(o => o.Kind == DeltaKind.SeqReplaceAt).ToList();
        Assert.Single(ops);
        Assert.Equal(1, ops[0].Index);

        var reader = new DeltaReader(doc); var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, ref reader);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void List_PrefixSuffix_Trim_Minimizes_Ops()
    {
        var a = NewOrder();
        var b = Clone(a);
        b.Items![1].Sku = "MID";

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(a, b, ref w);

        Assert.Single(doc.Operations.Where(op => op.Kind == DeltaKind.SeqReplaceAt));

        var reader = new DeltaReader(doc);
        var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, ref reader);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void List_All_Changed_Falls_Back_To_Replaces_Not_AddRemoves()
    {
        var a = NewOrder();
        var b = Clone(a);
        for (int i = 0; i < b.Items!.Count; i++) b.Items[i].Qty++;

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(a, b, ref w);

        Assert.True(doc.Operations.All(op => op.Kind == DeltaKind.SeqReplaceAt));

        var reader = new DeltaReader(doc); var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, ref reader);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void List_Mixed_Ops_Add_Remove_Replace_Applies_In_Correct_Order()
    {
        var a = new Order { Items = MakeItems(("A", 1), ("B", 2), ("C", 3), ("D", 4)) };
        var b = new Order { Items = MakeItems(("A", 1), ("BX", 2), ("C", 3), ("E", 5)) }; // replace B->BX, remove D, add E at tail

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(a, b, ref w);

        // We still require the middle replacement:
        Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.SeqReplaceAt && o.Index == 1);

        // And E at the end can be expressed as add/remove or simply a replace at >= last index
        Assert.True(
            doc.Operations.Any(o => o.Kind == DeltaKind.SeqRemoveAt) ||
            doc.Operations.Any(o => o.Kind == DeltaKind.SeqAddAt) ||
            doc.Operations.Any(o => o.Kind == DeltaKind.SeqReplaceAt && o.Index >= 3)
        );

        // Apply correctness remains the key check
        var reader = new DeltaReader(doc);
        var target = new Order { Items = MakeItems(("A", 1), ("B", 2), ("C", 3), ("D", 4)) };
        OrderDeepOps.ApplyDelta(ref target, ref reader);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));

    }

    [Fact]
    public void List_Operation_Ordering_Stability()
    {
        var a = new Order { Items = MakeItems(("A", 1), ("B", 2), ("C", 3), ("D", 4), ("E", 5)) };
        var b = new Order { Items = MakeItems(("A", 1), ("BX", 2), ("C", 3), ("E", 5), ("F", 6)) }; // replace B, remove D, add F at tail

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(a, b, ref w);

        var removes = doc.Operations.Where(o => o.Kind == DeltaKind.SeqRemoveAt).Select(o => o.Index).ToList();
        var adds = doc.Operations.Where(o => o.Kind == DeltaKind.SeqAddAt).Select(o => o.Index).ToList();

        // removes should be in descending order to avoid index shift issues
        Assert.True(removes.SequenceEqual(removes.OrderByDescending(x => x)));
        // adds should be in ascending order
        Assert.True(adds.SequenceEqual(adds.OrderBy(x => x)));

        var reader = new DeltaReader(doc); var target = a;
        OrderDeepOps.ApplyDelta(ref target, ref reader);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void List_ReadOnly_Typed_Property_Falls_Back_To_SetMember()
    {
        var a = new ReadOnlyListHost { Items = MakeItems(("A", 1), ("B", 2)).AsReadOnly() };
        var b = new ReadOnlyListHost { Items = MakeItems(("A", 1), ("B", 9)).AsReadOnly() };

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        ReadOnlyListHostDeepOps.ComputeDelta(a, b, ref w);

        Assert.DoesNotContain(doc.Operations, o => o.Kind == DeltaKind.SeqReplaceAt || o.Kind == DeltaKind.SeqAddAt || o.Kind == DeltaKind.SeqRemoveAt);
        Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.SetMember);

        var reader = new DeltaReader(doc); var target = new ReadOnlyListHost { Items = MakeItems(("A", 1), ("B", 2)).AsReadOnly() };
        ReadOnlyListHostDeepOps.ApplyDelta(ref target, ref reader);
        Assert.True(ReadOnlyListHostDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void List_Idempotence_SetMember_Is_Idempotent()
    {
        // Using SetMember path (arrays fallback) to demonstrate idempotence
        var a = new WithArray { Values = new[] { 1, 2, 3 } };
        var b = new WithArray { Values = new[] { 1, 9, 3 } };

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        WithArrayDeepOps.ComputeDelta(a, b, ref w);

        var target = new WithArray { Values = new[] { 1, 2, 3 } };
        var reader = new DeltaReader(doc);
        WithArrayDeepOps.ApplyDelta(ref target, ref reader);

        // apply same patch again
        var reader2 = new DeltaReader(doc);
        WithArrayDeepOps.ApplyDelta(ref target, ref reader2);

        Assert.True(WithArrayDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void List_Idempotence_Add_Is_Not_Idempotent()
    {
        var a = new Order { Items = MakeItems(("A", 1), ("B", 2)) };
        var b = new Order { Items = MakeItems(("A", 1), ("B", 2), ("C", 3)) };

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(a, b, ref w);

        var t1 = new Order { Items = MakeItems(("A", 1), ("B", 2)) };
        var r1 = new DeltaReader(doc); OrderDeepOps.ApplyDelta(ref t1, ref r1);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, t1));

        // apply same patch again -> we expect duplicate add (non-idempotent), but no throw
        var r2 = new DeltaReader(doc);
        OrderDeepOps.ApplyDelta(ref t1, ref r2);
        Assert.NotEqual(b.Items!.Count, t1.Items!.Count);
    }

    [Fact]
    public void List_Mismatch_Target_Not_BaseOfPatch_Does_Not_Throw_For_Replaces()
    {
        // Compute patch with only replaces (no add/remove)
        var a = new Order { Items = MakeItems(("A", 1), ("B", 2), ("C", 3)) };
        var b = new Order { Items = MakeItems(("A", 1), ("BX", 2), ("C", 3)) };

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(a, b, ref w);

        // Apply to unrelated target with same length but different content at index 1
        var target = new Order { Items = MakeItems(("A", 1), ("Q", 5), ("C", 3)) };
        var reader = new DeltaReader(doc);
        var ex = Record.Exception(() => OrderDeepOps.ApplyDelta(ref target, ref reader));
        Assert.Null(ex);
        Assert.Equal("BX", target.Items![1].Sku);
    }

    // ---------- Dictionaries granular ops & edges ----------

    [Fact]
    public void Dict_Value_Change_Emits_DictSet_And_Applies()
    {
        var a = NewOrder();
        var b = Clone(a);
        b.Meta!["who"] = "z";

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(a, b, ref w);

        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.DictSet && (string)op.Key! == "who" && (string?)op.Value == "z");

        var reader = new DeltaReader(doc);
        var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, ref reader);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void Dict_Add_And_Remove_Emit_Ops_And_Apply()
    {
        var a = NewOrder();
        var b = Clone(a);
        b.Meta!.Remove("env");
        b.Meta!["new"] = "v";

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(a, b, ref w);

        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.DictRemove && (string)op.Key! == "env");
        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.DictSet && (string)op.Key! == "new" && (string?)op.Value == "v");

        var reader = new DeltaReader(doc);
        var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, ref reader);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void Dict_Nested_Object_Value_Uses_DictNested_Mutates_Instance()
    {
        var a = new PolyDictHost { Pets = new Dictionary<string, IAnimal> { ["d"] = new Dog { Name = "f", Bones = 1 } } };
        var b = new PolyDictHost { Pets = new Dictionary<string, IAnimal> { ["d"] = new Dog { Name = "f", Bones = 2 } } };

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        PolyDictHostDeepOps.ComputeDelta(a, b, ref w);

        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.DictNested && (string)op.Key! == "d");

        var reader = new DeltaReader(doc);
        var target = new PolyDictHost { Pets = new Dictionary<string, IAnimal> { ["d"] = new Dog { Name = "f", Bones = 1 } } };
        var beforeRef = target.Pets!["d"];
        PolyDictHostDeepOps.ApplyDelta(ref target, ref reader);

        // same instance (mutated), and value changed
        Assert.True(object.ReferenceEquals(beforeRef, target.Pets!["d"]));
        Assert.Equal(2, ((Dog)target.Pets["d"]).Bones);
    }

    [Fact]
    public void Dict_Custom_CaseInsensitive_KeyComparer_Respected_On_Apply()
    {
        var a = new CaseDictHost { Meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["who"] = "me" } };
        var b = new CaseDictHost { Meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["WHO"] = "you" } };

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        CaseDictHostDeepOps.ComputeDelta(a, b, ref w);

        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.DictSet && ((string)op.Key!).Equals("WHO", StringComparison.OrdinalIgnoreCase));

        var reader = new DeltaReader(doc);
        var target = new CaseDictHost { Meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["who"] = "me" } };
        CaseDictHostDeepOps.ApplyDelta(ref target, ref reader);

        Assert.True(target.Meta!.ContainsKey("who"));
        Assert.Equal("you", target.Meta["who"]); // key comparer handled casing
    }

    [Fact]
    public void Dict_Remove_On_Missing_Key_Is_Ignored_Safely()
    {
        var host = new CaseDictHost { Meta = new Dictionary<string, string> { ["a"] = "1" } };

        // Probe a real delta to get the actual MemberIndex for Meta
        var probeDoc = new DeltaDocument();
        var pw = new DeltaWriter(probeDoc);
        var a = new CaseDictHost { Meta = new Dictionary<string, string> { ["x"] = "1" } };
        var b = new CaseDictHost { Meta = new Dictionary<string, string> { ["y"] = "2" } };
        CaseDictHostDeepOps.ComputeDelta(a, b, ref pw);

        var metaIdx = probeDoc.Operations
            .First(op => op.Kind is DeltaKind.DictSet or DeltaKind.DictRemove).MemberIndex;

        // Now build a synthetic delta with a DictRemove for a key that doesn't exist
        var legacy = new DeltaDocument();
        var lw = new DeltaWriter(legacy);
        lw.WriteDictRemove(metaIdx, "missing");

        // Apply — should not throw and should leave original dict untouched
        var reader = new DeltaReader(legacy);
        var ex = Record.Exception(() => CaseDictHostDeepOps.ApplyDelta(ref host, ref reader));
        Assert.Null(ex);
        Assert.Equal("1", host.Meta!["a"]);
    }


    [Fact]
    public void Dict_ReadOnly_Typed_Property_Falls_Back_To_SetMember()
    {
        var a = new ReadOnlyDictHost { Meta = new Dictionary<string, string> { ["a"] = "1" } };
        var b = new ReadOnlyDictHost { Meta = new Dictionary<string, string> { ["a"] = "2" } };

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        ReadOnlyDictHostDeepOps.ComputeDelta(a, b, ref w);

        Assert.DoesNotContain(doc.Operations, o => o.Kind == DeltaKind.DictSet || o.Kind == DeltaKind.DictRemove || o.Kind == DeltaKind.DictNested);
        Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.SetMember);

        var reader = new DeltaReader(doc); var target = new ReadOnlyDictHost { Meta = new Dictionary<string, string> { ["a"] = "1" } };
        ReadOnlyDictHostDeepOps.ApplyDelta(ref target, ref reader);
        Assert.True(ReadOnlyDictHostDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void Dict_Idempotence_DictSet_Is_Idempotent()
    {
        var a = new CaseDictHost { Meta = new Dictionary<string, string> { ["a"] = "1" } };
        var b = new CaseDictHost { Meta = new Dictionary<string, string> { ["a"] = "2" } };

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        CaseDictHostDeepOps.ComputeDelta(a, b, ref w);

        var t = new CaseDictHost { Meta = new Dictionary<string, string> { ["a"] = "1" } };
        var r1 = new DeltaReader(doc); CaseDictHostDeepOps.ApplyDelta(ref t, ref r1);
        var r2 = new DeltaReader(doc); CaseDictHostDeepOps.ApplyDelta(ref t, ref r2);

        Assert.True(CaseDictHostDeepEqual.AreDeepEqual(b, t));
    }

    // ---------- Arrays (fallback SetMember) ----------

    [Fact]
    public void Arrays_Fallback_To_SetMember()
    {
        var a = new WithArray { Values = new[] { 1, 2, 3 } };
        var b = new WithArray { Values = new[] { 1, 9, 3 } };

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        WithArrayDeepOps.ComputeDelta(a, b, ref w);

        Assert.DoesNotContain(doc.Operations, op =>
            op.Kind == DeltaKind.SeqAddAt || op.Kind == DeltaKind.SeqRemoveAt || op.Kind == DeltaKind.SeqReplaceAt);

        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.SetMember);

        var reader = new DeltaReader(doc);
        var target = new WithArray { Values = new[] { 1, 2, 3 } };
        WithArrayDeepOps.ApplyDelta(ref target, ref reader);
        Assert.True(WithArrayDeepEqual.AreDeepEqual(b, target));
    }

    // ---------- Shallow & Skip semantics ----------

    [Fact]
    public void DeltaShallow_Produces_Set_Not_Nested()
    {
        var a = new ShallowWrap { Addr = new Address { Street = "A", City = "C" } };
        var b = new ShallowWrap { Addr = new Address { Street = "B", City = "C" } };

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        ShallowWrapDeepOps.ComputeDelta(a, b, ref w);

        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.SetMember);
        Assert.DoesNotContain(doc.Operations, op => op.Kind == DeltaKind.NestedMember);

        var reader = new DeltaReader(doc);
        var target = new ShallowWrap { Addr = new Address { Street = "A", City = "C" } };
        ShallowWrapDeepOps.ApplyDelta(ref target, ref reader);
        Assert.True(ShallowWrapDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void DeltaSkip_Member_Is_Not_Emitted_And_Not_Applied()
    {
        var a = new SkipWrap { Ignored = "x", Tracked = "t1" };
        var b = new SkipWrap { Ignored = "y", Tracked = "t2" };

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        SkipWrapDeepOps.ComputeDelta(a, b, ref w);

        // delta should only affect Tracked
        Assert.DoesNotContain(doc.Operations, op => op.Kind == DeltaKind.SetMember && (op.Value as string) == "y");

        var reader = new DeltaReader(doc);
        var target = new SkipWrap { Ignored = "x", Tracked = "t1" };
        SkipWrapDeepOps.ApplyDelta(ref target, ref reader);

        Assert.Equal("x", target.Ignored);
        Assert.Equal("t2", target.Tracked);
    }

    [Fact]
    public void DeltaShallow_On_Collection_Forces_SetMember()
    {
        var a = new ShallowCollectionWrap { Items = MakeItems(("A", 1), ("B", 2)) };
        var b = new ShallowCollectionWrap { Items = MakeItems(("A", 1), ("B", 9)) };

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        ShallowCollectionWrapDeepOps.ComputeDelta(a, b, ref w);

        // DeltaShallow => no granular ops for the collection
        Assert.DoesNotContain(doc.Operations, o =>
            o.Kind == DeltaKind.SeqAddAt ||
            o.Kind == DeltaKind.SeqRemoveAt ||
            o.Kind == DeltaKind.SeqReplaceAt ||
            o.Kind == DeltaKind.DictSet ||
            o.Kind == DeltaKind.DictRemove ||
            o.Kind == DeltaKind.DictNested);

        Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.SetMember);

        var reader = new DeltaReader(doc);
        var target = new ShallowCollectionWrap { Items = MakeItems(("A", 1), ("B", 2)) };
        ShallowCollectionWrapDeepOps.ApplyDelta(ref target, ref reader);

        Assert.True(ShallowCollectionWrapDeepEqual.AreDeepEqual(b, target));
    }


    [Fact]
    public void DeltaSkip_On_Collection_And_Dict_Emits_No_Ops_And_Does_Not_Change()
    {
        var a = new SkipCollectionWrap
        {
            Items = MakeItems(("A", 1), ("B", 2)),
            Meta = new Dictionary<string, string> { ["x"] = "1" }
        };
        var b = new SkipCollectionWrap
        {
            Items = MakeItems(("A", 1), ("B", 9)),
            Meta = new Dictionary<string, string> { ["x"] = "2" }
        };

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        SkipCollectionWrapDeepOps.ComputeDelta(a, b, ref w);

        Assert.True(doc.IsEmpty); // everything skipped

        var reader = new DeltaReader(doc);
        var target = new SkipCollectionWrap
        {
            Items = MakeItems(("A", 1), ("B", 2)),
            Meta = new Dictionary<string, string> { ["x"] = "1" }
        };

        SkipCollectionWrapDeepOps.ApplyDelta(ref target, ref reader);
        Assert.Equal(2, target.Items!.Count);
        Assert.Equal("1", target.Meta!["x"]);
    }

    // ---------- Polymorphism ----------

    [Fact]
    public void Polymorphic_Delta_On_Interface_Uses_Runtime_Dispatch()
    {
        var a = new Zoo { Pet = new Dog { Name = "fido", Bones = 1 } };
        var b = new Zoo { Pet = new Dog { Name = "fido", Bones = 2 } };

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        ZooDeepOps.ComputeDelta(a, b, ref w);

        Assert.False(doc.IsEmpty);

        var reader = new DeltaReader(doc);
        var target = new Zoo { Pet = new Dog { Name = "fido", Bones = 1 } };
        ZooDeepOps.ApplyDelta(ref target, ref reader);

        Assert.True(ZooDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void Polymorphic_Type_Change_Falls_Back_To_SetMember()
    {
        var a = new Zoo { Pet = new Dog { Name = "n", Bones = 1 } };
        var b = new Zoo { Pet = new Cat { Name = "n", Mice = 3 } };

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        ZooDeepOps.ComputeDelta(a, b, ref w);

        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.SetMember);

        var reader = new DeltaReader(doc);
        var target = new Zoo { Pet = new Dog { Name = "n", Bones = 1 } };
        ZooDeepOps.ApplyDelta(ref target, ref reader);
        Assert.True(ZooDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void Polymorphic_Unregistered_Runtime_Type_Falls_Back_To_SetMember()
    {
        var a = new Zoo { Pet = new Parrot { Name = "p", Seeds = 1 } }; // Parrot not decorated / not registered
        var b = new Zoo { Pet = new Parrot { Name = "p", Seeds = 2 } };

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        ZooDeepOps.ComputeDelta(a, b, ref w);

        // Expect SetMember fallback, not NestedMember
        Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.SetMember);
    }

    // ---------- Stability & operational semantics ----------

    [Fact]
    public void NoOps_When_Equal()
    {
        var a = NewOrder();
        var b = Clone(a);

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(a, b, ref w);

        Assert.True(doc.IsEmpty);

        var reader = new DeltaReader(doc);
        var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, ref reader);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void Apply_Ignores_Unrelated_Members()
    {
        var a = NewOrder();
        var b = Clone(a);
        b.Notes = "changed"; // only this member

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(a, b, ref w);

        var reader = new DeltaReader(doc);
        var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, ref reader);

        Assert.Equal(b.Notes, target.Notes);
        Assert.Equal(a.Id, target.Id);
        Assert.Equal(a.Items!.Count, target.Items!.Count);
        Assert.Equal(a.Customer!.Id, target.Customer!.Id);
    }


    [Fact]
    public void Null_Transitions_Across_Nested_List_Dict()
    {
        var a = new Order { Customer = null, Items = null, Meta = null };
        var b = new Order
        {
            Customer = new Customer { Id = 1, Name = "n", Home = new Address { Street = "s", City = "c" } },
            Items = MakeItems(("A", 1)),
            Meta = new Dictionary<string, string> { ["k"] = "v" }
        };

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(a, b, ref w);
        Assert.False(doc.IsEmpty);

        var reader = new DeltaReader(doc); var target = new Order();
        OrderDeepOps.ApplyDelta(ref target, ref reader);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));

        // reverse: non-null -> null
        var doc2 = new DeltaDocument(); var w2 = new DeltaWriter(doc2);
        OrderDeepOps.ComputeDelta(b, new Order(), ref w2);

        var target2 = b;
        var r2 = new DeltaReader(doc2);
        OrderDeepOps.ApplyDelta(ref target2, ref r2);
        Assert.True(OrderDeepEqual.AreDeepEqual(new Order(), target2));
    }

    [Fact]
    public void Empty_Collections_And_Dicts_Produce_Minimal_Ops()
    {
        var a = new Order { Items = new List<OrderItem>(), Meta = new Dictionary<string, string>(), Customer = null };
        var b = new Order { Items = new List<OrderItem>(), Meta = new Dictionary<string, string>(), Customer = null };

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(a, b, ref w);
        Assert.True(doc.IsEmpty);

        // add one item/key
        b.Items!.Add(new OrderItem { Sku = "A", Qty = 1 });
        b.Meta!["k"] = "v";
        var doc2 = new DeltaDocument(); var w2 = new DeltaWriter(doc2);
        OrderDeepOps.ComputeDelta(a, b, ref w2);

        Assert.Contains(doc2.Operations, o => o.Kind == DeltaKind.SeqAddAt);
        Assert.Contains(doc2.Operations, o => o.Kind == DeltaKind.DictSet);
    }

    [Fact]
    public void Large_List_Single_Middle_Change_Produces_Single_Replace()
    {
        var a = new IntListHost { Values = Enumerable.Range(0, 10_000).ToList() };
        var b = new IntListHost { Values = Enumerable.Range(0, 10_000).ToList() };
        b.Values![5_000]++;

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        IntListHostDeepOps.ComputeDelta(a, b, ref w);

        var replaces = doc.Operations.Count(o => o.Kind == DeltaKind.SeqReplaceAt);
        Assert.Equal(1, replaces);

        var reader = new DeltaReader(doc);
        var target = new IntListHost { Values = Enumerable.Range(0, 10_000).ToList() };
        IntListHostDeepOps.ApplyDelta(ref target, ref reader);
        Assert.True(IntListHostDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void Multiple_Middle_Edits_Yield_Minimal_Replaces()
    {
        var a = new IntListHost { Values = Enumerable.Range(0, 100).ToList() };
        var b = new IntListHost { Values = Enumerable.Range(0, 100).ToList() };
        b.Values![40]++; b.Values![41]++;

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        IntListHostDeepOps.ComputeDelta(a, b, ref w);

        var replaces = doc.Operations.Where(o => o.Kind == DeltaKind.SeqReplaceAt).ToList();
        Assert.Equal(2, replaces.Count);
        Assert.Contains(replaces, o => o.Index == 40);
        Assert.Contains(replaces, o => o.Index == 41);

        var reader = new DeltaReader(doc); var target = new IntListHost { Values = Enumerable.Range(0, 100).ToList() };
        IntListHostDeepOps.ApplyDelta(ref target, ref reader);
        Assert.True(IntListHostDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void Bounds_Safety_Invalid_Seq_Index_Throws()
    {
        var itemsIdx = ProbeItemsMemberIndex();

        // Craft a delta with an out-of-range remove
        var bad = new DeltaDocument(); var bw = new DeltaWriter(bad);
        bw.WriteSeqRemoveAt(itemsIdx, 999);

        var order = new Order { Items = MakeItems(("A", 1), ("B", 2)) };
        var reader = new DeltaReader(bad);

        Assert.Throws<ArgumentOutOfRangeException>(() => OrderDeepOps.ApplyDelta(ref order, ref reader));
    }

    [Fact]
    public void Unknown_Op_Kinds_Are_Ignored_Safely()
    {
        var o = NewOrder();

        // Probe any valid member index for Order so the op looks plausible
        var probeDoc = new DeltaDocument();
        var pw = new DeltaWriter(probeDoc);
        var a = NewOrder();
        var b = Clone(a); b.Notes = "changed"; // guarantee at least one op
        OrderDeepOps.ComputeDelta(a, b, ref pw);
        var anyIdx = probeDoc.Operations.First().MemberIndex;

        // Build a DeltaDocument with a single bogus op using reflection
        var doc = new DeltaDocument();

        // Grab the internal list: internal readonly List<DeltaOp> Ops
        var fi = typeof(DeltaDocument).GetField("Ops", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(fi);

        var ops = (System.Collections.IList)fi!.GetValue(doc)!;

        // Create a DeltaOp instance with an unknown kind (e.g. 999)
        var bogusOp = Activator.CreateInstance(
            typeof(DeltaOp),
            args: new object?[] { anyIdx, (DeltaKind)999, -1, null, "x", null }
        )!;

        ops.Add(bogusOp); // append bogus op

        var reader = new DeltaReader(doc);
        var ex = Record.Exception(() => OrderDeepOps.ApplyDelta(ref o, ref reader));

        // Apply should ignore unknown kinds and not throw
        Assert.Null(ex);
    }

    [Fact]
    public void ReplaceObject_Precedence_Wins_Over_Spurious_Member_Ops()
    {
        Address? left = null, right = new Address { Street = "S", City = "C" };

        // Build a doc that has a ReplaceObject, then a bogus SetMember.
        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        w.WriteReplaceObject(right);               // real replace op
        w.WriteSetMember(123, "noise");            // bogus op (won't be reached)

        var target = (Address?)null;
        var reader = new DeltaReader(doc);
        AddressDeepOps.ApplyDelta(ref target, ref reader);

        Assert.True(AddressDeepEqual.AreDeepEqual(right, target));
    }

    [Fact]
    public void DeltaReader_Partial_Consumption_Does_Not_Block_Apply()
    {
        var a = NewOrder();
        var b = Clone(a); b.Notes = "X"; b.Id = 99;

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(a, b, ref w);

        var reader = new DeltaReader(doc);
        // Partially enumerate one member (consume nothing or iterate)
        foreach (var op in reader.EnumerateMember(doc.Operations[0].MemberIndex)) { /* ignore */ break; }

        // Now apply with same reader
        var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, ref reader);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void Diff_And_Delta_Consistency()
    {
        var a = NewOrder();
        var b = Clone(a); b.Notes = "changed";

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(a, b, ref w);
        Assert.False(doc.IsEmpty);

        Assert.True(OrderDeepOps.TryGetDiff(a, b, out var diff));
        Assert.True(diff.HasChanges);
    }

    [Fact]
    public void RoundTrip_Fuzzed_Random_Changes()
    {
        var rng = new Random(123);
        for (int iter = 0; iter < 20; iter++)
        {
            var a = NewOrder();
            var b = Clone(a);

            // randomize items
            for (int i = 0; i < b.Items!.Count; i++)
            {
                if (rng.NextDouble() < 0.3) b.Items[i].Qty += rng.Next(1, 3);
            }
            // add/remove sometimes
            if (rng.NextDouble() < 0.3) b.Items!.Add(new OrderItem { Sku = "R" + rng.Next(100), Qty = 1 });
            if (rng.NextDouble() < 0.3 && b.Items!.Count > 0) b.Items!.RemoveAt(rng.Next(b.Items.Count));

            // dict tweaks
            if (rng.NextDouble() < 0.5) b.Meta!["who"] = "u" + rng.Next(10);
            if (rng.NextDouble() < 0.2) b.Meta!.Remove("env");

            var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
            OrderDeepOps.ComputeDelta(a, b, ref w);

            var target = Clone(a);
            var reader = new DeltaReader(doc);
            OrderDeepOps.ApplyDelta(ref target, ref reader);

            Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
        }
    }

    [Fact]
    public void ModuleInitializer_Works_Without_WarmUp()
    {
        var a = new ModuleInitFoo { V = "a" };
        var b = new ModuleInitFoo { V = "b" };

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        // No WarmUp for ModuleInitFoo — module initializer should run automatically
        ModuleInitFooDeepOps.ComputeDelta(a, b, ref w);
        var reader = new DeltaReader(doc);
        var target = new ModuleInitFoo { V = "a" };
        ModuleInitFooDeepOps.ApplyDelta(ref target, ref reader);
        Assert.True(ModuleInitFooDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void Sealed_Types_Nested_Delta_Correctness()
    {
        var a = new SealedThing { A = 1, Addr = new Address { Street = "s", City = "c" } };
        var b = new SealedThing { A = 2, Addr = new Address { Street = "sx", City = "c" } };

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        SealedThingDeepOps.ComputeDelta(a, b, ref w);

        Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.SetMember);           // for A
        Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.NestedMember);       // for Addr

        var reader = new DeltaReader(doc); var target = new SealedThing { A = 1, Addr = new Address { Street = "s", City = "c" } };
        SealedThingDeepOps.ApplyDelta(ref target, ref reader);
        Assert.True(SealedThingDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void ThreadSafety_Compute_And_Apply_In_Parallel()
    {
        var pairs = Enumerable.Range(0, 200).Select(_ =>
        {
            var a = NewOrder();
            var b = Clone(a);
            b.Items![^1].Qty++;
            return (a, b);
        }).ToArray();

        var errors = new ConcurrentBag<Exception>();
        Parallel.ForEach(pairs, p =>
        {
            try
            {
                var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
                OrderDeepOps.ComputeDelta(p.a, p.b, ref w);
                var t = Clone(p.a);
                var r = new DeltaReader(doc);
                OrderDeepOps.ApplyDelta(ref t, ref r);
                if (!OrderDeepEqual.AreDeepEqual(p.b, t)) throw new InvalidOperationException("Mismatch");
            }
            catch (Exception ex) { errors.Add(ex); }
        });

        Assert.True(errors.IsEmpty, string.Join(Environment.NewLine, errors.Select(e => e.Message)));
    }

    [Fact]
    public void DeltaDocument_Can_Be_Enumerated_Multiple_Times()
    {
        var a = NewOrder(); var b = Clone(a); b.Notes = "x"; b.Id = 99;

        var doc = new DeltaDocument(); var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(a, b, ref w);

        // Enumerate twice
        var c1 = doc.Operations.Count;
        var c2 = doc.Operations.Count;
        Assert.Equal(c1, c2);

        // Reader over same doc used twice
        var r1 = new DeltaReader(doc);
        var ops1 = r1.EnumerateAll().Count();
        var ops2 = r1.EnumerateAll().Count();
        Assert.Equal(ops1, ops2);
    }

    [Fact]
    public void BackCompat_SetMember_Only_Deltas_Still_Apply()
    {
        var a = NewOrder();
        var b = Clone(a);
        b.Items = MakeItems(("A", 1), ("B", 2));
        b.Meta = new Dictionary<string, string> { ["k"] = "v" };

        var itemsIdx = ProbeItemsMemberIndex();
        var metaIdx = ProbeMetaMemberIndex();

        // Build legacy delta (SetMember only) using the writer
        var legacy = new DeltaDocument();
        var lw = new DeltaWriter(legacy);
        lw.WriteSetMember(itemsIdx, b.Items);
        lw.WriteSetMember(metaIdx, b.Meta);

        var target = Clone(a);
        var reader = new DeltaReader(legacy);
        OrderDeepOps.ApplyDelta(ref target, ref reader);

        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

}