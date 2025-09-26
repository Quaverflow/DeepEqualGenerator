#nullable enable
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using DeepEqual.Generator.Shared;
using DeepEqual.RewrittenTests.Domain;
using Xunit;

namespace DeepEqual.RewrittenTests
{
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

        private static MemberChange FindNestedOf<T, TNested>(Diff<T> d)
        {
            var mc = Changes(d).FirstOrDefault(x =>
                x.Kind == MemberChangeKind.Nested &&
                x.ValueOrDiff is IDiff id &&
                id.GetType() == typeof(Diff<TNested>));
            Assert.True(mc.Kind == MemberChangeKind.Nested, $"Expected a Nested diff of type {typeof(TNested).Name}.");
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
            var a = NewOrder(); var b = Clone(a);
            b.Customer!.Name = "Janet";

            var (has, d) = Diff(a, b);
            Assert.True(has);
            var mc = FindNestedOf<Order, Customer>(d);
            Assert.False(((Diff<Customer>)mc.ValueOrDiff!).IsEmpty);
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
    }
}
