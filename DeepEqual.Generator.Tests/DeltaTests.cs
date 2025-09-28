#nullable enable
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using DeepEqual.Generator.Shared;
using DeepEqual.RewrittenTests.Domain;
using Newtonsoft.Json;
using Xunit;
using static DeepEqual.RewrittenTests.UnifiedFixture;

namespace DeepEqual.RewrittenTests
{
    /// <summary>
    /// Delta (patch) tests that mirror the scope of our Diff and Eq coverage.
    /// Uses extension-method surface: ComputeDeepDelta / ApplyDeepDelta / IsEmpty.
    /// We search deltas recursively since Seq*/Dict* ops are nested under member-level deltas.
    /// </summary>
    public class DeltaTests
    {
        // ---------- Minimal helpers bound to your new API ----------
        private static DeltaDocument Delta(Order? a, Order b, ComparisonContext? ctx = null)
            => a.ComputeDeepDelta(b, ctx);

        private static DeltaOp[] RootOps(DeltaDocument d) => new DeltaReader(d).AsSpan().ToArray();

        // Walk all nested delta docs (collection/dict ops live under NestedMember)
        private static IEnumerable<DeltaOp> EnumerateDeep(DeltaDocument d)
        {
            var stack = new Stack<DeltaDocument>();
            stack.Push(d);

            while (stack.Count > 0)
            {
                var cur = stack.Pop();

                // IMPORTANT: materialize the span before yielding anything
                var opsArr = new DeltaReader(cur).AsSpan().ToArray();

                foreach (var op in opsArr)
                {
                    yield return op;
                    if (op.Nested is not null)
                        stack.Push(op.Nested);
                }
            }
        }

        private static bool ContainsDeep(DeltaDocument d, Func<DeltaOp, bool> predicate)
            => EnumerateDeep(d).Any(predicate);


        private static void Apply(ref Order target, DeltaDocument d)
            => target.ApplyDeepDelta(d);

        // ---------- Basic ----------
        [Fact]
        public void Identical_Orders_EmptyDelta()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            var d = Delta(a, b);
            Assert.True(d.IsEmpty);
        }

        [Fact]
        public void Single_Primitive_Change_Sets_Member_And_Patches()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.Id = a.Id + "-X";

            var d = Delta(a, b);
            Assert.Contains(RootOps(d), o => o.Kind == DeltaKind.SetMember);

            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Enum_Change_Sets_Member_And_Patches()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.Status = OrderStatus.Completed;

            var d = Delta(a, b);
            Assert.Contains(RootOps(d), o => o.Kind == DeltaKind.SetMember);

            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Nullable_FromNull_ToValue_Sets_Member_And_Patches()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            a.MaybeDiscount = null;
            b.MaybeDiscount = 0.25m;

            var d = Delta(a, b);
            Assert.Contains(RootOps(d), o => o.Kind == DeltaKind.SetMember);

            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Nullable_FromValue_ToNull_Sets_Member_And_Patches()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.MaybeWhen = null;

            var d = Delta(a, b);
            Assert.Contains(RootOps(d), o => o.Kind == DeltaKind.SetMember);

            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Offset_Change_Sets_Member_And_Patches()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.Offset = a.Offset.AddMinutes(3);

            var d = Delta(a, b);
            Assert.Contains(RootOps(d), o => o.Kind == DeltaKind.SetMember);

            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(b));
        }

        // ---------- Lists (ordered + unordered/keyed) ----------
        [Fact]
        public void Widgets_Insert_Emits_SeqAddAt_And_Patches()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.Widgets.Insert(1, new Widget { Id = "WX", Count = 9 });

            var d = Delta(a, b);
            Assert.True(ContainsDeep(d, o => o.Kind == DeltaKind.SeqAddAt && o.Value is Widget));

            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Widgets_Remove_Emits_SeqRemoveAt_And_Patches()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.Widgets.RemoveAt(0);

            var d = Delta(a, b);
            Assert.True(ContainsDeep(d, o => o.Kind == DeltaKind.SeqRemoveAt));

            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Lines_ModifyExisting_InPlace_Emits_SeqNestedAt_And_Patches()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.Lines[0].Qty += 5;

            var d = Delta(a, b);
            Assert.True(ContainsDeep(d, o => o.Kind == DeltaKind.SeqNestedAt && o.Nested is not null),
                        "Expected a SeqNestedAt op with nested delta for Lines modification.");

            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Lines_Insert_Emits_SeqAddAt_And_Patches()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.Lines.Insert(1, new OrderLine { Sku = "INS", Qty = 1, Price = 1m });

            var d = Delta(a, b);
            Assert.True(ContainsDeep(d, o => o.Kind == DeltaKind.SeqAddAt && o.Value is OrderLine));

            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Lines_Remove_Emits_SeqRemoveAt_And_Patches()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.Lines.RemoveAt(1);

            var d = Delta(a, b);
            Assert.True(ContainsDeep(d, o => o.Kind == DeltaKind.SeqRemoveAt));

            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Lines_Reorder_Is_Ignored_Due_To_Keyed_Unordered()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            (b.Lines[0], b.Lines[1]) = (b.Lines[1], b.Lines[0]);

            var d = Delta(a, b);
            Assert.True(d.IsEmpty);

            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Lines_Insert_And_Modify_Produces_Both_Add_And_Nested()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.Lines.Insert(1, new OrderLine { Sku = "NEW", Qty = 1, Price = 1m });
            b.Lines[0].Qty += 2;

            var d = Delta(a, b);
            Assert.True(ContainsDeep(d, o => o.Kind == DeltaKind.SeqAddAt && o.Value is OrderLine));
            Assert.True(ContainsDeep(d, o => o.Kind == DeltaKind.SeqNestedAt && o.Nested is not null));

            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(b));
        }

        // ---------- Dictionaries ----------
        [Fact]
        public void Props_Set_Value_Emits_DictSet_And_Patches()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.Props["feature"] = "y";

            var d = Delta(a, b);
            Assert.True(ContainsDeep(d, o => o.Kind == DeltaKind.DictSet && (o.Key?.ToString() == "feature")));

            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Props_Remove_Key_Emits_DictRemove_And_Patches()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.Props.Remove("feature");

            var d = Delta(a, b);
            Assert.True(ContainsDeep(d, o => o.Kind == DeltaKind.DictRemove && (o.Key?.ToString() == "feature")));

            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Props_DictNested_Updates_Nested_Key()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            a.Props["child"] = new Dictionary<string, object?> { ["sub"] = 1 };
            b.Props["child"] = new Dictionary<string, object?> { ["sub"] = 2 };

            var d = Delta(a, b);

            // Find DictNested for "child" anywhere, then assert inside it we set "sub" to 2.
            var childDoc = EnumerateDeep(d)
                .FirstOrDefault(o => o.Kind == DeltaKind.DictNested && (o.Key?.ToString() == "child") && o.Nested is not null)
                .Nested;

            Assert.NotNull(childDoc);

            var nestedOps = new DeltaReader(childDoc!).AsSpan().ToArray();
            Assert.Contains(nestedOps, o => o.Kind == DeltaKind.DictSet && (o.Key?.ToString() == "sub") && (o.Value is int iv && iv == 2));

            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Expando_Add_Remove_And_Nested_Work()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            ((IDictionary<string, object?>)b.Expando)["extra"] = 123;
            ((IDictionary<string, object?>)b.Expando).Remove("path");

            var nested = (ExpandoObject)((IDictionary<string, object?>)b.Expando)["nested"]!;
            ((IDictionary<string, object?>)nested)["flag"] = false;

            var d = Delta(a, b);

            Assert.True(ContainsDeep(d, o => o.Kind == DeltaKind.DictSet && (o.Key?.ToString() == "extra")));
            Assert.True(ContainsDeep(d, o => o.Kind == DeltaKind.DictRemove && (o.Key?.ToString() == "path")));
            Assert.True(ContainsDeep(d, o => o.Kind == DeltaKind.DictNested && (o.Key?.ToString() == "nested") && o.Nested is not null), JsonConvert.SerializeObject(d));

            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Meta_ReorderEnumeration_NoDelta()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.Meta = new Dictionary<Guid, string>(a.Meta);

            var d = Delta(a, b);
            Assert.True(d.IsEmpty);
        }

        // ---------- Polymorphic member ----------
        [Fact]
        public void Shape_Same_RuntimeType_Emits_NestedMember()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            a.Shape = new Circle { Radius = 10.0 };
            b.Shape = new Circle { Radius = 12.0 };

            var d = Delta(a, b);
            Assert.Contains(RootOps(d), o => o.Kind == DeltaKind.NestedMember && o.Nested is not null);

            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Shape_RuntimeType_Mismatch_Emits_SetMember()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            a.Shape = new Circle { Radius = 10.0 };
            b.Shape = new Square { Side = 10.0 };

            var d = Delta(a, b);
            Assert.Contains(RootOps(d), o => o.Kind == DeltaKind.SetMember && o.Value is IShape);

            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(b));
        }

        // ---------- Root-level ReplaceObject ----------
        [Fact]
        public void Root_Null_Vs_Object_Produces_ReplaceObject_And_Apply_Replaces()
        {
            Order? a = null;
            var b = MakeBaseline();

            var d = a.ComputeDeepDelta(b);
            Assert.True(ContainsDeep(d, o => o.Kind == DeltaKind.ReplaceObject));

            var target = new Order { Id = "dummy" };
            target = target.ApplyDeepDelta(d);

            Assert.NotNull(target);
            Assert.True(target.AreDeepEqual(b));
        }
        [Fact]
        public void Lines_ReorderOnly_NoOps()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            (b.Lines[0], b.Lines[1]) = (b.Lines[1], b.Lines[0]);

            var d = Delta(a, b);
            Assert.True(d.IsEmpty);
        }

        [Fact]
        public void Lines_ModifyExisting_Yields_SeqNestedAt()
        {
            var a = MakeBaseline(); var b = Clone(a);
            b.Lines[0].Price += 1m;

            var d = Delta(a, b);
            Assert.True(ContainsDeep(d, o => o.Kind == DeltaKind.SeqNestedAt && o.Nested is not null));
            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(b));
        }
        [Fact]
        public void Expando_NestedValueChange_Yields_DictNested()
        {
            var a = MakeBaseline(); var b = Clone(a);
            var nested = (ExpandoObject)((IDictionary<string, object?>)b.Expando)["nested"]!;
            ((IDictionary<string, object?>)nested)["flag"] = false;

            var d = Delta(a, b);
            Assert.True(ContainsDeep(d, o => o.Kind == DeltaKind.DictNested && o.Key?.ToString() == "nested" && o.Nested is not null));
            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Props_Child_Dict_Is_Opaque_Change_Yields_DictSet()
        {
            var a = MakeBaseline(); var b = Clone(a);
            a.Props["child"] = new Dictionary<string, object?> { ["x"] = 1 };
            b.Props["child"] = new Dictionary<string, object?> { ["x"] = 2 };

            var d = Delta(a, b);
            Assert.True(ContainsDeep(d, o => o.Kind == DeltaKind.DictSet /* && o.Key=="child" */));
            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(b));
        }
        [Fact]
        public void Bytes_ElementChange_ReplacesWholeArray()
        {
            var a = MakeBaseline(); var b = Clone(a);
            b.Bytes![0] ^= 0xFF;
            var d = Delta(a, b);
            Assert.True(ContainsDeep(d, o => o.MemberIndex == 2 && o.Kind == DeltaKind.SetMember));
            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(b));
        }
   
        [Fact]
        public void Root_Null_To_Object_ReplaceObject_And_Apply_ReplacesRef()
        {
            Order? a = null;
            var b = MakeBaseline();

            var d = Delta(a, b);  // <- no a!
            Assert.True(ContainsDeep(d, o => o.Kind == DeltaKind.ReplaceObject));

            a = a.ApplyDeepDelta(d);   // <- capture the replaced instance
            Assert.NotNull(a);
            Assert.True(a!.AreDeepEqual(b));
        }

        [Fact]
        public void Grid_CellChange_ReplacesWholeMatrix()
        {
            var a = MakeBaseline(); var b = Clone(a);
            b.Grid![0, 0] += 1;
            var d = Delta(a, b);
            Assert.True(ContainsDeep(d, o => o.MemberIndex == 8 && o.Kind == DeltaKind.SetMember));
        }

        // ---------- Options feed into delta ----------
        [Fact]
        public void DoubleEpsilon_Suppresses_Tiny_Changes()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            a.Shape = new Circle { Radius = 10.0 };
            b.Shape = new Circle { Radius = 10.0 + 1e-7 };

            var ctx = new ComparisonContext(new ComparisonOptions { DoubleEpsilon = 1e-6 });
            var d = Delta(a, b, ctx);
            Assert.True(d.IsEmpty);

            b.Shape = new Circle { Radius = 10.0 + 1e-3 };
            d = Delta(a, b, ctx);
            Assert.Contains(RootOps(d), o => o.Kind == DeltaKind.NestedMember && o.Nested is not null);
        }

        // ---------- Idempotency ----------
        [Fact]
        public void Apply_Is_Idempotent()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            b.Id = "ORD-ALT";
            b.Customer.Name = "Alice";
            b.Lines[1].Qty += 10;
            b.Props["feature"] = "y";
            b.Widgets.Insert(0, new Widget { Id = "WZ", Count = 3 });

            var d = Delta(a, b);
            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(b));

            var d2 = Delta(a, b);
            Assert.True(d2.IsEmpty);

            Apply(ref a, d2);
            Assert.True(a.AreDeepEqual(b));
        }

        // ---------- Empty delta application ----------
        [Fact]
        public void Apply_Empty_Delta_Does_Not_Change_Target()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            var d = Delta(a, b);
            Assert.True(d.IsEmpty, JsonConvert.SerializeObject(d));

            var before = Clone(a);
            Apply(ref a, d);
            Assert.True(a.AreDeepEqual(before));
        }
    }
}
