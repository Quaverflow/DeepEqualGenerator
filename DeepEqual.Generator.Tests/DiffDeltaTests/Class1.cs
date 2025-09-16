
using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.DiffDeltaTests;
public class DeepOpsTests
{
    [Fact]
    public void Diff_NoChanges_IsEmpty()
    {
        GeneratedHelperRegistry.WarmUp(typeof(Order));

        var a = NewOrder();
        var b = NewOrder();

        Assert.False(OrderDeepOps.TryGetDiff(a, b, out var diff));
        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void Diff_ScalarChange_ReportsSet()
    {
        GeneratedHelperRegistry.WarmUp(typeof(Order));

        var a = NewOrder();
        var b = NewOrder();
        b.Notes = "updated";

        Assert.True(OrderDeepOps.TryGetDiff(a, b, out var diff));
        Assert.True(diff.HasChanges);
        Assert.False(diff.IsReplacement);

        // at least one Set change whose value is "updated"
        Assert.Contains(diff.MemberChanges!, mc =>
            mc.Kind == MemberChangeKind.Set &&
            Equals(mc.ValueOrDiff, "updated"));
    }

    [Fact]
    public void Diff_NestedObjectChange_ReportsNested()
    {
        GeneratedHelperRegistry.WarmUp(typeof(Customer));

        var a = new Customer { Id = 1, Name = "A", Home = new Address { Street = "S1", City = "C1" } };
        var b = new Customer { Id = 1, Name = "A", Home = new Address { Street = "S2", City = "C1" } };

        Assert.True(CustomerDeepOps.TryGetDiff(a, b, out var diff));
        Assert.True(diff.HasChanges);

        // There should be a nested change (Address diff)
        var nested = diff.MemberChanges!.FirstOrDefault(mc => mc.Kind == MemberChangeKind.Nested);
        Assert.True(nested.Kind == MemberChangeKind.Nested);

        // ValueOrDiff should be an IDiff with HasChanges
        Assert.True(nested.ValueOrDiff is IDiff);
        var idiff = (IDiff)nested.ValueOrDiff!;
        Assert.False(idiff.IsEmpty);
    }

    [Fact]
    public void Delta_Roundtrip_Apply_Produces_TargetEqualToAfter()
    {
        GeneratedHelperRegistry.WarmUp(typeof(Order));

        var before = NewOrder();
        var after = NewOrder();
        after.Customer!.Name = "B";
        after.Items![0].Qty = 7;               // collection change (v1 => SetMember expected)
        after.Meta!["who"] = "system";         // dict change (v1 => SetMember expected)

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(before, after, ref w);

        // sanity: delta should not be empty
        Assert.False(doc.IsEmpty);

        var reader = new DeltaReader(doc);
        Order? applyTarget = Clone(before);
        OrderDeepOps.ApplyDelta(ref applyTarget, ref reader);

        // equality helper should say they're equal now
        Assert.True(OrderDeepEqual.AreDeepEqual(after, applyTarget));
    }

    [Fact]
    public void Delta_NullToObject_IsReplacement()
    {
        GeneratedHelperRegistry.WarmUp(typeof(Address));

        Address? left = null;
        Address? right = new Address { Street = "S", City = "C" };

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        AddressDeepOps.ComputeDelta(left, right, ref w);

        var replace = doc.Operations.FirstOrDefault(op => op.Kind == DeltaKind.ReplaceObject);
        Assert.Equal(DeltaKind.ReplaceObject, replace.Kind);
        Assert.NotNull(replace.Value);

        var reader = new DeltaReader(doc);
        Address? target = null;
        AddressDeepOps.ApplyDelta(ref target, ref reader);

        Assert.True(AddressDeepEqual.AreDeepEqual(right, target));
    }

    [Fact]
    public void Delta_ObjectToNull_IsReplacement()
    {
        GeneratedHelperRegistry.WarmUp(typeof(Address));

        Address? left = new Address { Street = "S", City = "C" };
        Address? right = null;

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        AddressDeepOps.ComputeDelta(left, right, ref w);

        var replace = doc.Operations.FirstOrDefault(op => op.Kind == DeltaKind.ReplaceObject);
        Assert.Equal(DeltaKind.ReplaceObject, replace.Kind);
        Assert.Null(replace.Value);

        var reader = new DeltaReader(doc);
        Address? target = new Address { Street = "X", City = "Y" };
        AddressDeepOps.ApplyDelta(ref target, ref reader);

        Assert.True(AddressDeepEqual.AreDeepEqual(right, target));
    }

    [Fact]
    public void DeltaShallow_Member_Writes_Set_Not_Nested()
    {
        GeneratedHelperRegistry.WarmUp(typeof(ShallowWrapper));

        var a = new ShallowWrapper { Addr = new Address { Street = "A", City = "C" } };
        var b = new ShallowWrapper { Addr = new Address { Street = "B", City = "C" } };

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        ShallowWrapperDeepOps.ComputeDelta(a, b, ref w);

        Assert.True(doc.Operations.Any(op => op.Kind == DeltaKind.SetMember));
        Assert.False(doc.Operations.Any(op => op.Kind == DeltaKind.NestedMember));

        var reader = new DeltaReader(doc);
        var target = new ShallowWrapper { Addr = new Address { Street = "A", City = "C" } };
        ShallowWrapperDeepOps.ApplyDelta(ref target, ref reader);

        Assert.True(ShallowWrapperDeepEqual.AreDeepEqual(b, target));
    }

    [Fact]
    public void DeltaSkip_Member_IsNotEmitted()
    {
        GeneratedHelperRegistry.WarmUp(typeof(SkipWrapper));

        var a = new SkipWrapper { Ignored = "x", Tracked = "t1" };
        var b = new SkipWrapper { Ignored = "y", Tracked = "t2" };

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        SkipWrapperDeepOps.ComputeDelta(a, b, ref w);

        // v1 should at least have a SetMember for Tracked
        Assert.True(doc.Operations.Any(op => op.Kind == DeltaKind.SetMember));

        // Applying the delta should update Tracked but not Ignored
        var reader = new DeltaReader(doc);
        var target = new SkipWrapper { Ignored = "x", Tracked = "t1" };
        SkipWrapperDeepOps.ApplyDelta(ref target, ref reader);

        Assert.Equal("x", target.Ignored);   // unchanged
        Assert.Equal("t2", target.Tracked);  // updated
    }

    [Fact]
    public void Collections_V1_Produce_Shallow_SetMember()
    {
        GeneratedHelperRegistry.WarmUp(typeof(Order));

        var a = NewOrder();
        var b = NewOrder();
        b.Items![0].Qty = a.Items![0].Qty + 1;      // a difference
        b.Meta!["who"] = "z";

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        OrderDeepOps.ComputeDelta(a, b, ref w);

        // v1: no granular Seq/Dict ops yet
        Assert.False(doc.Operations.Any(op =>
            op.Kind == DeltaKind.SeqAddAt ||
            op.Kind == DeltaKind.SeqRemoveAt ||
            op.Kind == DeltaKind.SeqReplaceAt ||
            op.Kind == DeltaKind.DictSet ||
            op.Kind == DeltaKind.DictRemove ||
            op.Kind == DeltaKind.DictNested));

        Assert.True(doc.Operations.Any(op => op.Kind == DeltaKind.SetMember));
    }

    [Fact]
    public void Polymorphic_Delta_On_Interface_Resolves_Runtime_Helper()
    {
        GeneratedHelperRegistry.WarmUp(typeof(Zoo));
        GeneratedHelperRegistry.WarmUp(typeof(Dog));

        var a = new Zoo { Pet = new Dog { Name = "fido", Bones = 1 } };
        var b = new Zoo { Pet = new Dog { Name = "fido", Bones = 2 } };

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        ZooDeepOps.ComputeDelta(a, b, ref w);

        Assert.False(doc.IsEmpty);

        var reader = new DeltaReader(doc);
        var target = new Zoo { Pet = new Dog { Name = "fido", Bones = 1 } };
        ZooDeepOps.ApplyDelta(ref target, ref reader);

        Assert.True(ZooDeepEqual.AreDeepEqual(b, target));
    }

    // --- helpers ---

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
                },
            Meta = new Dictionary<string, string>
            {
                ["env"] = "test",
                ["who"] = "user"
            }
        };
    }

    private static Order Clone(Order src)
    {
        return new Order
        {
            Id = src.Id,
            Notes = src.Notes,
            Customer = src.Customer is null ? null : new Customer
            {
                Id = src.Customer.Id,
                Name = src.Customer.Name,
                Home = src.Customer.Home is null ? null : new Address
                {
                    Street = src.Customer.Home.Street,
                    City = src.Customer.Home.City
                }
            },
            Items = src.Items?.Select(i => new OrderItem { Sku = i.Sku, Qty = i.Qty }).ToList(),
            Meta = src.Meta is null ? null : new Dictionary<string, string>(src.Meta)
        };
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

    // For one test we want nested changes to be captured deeply
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

// For testing DeltaShallow on a member
[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class ShallowWrapper
{
    [DeepCompare(DeltaShallow = true)]
    public Address? Addr { get; set; }
}

// For testing DeltaSkip on a member
[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class SkipWrapper
{
    [DeepCompare(DeltaSkip = true)]
    public string? Ignored { get; set; }

    public string? Tracked { get; set; }
}

// Polymorphic
public interface IAnimal { string? Name { get; set; } }

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class Dog : IAnimal { public string? Name { get; set; } public int Bones { get; set; } }

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class Zoo
{
    public IAnimal? Pet { get; set; }
}