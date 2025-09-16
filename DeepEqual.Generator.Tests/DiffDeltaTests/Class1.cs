using System;
using System.Collections.Generic;
using System.Linq;
using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.DiffDeltaTests
{
    // ======= Models used by the tests =======

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

    public class DiffDeltaFullSuite
    {
        public DiffDeltaFullSuite()
        {
            // Ensure helpers are initialized (module initializer should do this, but belt & braces)
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

        // ---------- DIFF tests ----------

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

        // ---------- DELTA compute + apply: value-like & replace ----------

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

        // ---------- Lists (IList<T>) granular ops ----------

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
        public void List_PrefixSuffix_Trim_Minimizes_Ops()
        {
            var a = NewOrder();
            var b = Clone(a);
            // change only middle element (index 1) so prefix=1, suffix=1 and a single SeqReplaceAt
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
            // mutate each
            for (int i = 0; i < b.Items!.Count; i++) b.Items[i].Qty++;

            var doc = new DeltaDocument();
            var w = new DeltaWriter(doc);
            OrderDeepOps.ComputeDelta(a, b, ref w);

            // expect only SeqReplaceAt for positions
            Assert.True(doc.Operations.All(op => op.Kind == DeltaKind.SeqReplaceAt));

            var reader = new DeltaReader(doc);
            var target = Clone(a);
            OrderDeepOps.ApplyDelta(ref target, ref reader);
            Assert.True(OrderDeepEqual.AreDeepEqual(b, target));
        }

        // ---------- Dictionaries granular ops ----------

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
        public void Dict_Nested_Object_Value_Uses_DictNested()
        {
            var a = new Dictionary<string, Address> { ["a"] = new Address { Street = "S1", City = "C" } };
            var b = new Dictionary<string, Address> { ["a"] = new Address { Street = "S2", City = "C" } };

            // host type with this dict so generator emits ops
            var hostA = new DictHost { Map = a };
            var hostB = new DictHost { Map = b };

            var doc = new DeltaDocument();
            var w = new DeltaWriter(doc);
            DictHostDeepOps.ComputeDelta(hostA, hostB, ref w);

            Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.DictNested && (string)op.Key! == "a");

            var reader = new DeltaReader(doc);
            var target = new DictHost { Map = new Dictionary<string, Address> { ["a"] = new Address { Street = "S1", City = "C" } } };
            DictHostDeepOps.ApplyDelta(ref target, ref reader);

            Assert.Equal("S2", target.Map!["a"].Street);
        }

        [DeepComparable(GenerateDiff = true, GenerateDelta = true)]
        public sealed class DictHost
        {
            public Dictionary<string, Address>? Map { get; set; }
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

            // no Seq* ops for arrays in v1
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

            var doc = new DeltaDocument();
            var w = new DeltaWriter(doc);
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

            var doc = new DeltaDocument();
            var w = new DeltaWriter(doc);
            SkipWrapDeepOps.ComputeDelta(a, b, ref w);

            // delta should only affect Tracked
            Assert.DoesNotContain(doc.Operations, op => op.Kind == DeltaKind.SetMember && (op.Value as string) == "y");

            var reader = new DeltaReader(doc);
            var target = new SkipWrap { Ignored = "x", Tracked = "t1" };
            SkipWrapDeepOps.ApplyDelta(ref target, ref reader);

            Assert.Equal("x", target.Ignored);
            Assert.Equal("t2", target.Tracked);
        }

        // ---------- Polymorphism on interface ----------

        [Fact]
        public void Polymorphic_Delta_On_Interface_Uses_Runtime_Dispatch()
        {
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

        [Fact]
        public void Polymorphic_Type_Change_Falls_Back_To_SetMember()
        {
            var a = new Zoo { Pet = new Dog { Name = "n", Bones = 1 } };
            var b = new Zoo { Pet = new Cat { Name = "n", Mice = 3 } };

            var doc = new DeltaDocument();
            var w = new DeltaWriter(doc);
            ZooDeepOps.ComputeDelta(a, b, ref w);

            // Since Dog → Cat, expect a SetMember on Pet (not NestedMember)
            Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.SetMember);

            var reader = new DeltaReader(doc);
            var target = new Zoo { Pet = new Dog { Name = "n", Bones = 1 } };
            ZooDeepOps.ApplyDelta(ref target, ref reader);
            Assert.True(ZooDeepEqual.AreDeepEqual(b, target));
        }

        // ---------- Stability & no-ops ----------

        [Fact]
        public void NoOps_When_Equal()
        {
            var a = NewOrder();
            var b = Clone(a);

            var doc = new DeltaDocument();
            var w = new DeltaWriter(doc);
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

            var doc = new DeltaDocument();
            var w = new DeltaWriter(doc);
            OrderDeepOps.ComputeDelta(a, b, ref w);

            var reader = new DeltaReader(doc);
            var target = Clone(a);
            OrderDeepOps.ApplyDelta(ref target, ref reader);

            Assert.Equal(b.Notes, target.Notes);
            // spot-check unrelated stayed the same
            Assert.Equal(a.Id, target.Id);
            Assert.Equal(a.Items!.Count, target.Items!.Count);
            Assert.Equal(a.Customer!.Id, target.Customer!.Id);
        }
    }
}
