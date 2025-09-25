using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using DeepEqual;
using DeepEqual.Generator.Shared;
using DeepEqual.RewrittenTests.Domain;
using Xunit;
using static DeepEqual.RewrittenTests.UnifiedFixture;

namespace DeepEqual.RewrittenTests
{
    /// <summary>
    /// A single, exhaustive test class for deep equality of the Order graph.
    /// It aggregates and extends the existing coverage (collections, polymorphism, dynamics, options).
    /// Every test reuses the same baseline Order via MakeBaseline()/Clone() to avoid proliferating ad-hoc models.
    /// </summary>
    public sealed class FullEqualityTests
    {
        // --------------------------------------------------------
        // 0. Fundamental equality semantics
        // --------------------------------------------------------

        [Fact]
        public void Baseline_Equals_Its_Clone()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Reflexive_Symmetric_Consistent()
        {
            var a = MakeBaseline();
            Assert.True(a.AreDeepEqual(a)); // reflexive

            var b = Clone(a);
            Assert.True(a.AreDeepEqual(b)); // symmetric (1)
            Assert.True(b.AreDeepEqual(a)); // symmetric (2)

            // consistency under repeated calls
            for (int i = 0; i < 5; i++)
            {
                Assert.True(a.AreDeepEqual(b));
            }
        }

        [Fact]
        public void Transitive_When_All_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            var c = Clone(a);

            Assert.True(a.AreDeepEqual(b));
            Assert.True(b.AreDeepEqual(c));
            Assert.True(a.AreDeepEqual(c)); // transitive
        }

        [Fact]
        public void NonNull_Vs_Null_Is_Not_Equal()
        {
            var a = MakeBaseline();
            Order? n = null;
            Assert.False(a.AreDeepEqual(n));
        }

        // --------------------------------------------------------
        // 1. Primitives & timestamps
        // --------------------------------------------------------

        [Fact]
        public void Id_Differs_Is_Not_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.Id = a.Id + "-X";
            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void Status_Differs_Is_Not_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.Status = OrderStatus.Completed;
            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void CreatedUtc_Differs_Is_Not_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.CreatedUtc = a.CreatedUtc.AddSeconds(1);
            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void Offset_Differs_Is_Not_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.Offset = a.Offset.AddMinutes(1);
            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void TimeSpan_Differs_Is_Not_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.Span = a.Span.Add(TimeSpan.FromSeconds(30));
            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void Nullable_Value_Toggles_Affect_Equality()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            // both set identically in fixture
            Assert.True(a.AreDeepEqual(b));

            // one side null, other has value
            b.MaybeDiscount = null;
            Assert.False(a.AreDeepEqual(b));

            // both null -> equal
            a.MaybeDiscount = null;
            Assert.True(a.AreDeepEqual(b));

            // different values -> not equal
            b.MaybeDiscount = 7.5m;
            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void Nullable_DateTime_Toggles_Affect_Equality()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            b.MaybeWhen = null;
            Assert.False(a.AreDeepEqual(b));

            a.MaybeWhen = null;
            Assert.True(a.AreDeepEqual(b));

            b.MaybeWhen = new DateTime(2024, 01, 05, 00, 00, 00, DateTimeKind.Utc);
            Assert.False(a.AreDeepEqual(b));
        }

        // --------------------------------------------------------
        // 2. Customer (nested object) & arrays
        // --------------------------------------------------------

        [Fact]
        public void Customer_Name_Differs_Is_Not_Equal_Default_Ordinal()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.Customer.Name = a.Customer.Name.ToUpperInvariant();
            Assert.False(a.AreDeepEqual(b)); // default StringComparison = Ordinal
        }

        [Fact]
        public void Customer_Name_Case_Insensitive_With_Option()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.Customer.Name = a.Customer.Name.ToUpperInvariant();

            var ctx = new ComparisonContext(new ComparisonOptions { StringComparison = StringComparison.OrdinalIgnoreCase });
            Assert.True(a.AreDeepEqual(b, ctx));
        }

        [Fact]
        public void Customer_Vip_Toggle_Affects_Equality()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.Customer.Vip = !a.Customer.Vip;
            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void Customer_Tags_Are_Ordered_Arrays()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            // reorder on b
            Array.Reverse(b.Customer.Tags);
            Assert.False(a.AreDeepEqual(b));

            // restore, equality returns
            Array.Reverse(b.Customer.Tags);
            Assert.True(a.AreDeepEqual(b));

            // change a value
            b.Customer.Tags[0] = b.Customer.Tags[0] + "_X";
            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void Notes_Array_Differs_Is_Not_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.Notes[1] = "changed";
            Assert.False(a.AreDeepEqual(b));
        }

        // --------------------------------------------------------
        // 3. Lines (unordered, keyed by Sku) & Widgets (ordered)
        // --------------------------------------------------------

        [Fact]
        public void Lines_Order_Irrelevant_When_Same_Membership_By_Sku()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            // Reorder 'b' lines: move last to first
            var last = b.Lines[^1];
            b.Lines.RemoveAt(b.Lines.Count - 1);
            b.Lines.Insert(0, last);

            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Lines_Keyed_Equality_Respects_Qty_And_Price()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            // Change Qty for SKU "A"
            var la = b.Lines.First(l => l.Sku == "A");
            la.Qty++;

            Assert.False(a.AreDeepEqual(b));

            // Restore and change Price
            b = Clone(a);
            b.Lines.First(l => l.Sku == "B").Price += 0.01m;
            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void Lines_Membership_Differs_Is_Not_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            // Remove one line
            b.Lines.RemoveAt(0);
            Assert.False(a.AreDeepEqual(b));

            // Or duplicate a Sku (cardinality mismatch)
            b = Clone(a);
            b.Lines.Add(new OrderLine { Sku = "A", Qty = 1, Price = 2.5m, Notes = "dup" });
            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void Widgets_Are_Ordered_Collections()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            // If there are widgets, swap order
            if (b.Widgets.Count == 0)
            {
                b.Widgets.Add(new Widget { Id = "W1", Count = 1 });
                b.Widgets.Add(new Widget { Id = "W2", Count = 2 });
                // a has none — inequality already
                Assert.False(a.AreDeepEqual(b));
                return;
            }

            if (b.Widgets.Count > 1)
            {
                (b.Widgets[0], b.Widgets[1]) = (b.Widgets[1], b.Widgets[0]);
                Assert.False(a.AreDeepEqual(b));
            }
        }

        // --------------------------------------------------------
        // 4. Dictionaries & dynamics (Props, Bag, Expando)
        // --------------------------------------------------------

        [Fact]
        public void Props_Add_Remove_Change_Affects_Equality()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            // add key
            b.Props["added"] = 1;
            Assert.False(a.AreDeepEqual(b));

            // remove key
            b = Clone(a);
            b.Props.Remove("feature");
            Assert.False(a.AreDeepEqual(b));

            // change nested (dictionary) value
            b = Clone(a);
            var child = (Dictionary<string, object?>)b.Props["child"]!;
            child["sub"] = 321; // was 123
            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void Props_String_And_Double_Tolerance_Options()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            // feature differs by casing
            b.Props["feature"] = ((string)b.Props["feature"]!).ToUpperInvariant();

            // numerical within tolerance (threshold ~ 0.75)
            b.Props["threshold"] = 0.7500000001;

            var ctx = new ComparisonContext(new ComparisonOptions
            {
                StringComparison = StringComparison.OrdinalIgnoreCase,
                DoubleEpsilon = 1e-8
            });

            Assert.True(a.AreDeepEqual(b, ctx));
        }

        [Fact]
        public void Props_Keys_Are_Case_Sensitive_By_Default()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            // Introduce distinct key with different casing
            b.Props.Remove("feature");
            b.Props["FEATURE"] = "x";
            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void Bag_Behaves_As_Dictionary_By_Content()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            // change value
            b.Bag["bagv"] = "v2";
            Assert.False(a.AreDeepEqual(b));

            // remove
            b = Clone(a);
            b.Bag.Remove("bagv");
            Assert.False(a.AreDeepEqual(b));

            // add
            b = Clone(a);
            b.Bag["new"] = 123;
            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void Expando_Nested_Changes_Affect_Equality()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            var dict = (IDictionary<string, object?>)b.Expando;
            dict["path"] = "rootX";
            Assert.False(a.AreDeepEqual(b));

            b = Clone(a);
            var nested = (ExpandoObject)((IDictionary<string, object?>)b.Expando)["nested"]!;
            ((IDictionary<string, object?>)nested)["flag"] = false; // was true
            Assert.False(a.AreDeepEqual(b));

            b = Clone(a);
            ((IDictionary<string, object?>)b.Expando)["extra"] = 42;
            Assert.False(a.AreDeepEqual(b));
        }

        // --------------------------------------------------------
        // 5. Collections with ordering semantics (Queue/Stack/LinkedList) and sets
        // --------------------------------------------------------

        [Fact]
        public void Queue_Is_Ordered()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            // rotate queue in b (dequeue+enqueue)
            b.Queue.Enqueue(b.Queue.Dequeue());
            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void Stack_Is_Ordered_LIFO()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            b.Stack.Clear();
            b.Stack.Push(2);
            b.Stack.Push(1);

            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void LinkedList_Is_Ordered()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            // move first to last
            var first = b.Linked.First!.Value;
            b.Linked.RemoveFirst();
            b.Linked.AddLast(first);

            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void HashSet_Flags_Are_OrderInsensitive_And_CaseInsensitive()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            // Recreate with different insertion order and casing
            b.Flags.Clear();
            b.Flags.Add("y");
            b.Flags.Add("x");

            Assert.True(a.AreDeepEqual(b, new ComparisonContext(new ComparisonOptions{StringComparison = StringComparison.OrdinalIgnoreCase})));

            b.Flags.Add("Z");
            Assert.False(a.AreDeepEqual(b, new ComparisonContext(new ComparisonOptions { StringComparison = StringComparison.OrdinalIgnoreCase })));
        }

        [Fact]
        public void SortedSet_Membership_Differs_Is_Not_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.Sorted.Add(99);
            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void Meta_Dictionary_Differs_Is_Not_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            // change a value for existing key
            var key = b.Meta.Keys.First();
            b.Meta[key] = "X";
            Assert.False(a.AreDeepEqual(b));

            // add new key
            b = Clone(a);
            b.Meta[Guid.NewGuid()] = "NEW";
            Assert.False(a.AreDeepEqual(b));

            // remove key
            b = Clone(a);
            var first = b.Meta.Keys.First();
            b.Meta.Remove(first);
            Assert.False(a.AreDeepEqual(b));
        }

        // --------------------------------------------------------
        // 6. Binary-like payloads
        // --------------------------------------------------------

        [Fact]
        public void Bytes_Content_Differs_Is_Not_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            b.Bytes[1] ^= 0xFF; // flip a byte
            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void ReadOnlyMemoryBlob_Content_Differs_Is_Not_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            var blob = b.Blob.ToArray();
            blob[0] ^= 0xFF;
            b.Blob = new ReadOnlyMemory<byte>(blob);
            Assert.False(a.AreDeepEqual(b));
        }

        // --------------------------------------------------------
        // 7. Multidimensional arrays
        // --------------------------------------------------------

        [Fact]
        public void Grid_Differs_On_Element_Or_Shape()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            // change element
            b.Grid[0, 1] += 10;
            Assert.False(a.AreDeepEqual(b));

            // change shape
            b = Clone(a);
            b.Grid = new int[,] { { 1, 2, 3 }, { 4, 5, 6 } }; // 2x3 instead of 2x2
            Assert.False(a.AreDeepEqual(b));
        }

        // --------------------------------------------------------
        // 8. Polymorphic field (IShape) & numeric edge cases
        // --------------------------------------------------------

        [Fact]
        public void Shape_Different_Runtime_Types_Not_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            a.Shape = new Circle { Radius = 2 };
            b.Shape = new Square { Side = 2 };
            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void Shape_Same_Type_Same_Values_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            a.Shape = new Square { Side = 3 };
            b.Shape = new Square { Side = 3 };
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Shape_Both_Null_Are_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            a.Shape = null;
            b.Shape = null;
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Doubles_NaN_Treated_As_Equal_When_Option_Enabled()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            a.Shape = new Circle { Radius = double.NaN };
            b.Shape = new Circle { Radius = double.NaN };

            var ctx = new ComparisonContext(new ComparisonOptions { TreatNaNEqual = true });
            Assert.True(a.AreDeepEqual(b, ctx));
        }

        [Fact]
        public void Doubles_NegativeZero_Equals_PositiveZero()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            a.Shape = new Circle { Radius = -0.0 };
            b.Shape = new Circle { Radius = 0.0 };
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void DoubleTolerance_Applies_Via_Options()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            a.Shape = new Circle { Radius = 1.0 };
            b.Shape = new Circle { Radius = 1.0 + 1e-10 };

            // default: exact -> not equal
            Assert.False(a.AreDeepEqual(b));

            // with epsilon -> equal
            var ctx = new ComparisonContext(new ComparisonOptions { DoubleEpsilon = 1e-8 });
            Assert.True(a.AreDeepEqual(b, ctx));
        }

        [Fact]
        public void DecimalTolerance_Applies_Via_Options_On_Prices()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            // bump price by tiny amount within epsilon
            b.Lines[0].Price += 0.0000000000000000001m; // 1e-19

            // default exact -> not equal
            Assert.False(a.AreDeepEqual(b));

            // with epsilon -> equal
            var ctx = new ComparisonContext(new ComparisonOptions { DecimalEpsilon = 1e-18m });
            Assert.True(a.AreDeepEqual(b, ctx));
        }

        // --------------------------------------------------------
        // 9. External types (ExternalRoot via [ExternalDeepComparable])
        // --------------------------------------------------------

        [Fact]
        public void External_Object_Differs_Is_Not_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            b.External!.ExternalId = b.External.ExternalId + "-X";
            Assert.False(a.AreDeepEqual(b));

            b = Clone(a);
            b.External!.Meta["k"] = "w";
            Assert.False(a.AreDeepEqual(b));

            b = Clone(a);
            b.External = null;
            Assert.False(a.AreDeepEqual(b));
        }


        private static Dictionary<string, object?> MakeSelfCyclicDict(string name, object? payload = null)
        {
            var d = new Dictionary<string, object?>();
            d["name"] = name;
            d["self"] = d; // self-loop
            if (payload != null) d["payload"] = payload;
            return d;
        }

        [Fact]
        public void Cycles_SelfReference_On_Root_Order_Via_Props_Are_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            a.Props["self"] = a; // Order -> Props["self"] -> (this Order)
            b.Props["self"] = b;

            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Cycles_SelfReference_Present_Only_On_One_Side_Is_Not_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            a.Props["self"] = a;
            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void Cycles_TwoNode_Dictionary_Cycle_Are_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            var a1 = new Dictionary<string, object?>(); var a2 = new Dictionary<string, object?>();
            a1["next"] = a2; a2["next"] = a1; a1["v"] = 1; a2["v"] = 2;

            var b1 = new Dictionary<string, object?>(); var b2 = new Dictionary<string, object?>();
            b1["next"] = b2; b2["next"] = b1; b1["v"] = 1; b2["v"] = 2;

            a.Props["cycle"] = a1; b.Props["cycle"] = b1;
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Cycles_TwoNode_Dictionary_Cycle_Broken_On_One_Side_Is_Not_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            var a1 = new Dictionary<string, object?>(); var a2 = new Dictionary<string, object?>();
            a1["next"] = a2; a2["next"] = a1; a1["v"] = 1; a2["v"] = 2;

            var b1 = new Dictionary<string, object?>(); var b2 = new Dictionary<string, object?>();
            b1["next"] = b2; b2["next"] = null; // break the cycle
            b1["v"] = 1; b2["v"] = 2;

            a.Props["cycle"] = a1; b.Props["cycle"] = b1;
            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void Cycles_Expando_SelfReference_Are_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            dynamic ea = new ExpandoObject();
            ((IDictionary<string, object?>)ea)["self"] = ea;

            dynamic eb = new ExpandoObject();
            ((IDictionary<string, object?>)eb)["self"] = eb;

            a.Props["expCycle"] = (object)ea;
            b.Props["expCycle"] = (object)eb;

            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Cycles_List_SelfReference_Are_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            var la = new List<object?>(); la.Add(la);   // list → itself
            var lb = new List<object?>(); lb.Add(lb);

            a.Props["listCycle"] = la; 
            b.Props["listCycle"] = lb;
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Cycles_Nested_Path_Returns_To_Root_Are_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            // a: Props["nest"] -> dict["list"] -> list[0] -> a (root)
            var da = new Dictionary<string, object?>(); var la = new List<object?>();
            da["list"] = la; la.Add(a); a.Props["nest"] = da;

            // b mirrors back to its own root
            var db = new Dictionary<string, object?>(); var lb = new List<object?>();
            db["list"] = lb; lb.Add(b); b.Props["nest"] = db;

            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Cycles_Different_Shapes_Are_Not_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            // a: self-cyclic dict
            a.Props["cyc"] = MakeSelfCyclicDict("one");

            // b: two-node cycle
            var b1 = new Dictionary<string, object?>(); var b2 = new Dictionary<string, object?>();
            b1["next"] = b2; b2["next"] = b1;
            b.Props["cyc"] = b1;

            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void Cycles_Same_Shape_But_Internal_Value_Differs_Is_Not_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            var a1 = new Dictionary<string, object?>(); var a2 = new Dictionary<string, object?>();
            a1["next"] = a2; a2["next"] = a1; a1["v"] = 7; a2["v"] = 9;

            var b1 = new Dictionary<string, object?>(); var b2 = new Dictionary<string, object?>();
            b1["next"] = b2; b2["next"] = b1; b1["v"] = 7; b2["v"] = 8; // differs

            a.Props["cycle"] = a1; b.Props["cycle"] = b1;
            Assert.False(a.AreDeepEqual(b));
        }
    }
}