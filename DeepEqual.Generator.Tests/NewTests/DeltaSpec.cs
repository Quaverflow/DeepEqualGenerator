
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.NewTests
{
    public class DeltaSpec
    {
        private static int ProbeItemsMemberIndex()
        {
            var a = new S_Order { Items = SpecFactories.MakeItems(("A", 1), ("B", 2)) };
            var b = new S_Order { Items = SpecFactories.MakeItems(("A", 1), ("B", 9)) };
            var doc = S_OrderDeepOps.ComputeDelta(a, b);

                       var seq = doc.Operations.FirstOrDefault(op =>
                op.Kind is DeltaKind.SeqReplaceAt
                    or DeltaKind.SeqAddAt
                    or DeltaKind.SeqRemoveAt
                    or DeltaKind.SeqNestedAt);

            if (seq.Kind is DeltaKind.SeqReplaceAt
                or DeltaKind.SeqAddAt
                or DeltaKind.SeqRemoveAt
                or DeltaKind.SeqNestedAt)
            {
                return seq.MemberIndex;
            }

                       var set = doc.Operations.First(op => op is { Kind: DeltaKind.SetMember, Value: List<S_OrderItem> });
            return set.MemberIndex;
        }


        private static int ProbeMetaMemberIndex()
        {
            var a = new S_Order { Meta = new Dictionary<string, string> { ["k"] = "1" } };
            var b = new S_Order { Meta = new Dictionary<string, string> { ["k"] = "2" } };
            var doc = S_OrderDeepOps.ComputeDelta(a, b);
            var dict = doc.Operations.FirstOrDefault(op => op.Kind is DeltaKind.DictSet or DeltaKind.DictRemove or DeltaKind.DictNested);
            if (dict.Kind is DeltaKind.DictSet or DeltaKind.DictRemove or DeltaKind.DictNested) return dict.MemberIndex;
            var set = doc.Operations.First(op => op is { Kind: DeltaKind.SetMember, Value: Dictionary<string, string> });
            return set.MemberIndex;
        }

        [Fact]
        public void Delta_ReplaceObject_NullToObj()
        {
            S_Address? left = null, right = new S_Address { Street = "S", City = "C" };
            var doc = S_AddressDeepOps.ComputeDelta(left, right);
            Assert.Contains(doc.Operations, op => op is { Kind: DeltaKind.ReplaceObject, Value: S_Address });
            S_Address? target = null;
            S_AddressDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_AddressDeepEqual.AreDeepEqual(right, target));
        }

        [Fact]
        public void Delta_ReplaceObject_ObjToNull()
        {
            var left = new S_Address { Street = "S", City = "C" };
            S_Address? right = null;
            var doc = S_AddressDeepOps.ComputeDelta(left, right);
            Assert.Contains(doc.Operations, op => op is { Kind: DeltaKind.ReplaceObject, Value: null });
            var target = new S_Address { Street = "X", City = "Y" };
            S_AddressDeepOps.ApplyDelta(ref target, doc);
            Assert.Null(target);
        }

        [Fact] public void List_One_Element_Changed_Emits_Single_SeqReplaceAt_And_Applies()
        {
            var a = SpecFactories.NewOrder();
            var b = SpecFactories.Clone(a);
            b.Items![1].Qty++;
            var doc = S_OrderDeepOps.ComputeDelta(a, b);
            var repl = doc.Operations.Single(op => op.Kind == DeltaKind.SeqNestedAt);
            Assert.Equal(1, repl.Index);
            var target = SpecFactories.Clone(a);
            S_OrderDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_OrderDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void List_Add_In_Middle_Emits_SeqAddAt_And_Applies()
        {
            var a = SpecFactories.NewOrder();
            var b = SpecFactories.Clone(a);
            b.Items!.Insert(1, new S_OrderItem { Sku = "ADD", Qty = 9 });
            var doc = S_OrderDeepOps.ComputeDelta(a, b);
            Assert.Contains(doc.Operations, op => op is { Kind: DeltaKind.SeqAddAt, Index: 1 });
            var target = SpecFactories.Clone(a);
            S_OrderDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_OrderDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void List_Remove_In_Middle_Emits_SeqRemoveAt_And_Applies()
        {
            var a = SpecFactories.NewOrder();
            var b = SpecFactories.Clone(a);
            b.Items!.RemoveAt(1);
            var doc = S_OrderDeepOps.ComputeDelta(a, b);
            Assert.Contains(doc.Operations, op => op is { Kind: DeltaKind.SeqRemoveAt, Index: 1 });
            var target = SpecFactories.Clone(a);
            S_OrderDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_OrderDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void List_Add_Head_And_Tail_Then_Apply()
        {
            var a = SpecFactories.NewOrder();
            var b = SpecFactories.Clone(a);
            b.Items!.Insert(0, new S_OrderItem { Sku = "HEAD", Qty = 7 });
            b.Items!.Add(new S_OrderItem { Sku = "TAIL", Qty = 8 });
            var doc = S_OrderDeepOps.ComputeDelta(a, b);
            Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.SeqAddAt && op.Index == b.Items!.Count - 1);
            Assert.True(doc.Operations.Any(op => op is { Kind: DeltaKind.SeqAddAt, Index: 0 }) ||
                        doc.Operations.Any(op => op is { Kind: DeltaKind.SeqReplaceAt, Index: 0 }));
            var target = SpecFactories.Clone(a);
            S_OrderDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_OrderDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void List_Remove_Head_And_Tail_Then_Apply()
        {
            var a = SpecFactories.NewOrder();
            var b = SpecFactories.Clone(a);
            b.Items!.RemoveAt(0);
            b.Items!.RemoveAt(b.Items!.Count - 1);
            var doc = S_OrderDeepOps.ComputeDelta(SpecFactories.NewOrder(), b);
            Assert.True(doc.Operations.Count(op => op.Kind == DeltaKind.SeqRemoveAt) >= 2);
            var target = SpecFactories.NewOrder();
            S_OrderDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_OrderDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void List_Duplicate_Values_Replaces_By_Index()
        {
            var a = SpecFactories.NewOrder();
            a.Items = SpecFactories.MakeItems(("X", 1), ("X", 1), ("X", 1));
            var b = SpecFactories.Clone(a);
            b.Items![1].Qty = 99;
            var doc = S_OrderDeepOps.ComputeDelta(a, b);
            var ops = doc.Operations.Where(o => o.Kind == DeltaKind.SeqNestedAt).ToList();
            Assert.Single(ops);
            Assert.Equal(1, ops[0].Index);
            var target = SpecFactories.Clone(a);
            S_OrderDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_OrderDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void List_PrefixSuffix_Trim_Minimizes_Ops()
        {
            var a = SpecFactories.NewOrder();
            var b = SpecFactories.Clone(a);
            b.Items![1].Sku = "MID";
            var doc = S_OrderDeepOps.ComputeDelta(a, b);
            Assert.Single(doc.Operations.Where(op => op.Kind == DeltaKind.SeqNestedAt));
            var target = SpecFactories.Clone(a);
            S_OrderDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_OrderDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void List_All_Changed_Falls_Back_To_Replaces_Not_AddRemoves()
        {
            var a = SpecFactories.NewOrder();
            var b = SpecFactories.Clone(a);
            for (var i = 0; i < b.Items!.Count; i++) b.Items[i].Qty++;
            var doc = S_OrderDeepOps.ComputeDelta(a, b);
            Assert.True(doc.Operations.All(op =>
                op.Kind == DeltaKind.SeqNestedAt || op.Kind == DeltaKind.SeqReplaceAt));
            var target = SpecFactories.Clone(a);
            S_OrderDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_OrderDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void List_Mixed_Ops_Add_Remove_Replace_Applies_In_Correct_Order()
        {
            var a = new S_Order { Items = SpecFactories.MakeItems(("A", 1), ("B", 2), ("C", 3), ("D", 4)) };
            var b = new S_Order { Items = SpecFactories.MakeItems(("A", 1), ("BX", 2), ("C", 3), ("E", 5)) };
            var doc = S_OrderDeepOps.ComputeDelta(a, b);
            Assert.Contains(doc.Operations, o =>
                (o.Kind == DeltaKind.SeqNestedAt || o.Kind == DeltaKind.SeqReplaceAt) && o.Index == 1);
            var target = new S_Order { Items = SpecFactories.MakeItems(("A", 1), ("B", 2), ("C", 3), ("D", 4)) };
            S_OrderDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_OrderDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void List_Operation_Ordering_Stability()
        {
            var a = new S_Order { Items = SpecFactories.MakeItems(("A", 1), ("B", 2), ("C", 3), ("D", 4), ("E", 5)) };
            var b = new S_Order { Items = SpecFactories.MakeItems(("A", 1), ("BX", 2), ("C", 3), ("E", 5), ("F", 6)) };
            var doc = S_OrderDeepOps.ComputeDelta(a, b);
            var removes = doc.Operations.Where(o => o.Kind == DeltaKind.SeqRemoveAt).Select(o => o.Index).ToList();
            var adds = doc.Operations.Where(o => o.Kind == DeltaKind.SeqAddAt).Select(o => o.Index).ToList();
            Assert.True(removes.SequenceEqual(removes.OrderByDescending(x => x)));
            Assert.True(adds.SequenceEqual(adds.OrderBy(x => x)));
            var target = a;
            S_OrderDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_OrderDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void List_ReadOnly_Typed_Property_Falls_Back_To_SetMember()
        {
            var a = new S_ReadOnlyListHost { Items = SpecFactories.MakeItems(("A", 1), ("B", 2)).AsReadOnly() };
            var b = new S_ReadOnlyListHost { Items = SpecFactories.MakeItems(("A", 1), ("B", 9)).AsReadOnly() };
            var doc = S_ReadOnlyListHostDeepOps.ComputeDelta(a, b);
            Assert.DoesNotContain(doc.Operations, o => o.Kind is DeltaKind.SeqReplaceAt or DeltaKind.SeqAddAt or DeltaKind.SeqRemoveAt);
            Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.SetMember);
            var target = new S_ReadOnlyListHost { Items = SpecFactories.MakeItems(("A", 1), ("B", 2)).AsReadOnly() };
            S_ReadOnlyListHostDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_ReadOnlyListHostDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void List_Idempotence_SetMember_Is_Idempotent()
        {
            var a = new S_WithArray { Values = new[] { 1, 2, 3 } };
            var b = new S_WithArray { Values = new[] { 1, 9, 3 } };
            var doc = S_WithArrayDeepOps.ComputeDelta(a, b);
            var target = new S_WithArray { Values = new[] { 1, 2, 3 } };
            S_WithArrayDeepOps.ApplyDelta(ref target, doc);
            S_WithArrayDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_WithArrayDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void List_Idempotence_Add_Is_Not_Idempotent()
        {
            var a = new S_Order { Items = SpecFactories.MakeItems(("A", 1), ("B", 2)) };
            var b = new S_Order { Items = SpecFactories.MakeItems(("A", 1), ("B", 2), ("C", 3)) };
            var doc = S_OrderDeepOps.ComputeDelta(a, b);
            var t1 = new S_Order { Items = SpecFactories.MakeItems(("A", 1), ("B", 2)) };
            S_OrderDeepOps.ApplyDelta(ref t1, doc);
            Assert.True(S_OrderDeepEqual.AreDeepEqual(b, t1));
            S_OrderDeepOps.ApplyDelta(ref t1, doc);
            Assert.NotEqual(b.Items!.Count, t1.Items!.Count);
        }

        [Fact] public void List_Mismatch_Target_Not_BaseOfPatch_Does_Not_Throw_For_Replaces()
        {
            var a = new S_Order { Items = SpecFactories.MakeItems(("A", 1), ("B", 2), ("C", 3)) };
            var b = new S_Order { Items = SpecFactories.MakeItems(("A", 1), ("BX", 2), ("C", 3)) };
            var doc = S_OrderDeepOps.ComputeDelta(a, b);
            var target = new S_Order { Items = SpecFactories.MakeItems(("A", 1), ("Q", 5), ("C", 3)) };
            var ex = Record.Exception(() => S_OrderDeepOps.ApplyDelta(ref target, doc));
            Assert.Null(ex);
            Assert.Equal("BX", target.Items![1].Sku);
        }

        [Fact] public void Dict_Value_Change_Emits_DictSet_And_Applies()
        {
            var a = SpecFactories.NewOrder();
            var b = SpecFactories.Clone(a);
            b.Meta!["who"] = "z";
            var doc = S_OrderDeepOps.ComputeDelta(a, b);
            Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.DictSet && (string)op.Key! == "who" && (string?)op.Value == "z");
            var target = SpecFactories.Clone(a);
            S_OrderDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_OrderDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void Dict_Add_And_Remove_Emit_Ops_And_Apply()
        {
            var a = SpecFactories.NewOrder();
            var b = SpecFactories.Clone(a);
            b.Meta!.Remove("env");
            b.Meta!["new"] = "v";
            var doc = S_OrderDeepOps.ComputeDelta(a, b);
            Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.DictRemove && (string)op.Key! == "env");
            Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.DictSet && (string)op.Key! == "new" && (string?)op.Value == "v");
            var target = SpecFactories.Clone(a);
            S_OrderDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_OrderDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void Dict_Nested_Object_Value_Uses_DictNested_Mutates_Instance()
        {
            var a = new S_PolyDictHost { Pets = new Dictionary<string, IAnimal> { ["d"] = new S_Dog { Name = "f", Bones = 1 } } };
            var b = new S_PolyDictHost { Pets = new Dictionary<string, IAnimal> { ["d"] = new S_Dog { Name = "f", Bones = 2 } } };
            var doc = S_PolyDictHostDeepOps.ComputeDelta(a, b);
            Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.DictNested && (string)op.Key! == "d");
            var target = new S_PolyDictHost { Pets = new Dictionary<string, IAnimal> { ["d"] = new S_Dog { Name = "f", Bones = 1 } } };
            var beforeRef = target.Pets!["d"];
            S_PolyDictHostDeepOps.ApplyDelta(ref target, doc);
            Assert.Same(beforeRef, target.Pets!["d"]);
            Assert.Equal(2, ((S_Dog)target.Pets["d"]).Bones);
        }

        [Fact] public void Dict_Custom_CaseInsensitive_KeyComparer_Respected_On_Apply()
        {
            var a = new S_CaseDictHost { Meta = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase) { ["who"] = "me" } };
            var b = new S_CaseDictHost { Meta = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase) { ["WHO"] = "you" } };
            var doc = S_CaseDictHostDeepOps.ComputeDelta(a, b);
            Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.DictSet && ((string)op.Key!).Equals("WHO", System.StringComparison.OrdinalIgnoreCase));
            var target = new S_CaseDictHost { Meta = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase) { ["who"] = "me" } };
            S_CaseDictHostDeepOps.ApplyDelta(ref target, doc);
            Assert.True(target.Meta!.ContainsKey("who"));
            Assert.Equal("you", target.Meta["who"]);
        }

        [Fact] public void Dict_Remove_On_Missing_Key_Is_Ignored_Safely()
        {
            var host = new S_CaseDictHost { Meta = new Dictionary<string, string> { ["a"] = "1" } };
            var a = new S_CaseDictHost { Meta = new Dictionary<string, string> { ["x"] = "1" } };
            var b = new S_CaseDictHost { Meta = new Dictionary<string, string> { ["y"] = "2" } };
            var probeDoc = S_CaseDictHostDeepOps.ComputeDelta(a, b);
            var metaIdx = probeDoc.Operations.First(op => op.Kind is DeltaKind.DictSet or DeltaKind.DictRemove).MemberIndex;
            var legacy = new DeltaDocument();
            var lw = new DeltaWriter(legacy);
            lw.WriteDictRemove(metaIdx, "missing");
            var ex = Record.Exception(() => S_CaseDictHostDeepOps.ApplyDelta(ref host, legacy));
            Assert.Null(ex);
            Assert.Equal("1", host.Meta!["a"]);
        }

        [Fact] public void Dict_Idempotence_DictSet_Is_Idempotent()
        {
            var a = new S_CaseDictHost { Meta = new Dictionary<string, string> { ["a"] = "1" } };
            var b = new S_CaseDictHost { Meta = new Dictionary<string, string> { ["a"] = "2" } };
            var doc = S_CaseDictHostDeepOps.ComputeDelta(a, b);
            var t = new S_CaseDictHost { Meta = new Dictionary<string, string> { ["a"] = "1" } };
            S_CaseDictHostDeepOps.ApplyDelta(ref t, doc);
            S_CaseDictHostDeepOps.ApplyDelta(ref t, doc);
            Assert.True(S_CaseDictHostDeepEqual.AreDeepEqual(b, t));
        }

        [Fact] public void Arrays_Fallback_To_SetMember()
        {
            var a = new S_WithArray { Values = new[] { 1, 2, 3 } };
            var b = new S_WithArray { Values = new[] { 1, 9, 3 } };
            var doc = S_WithArrayDeepOps.ComputeDelta(a, b);
            Assert.DoesNotContain(doc.Operations, op => op.Kind is DeltaKind.SeqAddAt or DeltaKind.SeqRemoveAt or DeltaKind.SeqReplaceAt);
            Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.SetMember);
            var target = new S_WithArray { Values = new[] { 1, 2, 3 } };
            S_WithArrayDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_WithArrayDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void DeltaShallow_Produces_Set_Not_Nested()
        {
            var a = new S_ShallowWrap { Addr = new S_Address { Street = "A", City = "C" } };
            var b = new S_ShallowWrap { Addr = new S_Address { Street = "B", City = "C" } };
            var doc = S_ShallowWrapDeepOps.ComputeDelta(a, b);
            Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.SetMember);
            Assert.DoesNotContain(doc.Operations, op => op.Kind == DeltaKind.NestedMember);
            var target = new S_ShallowWrap { Addr = new S_Address { Street = "A", City = "C" } };
            S_ShallowWrapDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_ShallowWrapDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void DeltaSkip_Member_Is_Not_Emitted_And_Not_Applied()
        {
            var a = new S_SkipWrap { Ignored = "x", Tracked = "t1" };
            var b = new S_SkipWrap { Ignored = "y", Tracked = "t2" };
            var doc = S_SkipWrapDeepOps.ComputeDelta(a, b);
            Assert.DoesNotContain(doc.Operations, op => op.Kind == DeltaKind.SetMember && (op.Value as string) == "y");
            var target = new S_SkipWrap { Ignored = "x", Tracked = "t1" };
            S_SkipWrapDeepOps.ApplyDelta(ref target, doc);
            Assert.Equal("x", target.Ignored);
            Assert.Equal("t2", target.Tracked);
        }

        [Fact] public void DeltaShallow_On_Collection_Forces_SetMember()
        {
            var a = new S_ShallowCollectionWrap { Items = SpecFactories.MakeItems(("A", 1), ("B", 2)) };
            var b = new S_ShallowCollectionWrap { Items = SpecFactories.MakeItems(("A", 1), ("B", 9)) };
            var doc = S_ShallowCollectionWrapDeepOps.ComputeDelta(a, b);
            Assert.DoesNotContain(doc.Operations, o => o.Kind is DeltaKind.SeqAddAt or DeltaKind.SeqRemoveAt or DeltaKind.SeqReplaceAt or DeltaKind.DictSet or DeltaKind.DictRemove or DeltaKind.DictNested);
            Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.SetMember);
            var target = new S_ShallowCollectionWrap { Items = SpecFactories.MakeItems(("A", 1), ("B", 2)) };
            S_ShallowCollectionWrapDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_ShallowCollectionWrapDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void DeltaSkip_On_Collection_And_Dict_Emits_No_Ops_And_Does_Not_Change()
        {
            var a = new S_SkipCollectionWrap { Items = SpecFactories.MakeItems(("A", 1), ("B", 2)), Meta = new Dictionary<string, string> { ["x"] = "1" } };
            var b = new S_SkipCollectionWrap { Items = SpecFactories.MakeItems(("A", 1), ("B", 9)), Meta = new Dictionary<string, string> { ["x"] = "2" } };
            var doc = S_SkipCollectionWrapDeepOps.ComputeDelta(a, b);
            Assert.True(doc.IsEmpty);
            var target = new S_SkipCollectionWrap { Items = SpecFactories.MakeItems(("A", 1), ("B", 2)), Meta = new Dictionary<string, string> { ["x"] = "1" } };
            S_SkipCollectionWrapDeepOps.ApplyDelta(ref target, doc);
            Assert.Equal(2, target.Items!.Count);
            Assert.Equal("1", target.Meta!["x"]);
        }

        [Fact] public void Polymorphic_Delta_On_Interface_Uses_Runtime_Dispatch()
        {
            var a = new S_Zoo { Pet = new S_Dog { Name = "fido", Bones = 1 } };
            var b = new S_Zoo { Pet = new S_Dog { Name = "fido", Bones = 2 } };
            var doc = S_ZooDeepOps.ComputeDelta(a, b);
            Assert.False(doc.IsEmpty);
            var target = new S_Zoo { Pet = new S_Dog { Name = "fido", Bones = 1 } };
            S_ZooDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_ZooDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void Polymorphic_Type_Change_Falls_Back_To_SetMember()
        {
            var a = new S_Zoo { Pet = new S_Dog { Name = "n", Bones = 1 } };
            var b = new S_Zoo { Pet = new S_Cat { Name = "n", Mice = 3 } };
            var doc = S_ZooDeepOps.ComputeDelta(a, b);
            Assert.Contains(doc.Operations, op => op.Kind == DeltaKind.SetMember);
            var target = new S_Zoo { Pet = new S_Dog { Name = "n", Bones = 1 } };
            S_ZooDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_ZooDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void Polymorphic_Unregistered_Runtime_Type_Falls_Back_To_SetMember()
        {
            var a = new S_Zoo { Pet = new S_Parrot { Name = "p", Seeds = 1 } };
            var b = new S_Zoo { Pet = new S_Parrot { Name = "p", Seeds = 2 } };
            var doc = S_ZooDeepOps.ComputeDelta(a, b);
            Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.SetMember);
        }

        [Fact] public void NoOps_When_Equal()
        {
            var a = SpecFactories.NewOrder();
            var b = SpecFactories.Clone(a);
            var doc = S_OrderDeepOps.ComputeDelta(a, b);
            Assert.True(doc.IsEmpty);
            var target = SpecFactories.Clone(a);
            S_OrderDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_OrderDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void Apply_Ignores_Unrelated_Members()
        {
            var a = SpecFactories.NewOrder();
            var b = SpecFactories.Clone(a); b.Notes = "changed";
            var doc = S_OrderDeepOps.ComputeDelta(a, b);
            var target = SpecFactories.Clone(a);
            S_OrderDeepOps.ApplyDelta(ref target, doc);
            Assert.Equal(b.Notes, target.Notes);
            Assert.Equal(a.Id, target.Id);
            Assert.Equal(a.Items!.Count, target.Items!.Count);
            Assert.Equal(a.Customer!.Id, target.Customer!.Id);
        }

        [Fact] public void Null_Transitions_Across_Nested_List_Dict()
        {
            var a = new S_Order { Customer = null, Items = null, Meta = null };
            var b = new S_Order
            {
                Customer = new S_Customer { Id = 1, Name = "n", Home = new S_Address { Street = "s", City = "c" } },
                Items = SpecFactories.MakeItems(("A", 1)),
                Meta = new Dictionary<string, string> { ["k"] = "v" }
            };
            var doc = S_OrderDeepOps.ComputeDelta(a, b);
            Assert.False(doc.IsEmpty);
            var target = new S_Order();
            S_OrderDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_OrderDeepEqual.AreDeepEqual(b, target));
            var doc2 = S_OrderDeepOps.ComputeDelta(b, new S_Order());
            var target2 = b;
            S_OrderDeepOps.ApplyDelta(ref target2, doc2);
            Assert.True(S_OrderDeepEqual.AreDeepEqual(new S_Order(), target2));
        }

        [Fact] public void Empty_Collections_And_Dicts_Produce_Minimal_Ops()
        {
            var a = new S_Order { Items = new(), Meta = new Dictionary<string, string>(), Customer = null };
            var b = new S_Order { Items = new(), Meta = new Dictionary<string, string>(), Customer = null };
            var doc = S_OrderDeepOps.ComputeDelta(a, b);
            Assert.True(doc.IsEmpty);
            b.Items!.Add(new S_OrderItem { Sku = "A", Qty = 1 });
            b.Meta!["k"] = "v";
            var doc2 = S_OrderDeepOps.ComputeDelta(a, b);
            Assert.Contains(doc2.Operations, o => o.Kind == DeltaKind.SeqAddAt);
            Assert.Contains(doc2.Operations, o => o.Kind == DeltaKind.DictSet);
        }

        [Fact] public void Large_List_Single_Middle_Change_Produces_Single_Replace()
        {
            var a = new S_IntListHost { Values = Enumerable.Range(0, 10_000).ToList() };
            var b = new S_IntListHost { Values = Enumerable.Range(0, 10_000).ToList() };
            b.Values![5_000]++;
            var doc = S_IntListHostDeepOps.ComputeDelta(a, b);
            var replaces = doc.Operations.Count(o => o.Kind == DeltaKind.SeqReplaceAt);
            Assert.Equal(1, replaces);
            var target = new S_IntListHost { Values = Enumerable.Range(0, 10_000).ToList() };
            S_IntListHostDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_IntListHostDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void Multiple_Middle_Edits_Yield_Minimal_Replaces()
        {
            var a = new S_IntListHost { Values = Enumerable.Range(0, 100).ToList() };
            var b = new S_IntListHost { Values = Enumerable.Range(0, 100).ToList() };
            b.Values![40]++; b.Values![41]++;
            var doc = S_IntListHostDeepOps.ComputeDelta(a, b);
            var replaces = doc.Operations.Where(o => o.Kind == DeltaKind.SeqReplaceAt).ToList();
            Assert.Equal(2, replaces.Count);
            Assert.Contains(replaces, o => o.Index == 40);
            Assert.Contains(replaces, o => o.Index == 41);
            var target = new S_IntListHost { Values = Enumerable.Range(0, 100).ToList() };
            S_IntListHostDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_IntListHostDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void Bounds_Safety_Invalid_Seq_Index_Throws()
        {
            var itemsIdx = ProbeItemsMemberIndex();
            var bad = new DeltaDocument(); var bw = new DeltaWriter(bad);
            bw.WriteSeqRemoveAt(itemsIdx, 999);
            var order = new S_Order { Items = SpecFactories.MakeItems(("A", 1), ("B", 2)) };
            Assert.Throws<ArgumentOutOfRangeException>(() => S_OrderDeepOps.ApplyDelta(ref order, bad));
        }

        [Fact] public void Unknown_Op_Kinds_Are_Ignored_Safely()
        {
            var o = SpecFactories.NewOrder();
            var a = SpecFactories.NewOrder();
            var b = SpecFactories.Clone(a); b.Notes = "changed";
            var probe = S_OrderDeepOps.ComputeDelta(a, b);
            var anyIdx = probe.Operations.First().MemberIndex;
            var doc = new DeltaDocument();
            var fi = typeof(DeltaDocument).GetField("Ops", BindingFlags.Instance | BindingFlags.NonPublic);
            var ops = (IList)fi!.GetValue(doc)!;
            var bogusOp = Activator.CreateInstance(typeof(DeltaOp), args: new object[] { anyIdx, (DeltaKind)999, -1, null, "x", null })!;
            ops.Add(bogusOp);
            var ex = Record.Exception(() => S_OrderDeepOps.ApplyDelta(ref o, doc));
            Assert.Null(ex);
        }

        [Fact] public void ReplaceObject_Precedence_Wins_Over_Spurious_Member_Ops()
        {
            S_Address? left = null, right = new S_Address { Street = "S", City = "C" };
            var doc = new DeltaDocument();
            var w = new DeltaWriter(doc);
            w.WriteReplaceObject(right);
            w.WriteSetMember(123, "noise");
            var target = (S_Address?)null;
            S_AddressDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_AddressDeepEqual.AreDeepEqual(right, target));
        }

        [Fact] public void DeltaReader_Partial_Consumption_Does_Not_Block_Apply()
        {
            var a = SpecFactories.NewOrder();
            var b = SpecFactories.Clone(a); b.Notes = "X"; b.Id = 99;
            var doc = S_OrderDeepOps.ComputeDelta(a, b);
            var reader = new DeltaReader(doc);
            foreach (var _ in reader.EnumerateMember(doc.Operations[0].MemberIndex)) break;
            var target = SpecFactories.Clone(a);
            S_OrderDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_OrderDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void RoundTrip_Fuzzed_Random_Changes()
        {
            var rng = new Random(123);
            for (var iter = 0; iter < 20; iter++)
            {
                var a = SpecFactories.NewOrder();
                var b = SpecFactories.Clone(a);
                for (var i = 0; i < b.Items!.Count; i++) if (rng.NextDouble() < 0.3) b.Items[i].Qty += rng.Next(1, 3);
                if (rng.NextDouble() < 0.3) b.Items!.Add(new S_OrderItem { Sku = "R" + rng.Next(100), Qty = 1 });
                if (rng.NextDouble() < 0.3 && b.Items!.Count > 0) b.Items!.RemoveAt(rng.Next(b.Items.Count));
                if (rng.NextDouble() < 0.5) b.Meta!["who"] = "u" + rng.Next(10);
                if (rng.NextDouble() < 0.2) b.Meta!.Remove("env");
                var doc = S_OrderDeepOps.ComputeDelta(a, b);
                var target = SpecFactories.Clone(a);
                S_OrderDeepOps.ApplyDelta(ref target, doc);
                Assert.True(S_OrderDeepEqual.AreDeepEqual(b, target));
            }
        }

        [Fact] public void ModuleInitializer_Works_Without_WarmUp()
        {
            var a = new S_ModuleInitFoo { V = "a" };
            var b = new S_ModuleInitFoo { V = "b" };
            var doc = S_ModuleInitFooDeepOps.ComputeDelta(a, b);
            var target = new S_ModuleInitFoo { V = "a" };
            S_ModuleInitFooDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_ModuleInitFooDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void Sealed_Types_Nested_Delta_Correctness()
        {
            var a = new S_SealedThing { A = 1, Addr = new S_Address { Street = "s", City = "c" } };
            var b = new S_SealedThing { A = 2, Addr = new S_Address { Street = "sx", City = "c" } };
            var doc = S_SealedThingDeepOps.ComputeDelta(a, b);
            Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.SetMember);
            Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.NestedMember);
            var target = new S_SealedThing { A = 1, Addr = new S_Address { Street = "s", City = "c" } };
            S_SealedThingDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_SealedThingDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void ThreadSafety_Compute_And_Apply_In_Parallel()
        {
            var pairs = Enumerable.Range(0, 200).Select(_ => { var a = SpecFactories.NewOrder(); var b = SpecFactories.Clone(a); b.Items![^1].Qty++; return (a, b); }).ToArray();
            var errors = new ConcurrentBag<Exception>();
            System.Threading.Tasks.Parallel.ForEach(pairs, p =>
            {
                try
                {
                    var doc = S_OrderDeepOps.ComputeDelta(p.a, p.b);
                    var t = SpecFactories.Clone(p.a);
                    S_OrderDeepOps.ApplyDelta(ref t, doc);
                    if (!S_OrderDeepEqual.AreDeepEqual(p.b, t)) throw new InvalidOperationException("Mismatch");
                }
                catch (Exception ex) { errors.Add(ex); }
            });
            Assert.True(errors.IsEmpty, string.Join(Environment.NewLine, errors.Select(e => e.Message)));
        }

        [Fact] public void DeltaDocument_Can_Be_Enumerated_Multiple_Times()
        {
            var a = SpecFactories.NewOrder(); var b = SpecFactories.Clone(a); b.Notes = "x"; b.Id = 99;
            var doc = S_OrderDeepOps.ComputeDelta(a, b);
            var c1 = doc.Operations.Count; var c2 = doc.Operations.Count;
            Assert.Equal(c1, c2);
            var r1 = new DeltaReader(doc);
            var ops1 = r1.EnumerateAll().Count(); var ops2 = r1.EnumerateAll().Count();
            Assert.Equal(ops1, ops2);
        }

        [Fact] public void BackCompat_SetMember_Only_Deltas_Still_Apply()
        {
            var a = SpecFactories.NewOrder();
            var b = SpecFactories.Clone(a);
            b.Items = SpecFactories.MakeItems(("A", 1), ("B", 2));
            b.Meta = new Dictionary<string, string> { ["k"] = "v" };
            var itemsIdx = ProbeItemsMemberIndex();
            var metaIdx = ProbeMetaMemberIndex();
            var legacy = new DeltaDocument();
            var lw = new DeltaWriter(legacy);
            lw.WriteSetMember(itemsIdx, b.Items);
            lw.WriteSetMember(metaIdx, b.Meta);
            var target = SpecFactories.Clone(a);
            S_OrderDeepOps.ApplyDelta(ref target, legacy);
            Assert.True(S_OrderDeepEqual.AreDeepEqual(b, target));
        }

        private static IReadOnlyDictionary<string, string> RO(params (string k, string v)[] xs) => new Dictionary<string, string>(xs.ToDictionary(p => p.k, p => p.v));
        private static IReadOnlyDictionary<string, S_Address> RO(params (string k, S_Address v)[] xs) => new Dictionary<string, S_Address>(xs.ToDictionary(p => p.k, p => p.v));
        private static IReadOnlyDictionary<string, string> ROD(params (string k, string v)[] xs) => new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(xs.ToDictionary(p => p.k, p => p.v));

        [Fact] public void IReadOnlyDictionary_Value_Change_Emits_DictSet_And_Applies_By_Clone()
        {
            var a = new S_RODictGranularHost { Meta = RO(("who", "me"), ("env", "test")) };
            var b = new S_RODictGranularHost { Meta = RO(("who", "you"), ("env", "test")) };
            var doc = S_RODictGranularHostDeepOps.ComputeDelta(a, b);
            Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.DictSet && (string)o.Key! == "who" && (string?)o.Value == "you");
            Assert.DoesNotContain(doc.Operations, o => o.Kind == DeltaKind.SetMember);
            var target = new S_RODictGranularHost { Meta = ROD(("who", "me"), ("env", "test")) };
            var before = target.Meta;
            S_RODictGranularHostDeepOps.ApplyDelta(ref target, doc);
            Assert.Equal("you", target.Meta!["who"]);
            Assert.True(S_RODictGranularHostDeepEqual.AreDeepEqual(b, target));
            Assert.NotSame(before, target.Meta);
        }

        [Fact] public void IReadOnlyDictionary_Value_Change_Mutates_InPlace_When_Target_Is_Mutable()
        {
            var a = new S_RODictGranularHost { Meta = RO(("k", "v1")) };
            var b = new S_RODictGranularHost { Meta = RO(("k", "v2")) };
            var doc = S_RODictGranularHostDeepOps.ComputeDelta(a, b);
            var target = new S_RODictGranularHost { Meta = RO(("k", "v1")) };
            var before = target.Meta;
            S_RODictGranularHostDeepOps.ApplyDelta(ref target, doc);
            Assert.Same(before, target.Meta);
            Assert.Equal("v2", target.Meta!["k"]);
        }

        [Fact] public void IReadOnlyDictionary_Add_Remove_Emit_Granular_Ops_And_Apply()
        {
            var a = new S_RODictGranularHost { Meta = RO(("a", "1"), ("b", "2")) };
            var b = new S_RODictGranularHost { Meta = RO(("a", "1"), ("c", "3")) };
            var doc = S_RODictGranularHostDeepOps.ComputeDelta(a, b);
            Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.DictRemove && (string)o.Key! == "b");
            Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.DictSet && (string)o.Key! == "c" && (string?)o.Value == "3");
            Assert.DoesNotContain(doc.Operations, o => o.Kind == DeltaKind.SetMember);
            var target = new S_RODictGranularHost { Meta = RO(("a", "1"), ("b", "2")) };
            S_RODictGranularHostDeepOps.ApplyDelta(ref target, doc);
            Assert.False(target.Meta!.ContainsKey("b"));
            Assert.Equal("3", target.Meta["c"]);
            Assert.True(S_RODictGranularHostDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void IReadOnlyDictionary_Nested_Object_Value_Emits_DictNested_And_Mutates_Value_Instance()
        {
            var left = new S_Address { Street = "S1", City = "C" };
            var right = new S_Address { Street = "S2", City = "C" };
            var a = new S_RODictNestedHost { Map = RO(("d", left)) };
            var b = new S_RODictNestedHost { Map = RO(("d", right)) };
            var doc = S_RODictNestedHostDeepOps.ComputeDelta(a, b);
            Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.DictNested && (string)o.Key! == "d");
            Assert.DoesNotContain(doc.Operations, o => o is { Kind: DeltaKind.SetMember, Value: IReadOnlyDictionary<string, S_Address> });
            var target = new S_RODictNestedHost { Map = RO(("d", new S_Address { Street = "S1", City = "C" })) };
            var beforeRef = target.Map!["d"];
            S_RODictNestedHostDeepOps.ApplyDelta(ref target, doc);
            Assert.Same(beforeRef, target.Map!["d"]);
            Assert.Equal("S2", target.Map["d"].Street);
            Assert.True(S_RODictNestedHostDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void IReadOnlyDictionary_Null_Transitions_Use_SetMember_At_Member_Scope()
        {
            var a = new S_RODictGranularHost { Meta = null };
            var b = new S_RODictGranularHost { Meta = RO(("k", "v")) };
            var doc = S_RODictGranularHostDeepOps.ComputeDelta(a, b);
            Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.SetMember);
            var target = new S_RODictGranularHost { Meta = null };
            S_RODictGranularHostDeepOps.ApplyDelta(ref target, doc);
            Assert.NotNull(target.Meta);
            Assert.Equal("v", target.Meta!["k"]);
            Assert.True(S_RODictGranularHostDeepEqual.AreDeepEqual(b, target));
        }

        [Fact] public void IReadOnlyDictionary_NoOps_When_Equal()
        {
            var a = new S_RODictGranularHost { Meta = RO(("x", "1"), ("y", "2")) };
            var b = new S_RODictGranularHost { Meta = RO(("x", "1"), ("y", "2")) };
            var doc = S_RODictGranularHostDeepOps.ComputeDelta(a, b);
            Assert.True(doc.IsEmpty);
            var target = new S_RODictGranularHost { Meta = RO(("x", "1"), ("y", "2")) };
            S_RODictGranularHostDeepOps.ApplyDelta(ref target, doc);
            Assert.True(S_RODictGranularHostDeepEqual.AreDeepEqual(b, target));
        }
    }
}
