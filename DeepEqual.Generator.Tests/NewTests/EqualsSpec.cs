
// SPDX-License-Identifier: MIT

namespace DeepEqual.Generator.Tests.NewTests
{
    public class EqualsSpec
    {
        [Fact]
        public void AreEqual_ValueLike_Same()
        {
            var a = new S_Address { Street = "S", City = "C" };
            var b = new S_Address { Street = "S", City = "C" };
            Assert.True(S_AddressDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void AreEqual_ValueLike_Different()
        {
            var a = new S_Address { Street = "S", City = "C1" };
            var b = new S_Address { Street = "S", City = "C2" };
            Assert.False(S_AddressDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void AreEqual_NestedObject_NoChange()
        {
            var a = new S_Customer { Id = 1, Name = "A", Home = new S_Address { Street = "S1", City = "C" } };
            var b = new S_Customer { Id = 1, Name = "A", Home = new S_Address { Street = "S1", City = "C" } };
            Assert.True(S_CustomerDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void AreEqual_NestedObject_Changed()
        {
            var a = new S_Customer { Id = 1, Name = "A", Home = new S_Address { Street = "S1", City = "C" } };
            var b = new S_Customer { Id = 1, Name = "A", Home = new S_Address { Street = "S2", City = "C" } };
            Assert.False(S_CustomerDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void AreEqual_List_OrderSensitive()
        {
            var a = new S_Order { Items = SpecFactories.MakeItems(("A", 1), ("B", 2)) };
            var b = new S_Order { Items = SpecFactories.MakeItems(("B", 2), ("A", 1)) };
            Assert.False(S_OrderDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void AreEqual_List_SameValues_DifferentInstances_AreEqual()
        {
            var a = new S_Order { Items = SpecFactories.MakeItems(("A", 1), ("B", 2)) };
            var b = new S_Order { Items = SpecFactories.MakeItems(("A", 1), ("B", 2)) };
            Assert.True(S_OrderDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void AreEqual_ReadOnlyList_Is_Equal_By_Content()
        {
            var a = new S_ReadOnlyListHost { Items = SpecFactories.MakeItems(("A", 1), ("B", 2)).AsReadOnly() };
            var b = new S_ReadOnlyListHost { Items = SpecFactories.MakeItems(("A", 1), ("B", 2)).AsReadOnly() };
            Assert.True(S_ReadOnlyListHostDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void AreEqual_Dictionary_ValueChange_Detects_Inequality()
        {
            var a = new S_Order { Meta = new System.Collections.Generic.Dictionary<string, string> { ["k"] = "v1" } };
            var b = new S_Order { Meta = new System.Collections.Generic.Dictionary<string, string> { ["k"] = "v2" } };
            Assert.False(S_OrderDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void AreEqual_Arrays_ContentEquality()
        {
            var a = new S_WithArray { Values = new[] { 1, 2, 3 } };
            var b = new S_WithArray { Values = new[] { 1, 2, 3 } };
            Assert.True(S_WithArrayDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void AreEqual_Polymorphic_SameRuntimeType()
        {
            var a = new S_Zoo { Pet = new S_Dog { Name = "fido", Bones = 1 } };
            var b = new S_Zoo { Pet = new S_Dog { Name = "fido", Bones = 1 } };
            Assert.True(S_ZooDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void AreEqual_Polymorphic_DifferentRuntimeType()
        {
            var a = new S_Zoo { Pet = new S_Dog { Name = "n", Bones = 1 } };
            var b = new S_Zoo { Pet = new S_Cat { Name = "n", Mice = 1 } };
            Assert.False(S_ZooDeepEqual.AreDeepEqual(a, b));
        }
     
        [Fact]
        public void AreEqual_Object_Typed_Dynamic_Fallback_Works()
        {
            // Force dynamic reflection-based equality by using an object-typed payload
            var a = new S_DynamicBox { Value = new { X = 1, Y = "a" } };
            var b = new S_DynamicBox { Value = new { X = 1, Y = "a" } };

            Assert.True(S_DynamicBoxDeepEqual.AreDeepEqual(a, b)); // dynamic path
        }


        [Fact]
        public void AreEqual_Null_And_Object_Are_Not_Equal()
        {
            var a = new S_Address { Street = "S", City = "C" };
            S_Address? b = null;
            Assert.False(S_AddressDeepEqual.AreDeepEqual(a, b));
            Assert.False(S_AddressDeepEqual.AreDeepEqual(b, a));
        }

        [Fact]
        public void AreEqual_Cycles_Equal_Without_Overflow()
        {
            var (a, b, _) = SpecFactories.MakeCyclicTriplet();
            Assert.True(S_NodeDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void AreEqual_Cycles_Detects_Difference()
        {
            var (a, _, diff) = SpecFactories.MakeCyclicTriplet();
            Assert.False(S_NodeDeepEqual.AreDeepEqual(a, diff));
        }

        [Fact]
        public void AreEqual_ThreadSafety_Parallel()
        {
            var pairs = Enumerable.Range(0, 400).Select(_ =>
            {
                var a = SpecFactories.NewOrder();
                var b = SpecFactories.Clone(a);
                b.Items![^1] = new()
                {
                    Sku = b.Items![^1].Sku,
                    Qty = 333
                };

                if (S_OrderDeepEqual.AreDeepEqual(a, b))
                    throw new Exception();
                return (a, b);
            }).ToArray();

            var errors = new System.Collections.Concurrent.ConcurrentBag<string>();

            Parallel.ForEach(pairs, p =>
            {
                if (S_OrderDeepEqual.AreDeepEqual(p.a, p.b))
                    errors.Add("Different pair reported equal.");

                if (!S_OrderDeepEqual.AreDeepEqual(p.a, p.a))
                    errors.Add("Same instance reported not equal.");
            });

            Assert.True(errors.IsEmpty, string.Join(Environment.NewLine, errors));
        }

    }
}
