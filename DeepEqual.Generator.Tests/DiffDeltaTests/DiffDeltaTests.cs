using DeepEqual.Generator.Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DeepEqual.Generator.Tests.DiffDeltaTests;


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

    public List<OrderItem>? Items { get; set; }

    public Dictionary<string, string>? Meta { get; set; }

    public string? Notes { get; set; }
}

public interface IAnimal { string? Name { get; set; } }

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class Dog : IAnimal { public string? Name { get; set; } public int Bones { get; set; } }

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


[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class DictHost
{
    public Dictionary<string, Address>? Map { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class ReadOnlyListHost
{
    public IReadOnlyList<OrderItem>? Items { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class ReadOnlyDictHost
{
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


public class DiffDeltaFullSuite
{
    public DiffDeltaFullSuite()
    {
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

    private static int ProbeItemsMemberIndex()
    {
        var a = new Order { Items = new List<OrderItem> { new() { Sku = "A", Qty = 1 }, new() { Sku = "B", Qty = 2 } } };
        var b = new Order { Items = new List<OrderItem> { new() { Sku = "A", Qty = 1 }, new() { Sku = "B", Qty = 9 } } }; // change forces diff

        var doc = OrderDeepOps.ComputeDelta(a, b);
        var seq = doc.Operations.FirstOrDefault(op =>
            (op.Kind == DeltaKind.SeqReplaceAt || op.Kind == DeltaKind.SeqAddAt || op.Kind == DeltaKind.SeqRemoveAt));
        if (seq.Kind == DeltaKind.SeqReplaceAt || seq.Kind == DeltaKind.SeqAddAt || seq.Kind == DeltaKind.SeqRemoveAt)
            return seq.MemberIndex;

        var set = doc.Operations.First(op => op.Kind == DeltaKind.SetMember && op.Value is List<OrderItem>);
        return set.MemberIndex;
    }

    private static int ProbeMetaMemberIndex()
    {
        var a = new Order { Meta = new Dictionary<string, string> { ["k"] = "1" } };
        var b = new Order { Meta = new Dictionary<string, string> { ["k"] = "2" } };

        var doc = OrderDeepOps.ComputeDelta(a, b);

        var dict = doc.Operations.FirstOrDefault(op =>
            op.Kind == DeltaKind.DictSet || op.Kind == DeltaKind.DictRemove || op.Kind == DeltaKind.DictNested);
        if (dict.Kind == DeltaKind.DictSet || dict.Kind == DeltaKind.DictRemove || dict.Kind == DeltaKind.DictNested)
            return dict.MemberIndex;

        var set = doc.Operations.First(op => op.Kind == DeltaKind.SetMember && op.Value is Dictionary<string, string>);
        return set.MemberIndex;
    }


    [Fact]
    public void Diff_ValueLike_NoChange_NoDiff()
    {
        var a = new Address { Street = "S", City = "C" };
        var b = new Address { Street = "S", City = "C" };

        var (hasDiff, diff) = AddressDeepOps.GetDiff(a, b);
        Assert.False(hasDiff);
        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void Diff_ValueLike_Change_Records_Set()
    {
        var a = new Address { Street = "S", City = "C1" };
        var b = new Address { Street = "S", City = "C2" };

        var (hasDiff, diff) = AddressDeepOps.GetDiff(a, b);
        Assert.True(hasDiff);
        Assert.True(diff.HasChanges);
        Assert.Contains(diff.MemberChanges!, mc => mc.Kind == MemberChangeKind.Set);
    }

    [Fact]
    public void Diff_NestedObject_Records_Nested()
    {
        var a = new Customer { Id = 1, Name = "A", Home = new Address { Street = "S1", City = "C" } };
        var b = new Customer { Id = 1, Name = "A", Home = new Address { Street = "S2", City = "C" } };

        var (hasDiff, diff) = CustomerDeepOps.GetDiff(a, b);
        Assert.True(hasDiff);
        Assert.Contains(diff.MemberChanges!, mc => mc.Kind == MemberChangeKind.Nested);
    }

    [Fact]
    public void Diff_Collections_Granular_Still_Registers_Change()
    {
        var a = NewOrder();
        var b = Clone(a);
        b.Items![1].Qty++;

        var (hasDiff, diff) = OrderDeepOps.GetDiff(a, b);
        Assert.True(hasDiff);
        Assert.True(diff.HasChanges);
    }


    [Fact]
    public void Delta_ReplaceObject_NullToObj()
    {
        Address? left = null, right = new Address { Street = "S", City = "C" };

        var doc = AddressDeepOps.ComputeDelta(left, right);
        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.ReplaceObject && op.Value is Address);

        Address? target = null;
        AddressDeepOps.ApplyDelta(ref target, doc);

        Assert.True(AddressDeepEqual.AreDeepEqual(right, target));
    }

    [Fact]
    public void Delta_ReplaceObject_ObjToNull()
    {
        Address? left = new Address { Street = "S", City = "C" };
        Address? right = null;

        var doc = AddressDeepOps.ComputeDelta(left, right);
        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.ReplaceObject && op.Value is null);

        Address? target = new Address { Street = "X", City = "Y" };
        AddressDeepOps.ApplyDelta(ref target, doc);

        Assert.Null(target);
    }


    [Fact]
    public void List_One_Element_Changed_Emits_Single_SeqReplaceAt_And_Applies()
    {
        var a = NewOrder();
        var b = Clone(a);
        b.Items![1].Qty++;

        var doc = OrderDeepOps.ComputeDelta(a, b);

        var repl = doc.Operations.Single(op => op.Kind == DeltaKind.SeqReplaceAt);
        Assert.Equal(1, repl.Index);

        var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, doc);

        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void List_Add_In_Middle_Emits_SeqAddAt_And_Applies()
    {
        var a = NewOrder();
        var b = Clone(a);
        b.Items!.Insert(1, new OrderItem { Sku = "ADD", Qty = 9 });

        var doc = OrderDeepOps.ComputeDelta(a, b);
        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.SeqAddAt && op.Index == 1);

        var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, doc);

        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void List_Remove_In_Middle_Emits_SeqRemoveAt_And_Applies()
    {
        var a = NewOrder();
        var b = Clone(a);
        b.Items!.RemoveAt(1);

        var doc = OrderDeepOps.ComputeDelta(a, b);
        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.SeqRemoveAt && op.Index == 1);

        var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, doc);

        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void List_Add_Head_And_Tail_Then_Apply()
    {
        var a = NewOrder();
        var b = Clone(a);
        b.Items!.Insert(0, new OrderItem { Sku = "HEAD", Qty = 7 });
        b.Items!.Add(new OrderItem { Sku = "TAIL", Qty = 8 });

        var doc = OrderDeepOps.ComputeDelta(a, b);
        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.SeqAddAt && op.Index == b.Items!.Count - 1);

        Assert.True(
            doc.Operations.Any(op => op.Kind == DeltaKind.SeqAddAt && op.Index == 0) ||
            doc.Operations.Any(op => op.Kind == DeltaKind.SeqReplaceAt && op.Index == 0)
        );

        var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, doc);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void List_Remove_Head_And_Tail_Then_Apply()
    {
        var a = NewOrder();
        var b = Clone(a);
        b.Items!.RemoveAt(0);
        b.Items!.RemoveAt(b.Items!.Count - 1);

        var doc = OrderDeepOps.ComputeDelta(NewOrder(), b);
        Assert.True(doc.Operations.Count(op => op.Kind == DeltaKind.SeqRemoveAt) >= 2);

        var target = NewOrder();
        OrderDeepOps.ApplyDelta(ref target, doc);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void List_Duplicate_Values_Replaces_By_Index()
    {
        var a = NewOrder();
        a.Items = MakeItems(("X", 1), ("X", 1), ("X", 1));
        var b = Clone(a);
        b.Items![1].Qty = 99; // change middle duplicate

        var doc = OrderDeepOps.ComputeDelta(a, b);

        var ops = doc.Operations.Where(o => o.Kind == DeltaKind.SeqReplaceAt).ToList();
        Assert.Single(ops);
        Assert.Equal(1, ops[0].Index);

        var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, doc);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void List_PrefixSuffix_Trim_Minimizes_Ops()
    {
        var a = NewOrder();
        var b = Clone(a);
        b.Items![1].Sku = "MID";

        var doc = OrderDeepOps.ComputeDelta(a, b);
        Assert.Single(doc.Operations.Where(op => op.Kind == DeltaKind.SeqReplaceAt));

        var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, doc);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void List_All_Changed_Falls_Back_To_Replaces_Not_AddRemoves()
    {
        var a = NewOrder();
        var b = Clone(a);
        for (int i = 0; i < b.Items!.Count; i++) b.Items[i].Qty++;

        var doc = OrderDeepOps.ComputeDelta(a, b);
        Assert.True(doc.Operations.All(op => op.Kind == DeltaKind.SeqReplaceAt));

        var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, doc);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void List_Mixed_Ops_Add_Remove_Replace_Applies_In_Correct_Order()
    {
        var a = new Order { Items = MakeItems(("A", 1), ("B", 2), ("C", 3), ("D", 4)) };
        var b = new Order { Items = MakeItems(("A", 1), ("BX", 2), ("C", 3), ("E", 5)) }; // replace B->BX, remove D, add E at tail

        var doc = OrderDeepOps.ComputeDelta(a, b);

        Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.SeqReplaceAt && o.Index == 1);

        Assert.True(
            doc.Operations.Any(o => o.Kind == DeltaKind.SeqRemoveAt) ||
            doc.Operations.Any(o => o.Kind == DeltaKind.SeqAddAt) ||
            doc.Operations.Any(o => o.Kind == DeltaKind.SeqReplaceAt && o.Index >= 3)
        );

        var target = new Order { Items = MakeItems(("A", 1), ("B", 2), ("C", 3), ("D", 4)) };
        OrderDeepOps.ApplyDelta(ref target, doc);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void List_Operation_Ordering_Stability()
    {
        var a = new Order { Items = MakeItems(("A", 1), ("B", 2), ("C", 3), ("D", 4), ("E", 5)) };
        var b = new Order { Items = MakeItems(("A", 1), ("BX", 2), ("C", 3), ("E", 5), ("F", 6)) }; // replace B, remove D, add F at tail

        var doc = OrderDeepOps.ComputeDelta(a, b);

        var removes = doc.Operations.Where(o => o.Kind == DeltaKind.SeqRemoveAt).Select(o => o.Index).ToList();
        var adds = doc.Operations.Where(o => o.Kind == DeltaKind.SeqAddAt).Select(o => o.Index).ToList();

        Assert.True(removes.SequenceEqual(removes.OrderByDescending(x => x))); // removes descending
        Assert.True(adds.SequenceEqual(adds.OrderBy(x => x)));                 // adds ascending

        var target = a;
        OrderDeepOps.ApplyDelta(ref target, doc);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void List_ReadOnly_Typed_Property_Falls_Back_To_SetMember()
    {
        var a = new ReadOnlyListHost { Items = MakeItems(("A", 1), ("B", 2)).AsReadOnly() };
        var b = new ReadOnlyListHost { Items = MakeItems(("A", 1), ("B", 9)).AsReadOnly() };

        var doc = ReadOnlyListHostDeepOps.ComputeDelta(a, b);

        Assert.DoesNotContain(doc.Operations, o => o.Kind == DeltaKind.SeqReplaceAt || o.Kind == DeltaKind.SeqAddAt || o.Kind == DeltaKind.SeqRemoveAt);
        Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.SetMember);

        var target = new ReadOnlyListHost { Items = MakeItems(("A", 1), ("B", 2)).AsReadOnly() };
        ReadOnlyListHostDeepOps.ApplyDelta(ref target, doc);
        Assert.True(ReadOnlyListHostDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void List_Idempotence_SetMember_Is_Idempotent()
    {
        var a = new WithArray { Values = new[] { 1, 2, 3 } };
        var b = new WithArray { Values = new[] { 1, 9, 3 } };

        var doc = WithArrayDeepOps.ComputeDelta(a, b);

        var target = new WithArray { Values = new[] { 1, 2, 3 } };
        WithArrayDeepOps.ApplyDelta(ref target, doc);

        WithArrayDeepOps.ApplyDelta(ref target, doc);
        Assert.True(WithArrayDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void List_Idempotence_Add_Is_Not_Idempotent()
    {
        var a = new Order { Items = MakeItems(("A", 1), ("B", 2)) };
        var b = new Order { Items = MakeItems(("A", 1), ("B", 2), ("C", 3)) };

        var doc = OrderDeepOps.ComputeDelta(a, b);

        var t1 = new Order { Items = MakeItems(("A", 1), ("B", 2)) };
        OrderDeepOps.ApplyDelta(ref t1, doc);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, t1));

        OrderDeepOps.ApplyDelta(ref t1, doc);
        Assert.NotEqual(b.Items!.Count, t1.Items!.Count);
    }

    [Fact]
    public void List_Mismatch_Target_Not_BaseOfPatch_Does_Not_Throw_For_Replaces()
    {
        var a = new Order { Items = MakeItems(("A", 1), ("B", 2), ("C", 3)) };
        var b = new Order { Items = MakeItems(("A", 1), ("BX", 2), ("C", 3)) };

        var doc = OrderDeepOps.ComputeDelta(a, b);

        var target = new Order { Items = MakeItems(("A", 1), ("Q", 5), ("C", 3)) };
        var ex = Record.Exception(() => OrderDeepOps.ApplyDelta(ref target, doc));
        Assert.Null(ex);
        Assert.Equal("BX", target.Items![1].Sku);
    }


    [Fact]
    public void Dict_Value_Change_Emits_DictSet_And_Applies()
    {
        var a = NewOrder();
        var b = Clone(a);
        b.Meta!["who"] = "z";

        var doc = OrderDeepOps.ComputeDelta(a, b);

        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.DictSet && (string)op.Key! == "who" && (string?)op.Value == "z");

        var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, doc);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void Dict_Add_And_Remove_Emit_Ops_And_Apply()
    {
        var a = NewOrder();
        var b = Clone(a);
        b.Meta!.Remove("env");
        b.Meta!["new"] = "v";

        var doc = OrderDeepOps.ComputeDelta(a, b);

        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.DictRemove && (string)op.Key! == "env");
        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.DictSet && (string)op.Key! == "new" && (string?)op.Value == "v");

        var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, doc);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void Dict_Nested_Object_Value_Uses_DictNested_Mutates_Instance()
    {
        var a = new PolyDictHost { Pets = new Dictionary<string, IAnimal> { ["d"] = new Dog { Name = "f", Bones = 1 } } };
        var b = new PolyDictHost { Pets = new Dictionary<string, IAnimal> { ["d"] = new Dog { Name = "f", Bones = 2 } } };

        var doc = PolyDictHostDeepOps.ComputeDelta(a, b);

        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.DictNested && (string)op.Key! == "d");

        var target = new PolyDictHost { Pets = new Dictionary<string, IAnimal> { ["d"] = new Dog { Name = "f", Bones = 1 } } };
        var beforeRef = target.Pets!["d"];
        PolyDictHostDeepOps.ApplyDelta(ref target, doc);

        Assert.True(object.ReferenceEquals(beforeRef, target.Pets!["d"]));
        Assert.Equal(2, ((Dog)target.Pets["d"]).Bones);
    }

    [Fact]
    public void Dict_Custom_CaseInsensitive_KeyComparer_Respected_On_Apply()
    {
        var a = new CaseDictHost { Meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["who"] = "me" } };
        var b = new CaseDictHost { Meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["WHO"] = "you" } };

        var doc = CaseDictHostDeepOps.ComputeDelta(a, b);

        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.DictSet && ((string)op.Key!).Equals("WHO", StringComparison.OrdinalIgnoreCase));

        var target = new CaseDictHost { Meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["who"] = "me" } };
        CaseDictHostDeepOps.ApplyDelta(ref target, doc);

        Assert.True(target.Meta!.ContainsKey("who"));
        Assert.Equal("you", target.Meta["who"]);
    }

    [Fact]
    public void Dict_Remove_On_Missing_Key_Is_Ignored_Safely()
    {
        var host = new CaseDictHost { Meta = new Dictionary<string, string> { ["a"] = "1" } };

        var a = new CaseDictHost { Meta = new Dictionary<string, string> { ["x"] = "1" } };
        var b = new CaseDictHost { Meta = new Dictionary<string, string> { ["y"] = "2" } };
        var probeDoc = CaseDictHostDeepOps.ComputeDelta(a, b);

        var metaIdx = probeDoc.Operations
            .First(op => op.Kind is DeltaKind.DictSet or DeltaKind.DictRemove).MemberIndex;

        var legacy = new DeltaDocument();
        var lw = new DeltaWriter(legacy);
        lw.WriteDictRemove(metaIdx, "missing");

        var ex = Record.Exception(() => CaseDictHostDeepOps.ApplyDelta(ref host, legacy));
        Assert.Null(ex);
        Assert.Equal("1", host.Meta!["a"]);
    }

    [Fact]
    public void Dict_Idempotence_DictSet_Is_Idempotent()
    {
        var a = new CaseDictHost { Meta = new Dictionary<string, string> { ["a"] = "1" } };
        var b = new CaseDictHost { Meta = new Dictionary<string, string> { ["a"] = "2" } };

        var doc = CaseDictHostDeepOps.ComputeDelta(a, b);

        var t = new CaseDictHost { Meta = new Dictionary<string, string> { ["a"] = "1" } };
        CaseDictHostDeepOps.ApplyDelta(ref t, doc);
        CaseDictHostDeepOps.ApplyDelta(ref t, doc);

        Assert.True(CaseDictHostDeepEqual.AreDeepEqual(b, t));
    }


    [Fact]
    public void Arrays_Fallback_To_SetMember()
    {
        var a = new WithArray { Values = new[] { 1, 2, 3 } };
        var b = new WithArray { Values = new[] { 1, 9, 3 } };

        var doc = WithArrayDeepOps.ComputeDelta(a, b);

        Assert.DoesNotContain(doc.Operations, op =>
            op.Kind == DeltaKind.SeqAddAt || op.Kind == DeltaKind.SeqRemoveAt || op.Kind == DeltaKind.SeqReplaceAt);

        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.SetMember);

        var target = new WithArray { Values = new[] { 1, 2, 3 } };
        WithArrayDeepOps.ApplyDelta(ref target, doc);
        Assert.True(WithArrayDeepEqual.AreDeepEqual(b, target));
    }


    [Fact]
    public void DeltaShallow_Produces_Set_Not_Nested()
    {
        var a = new ShallowWrap { Addr = new Address { Street = "A", City = "C" } };
        var b = new ShallowWrap { Addr = new Address { Street = "B", City = "C" } };

        var doc = ShallowWrapDeepOps.ComputeDelta(a, b);

        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.SetMember);
        Assert.DoesNotContain(doc.Operations, op => op.Kind == DeltaKind.NestedMember);

        var target = new ShallowWrap { Addr = new Address { Street = "A", City = "C" } };
        ShallowWrapDeepOps.ApplyDelta(ref target, doc);
        Assert.True(ShallowWrapDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void DeltaSkip_Member_Is_Not_Emitted_And_Not_Applied()
    {
        var a = new SkipWrap { Ignored = "x", Tracked = "t1" };
        var b = new SkipWrap { Ignored = "y", Tracked = "t2" };

        var doc = SkipWrapDeepOps.ComputeDelta(a, b);

        Assert.DoesNotContain(doc.Operations, op => op.Kind == DeltaKind.SetMember && (op.Value as string) == "y");

        var target = new SkipWrap { Ignored = "x", Tracked = "t1" };
        SkipWrapDeepOps.ApplyDelta(ref target, doc);

        Assert.Equal("x", target.Ignored);
        Assert.Equal("t2", target.Tracked);
    }

    [Fact]
    public void DeltaShallow_On_Collection_Forces_SetMember()
    {
        var a = new ShallowCollectionWrap { Items = MakeItems(("A", 1), ("B", 2)) };
        var b = new ShallowCollectionWrap { Items = MakeItems(("A", 1), ("B", 9)) };

        var doc = ShallowCollectionWrapDeepOps.ComputeDelta(a, b);

        Assert.DoesNotContain(doc.Operations, o =>
            o.Kind == DeltaKind.SeqAddAt ||
            o.Kind == DeltaKind.SeqRemoveAt ||
            o.Kind == DeltaKind.SeqReplaceAt ||
            o.Kind == DeltaKind.DictSet ||
            o.Kind == DeltaKind.DictRemove ||
            o.Kind == DeltaKind.DictNested);

        Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.SetMember);

        var target = new ShallowCollectionWrap { Items = MakeItems(("A", 1), ("B", 2)) };
        ShallowCollectionWrapDeepOps.ApplyDelta(ref target, doc);

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

        var doc = SkipCollectionWrapDeepOps.ComputeDelta(a, b);

        Assert.True(doc.IsEmpty);

        var target = new SkipCollectionWrap
        {
            Items = MakeItems(("A", 1), ("B", 2)),
            Meta = new Dictionary<string, string> { ["x"] = "1" }
        };

        SkipCollectionWrapDeepOps.ApplyDelta(ref target, doc);
        Assert.Equal(2, target.Items!.Count);
        Assert.Equal("1", target.Meta!["x"]);
    }


    [Fact]
    public void Polymorphic_Delta_On_Interface_Uses_Runtime_Dispatch()
    {
        var a = new Zoo { Pet = new Dog { Name = "fido", Bones = 1 } };
        var b = new Zoo { Pet = new Dog { Name = "fido", Bones = 2 } };

        var doc = ZooDeepOps.ComputeDelta(a, b);
        Assert.False(doc.IsEmpty);

        var target = new Zoo { Pet = new Dog { Name = "fido", Bones = 1 } };
        ZooDeepOps.ApplyDelta(ref target, doc);

        Assert.True(ZooDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void Polymorphic_Type_Change_Falls_Back_To_SetMember()
    {
        var a = new Zoo { Pet = new Dog { Name = "n", Bones = 1 } };
        var b = new Zoo { Pet = new Cat { Name = "n", Mice = 3 } };

        var doc = ZooDeepOps.ComputeDelta(a, b);
        Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.SetMember);

        var target = new Zoo { Pet = new Dog { Name = "n", Bones = 1 } };
        ZooDeepOps.ApplyDelta(ref target, doc);
        Assert.True(ZooDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void Polymorphic_Unregistered_Runtime_Type_Falls_Back_To_SetMember()
    {
        var a = new Zoo { Pet = new Parrot { Name = "p", Seeds = 1 } }; // Parrot not decorated / not registered
        var b = new Zoo { Pet = new Parrot { Name = "p", Seeds = 2 } };

        var doc = ZooDeepOps.ComputeDelta(a, b);
        Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.SetMember);
    }


    [Fact]
    public void NoOps_When_Equal()
    {
        var a = NewOrder();
        var b = Clone(a);

        var doc = OrderDeepOps.ComputeDelta(a, b);
        Assert.True(doc.IsEmpty);

        var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, doc);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void Apply_Ignores_Unrelated_Members()
    {
        var a = NewOrder();
        var b = Clone(a);
        b.Notes = "changed"; // only this member

        var doc = OrderDeepOps.ComputeDelta(a, b);

        var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, doc);

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

        var doc = OrderDeepOps.ComputeDelta(a, b);
        Assert.False(doc.IsEmpty);

        var target = new Order();
        OrderDeepOps.ApplyDelta(ref target, doc);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));

        var doc2 = OrderDeepOps.ComputeDelta(b, new Order());
        var target2 = b;
        OrderDeepOps.ApplyDelta(ref target2, doc2);
        Assert.True(OrderDeepEqual.AreDeepEqual(new Order(), target2));
    }

    [Fact]
    public void Empty_Collections_And_Dicts_Produce_Minimal_Ops()
    {
        var a = new Order { Items = new List<OrderItem>(), Meta = new Dictionary<string, string>(), Customer = null };
        var b = new Order { Items = new List<OrderItem>(), Meta = new Dictionary<string, string>(), Customer = null };

        var doc = OrderDeepOps.ComputeDelta(a, b);
        Assert.True(doc.IsEmpty);

        b.Items!.Add(new OrderItem { Sku = "A", Qty = 1 });
        b.Meta!["k"] = "v";
        var doc2 = OrderDeepOps.ComputeDelta(a, b);

        Assert.Contains(doc2.Operations, o => o.Kind == DeltaKind.SeqAddAt);
        Assert.Contains(doc2.Operations, o => o.Kind == DeltaKind.DictSet);
    }

    [Fact]
    public void Large_List_Single_Middle_Change_Produces_Single_Replace()
    {
        var a = new IntListHost { Values = Enumerable.Range(0, 10_000).ToList() };
        var b = new IntListHost { Values = Enumerable.Range(0, 10_000).ToList() };
        b.Values![5_000]++;

        var doc = IntListHostDeepOps.ComputeDelta(a, b);

        var replaces = doc.Operations.Count(o => o.Kind == DeltaKind.SeqReplaceAt);
        Assert.Equal(1, replaces);

        var target = new IntListHost { Values = Enumerable.Range(0, 10_000).ToList() };
        IntListHostDeepOps.ApplyDelta(ref target, doc);
        Assert.True(IntListHostDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void Multiple_Middle_Edits_Yield_Minimal_Replaces()
    {
        var a = new IntListHost { Values = Enumerable.Range(0, 100).ToList() };
        var b = new IntListHost { Values = Enumerable.Range(0, 100).ToList() };
        b.Values![40]++; b.Values![41]++;

        var doc = IntListHostDeepOps.ComputeDelta(a, b);

        var replaces = doc.Operations.Where(o => o.Kind == DeltaKind.SeqReplaceAt).ToList();
        Assert.Equal(2, replaces.Count);
        Assert.Contains(replaces, o => o.Index == 40);
        Assert.Contains(replaces, o => o.Index == 41);

        var target = new IntListHost { Values = Enumerable.Range(0, 100).ToList() };
        IntListHostDeepOps.ApplyDelta(ref target, doc);
        Assert.True(IntListHostDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void Bounds_Safety_Invalid_Seq_Index_Throws()
    {
        var itemsIdx = ProbeItemsMemberIndex();

        var bad = new DeltaDocument(); var bw = new DeltaWriter(bad);
        bw.WriteSeqRemoveAt(itemsIdx, 999);

        var order = new Order { Items = MakeItems(("A", 1), ("B", 2)) };

        Assert.Throws<ArgumentOutOfRangeException>(() => OrderDeepOps.ApplyDelta(ref order, bad));
    }

    [Fact]
    public void Unknown_Op_Kinds_Are_Ignored_Safely()
    {
        var o = NewOrder();

        var a = NewOrder();
        var b = Clone(a); b.Notes = "changed"; // guarantee at least one op
        var probe = OrderDeepOps.ComputeDelta(a, b);
        var anyIdx = probe.Operations.First().MemberIndex;

        var doc = new DeltaDocument();

        var fi = typeof(DeltaDocument).GetField("Ops", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(fi);

        var ops = (System.Collections.IList)fi!.GetValue(doc)!;

        var bogusOp = Activator.CreateInstance(
            typeof(DeltaOp),
            args: new object?[] { anyIdx, (DeltaKind)999, -1, null, "x", null }
        )!;

        ops.Add(bogusOp); // append bogus op

        var ex = Record.Exception(() => OrderDeepOps.ApplyDelta(ref o, doc));

        Assert.Null(ex);
    }

    [Fact]
    public void ReplaceObject_Precedence_Wins_Over_Spurious_Member_Ops()
    {
        Address? left = null, right = new Address { Street = "S", City = "C" };

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        w.WriteReplaceObject(right);               // real replace op
        w.WriteSetMember(123, "noise");            // bogus op (won't be reached)

        var target = (Address?)null;
        AddressDeepOps.ApplyDelta(ref target, doc);

        Assert.True(AddressDeepEqual.AreDeepEqual(right, target));
    }

    [Fact]
    public void DeltaReader_Partial_Consumption_Does_Not_Block_Apply()
    {
        var a = NewOrder();
        var b = Clone(a); b.Notes = "X"; b.Id = 99;

        var doc = OrderDeepOps.ComputeDelta(a, b);

        var reader = new DeltaReader(doc);
        foreach (var op in reader.EnumerateMember(doc.Operations[0].MemberIndex)) { break; }

        var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, doc);
        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void Diff_And_Delta_Consistency()
    {
        var a = NewOrder();
        var b = Clone(a); b.Notes = "changed";

        var doc = OrderDeepOps.ComputeDelta(a, b);
        Assert.False(doc.IsEmpty);

        var (hasDiff, diff) = OrderDeepOps.GetDiff(a, b);
        Assert.True(hasDiff);
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

            for (int i = 0; i < b.Items!.Count; i++)
            {
                if (rng.NextDouble() < 0.3) b.Items[i].Qty += rng.Next(1, 3);
            }
            if (rng.NextDouble() < 0.3) b.Items!.Add(new OrderItem { Sku = "R" + rng.Next(100), Qty = 1 });
            if (rng.NextDouble() < 0.3 && b.Items!.Count > 0) b.Items!.RemoveAt(rng.Next(b.Items.Count));

            if (rng.NextDouble() < 0.5) b.Meta!["who"] = "u" + rng.Next(10);
            if (rng.NextDouble() < 0.2) b.Meta!.Remove("env");

            var doc = OrderDeepOps.ComputeDelta(a, b);

            var target = Clone(a);
            OrderDeepOps.ApplyDelta(ref target, doc);

            Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
        }
    }

    [Fact]
    public void ModuleInitializer_Works_Without_WarmUp()
    {
        var a = new ModuleInitFoo { V = "a" };
        var b = new ModuleInitFoo { V = "b" };

        var doc = ModuleInitFooDeepOps.ComputeDelta(a, b);
        var target = new ModuleInitFoo { V = "a" };
        ModuleInitFooDeepOps.ApplyDelta(ref target, doc);
        Assert.True(ModuleInitFooDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void Sealed_Types_Nested_Delta_Correctness()
    {
        var a = new SealedThing { A = 1, Addr = new Address { Street = "s", City = "c" } };
        var b = new SealedThing { A = 2, Addr = new Address { Street = "sx", City = "c" } };

        var doc = SealedThingDeepOps.ComputeDelta(a, b);

        Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.SetMember);           // for A
        Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.NestedMember);       // for Addr

        var target = new SealedThing { A = 1, Addr = new Address { Street = "s", City = "c" } };
        SealedThingDeepOps.ApplyDelta(ref target, doc);
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
                var doc = OrderDeepOps.ComputeDelta(p.a, p.b);
                var t = Clone(p.a);
                OrderDeepOps.ApplyDelta(ref t, doc);
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

        var doc = OrderDeepOps.ComputeDelta(a, b);

        var c1 = doc.Operations.Count;
        var c2 = doc.Operations.Count;
        Assert.Equal(c1, c2);

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

        var legacy = new DeltaDocument();
        var lw = new DeltaWriter(legacy);
        lw.WriteSetMember(itemsIdx, b.Items);
        lw.WriteSetMember(metaIdx, b.Meta);

        var target = Clone(a);
        OrderDeepOps.ApplyDelta(ref target, legacy);

        Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
    }

    [DeepComparable(GenerateDiff = true, GenerateDelta = true)]
    public sealed class ReadOnlyDictGranularHost
    {
        public IReadOnlyDictionary<string, string>? Meta { get; set; }
    }

    [DeepComparable(GenerateDiff = true, GenerateDelta = true)]
    public sealed class ReadOnlyDictNestedHost
    {
        public IReadOnlyDictionary<string, Address>? Map { get; set; }
    }

    public sealed class ReadOnlyDictGranularTests
    {
        public ReadOnlyDictGranularTests()
        {
            GeneratedHelperRegistry.WarmUp(typeof(ReadOnlyDictGranularHost));
            GeneratedHelperRegistry.WarmUp(typeof(ReadOnlyDictNestedHost));
            GeneratedHelperRegistry.WarmUp(typeof(Address));
        }

        private static IReadOnlyDictionary<string, string> RO(params (string k, string v)[] xs)
            => new Dictionary<string, string>(xs.ToDictionary(p => p.k, p => p.v));

        private static IReadOnlyDictionary<string, Address> RO(params (string k, Address v)[] xs)
            => new Dictionary<string, Address>(xs.ToDictionary(p => p.k, p => p.v));

        private static IReadOnlyDictionary<string, string> ROD(params (string k, string v)[] xs)
            => new ReadOnlyDictionary<string, string>(xs.ToDictionary(p => p.k, p => p.v));

        [Fact]
        public void IReadOnlyDictionary_Value_Change_Emits_DictSet_And_Applies_By_Clone()
        {
            var a = new ReadOnlyDictGranularHost { Meta = RO(("who", "me"), ("env", "test")) };
            var b = new ReadOnlyDictGranularHost { Meta = RO(("who", "you"), ("env", "test")) }; // only 'who' changes

            var doc = ReadOnlyDictGranularHostDeepOps.ComputeDelta(a, b);

            Assert.Contains(doc.Operations, o =>
                o.Kind == DeltaKind.DictSet && (string)o.Key! == "who" && (string?)o.Value == "you");
            Assert.DoesNotContain(doc.Operations, o => o.Kind == DeltaKind.SetMember);

            var target = new ReadOnlyDictGranularHost { Meta = ROD(("who", "me"), ("env", "test")) };
            var before = target.Meta;
            ReadOnlyDictGranularHostDeepOps.ApplyDelta(ref target, doc);

            Assert.Equal("you", target.Meta!["who"]);
            Assert.True(ReadOnlyDictGranularHostDeepEqual.AreDeepEqual(b, target));
            Assert.NotSame(before, target.Meta); // now passes: read-only target → clone-and-assign
        }

        [Fact]
        public void IReadOnlyDictionary_Value_Change_Mutates_InPlace_When_Target_Is_Mutable()
        {
            var a = new ReadOnlyDictGranularHost { Meta = RO(("k", "v1")) };
            var b = new ReadOnlyDictGranularHost { Meta = RO(("k", "v2")) };

            var doc = ReadOnlyDictGranularHostDeepOps.ComputeDelta(a, b);

            var target = new ReadOnlyDictGranularHost { Meta = RO(("k", "v1")) }; // mutable Dictionary
            var before = target.Meta;
            ReadOnlyDictGranularHostDeepOps.ApplyDelta(ref target, doc);

            Assert.Same(before, target.Meta);        // mutated in place
            Assert.Equal("v2", target.Meta!["k"]);   // value updated
        }

        [Fact]
        public void IReadOnlyDictionary_Add_Remove_Emit_Granular_Ops_And_Apply()
        {
            var a = new ReadOnlyDictGranularHost { Meta = RO(("a", "1"), ("b", "2")) };
            var b = new ReadOnlyDictGranularHost { Meta = RO(("a", "1"), ("c", "3")) }; // remove b, add c

            var doc = ReadOnlyDictGranularHostDeepOps.ComputeDelta(a, b);

            Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.DictRemove && (string)o.Key! == "b");
            Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.DictSet && (string)o.Key! == "c" && (string?)o.Value == "3");
            Assert.DoesNotContain(doc.Operations, o => o.Kind == DeltaKind.SetMember);

            var target = new ReadOnlyDictGranularHost { Meta = RO(("a", "1"), ("b", "2")) };
            ReadOnlyDictGranularHostDeepOps.ApplyDelta(ref target, doc);

            Assert.False(target.Meta!.ContainsKey("b"));
            Assert.Equal("3", target.Meta["c"]);
            Assert.True(ReadOnlyDictGranularHostDeepEqual.AreDeepEqual(b, target));
        }

        [Fact]
        public void IReadOnlyDictionary_Nested_Object_Value_Emits_DictNested_And_Mutates_Value_Instance()
        {
            var leftDog = new Address { Street = "S1", City = "C" };
            var rightDog = new Address { Street = "S2", City = "C" };

            var a = new ReadOnlyDictNestedHost { Map = RO(("d", leftDog)) };
            var b = new ReadOnlyDictNestedHost { Map = RO(("d", rightDog)) };

            var doc = ReadOnlyDictNestedHostDeepOps.ComputeDelta(a, b);

            Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.DictNested && (string)o.Key! == "d");
            Assert.DoesNotContain(doc.Operations, o => o.Kind == DeltaKind.SetMember && (o.Value is IReadOnlyDictionary<string, Address>));

            var target = new ReadOnlyDictNestedHost { Map = RO(("d", new Address { Street = "S1", City = "C" })) };
            var beforeRef = target.Map!["d"];
            ReadOnlyDictNestedHostDeepOps.ApplyDelta(ref target, doc);

            Assert.Same(beforeRef, target.Map!["d"]);
            Assert.Equal("S2", target.Map["d"].Street);
            Assert.True(ReadOnlyDictNestedHostDeepEqual.AreDeepEqual(b, target));
        }

        [Fact]
        public void IReadOnlyDictionary_Null_Transitions_Use_SetMember_At_Member_Scope()
        {
            var a = new ReadOnlyDictGranularHost { Meta = null };
            var b = new ReadOnlyDictGranularHost { Meta = RO(("k", "v")) };

            var doc = ReadOnlyDictGranularHostDeepOps.ComputeDelta(a, b);

            Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.SetMember);

            var target = new ReadOnlyDictGranularHost { Meta = null };
            ReadOnlyDictGranularHostDeepOps.ApplyDelta(ref target, doc);

            Assert.NotNull(target.Meta);
            Assert.Equal("v", target.Meta!["k"]);
            Assert.True(ReadOnlyDictGranularHostDeepEqual.AreDeepEqual(b, target));
        }

        [Fact]
        public void IReadOnlyDictionary_NoOps_When_Equal()
        {
            var a = new ReadOnlyDictGranularHost { Meta = RO(("x", "1"), ("y", "2")) };
            var b = new ReadOnlyDictGranularHost { Meta = RO(("x", "1"), ("y", "2")) };

            var doc = ReadOnlyDictGranularHostDeepOps.ComputeDelta(a, b);

            Assert.True(doc.IsEmpty);

            var target = new ReadOnlyDictGranularHost { Meta = RO(("x", "1"), ("y", "2")) };
            ReadOnlyDictGranularHostDeepOps.ApplyDelta(ref target, doc);

            Assert.True(ReadOnlyDictGranularHostDeepEqual.AreDeepEqual(b, target));
        }

        [Fact]
        public void IReadOnlyDictionary_DictNested_Emits_Set_When_No_Nested_Ops()
        {
            var a = new ReadOnlyDictNestedHost { Map = RO(("d", new Address { Street = "S", City = "C" })) };
            var b = new ReadOnlyDictNestedHost { Map = RO(("d", new Address { Street = "S", City = "C" })) };

            var doc = ReadOnlyDictNestedHostDeepOps.ComputeDelta(a, b);
            Assert.True(doc.IsEmpty);

            var c = new ReadOnlyDictNestedHost { Map = RO(("d", new Address { Street = "S2", City = "C" })) };
            var doc2 = ReadOnlyDictNestedHostDeepOps.ComputeDelta(a, c);

            Assert.DoesNotContain(doc2.Operations, o => o.Kind == DeltaKind.SetMember && o.Value is IReadOnlyDictionary<string, Address>);
        }
    }
}
