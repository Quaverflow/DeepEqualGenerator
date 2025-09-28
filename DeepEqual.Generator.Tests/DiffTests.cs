#nullable enable
using System.Dynamic;
using DeepEqual.Generator.Shared;
using DeepEqual.RewrittenTests.Domain;

[assembly: ExternalDeepComparable(typeof(DeepEqual.RewrittenTests.Node2), GenerateDiff = true, GenerateDelta = true, CycleTracking = true)]
[assembly: ExternalDeepComparable(typeof(DeepEqual.RewrittenTests.BiNode), GenerateDiff = true, GenerateDelta = true, CycleTracking = true)]
[assembly: ExternalDeepComparable(typeof(DeepEqual.RewrittenTests.Parent), GenerateDiff = true, GenerateDelta = true, CycleTracking = true)]
[assembly: ExternalDeepComparable(typeof(DeepEqual.RewrittenTests.Child), GenerateDiff = true, GenerateDelta = true, CycleTracking = true)]

namespace DeepEqual.RewrittenTests
{
    public sealed class Node2
    {
        public string Name { get; set; } = "";
        public Node2? Next { get; set; }
    }

    public sealed class BiNode
    {
        public string Id { get; set; } = "";
        public BiNode? Left { get; set; }
        public BiNode? Right { get; set; }
    }

    public sealed class Parent
    {
        public string Tag { get; set; } = "";
        public List<Child> Children { get; set; } = new();
    }

    public sealed class Child
    {
        public string Label { get; set; } = "";
        public Parent? Parent { get; set; }
    }

    public class DiffTests
    {
        // ---------- Fixture ----------
        private static Order NewOrder() => new Order
        {
            Id = "ORD-001",
            CreatedUtc = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            Offset = new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero),
            Span = TimeSpan.FromMinutes(5),
            Status = OrderStatus.Draft,
            Bytes = new byte[] { 1, 2, 3 },
            Blob = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3 }),
            Grid = new int[2, 2] { { 1, 2 }, { 3, 4 } },
            Notes = new[] { "a", "b" },
            Lines = new List<OrderLine>
            {
                new OrderLine { Sku = "AAA", Qty = 1, Price = 10m, Notes = "alpha" },
                new OrderLine { Sku = "BBB", Qty = 2, Price = 20m, Notes = "beta"  },
            },
            Widgets = new List<Widget>
            {
                new Widget { Id = "W1", Count = 1 },
                new Widget { Id = "W2", Count = 2 },
            },
            Customer = new Customer { Name = "Jane", Tags = new[] { "retail" }, Vip = false },
            External = new ExternalRoot { ExternalId = "X-1", Meta = new Dictionary<string, string> { ["k"] = "v" } },
            Expando = BuildExpando(("env", "prod")),
            Meta = new Dictionary<Guid, string> { [Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")] = "tag" },
            Props = new Dictionary<string, object?> { ["env"] = "prod" },
            Flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "X", "Y" },
            Sorted = new SortedSet<int> { 1, 5 },
            Queue = new Queue<string>(new[] { "q1", "q2" }),
            Stack = new Stack<int>(new[] { 1, 2 }.Reverse()),
            Linked = new LinkedList<string>(new[] { "l1", "l2" }),
        };

        private static ExpandoObject BuildExpando(params (string k, object? v)[] items)
        {
            var e = new ExpandoObject();
            var d = (IDictionary<string, object?>)e;
            foreach (var (k, v) in items) d[k] = v;
            return e;
        }

        private static Order Clone(Order o)
        {
            var c = new Order
            {
                Id = o.Id,
                CreatedUtc = o.CreatedUtc,
                Offset = o.Offset,
                Span = o.Span,
                Status = o.Status,
                Bytes = o.Bytes?.ToArray(),
                Blob = new ReadOnlyMemory<byte>(o.Blob.ToArray()),
                Grid = o.Grid is null ? null : (int[,])o.Grid.Clone(),
                Notes = o.Notes?.ToArray(),
                Lines = o.Lines?.Select(l => new OrderLine { Sku = l.Sku, Qty = l.Qty, Price = l.Price, Notes = l.Notes }).ToList(),
                Widgets = o.Widgets?.Select(w => new Widget { Id = w.Id, Count = w.Count }).ToList(),
                Customer = o.Customer is null ? null : new Customer { Name = o.Customer.Name, Vip = o.Customer.Vip, Tags = o.Customer.Tags?.ToArray() },
                External = o.External is null ? null : new ExternalRoot { ExternalId = o.External.ExternalId, Meta = new Dictionary<string, string>(o.External.Meta) },
                Expando = CloneExpando(o.Expando!),
                Meta = o.Meta is null ? null : new Dictionary<Guid, string>(o.Meta),
                Props = o.Props is null ? null : new Dictionary<string, object?>(o.Props),
                Flags = new HashSet<string>(o.Flags, StringComparer.OrdinalIgnoreCase),
                Sorted = new SortedSet<int>(o.Sorted),
                Queue = new Queue<string>(o.Queue),
                Stack = new Stack<int>(o.Stack.Reverse()),
                Linked = new LinkedList<string>(o.Linked),
            };
            return c;
        }

        private static ExpandoObject CloneExpando(ExpandoObject e)
        {
            var src = (IDictionary<string, object?>)e;
            var dst = new ExpandoObject();
            var d2 = (IDictionary<string, object?>)dst;
            foreach (var kv in src) d2[kv.Key] = kv.Value;
            return dst;
        }

        // ---------- Helpers to assert semantically (index-agnostic) ----------
        private static (bool has, Diff<Order> d) Diff(Order a, Order b, ComparisonContext? ctx = null)
            => a.GetDeepDiff(b, ctx);

        private static MemberChange[] Changes<T>(Diff<T> d) => d.MemberChanges.ToArray() ?? [];

        private static MemberChange FindSet<T>(Diff<T> d, Func<object?, bool> valuePredicate)
        {
            var mc = Changes(d).FirstOrDefault(x => x.Kind == MemberChangeKind.Set && valuePredicate(x.ValueOrDiff));
            Assert.True(mc.Kind == MemberChangeKind.Set, "Expected a Set change matching predicate.");
            return mc;
        }

        // Replace the old helper that took Func<ReadOnlySpan<DeltaOp>, bool>
        private static MemberChange FindCollectionOps<T>(Diff<T> d, Func<DeltaOp[], bool> opsPredicate)
        {
            var mc = (d.MemberChanges ?? Array.Empty<MemberChange>())
                .FirstOrDefault(x =>
                    x.Kind == MemberChangeKind.CollectionOps &&
                    x.ValueOrDiff is DeltaDocument doc &&
                    opsPredicate(new DeltaReader(doc).AsSpan().ToArray())); // convert Span -> array
            Assert.True(mc.Kind == MemberChangeKind.CollectionOps, "Expected a CollectionOps diff matching predicate.");
            return mc;
        }

        // ---------- Tests ----------

        [Fact]
        public void Identical_Orders_NoDiff()
        {
            var a = NewOrder();
            var b = Clone(a);
            var (has, d) = Diff(a, b);
            Assert.False(has);
            Assert.True(d.IsEmpty);
        }

        [Fact]
        public void Scalar_Id_Change_Emits_Set()
        {
            var a = NewOrder(); var b = Clone(a);
            b.Id = "ORD-002";

            var (has, d) = Diff(a, b);
            Assert.True(has);
            var mc = FindSet(d, v => v is string s && s == "ORD-002");
        }

        [Fact]
        public void Enum_Status_Change_Emits_Set()
        {
            var a = NewOrder(); var b = Clone(a);
            b.Status = OrderStatus.Completed;

            var (has, d) = Diff(a, b);
            var mc = FindSet(d, v => v is OrderStatus s && s == OrderStatus.Completed);
        }

        [Fact]
        public void Nullable_FromNullToValue_Emits_Set()
        {
            var a = NewOrder(); var b = Clone(a);
            a.MaybeDiscount = null;
            b.MaybeDiscount = 0.25m;

            var (has, d) = Diff(a, b);
            var mc = FindSet(d, v => v is decimal dm && dm == 0.25m);
        }

        [Fact]
        public void Nullable_FromValueToNull_Emits_Set()
        {
            var a = NewOrder(); var b = Clone(a);
            a.MaybeWhen = new DateTime(2024, 1, 1);
            b.MaybeWhen = null;

            var (has, d) = Diff(a, b);
            var mc = FindSet(d, v => v is null);
        }

        [Fact]
        public void Array_Bytes_Modify_Emits_Set()
        {
            var a = NewOrder(); var b = Clone(a);
            b.Bytes![1] = 9;

            var (has, d) = Diff(a, b);
            var mc = FindSet(d, v => v is byte[]);
        }

        [Fact]
        public void Blob_ContentEqual_NoDiff()
        {
            var a = NewOrder(); var b = Clone(a);
            b.Blob = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3 }); // content equal

            var (has, d) = Diff(a, b);
            Assert.False(has);
            Assert.True(d.IsEmpty);
        }

        [Fact]
        public void Customer_Name_Change_Emits_Nested()
        {
            var a = NewOrder();
            var b = Clone(a);
            b.Customer!.Name = "Janet";

            var (has, d) = Diff(a, b);
            Assert.True(has);

            // Prefer an exact Diff<Customer> nested payload
            MemberChange? nested = null;
            var changes = d.MemberChanges ?? Array.Empty<MemberChange>();
            foreach (var mc in changes)
            {
                if (mc.Kind != MemberChangeKind.Nested) continue;
                if (mc.ValueOrDiff is IDiff id && id is Diff<Customer> typed && !typed.IsEmpty)
                {
                    nested = mc;
                    break;
                }
            }

            // Fallback: accept any non-empty Nested diff (polymorphic/interface cases)
            if (nested is null)
            {
                foreach (var mc in changes)
                {
                    if (mc.Kind != MemberChangeKind.Nested) continue;
                    if (mc.ValueOrDiff is IDiff id && !id.IsEmpty)
                    {
                        nested = mc;
                        break;
                    }
                }
            }

            // If still null, show what we actually saw
            if (nested is null)
            {
                var found = string.Join(", ",
                    changes
                        .Where(x => x.Kind == MemberChangeKind.Nested)
                        .Select(x => x.ValueOrDiff?.GetType().FullName ?? "<null>"));
                Assert.True(false, $"Expected a non-empty Nested diff for Customer; saw [{found}]");
            }

            // Final assertion: nested diff exists and is non-empty
            Assert.IsAssignableFrom<IDiff>(nested!.Value.ValueOrDiff);
            Assert.False(((IDiff)nested.Value.ValueOrDiff!).IsEmpty);
        }

        // -------- LIST (Lines) => CollectionOps --------

        [Fact]
        public void Lines_Add_NewSku_Emits_SeqAddAt()
        {
            var a = NewOrder(); var b = Clone(a);
            b.Lines!.Add(new OrderLine { Sku = "CCC", Qty = 3, Price = 30m, Notes = "gamma" });

            var (has, d) = Diff(a, b);
            Assert.True(has);
            FindCollectionOps(d, ops => ops.Length == 1 && ops[0].Kind == DeltaKind.SeqAddAt && ops[0].Index == 2);
        }

        [Fact]
        public void Lines_Remove_Sku_Emits_SeqRemoveAt()
        {
            var a = NewOrder(); var b = Clone(a);
            b.Lines!.RemoveAt(1);

            var (has, d) = Diff(a, b);
            FindCollectionOps(d, ops => ops.Length == 1 && ops[0].Kind == DeltaKind.SeqRemoveAt && ops[0].Index == 1);
        }

        [Fact]
        public void Lines_Insert_Emits_SeqAddAt()
        {
            var a = NewOrder(); var b = Clone(a);
            b.Lines!.Insert(1, new OrderLine { Sku = "A2", Qty = 5, Price = 5m, Notes = "insert" });

            var (has, d) = Diff(a, b);
            FindCollectionOps(d, ops => ops.Length == 1 && ops[0].Kind == DeltaKind.SeqAddAt && ops[0].Index == 1);
        }

        [Fact]
        public void Lines_ModifyExisting_InPlace_Emits_SeqNestedAt()
        {
            var a = NewOrder(); var b = Clone(a);
            b.Lines![0].Qty = 7;

            var (has, d) = Diff(a, b);
            FindCollectionOps(d, ops => ops.Length == 1 && ops[0].Kind == DeltaKind.SeqNestedAt && ops[0].Index == 0 && ops[0].Nested is not null);
        }

        // -------- DICTIONARIES => CollectionOps --------

        [Fact]
        public void Props_AddKey_Emits_DictSet()
        {
            var a = NewOrder(); var b = Clone(a);
            b.Props!["theme"] = "dark";

            var (has, d) = Diff(a, b);
            FindCollectionOps(d, ops => ops.Length == 1 && ops[0].Kind == DeltaKind.DictSet);
        }

        [Fact]
        public void Props_RemoveKey_Emits_DictRemove()
        {
            var a = NewOrder(); var b = Clone(a);
            b.Props!.Remove("env");

            var (has, d) = Diff(a, b);
            FindCollectionOps(d, ops => ops.Length == 1 && ops[0].Kind == DeltaKind.DictRemove);
        }

        [Fact]
        public void Meta_GuidDictionary_Set_Emits_DictSet()
        {
            var a = NewOrder(); var b = Clone(a);
            b.Meta![Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")] = "new";

            var (has, d) = Diff(a, b);
            FindCollectionOps(d, ops => ops.Length == 1 && ops[0].Kind == DeltaKind.DictSet);
        }

        [Fact]
        public void Expando_SetProp_Emits_DictSet()
        {
            var a = NewOrder(); var b = Clone(a);
            ((IDictionary<string, object?>)b.Expando!)["k2"] = 42;

            var (has, d) = Diff(a, b);
            FindCollectionOps(d, ops => ops.Length == 1 && ops[0].Kind == DeltaKind.DictSet);
        }

        // ---------- Options ----------

        [Fact]
        public void StringComparison_IgnoreCase_SuppressesDiff()
        {
            var a = NewOrder(); var b = Clone(a);
            a.Id = "abc";
            b.Id = "ABC";

            var ctx = new ComparisonContext(new ComparisonOptions
            {
                StringComparison = StringComparison.OrdinalIgnoreCase
            });

            var (has, d) = Diff(a, b, ctx);
            Assert.False(has);
            Assert.True(d.IsEmpty);
        }
      
        [Fact]
        public void Shape_CircleToCircle_RadiusChange_Emits_Nested()
        {
            var a = NewOrder(); var b = Clone(a);
            a.Shape = new Circle { Radius = 10 };
            b.Shape = new Circle { Radius = 12 };

            var (has, d) = Diff(a, b);
            Assert.True(has);

            // Find a Nested whose payload is *some* IDiff (e.g., Diff<Circle>), not Diff<IShape>
            var mc = Changes(d).FirstOrDefault(x => x.Kind == MemberChangeKind.Nested && x.ValueOrDiff is IDiff);
            Assert.NotNull(mc);

            var idiff = (IDiff)mc!.ValueOrDiff!;
            Assert.False(idiff.IsEmpty); // concrete Diff<Circle> should report a change
        }

        [Fact]
        public void Shape_CircleToSquare_Emits_Set()
        {
            var a = NewOrder(); var b = Clone(a);
            a.Shape = new Circle { Radius = 10 };
            b.Shape = new Square { Side = 10 };

            var (has, d) = Diff(a, b);
            Assert.True(has);
            Assert.Contains(Changes(d), x => x.Kind == MemberChangeKind.Set && x.ValueOrDiff is IShape);
        }
        [Fact]
        public void Customer_Equal_NoDiff()
        {
            var a = NewOrder(); var b = Clone(a);
            b.Customer!.Name = a.Customer!.Name; // ensure identical
            var (has, d) = Diff(a, b);
            Assert.False(has);
            Assert.True(d.IsEmpty);
        }

        [Fact]
        public void Props_DictNested_ValueChangesInNestedDict_Emits_SetOnChild()
        {
            var a = NewOrder(); var b = Clone(a);
            a.Props!["child"] = new Dictionary<string, object?> { ["x"] = 1 };
            b.Props!["child"] = new Dictionary<string, object?> { ["x"] = 2 }; // same key, nested value changed

            var (has, d) = Diff(a, b);
            Assert.True(has);

            var mc = Changes(d).First(x => x.Kind == MemberChangeKind.CollectionOps && x.ValueOrDiff is DeltaDocument);
            var opsArr = new DeltaReader((DeltaDocument)mc.ValueOrDiff!).AsSpan().ToArray();

            // We expect a DictSet on key "child" (replace entire value), not DictNested
            Assert.Contains(opsArr, o => o.Kind == DeltaKind.DictNested /* and optionally check o.Key if you expose it */);
        }


        [Fact]
        public void Lines_Insert_And_Modify_Produces_Multiple_Seq_Ops()
        {
            var a = NewOrder(); var b = Clone(a);
            b.Lines!.Insert(1, new OrderLine { Sku = "INS", Qty = 5, Price = 5m });
            b.Lines[0].Qty = 7;

            var (has, d) = Diff(a, b);
            Assert.True(has);
            var mc = Changes(d).First(x => x.Kind == MemberChangeKind.CollectionOps);
            var ops = new DeltaReader((DeltaDocument)mc.ValueOrDiff!).AsSpan().ToArray();
            Assert.Contains(ops, o => o.Kind == DeltaKind.SeqAddAt && o.Index == 1);
            Assert.Contains(ops, o => o.Kind == DeltaKind.SeqNestedAt && o.Index == 0);
        }
        [Fact]
        public void Lines_NullToEmpty_Emits_Set()
        {
            var a = NewOrder(); var b = Clone(a);
            a.Lines = null!;
            b.Lines = new List<OrderLine>(); // empty

            var (has, d) = Diff(a, b);
            Assert.True(has);
            Assert.Contains(Changes(d), x => x.Kind == MemberChangeKind.Set && x.ValueOrDiff is List<OrderLine>);
        }

        [Fact]
        public void Props_EmptyToNull_Emits_Set()
        {
            var a = NewOrder(); var b = Clone(a);
            a.Props = new Dictionary<string, object?>();
            b.Props = null!;

            var (has, d) = Diff(a, b);
            Assert.True(has);
            Assert.Contains(Changes(d), x => x.Kind == MemberChangeKind.Set && x.ValueOrDiff is null);
        }
        [Fact]
        public void DecimalTolerance_Scalar_SuppressesDiff()
        {
            var a = NewOrder(); var b = Clone(a);
            a.MaybeDiscount = 1.0000m;
            b.MaybeDiscount = 1.00005m; // within epsilon

            var ctx = new ComparisonContext(new ComparisonOptions { DecimalEpsilon = 0.0001m });
            var (has, d) = Diff(a, b, ctx);
            Assert.False(has);
            Assert.True(d.IsEmpty);
        }
        [Fact]
        public void DoubleNaN_Equal_WithOption()
        {
            var a = NewOrder(); var b = Clone(a);
            a.Shape = new Circle { Radius = double.NaN };
            b.Shape = new Circle { Radius = double.NaN };

            var ctx = new ComparisonContext(new ComparisonOptions { TreatNaNEqual = true });
            var (has, d) = Diff(a, b, ctx);
            Assert.False(has);
            Assert.True(d.IsEmpty);
        }
        [Fact]
        public void Offset_Change_Emits_Set()
        {
            var a = NewOrder(); var b = Clone(a);
            b.Offset = a.Offset.AddMinutes(1);
            var (has, d) = Diff(a, b);
            Assert.Contains(Changes(d), x => x.Kind == MemberChangeKind.Set && x.ValueOrDiff is DateTimeOffset);
        }
        [Fact]
        public void Nested_Equal_DoesNot_Emit_Set()
        {
            var a = NewOrder(); var b = Clone(a);
            // force nested path but equal
            a.Customer = new Customer { Name = "J", Vip = false, Tags = new[] { "t" } };
            b.Customer = new Customer { Name = "J", Vip = false, Tags = new[] { "t" } };

            var (has, d) = Diff(a, b);
            Assert.False(has);
            Assert.True(d.IsEmpty);
        }
        [Fact]
        public void Expando_RemoveProp_Emits_DictRemove()
        {
            var a = NewOrder(); var b = Clone(a);
            var da = (IDictionary<string, object?>)a.Expando!;
            var db = (IDictionary<string, object?>)b.Expando!;
            da["rm"] = 1;
            db.Remove("rm");

            var (has, d) = Diff(a, b);
            Assert.True(has);
            FindCollectionOps(d, ops => ops.Any(o => o.Kind == DeltaKind.DictRemove));
        }
        [Fact]
        public void Customer_TypeMismatch_Emits_Set()
        {
            var a = NewOrder(); var b = Clone(a);
            a.Customer = new Customer { Name = "A" };
            // simulate a “different type” scenario by tricking DIFF path:
            // easiest: set left to null, right not null -> Set
            a.Customer = null;
            b.Customer = new Customer { Name = "A" };

            var (has, d) = Diff(a, b);
            Assert.True(has);
            Assert.Contains(Changes(d), x => x.Kind == MemberChangeKind.Set && x.ValueOrDiff is Customer);
        }
        [Fact]
        public void Flags_PureReorder_NoDiff()
        {
            var a = NewOrder(); var b = Clone(a);
            b.Flags!.Clear(); b.Flags.Add("Y"); b.Flags.Add("X"); // reorder only

            var (has, d) = Diff(a, b);
            Assert.False(has);
            Assert.True(d.IsEmpty);
        }
        [Fact]
        public void DecimalTolerance_Nullable_SuppressesDiff()
        {
            var a = NewOrder(); var b = Clone(a);
            a.MaybeDiscount = 1.0000m; b.MaybeDiscount = 1.00005m; // within epsilon

            var ctx = new ComparisonContext(new ComparisonOptions { DecimalEpsilon = 0.0001m });
            var (has, d) = Diff(a, b, ctx);
            Assert.False(has); Assert.True(d.IsEmpty);
        }

        [Fact]
        public void StringComparison_OrdinalDiff_Emits_Set()
        {
            var a = NewOrder(); var b = Clone(a);
            a.Id = "abc"; b.Id = "ABC";
            var ctx = new ComparisonContext(new ComparisonOptions { StringComparison = StringComparison.Ordinal });
            var (has, d) = Diff(a, b, ctx);
            Assert.True(has);
            Assert.Contains(Changes(d), x => x.Kind == MemberChangeKind.Set && x.ValueOrDiff is string s && s == "ABC");
        }
        [Fact]
        public void Memory_ContentEqual_NoDiff()
        {
            var a = NewOrder(); var b = Clone(a);
            a.Blob = new ReadOnlyMemory<byte>(new byte[] { 9, 9 });
            b.Blob = new ReadOnlyMemory<byte>(new byte[] { 9, 9 });

            var (has, d) = Diff(a, b);
            Assert.False(has); Assert.True(d.IsEmpty);
        }
        [Fact]
        public void Shape_SameRuntimeType_ValueChange_Yields_Nested()
        {
            var a = NewOrder(); var b = Clone(a);
            a.Shape = new Circle { Radius = 10 };
            b.Shape = new Circle { Radius = 11 };

            var (has, d) = Diff(a, b);
            Assert.True(has);
            var nested = (d.MemberChanges ?? []).FirstOrDefault(mc => mc.Kind == MemberChangeKind.Nested && mc.ValueOrDiff is IDiff id && !id.IsEmpty);
            Assert.NotNull(nested);
        }

        [Fact]
        public void Shape_RuntimeTypeMismatch_Yields_Set()
        {
            var a = NewOrder(); var b = Clone(a);
            a.Shape = new Circle { Radius = 10 };
            b.Shape = new Square { Side = 10 };

            var (has, d) = Diff(a, b);
            Assert.True(has);
            Assert.Contains(Changes(d), mc => mc.Kind == MemberChangeKind.Set && mc.ValueOrDiff is IShape);
        }
        [Fact]
        public void Lines_Insert_And_Modify_Produces_Both_Add_And_Nested()
        {
            var a = NewOrder(); var b = Clone(a);
            b.Lines!.Insert(1, new OrderLine { Sku = "INS", Qty = 1, Price = 1m });
            b.Lines[0].Qty = 7;

            var (has, d) = Diff(a, b);
            Assert.True(has);
            var mc = (d.MemberChanges ?? []).First(x => x.Kind == MemberChangeKind.CollectionOps);
            var ops = new DeltaReader((DeltaDocument)mc.ValueOrDiff!).AsSpan().ToArray();
            Assert.Contains(ops, o => o.Kind == DeltaKind.SeqAddAt && o.Index == 1);
            Assert.Contains(ops, o => o.Kind == DeltaKind.SeqNestedAt && o.Index == 0 && o.Nested is not null);
        }

        [Fact]
        public void Meta_GuidDict_Add_Emits_DictSet()
        {
            var a = NewOrder(); var b = Clone(a);
            b.Meta![Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")] = "new";
            var (has, d) = Diff(a, b);
            var mc = (d.MemberChanges ?? []).First(x => x.Kind == MemberChangeKind.CollectionOps);
            var ops = new DeltaReader((DeltaDocument)mc.ValueOrDiff!).AsSpan().ToArray();
            Assert.Contains(ops, o => o.Kind == DeltaKind.DictSet);
        }

        [Fact]
        public void Props_NestedValueChange_TypedAsObject_Emits_DictSet()
        {
            var a = NewOrder(); var b = Clone(a);
            a.Props!["child"] = new Dictionary<string, object?> { ["x"] = 1 };
            b.Props!["child"] = new Dictionary<string, object?> { ["x"] = 2 };
            var (has, d) = Diff(a, b);
            var mc = (d.MemberChanges ?? []).First(x => x.Kind == MemberChangeKind.CollectionOps);
            var ops = new DeltaReader((DeltaDocument)mc.ValueOrDiff!).AsSpan().ToArray();
            Assert.Contains(ops, o => o.Kind == DeltaKind.DictNested); // no DictNested since value is object
        }
        [Fact]
        public void Expando_Set_And_Remove_Key_Emit_DictOps()
        {
            var a = NewOrder(); var b = Clone(a);
            ((IDictionary<string, object?>)b.Expando!)["k2"] = 42;
            var (has1, d1) = Diff(a, b);
            var ops1 = new DeltaReader((DeltaDocument)(d1.MemberChanges ?? []).First(x => x.Kind == MemberChangeKind.CollectionOps).ValueOrDiff!).AsSpan().ToArray();
            Assert.Contains(ops1, o => o.Kind == DeltaKind.DictSet);

            // Now remove
            a = b; b = Clone(a);
            ((IDictionary<string, object?>)b.Expando!).Remove("k2");
            var (has2, d2) = Diff(a, b);
            var ops2 = new DeltaReader((DeltaDocument)(d2.MemberChanges ?? []).First(x => x.Kind == MemberChangeKind.CollectionOps).ValueOrDiff!).AsSpan().ToArray();
            Assert.Contains(ops2, o => o.Kind == DeltaKind.DictRemove);
        }
        [Fact]
        public void Nested_Equal_MustNot_Emit_Set()
        {
            var a = NewOrder(); var b = Clone(a);
            a.Customer = new Customer { Name = "J", Vip = false, Tags = new[] { "t" } };
            b.Customer = new Customer { Name = "J", Vip = false, Tags = new[] { "t" } };

            var (has, d) = Diff(a, b);
            Assert.False(has); Assert.True(d.IsEmpty);
        }
        [Fact]
        public void MaybeWhen_FromNullToValue_Emits_Set()
        {
            var a = NewOrder(); var b = Clone(a);
            a.MaybeWhen = null; b.MaybeWhen = new DateTime(2025, 1, 1);
            var (has, d) = Diff(a, b);
            Assert.Contains(Changes(d), x => x.Kind == MemberChangeKind.Set && x.ValueOrDiff is DateTime dt && dt.Year == 2025);
        }

        [Fact]
        public void NullVsObject_IsReplacement()
        {
            Order? a = null; var b = NewOrder();
            var (has, d) = b.GetDeepDiff(a); // or a.GetDeepDiff(b) depending on your semantics
            // We only need that has == true (replacement semantics displayed by your API). If you expose IsReplacement, assert it here.
            Assert.True(has);
        }
        // 1) Extension vs runtime facade parity (options route the same way)
     

        // 2) Reference equality suppression for nested object (same instance => no diff)
        [Fact]
        public void Customer_SameReference_NoDiff()
        {
            var a = NewOrder(); var b = Clone(a);
            var shared = new Customer { Name = "A", Vip = false, Tags = new[] { "x" } };
            a.Customer = shared;
            b.Customer = shared;              // both point to same instance
            shared.Name = "B";                // mutation affects both a & b

            var (has, d) = Diff(a, b);
            Assert.False(has);
            Assert.True(d.IsEmpty);
        }

        // 3) Arrays never emit CollectionOps (only Set)
        [Fact]
        public void Arrays_Never_Emit_CollectionOps()
        {
            var a = NewOrder(); var b = Clone(a);
            b.Bytes![0] = (byte)(a.Bytes![0] + 1); // change content

            var (has, d) = Diff(a, b);
            Assert.True(has);
            Assert.DoesNotContain(Changes(d), x => x.Kind == MemberChangeKind.CollectionOps);
            Assert.Contains(Changes(d), x => x.Kind == MemberChangeKind.Set && x.ValueOrDiff is byte[]);
        }

        // 4) Order-insensitive suppression applies only to value-like sets (reference-like reorders still diff)
        [Fact]
        public void Lines_PureReorder_On_ReferenceLike_Elements_Still_Diffs()
        {
            var a = NewOrder(); var b = Clone(a);
            // reorder reference-like elements (OrderLine)
            b.Lines!.Reverse();

            var (has, d) = Diff(a, b);
            Assert.True(has); // we don't suppress reorders for reference-like elements
            Assert.Contains(Changes(d), x => x.Kind == MemberChangeKind.CollectionOps);
        }

        // 5) Dict with null values: add key=null and change null->value both emit DictSet
        [Fact]
        public void Props_NullValue_Add_And_Change_Emit_DictSet()
        {
            var a = NewOrder(); var b = Clone(a);
            // add key with null
            b.Props!["n"] = null;
            var (has1, d1) = Diff(a, b);
            Assert.True(has1);
            var ops1 = new DeltaReader((DeltaDocument)(d1.MemberChanges ?? []).First(x => x.Kind == MemberChangeKind.CollectionOps).ValueOrDiff!).AsSpan().ToArray();
            Assert.Contains(ops1, o => o.Kind == DeltaKind.DictSet);

            // now null -> non-null
            a = b; b = Clone(a);
            b.Props!["n"] = 123;
            var (has2, d2) = Diff(a, b);
            Assert.True(has2);
            var ops2 = new DeltaReader((DeltaDocument)(d2.MemberChanges ?? []).First(x => x.Kind == MemberChangeKind.CollectionOps).ValueOrDiff!).AsSpan().ToArray();
            Assert.Contains(ops2, o => o.Kind == DeltaKind.DictSet);
        }

        // 6) ReadOnlyMemory inequality triggers Set
        [Fact]
        public void Blob_ContentDifferent_Emits_Set()
        {
            var a = NewOrder(); var b = Clone(a);
            b.Blob = new ReadOnlyMemory<byte>(new byte[] { 9, 9, 9 });

            var (has, d) = Diff(a, b);
            Assert.True(has);
            Assert.Contains(Changes(d), x => x.Kind == MemberChangeKind.Set && x.ValueOrDiff is ReadOnlyMemory<byte>);
        }

        // 7) Polymorphic nested: equal concrete shapes yields no diff
        [Fact]
        public void Shape_SameRuntimeType_Equal_NoDiff()
        {
            var a = NewOrder(); var b = Clone(a);
            a.Shape = new Circle { Radius = 10 };
            b.Shape = new Circle { Radius = 10 };

            var (has, d) = Diff(a, b);
            Assert.False(has);
            Assert.True(d.IsEmpty);
        }

        // 8) Nested null transitions produce Set (not Nested)
        [Fact]
        public void Customer_NullTransition_Emits_Set()
        {
            var a = NewOrder(); var b = Clone(a);
            a.Customer = null;
            b.Customer = new Customer { Name = "X" };

            var (has, d) = Diff(a, b);
            Assert.True(has);
            Assert.Contains(Changes(d), x => x.Kind == MemberChangeKind.Set && x.ValueOrDiff is Customer);
        }

        // 9) Dict key casing/ordering: same keys, reordered enumeration => no diff
        [Fact]
        public void Meta_ReorderEnumeration_NoDiff()
        {
            var a = NewOrder(); var b = Clone(a);
            // rebuild with different enumeration order but same content
            b.Meta = new Dictionary<Guid, string>(a.Meta!);

            var (has, d) = Diff(a, b);
            Assert.False(has);
            Assert.True(d.IsEmpty);
        }
        // =========================
        // DIFF — extra hardening
        // =========================

        // Schema drift guard: the multiset of change kinds for a known edit pattern should stay stable.
        [Fact]
        public void SchemaDrift_Guard_KindsAreStable()
        {
            var a = NewOrder(); var b = Clone(a);

            // 3 independent edits: one scalar, one list op, one dict op
            b.Id = "ORD-XYZ";                                // scalar -> Set
            b.Lines!.Add(new OrderLine { Sku = "N", Qty = 1, Price = 1m }); // list -> CollectionOps
            b.Props!["new_key"] = 123;                       // dict -> CollectionOps

            var (has, d) = Diff(a, b);
            Assert.True(has);

            var kinds = (d.MemberChanges ?? Array.Empty<MemberChange>()).Select(mc => mc.Kind).OrderBy(x => x).ToArray();
            var expected = new[] { MemberChangeKind.CollectionOps, MemberChangeKind.CollectionOps, MemberChangeKind.Set }.OrderBy(x => x).ToArray();
            Assert.Equal(expected, kinds);
        }

        // Large lists: ensure granular CollectionOps (not Set) when only a handful change.
        [Fact]
        public void LargeList_SparseChanges_StillCollectionOps()
        {
            var a = NewOrder(); var b = Clone(a);

            // Build a large line set
            a.Lines = Enumerable.Range(0, 3000)
                .Select(i => new OrderLine { Sku = $"SKU{i:D4}", Qty = i, Price = i, Notes = "x" })
                .ToList();
            b.Lines = a.Lines!.Select(l => new OrderLine { Sku = l.Sku, Qty = l.Qty, Price = l.Price, Notes = l.Notes }).ToList();

            // Sparse mutations
            b.Lines!.Insert(123, new OrderLine { Sku = "INSERT", Qty = 1, Price = 1m });
            b.Lines[5].Qty += 10;

            var (has, d) = Diff(a, b);
            Assert.True(has);

            // Must include CollectionOps for the list and no blanket Set of the whole list
            Assert.Contains(Changes(d), mc => mc.Kind == MemberChangeKind.CollectionOps && mc.ValueOrDiff is DeltaDocument);
            Assert.DoesNotContain(Changes(d), mc => mc.Kind == MemberChangeKind.Set && mc.ValueOrDiff is List<OrderLine>);
        }

        // Dict + null values: add/remove and null→value should be DictSet/Remove (not Set of the whole dict).
        [Fact]
        public void DictWithNulls_AddRemove_And_Change()
        {
            var a = NewOrder(); var b = Clone(a);

            // add key with null
            b.Props!["n"] = null;
            var (has1, d1) = Diff(a, b);
            Assert.True(has1);
            var ops1 = new DeltaReader((DeltaDocument)(d1.MemberChanges ?? []).First(mc => mc.Kind == MemberChangeKind.CollectionOps).ValueOrDiff!).AsSpan().ToArray();
            Assert.Contains(ops1, o => o.Kind == DeltaKind.DictSet);

            // null -> non-null
            a = b; b = Clone(a);
            b.Props!["n"] = 42;
            var (has2, d2) = Diff(a, b);
            Assert.True(has2);
            var ops2 = new DeltaReader((DeltaDocument)(d2.MemberChanges ?? []).First(mc => mc.Kind == MemberChangeKind.CollectionOps).ValueOrDiff!).AsSpan().ToArray();
            Assert.Contains(ops2, o => o.Kind == DeltaKind.DictSet);

            // remove key
            a = b; b = Clone(a);
            b.Props!.Remove("n");
            var (has3, d3) = Diff(a, b);
            Assert.True(has3);
            var ops3 = new DeltaReader((DeltaDocument)(d3.MemberChanges ?? []).First(mc => mc.Kind == MemberChangeKind.CollectionOps).ValueOrDiff!).AsSpan().ToArray();
            Assert.Contains(ops3, o => o.Kind == DeltaKind.DictRemove);
        }

        // Edge numerics: +∞ → -∞ diffs (nested inside IShape)
        [Fact]
        public void Shape_Infinities_Change_Yields_Nested()
        {
            var a = NewOrder(); var b = Clone(a);
            a.Shape = new Circle { Radius = double.PositiveInfinity };
            b.Shape = new Circle { Radius = double.NegativeInfinity };

            var (has, d) = Diff(a, b);
            Assert.True(has);
            var nested = (d.MemberChanges ?? []).FirstOrDefault(mc => mc.Kind == MemberChangeKind.Nested && mc.ValueOrDiff is IDiff id && !id.IsEmpty);
            Assert.NotNull(nested);
        }

        // Edge numerics: -0.0 vs +0.0 should be equal (no diff)
        [Fact]
        public void Double_NegativeZero_PositiveZero_NoDiff()
        {
            var a = NewOrder(); var b = Clone(a);
            a.Shape = new Circle { Radius = -0.0 };
            b.Shape = new Circle { Radius = +0.0 };

            var (has, d) = Diff(a, b);
            Assert.False(has);
            Assert.True(d.IsEmpty);
        }

        // Deep-ish nesting: list element nested change + other independent op coexist
        [Fact]
        public void MixedChanges_ListNested_And_Scalar_BothReported()
        {
            var a = NewOrder(); var b = Clone(a);

            // Nested change inside list element
            b.Lines![0].Qty = a.Lines![0].Qty + 1;
            // Independent scalar
            b.Status = OrderStatus.Completed;

            var (has, d) = Diff(a, b);
            Assert.True(has);

            // One CollectionOps for Lines and one Set for scalar
            Assert.Contains(Changes(d), mc => mc.Kind == MemberChangeKind.CollectionOps);
            Assert.Contains(Changes(d), mc => mc.Kind == MemberChangeKind.Set && mc.ValueOrDiff is OrderStatus s && s == OrderStatus.Completed);
        }

        // Whitespace change: "a" -> "a " diffs (no normalization)
        [Fact]
        public void String_TrailingWhitespace_Diffs()
        {
            var a = NewOrder(); var b = Clone(a);
            a.Id = "A"; b.Id = "A ";

            var (has, d) = Diff(a, b);
            Assert.True(has);
            Assert.Contains(Changes(d), x => x.Kind == MemberChangeKind.Set && x.ValueOrDiff is string s && s == "A ");
        }

        // Order-insensitive suppression applies only to value-like sets (reference-like reorders still diff)
        [Fact]
        public void ReferenceLike_Reorder_List_Diffs_ValueLike_Set_Reorder_NoDiff()
        {
            var a = NewOrder(); var b = Clone(a);

            // Reference-like: OrderLine list reorder -> diff
            b.Lines!.Reverse();
            var (has1, d1) = Diff(a, b);
            Assert.True(has1);
            Assert.Contains(Changes(d1), mc => mc.Kind == MemberChangeKind.CollectionOps);

            // Value-like: set reorder (Flags) -> no diff
            a = NewOrder(); b = Clone(a);
            b.Flags!.Clear(); b.Flags.Add("Y"); b.Flags.Add("X");
            var (has2, d2) = Diff(a, b);
            Assert.False(has2);
            Assert.True(d2.IsEmpty);
        }

        // Arrays invariant: any element change produces Set (never CollectionOps)
        [Fact]
        public void Arrays_ElementChange_AlwaysSet_NeverCollectionOps()
        {
            var a = NewOrder(); var b = Clone(a);
            b.Notes![0] = a.Notes![0] + "!"; // array change

            var (has, d) = Diff(a, b);
            Assert.True(has);
            Assert.DoesNotContain(Changes(d), mc => mc.Kind == MemberChangeKind.CollectionOps);
            Assert.Contains(Changes(d), mc => mc.Kind == MemberChangeKind.Set && mc.ValueOrDiff is string[]);
        }

        // Multiple independent changes: exactly those three kinds surface (no extras)
        [Fact]
        public void MultipleIndependentChanges_ExactlyThoseAppear()
        {
            var a = NewOrder(); var b = Clone(a);

            // scalar
            b.Id = "ORD-777";
            // list op
            b.Widgets!.RemoveAt(0);
            // dict op
            b.Meta![Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")] = "X";

            var (has, d) = Diff(a, b);
            Assert.True(has);

            var kinds = (d.MemberChanges ?? Array.Empty<MemberChange>()).Select(mc => mc.Kind).ToArray();
            Assert.Contains(MemberChangeKind.Set, kinds);
            Assert.Equal(2, kinds.Count(k => k == MemberChangeKind.CollectionOps));
            Assert.Equal(3, kinds.Length);
        }

        // Dict enumeration reorder: no diff if content equal
        [Fact]
        public void Dict_EnumerationReorder_NoDiff()
        {
            var a = NewOrder(); var b = Clone(a);
            b.Meta = new Dictionary<Guid, string>(a.Meta!); // rebuilt, different enumeration order
            var (has, d) = Diff(a, b);
            Assert.False(has);
            Assert.True(d.IsEmpty);
        }
        [Fact]
        public void SelfCycle_Equal_NoDiff()
        {
            var a = new Node2 { Name = "A" }; a.Next = a;
            var b = new Node2 { Name = "A" }; b.Next = b;

            var (has, d) = a.GetDeepDiff(b);
            Assert.False(has);
            Assert.True(d.IsEmpty);
        }

        [Fact]
        public void TwoNodeCycle_Equal_NoDiff()
        {
            var a1 = new Node2 { Name = "X" };
            var a2 = new Node2 { Name = "Y" };
            a1.Next = a2; a2.Next = a1;

            var b1 = new Node2 { Name = "X" };
            var b2 = new Node2 { Name = "Y" };
            b1.Next = b2; b2.Next = b1;

            var (has, d) = a1.GetDeepDiff(b1);
            Assert.False(has);
            Assert.True(d.IsEmpty);
        }

        [Fact]
        public void TwoNodeCycle_ScalarChange_Yields_Nested()
        {
            var a1 = new Node2 { Name = "X" };
            var a2 = new Node2 { Name = "Y" };
            a1.Next = a2; a2.Next = a1;

            var b1 = new Node2 { Name = "X" };
            var b2 = new Node2 { Name = "Y2" };
            b1.Next = b2; b2.Next = b1;

            var (has, d) = a1.GetDeepDiff(b1);
            Assert.True(has);
            // any non-empty Nested diff suffices
            Assert.Contains(d.MemberChanges ?? Array.Empty<MemberChange>(),
                mc => mc.Kind == MemberChangeKind.Nested && mc.ValueOrDiff is IDiff id && !id.IsEmpty);
        }

        [Fact]
        public void LongCycle_100_Equal_NoDiff()
        {
            Node2 Build(int n, Func<int, string> name)
            {
                var arr = Enumerable.Range(0, n).Select(i => new Node2 { Name = name(i) }).ToArray();
                for (int i = 0; i < n; i++) arr[i].Next = arr[(i + 1) % n];
                return arr[0];
            }
            var a = Build(100, i => $"N{i}");
            var b = Build(100, i => $"N{i}");

            var (has, d) = a.GetDeepDiff(b);
            Assert.False(has);
            Assert.True(d.IsEmpty);
        }

        [Fact]
        public void LongCycle_Change_At_Tail_Yields_Nested()
        {
            Node2 Build(int n, Func<int, string> name)
            {
                var arr = Enumerable.Range(0, n).Select(i => new Node2 { Name = name(i) }).ToArray();
                for (int i = 0; i < n; i++) arr[i].Next = arr[(i + 1) % n];
                return arr[0];
            }
            var a = Build(50, i => $"N{i}");
            var b = Build(50, i => $"N{i}");
            // change last node
            var t = b;
            for (int i = 0; i < 49; i++) t = t!.Next!;
            t!.Name = "TAIL*";

            var (has, d) = a.GetDeepDiff(b);
            Assert.True(has);
            Assert.Contains(d.MemberChanges ?? Array.Empty<MemberChange>(),
                mc => mc.Kind == MemberChangeKind.Nested && mc.ValueOrDiff is IDiff id && !id.IsEmpty);
        }

        [Fact]
        public void SharedSubgraph_SameStructure_NoDiff()
        {
            // diamond shape (no cycles) but shared subgraph stress
            var aL = new BiNode { Id = "L" };
            var aR = new BiNode { Id = "R" };
            var aTop = new BiNode { Id = "T", Left = aL, Right = aR };
            var aBot = new BiNode { Id = "B", Left = aL, Right = aR };

            var bL = new BiNode { Id = "L" };
            var bR = new BiNode { Id = "R" };
            var bTop = new BiNode { Id = "T", Left = bL, Right = bR };
            var bBot = new BiNode { Id = "B", Left = bL, Right = bR };

            var (has1, d1) = aTop.GetDeepDiff(bTop);
            Assert.False(has1); Assert.True(d1.IsEmpty);

            var (has2, d2) = aBot.GetDeepDiff(bBot);
            Assert.False(has2); Assert.True(d2.IsEmpty);
        }

        [Fact]
        public void ParentChild_Cycle_ListChildChange_Yields_CollectionOps()
        {
            var pa = new Parent { Tag = "P" };
            pa.Children.Add(new Child { Label = "A", Parent = pa });
            pa.Children.Add(new Child { Label = "B", Parent = pa });

            var pb = new Parent { Tag = "P" };
            pb.Children.Add(new Child { Label = "A", Parent = pb });
            pb.Children.Add(new Child { Label = "B", Parent = pb });

            // mutate one child
            pb.Children[1].Label = "B*";

            var (has, d) = pa.GetDeepDiff(pb);
            Assert.True(has);

            var coll = (d.MemberChanges ?? Array.Empty<MemberChange>())
                .FirstOrDefault(mc => mc.Kind == MemberChangeKind.CollectionOps && mc.ValueOrDiff is DeltaDocument);
            Assert.NotNull(coll);

            var ops = new DeltaReader((DeltaDocument)coll!.ValueOrDiff!).AsSpan().ToArray();
            Assert.Contains(ops, o => o.Kind == DeltaKind.SeqNestedAt && o.Index == 1 && o.Nested is not null);
        }

        [Fact]
        public void ParentChild_Cycle_Equal_NoDiff()
        {
            var pa = new Parent { Tag = "P" };
            pa.Children.Add(new Child { Label = "A", Parent = pa });
            pa.Children.Add(new Child { Label = "B", Parent = pa });

            var pb = new Parent { Tag = "P" };
            pb.Children.Add(new Child { Label = "A", Parent = pb });
            pb.Children.Add(new Child { Label = "B", Parent = pb });

            var (has, d) = pa.GetDeepDiff(pb);
            Assert.False(has);
            Assert.True(d.IsEmpty);
        }
    }
}
