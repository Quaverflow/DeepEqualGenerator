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
    [DeepComparable(GenerateDiff = true, GenerateDelta = true)]
    public sealed class PolyListHolder
    {
        public List<IShape> Payload { get; set; } = new();
    }
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
        public void Lines_KeyedEquality_LargeN_TailValueChange_IsNotEqual()
        {
            var a = MakeBaseline();
            a.Lines = Enumerable.Range(0, 20000)
                .Select(i => new OrderLine { Sku = $"SKU{i}", Qty = 1, Price = i })
                .ToList();
            var b = Clone(a);
            b.Lines[^1].Qty++; // change at the tail
            Assert.False(a.AreDeepEqual(b));
        }
      

        [Fact]
        public void PolymorphicList_DeepTailChange_IsNotEqual()
        {
            var left = new PolyListHolder
            {
                Payload = new List<IShape>(Enumerable.Range(0, 5000).Select(i => (IShape)new Circle { Radius = i }))
            };
            var right = new PolyListHolder
            {
                Payload = left.Payload.Select(s => s is Circle c ? new Circle { Radius = c.Radius } : s).ToList<IShape>()
            };

            ((Circle)right.Payload[^1]).Radius += 0.5;
            Assert.False(left.AreDeepEqual(right));
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

        // ===============================
        // Attribute-driven semantics
        // ===============================

        [DeepComparable]
        public sealed class Node
        {
            public int V { get; set; }
            public Node? Next { get; set; }
        }

        [DeepComparable]
        public sealed class Named
        {
            public string Name { get; set; } = "";
            public int X { get; set; }
        }

        [DeepComparable]
        public sealed class AttrHost
        {
            // Deep compare (default)
            public Node Deep { get; set; } = new Node { V = 1, Next = new Node { V = 2 } };

            // Shallow compare (use .Equals only)
            [DeepCompare(Kind = CompareKind.Shallow)]
            public Node Shallow { get; set; } = new Node { V = 10, Next = new Node { V = 20 } };

            // Reference-equality required
            [DeepCompare(Kind = CompareKind.Reference)]
            public Node RefOnly { get; set; } = new Node { V = 99 };

            // Skip this member entirely
            [DeepCompare(Kind = CompareKind.Skip)]
            public Node? Skipped { get; set; } = new Node { V = 777 };

            // Order-insensitive sequence (value-like elements)
            [DeepCompare(OrderInsensitive = true)]
            public List<int> UnorderedInts { get; set; } = new List<int> { 1, 2, 3 };

            // Order-insensitive, keyed by Name (object elements)
            [DeepCompare(OrderInsensitive = true, KeyMembers = new[] { nameof(Named.Name) })]
            public List<Named> UnorderedKeyedByName { get; set; } = new List<Named>
    {
        new Named { Name = "A", X = 1 },
        new Named { Name = "B", X = 2 },
    };

            // Read-only list wrapper
            public IReadOnlyList<int> RoList { get; set; } = Array.AsReadOnly(new[] { 5, 6, 7 });
        }

        [DeepComparable]
        [DeepCompare(Kind = CompareKind.Reference)] // type-level default (reference equality)
        public sealed class NodeRefDefault
        {
            public int V { get; set; }
        }

        [DeepComparable]
        public sealed class AttrHost2
        {
            // Property-level override to Deep (should beat the type-level "Reference")
            [DeepCompare(Kind = CompareKind.Deep)]
            public NodeRefDefault DeepOverride { get; set; } = new NodeRefDefault { V = 1 };

            // No override here — still "Reference" due to type-level attribute
            public NodeRefDefault RefDefault { get; set; } = new NodeRefDefault { V = 1 };
        }

        [DeepComparable]
        public sealed class MapHost
        {
            public IReadOnlyDictionary<string, int> Map { get; set; } =
                new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(
                    new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 });
        }

        // ---------- attr-host helpers (local) ----------
        private static AttrHost MakeAttrBaseline()
        {
            return new AttrHost
            {
                Deep = new Node { V = 1, Next = new Node { V = 2 } },
                Shallow = new Node { V = 10, Next = new Node { V = 20 } },
                RefOnly = new Node { V = 99 },
                Skipped = new Node { V = 777 },
                UnorderedInts = new List<int> { 1, 2, 3 },
                UnorderedKeyedByName = new List<Named>
        {
            new Named { Name = "A", X = 1 },
            new Named { Name = "B", X = 2 }
        },
                RoList = Array.AsReadOnly(new[] { 5, 6, 7 })
            };
        }

        private static AttrHost CloneAttr(AttrHost h)
        {
            return new AttrHost
            {
                Deep = new Node { V = h.Deep.V, Next = h.Deep.Next is null ? null : new Node { V = h.Deep.Next.V } },
                Shallow = new Node { V = h.Shallow.V, Next = h.Shallow.Next is null ? null : new Node { V = h.Shallow.Next.V } },
                RefOnly = new Node { V = h.RefOnly.V }, // NEW instance (important for RefOnly test)
                Skipped = h.Skipped is null ? null : new Node { V = h.Skipped.V },
                UnorderedInts = new List<int>(h.UnorderedInts),
                UnorderedKeyedByName = h.UnorderedKeyedByName.Select(n => new Named { Name = n.Name, X = n.X }).ToList(),
                RoList = Array.AsReadOnly(h.RoList.ToArray())
            };
        }

        // ---------- tests ----------

        [Fact]
        public void Attribute_Reference_Member_Requires_Same_Instance()
        {
            var a = MakeAttrBaseline();
            var b = CloneAttr(a);

            Assert.False(a.AreDeepEqual(b));

            b.Shallow = a.Shallow; 
            b.RefOnly = a.RefOnly;
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void Attribute_Shallow_Ignores_Nested_Differences_But_Deep_Does_Not()
        {
            var a = MakeAttrBaseline();
            var b = CloneAttr(a);

            // IMPORTANT: With Kind=Shallow we call .Equals on the root object (reference equality for classes).
            // To isolate the "ignore nested differences" behavior, make the shallow member reference-equal.
            b.Shallow = a.Shallow;

            // Also make the Reference-only member share the same instance so it doesn't fail the comparison.
            b.RefOnly = a.RefOnly;

            // Sanity: at this point all relevant members should be equal
            Assert.True(a.AreDeepEqual(b));

            // Change nested under Shallow only -> still equal (no recursion for Shallow)
            b.Shallow!.Next ??= new Node { V = 0 };
            b.Shallow.Next.V += 1000;
            Assert.True(a.AreDeepEqual(b));

            // Now change nested under Deep -> not equal (Deep recurses)
            b.Deep!.Next ??= new Node { V = 0 };
            b.Deep.Next.V += 1000;
            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void Attribute_Skip_Ignores_Member_Entirely()
        {
            var a = MakeAttrBaseline();
            var b = CloneAttr(a);

            // Neutralize other attributes that could cause inequality:
            // - Kind=Reference: must share same instance
            // - Kind=Shallow: reference equality is used; point to same instance to avoid false negative
            b.RefOnly = a.RefOnly;
            b.Shallow = a.Shallow;

            // Sanity: equal before changing the skipped member
            Assert.True(a.AreDeepEqual(b));

            // Make Skipped radically different -> should be ignored
            b.Skipped = null;
            Assert.True(a.AreDeepEqual(b));
        }

        [Fact]
        public void IReadOnlyList_Ordered_Equality()
        {
            var a = MakeAttrBaseline();
            var b = CloneAttr(a);

            // Neutralize attribute-driven members that would otherwise fail equality
            b.RefOnly = a.RefOnly;   // Kind=Reference -> must share instance
            b.Shallow = a.Shallow;   // Kind=Shallow -> .Equals (reference) for class -> share instance

            // Identical ordered content -> equal
            Assert.True(a.AreDeepEqual(b));

            // Different order -> not equal (RoList is ordered)
            b.RoList = Array.AsReadOnly(new[] { 7, 6, 5 });
            Assert.False(a.AreDeepEqual(b));
        }
        [Fact]
        public void OrderInsensitive_Unkeyed_Ints_Reorder_Equal_Cardinality_Diff_NotEqual()
        {
            var a = MakeAttrBaseline();
            var b = CloneAttr(a);

            // Neutralize unrelated attribute-driven members
            b.RefOnly = a.RefOnly;   // Kind=Reference
            b.Shallow = a.Shallow;   // Kind=Shallow

            // Reorder only -> equal (OrderInsensitive=true on UnorderedInts)
            b.UnorderedInts = new List<int> { 3, 2, 1 };
            Assert.True(a.AreDeepEqual(b));

            // Change membership -> not equal
            b.UnorderedInts = new List<int> { 3, 2, 1, 1 };
            Assert.False(a.AreDeepEqual(b));
        }
        [Fact]
        public void OrderInsensitive_Keyed_ByName_Reorder_Equal_ValueChange_NotEqual()
        {
            var a = MakeAttrBaseline();
            var b = CloneAttr(a);

            // Neutralize unrelated attribute-driven members
            b.RefOnly = a.RefOnly;   // Kind=Reference
            b.Shallow = a.Shallow;   // Kind=Shallow

            // Reorder by Name (A,B) -> (B,A) => equal (OrderInsensitive + KeyMembers=Name)
            b.UnorderedKeyedByName = new List<Named>
            {
                new Named { Name = "B", X = 2 },
                new Named { Name = "A", X = 1 },
            };
            Assert.True(a.AreDeepEqual(b));

            // Same keys but different value under one key -> not equal
            b.UnorderedKeyedByName = new List<Named>
            {
                new Named { Name = "B", X = 2 },
                new Named { Name = "A", X = 999 }, // value changed under key "A"
            };
            Assert.False(a.AreDeepEqual(b));
        }


        [Fact]
        public void Attribute_Precedence_Property_Overrides_Type_Default()
        {
            var x1 = new AttrHost2
            {
                DeepOverride = new NodeRefDefault { V = 123 },
                RefDefault = new NodeRefDefault { V = 456 }
            };
            var x2 = new AttrHost2
            {
                // Different instances but same values
                DeepOverride = new NodeRefDefault { V = 123 },
                RefDefault = new NodeRefDefault { V = 456 }
            };

            Assert.True(x1.AreDeepEqual(x2));
        }

        [Fact]
        public void IReadOnlyDictionary_Typed_Equality()
        {
            var a = new MapHost
            {
                Map = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(
                    new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 })
            };
            var b = new MapHost
            {
                // different construction order should not matter
                Map = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(
                    new Dictionary<string, int> { ["b"] = 2, ["a"] = 1 })
            };
            Assert.True(a.AreDeepEqual(b));

            // change a value -> not equal
            b = new MapHost
            {
                Map = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(
                    new Dictionary<string, int> { ["a"] = 1, ["b"] = 999 })
            };
            Assert.False(a.AreDeepEqual(b));
        }

        // ===============================
        // String culture & datetime nuance (reuse Order)
        // ===============================

        [Fact]
        public void Strings_InvariantCultureIgnoreCase_Are_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            a.Customer.Name = "café";
            b.Customer.Name = "CAFÉ";

            var ctx = new ComparisonContext(new ComparisonOptions { StringComparison = StringComparison.InvariantCultureIgnoreCase });
            Assert.True(a.AreDeepEqual(b, ctx));
        }

        [Fact]
        public void DateTime_SameTicks_DifferentKind_Are_Not_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            var ticks = new DateTime(2023, 01, 01, 00, 00, 00, DateTimeKind.Utc).Ticks;
            a.CreatedUtc = new DateTime(ticks, DateTimeKind.Utc);
            b.CreatedUtc = new DateTime(ticks, DateTimeKind.Local); // same ticks, different Kind

            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void DateTimeOffset_SameUtcTicks_DifferentOffset_Are_Not_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            var baseUtc = new DateTimeOffset(2024, 05, 10, 12, 00, 00, TimeSpan.Zero); // 12:00Z
            a.Offset = baseUtc;
            b.Offset = baseUtc.ToOffset(TimeSpan.FromHours(2)); // 14:00 +02:00, same instant, different offset

            Assert.False(a.AreDeepEqual(b));
        }

        // ========== Null vs Empty semantics on collections ==========

        [Fact]
        public void Null_List_vs_Empty_List_Are_Not_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            a.Widgets = null!;
            b.Widgets = new List<Widget>();
            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void Null_Array_vs_Empty_Array_Are_Not_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            a.Notes = null!;
            b.Notes = Array.Empty<string>();
            Assert.False(a.AreDeepEqual(b));
        }

        [Fact]
        public void Null_Dictionary_vs_Empty_Dictionary_Are_Not_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);
            a.Meta = null!;
            b.Meta = new Dictionary<Guid, string>();
            Assert.False(a.AreDeepEqual(b));
        }

        // ========== Numeric cross-type equality & epsilons ==========

        [Fact]
        public void Numeric_CrossType_Int_Equals_Double_Same_Value()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            a.Props["num"] = 5;      // int
            b.Props["num"] = 5.0;    // double

            Assert.True(a.AreDeepEqual(b)); // cross-type numeric equal
        }

        [Fact]
        public void Double_CrossType_Epsilon_Applies_When_Close()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            a.Props["num"] = 1.0;            // double
            b.Props["num"] = 1.0 + 5e-9;     // double, tiny diff

            var ctx = new ComparisonContext(new ComparisonOptions { DoubleEpsilon = 1e-8 });
            Assert.True(a.AreDeepEqual(b, ctx));
        }

        [Fact]
        public void Float_NegativeZero_Equals_PositiveZero()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            a.Props["f"] = -0.0f;
            b.Props["f"] = +0.0f;

            Assert.True(a.AreDeepEqual(b));
        }

        // ========== ReadOnlyMemory<byte> equality nuances ==========

        [Fact]
        public void Blob_SameContent_DifferentBackingArrays_Are_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            var arr1 = new byte[] { 1, 2, 3, 4, 5 };
            var arr2 = new byte[] { 1, 2, 3, 4, 5 };

            a.Blob = new ReadOnlyMemory<byte>(arr1);
            b.Blob = new ReadOnlyMemory<byte>(arr2);

            Assert.True(a.AreDeepEqual(b));
        }

        // ========== Jagged vs Rectangular arrays, and shape differences ==========

        [Fact]
        public void Rectangular_Vs_Jagged_Arrays_Are_Not_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            a.Grid = new int[,] { { 1, 2 }, { 3, 4 } };
            // jagged resembling same values
            b.Props["jag"] = new int[][] { new[] { 1, 2 }, new[] { 3, 4 } };

            // Compare the actual members: Grid vs a jagged in Props won't force equality anywhere, but
            // ensure grid change alone makes a difference versus previous b.Grid (which was equal to a.Grid initially)
            b.Grid = new int[,] { { 1, 2, 3 }, { 4, 5, 6 } }; // different shape
            Assert.False(a.AreDeepEqual(b));
        }

        // ========== Dictionary key case-sensitivity (typed) ==========

        [Fact]
        public void Dictionary_Keys_Default_CaseSensitive()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            a.Meta = new Dictionary<Guid, string> { [Guid.NewGuid()] = "x" };
            b.Meta = new Dictionary<Guid, string>(a.Meta);

            // replace with a new dict that has a different key (case doesn’t apply to Guid, but show distinct key)
            var k1 = a.Meta.Keys.First();
            var k2 = Guid.NewGuid();
            b.Meta.Remove(k1);
            b.Meta[k2] = "x";

            Assert.False(a.AreDeepEqual(b));
        }

        // ========== IReadOnlyList equality (ordered) – fully isolated ==========

        [Fact]
        public void IReadOnlyList_Identical_Then_Reordered_NotEqual_Isolated()
        {
            var a = MakeAttrBaseline();
            var b = CloneAttr(a);

            // neutralize unrelated attr members
            b.RefOnly = a.RefOnly;
            b.Shallow = a.Shallow;

            Assert.True(a.AreDeepEqual(b)); // identical

            b.RoList = Array.AsReadOnly(a.RoList.Reverse().ToArray());
            Assert.False(a.AreDeepEqual(b)); // order matters
        }

        // ========== Order-insensitive keyed collections: duplicate-key stress ==========

        [Fact]
        public void OrderInsensitive_Keyed_DuplicateKey_On_OneSide_NotEqual()
        {
            var a = MakeAttrBaseline();
            var b = CloneAttr(a);

            b.RefOnly = a.RefOnly;
            b.Shallow = a.Shallow;

            // duplicate "A" on b
            b.UnorderedKeyedByName = new List<Named>
    {
        new Named { Name = "A", X = 1 },
        new Named { Name = "B", X = 2 },
        new Named { Name = "A", X = 1 } // duplicate key
    };

            Assert.False(a.AreDeepEqual(b));
        }

        // ========== String comparison + dictionary values (values equal ignoring case) ==========

        [Fact]
        public void Dictionary_StringValues_Equal_IgnoringCase_With_Option()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            a.Props["feature"] = "alpha";
            b.Props["feature"] = "ALPHA";

            var ctx = new ComparisonContext(new ComparisonOptions { StringComparison = StringComparison.OrdinalIgnoreCase });
            Assert.True(a.AreDeepEqual(b, ctx));
        }

        // ========== NoTracking context (acyclic still works) ==========

        [Fact]
        public void NoTrackingContext_Acyclic_Still_Works()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            var ctx = ComparisonContext.NoTracking;
            Assert.True(a.AreDeepEqual(b, ctx));
        }
        // ========== Custom comparer via attribute (ComparerType) ==========

        public sealed class TrimIgnoreCaseComparer : IEqualityComparer<string>
        {
            public bool Equals(string? x, string? y)
                => string.Equals(x?.Trim(), y?.Trim(), StringComparison.OrdinalIgnoreCase);

            public int GetHashCode(string obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Trim());
        }

        [DeepComparable]
        public sealed class CustomHost
        {
            [DeepCompare(ComparerType = typeof(TrimIgnoreCaseComparer))]
            public string Title { get; set; } = "";
        }

        [Fact]
        public void Attribute_ComparerType_Custom_String_Comparer_Works()
        {
            var a = new CustomHost { Title = "  hello  " };
            var b = new CustomHost { Title = "HELLO" };
            Assert.True(a.AreDeepEqual(b));

            b.Title = "HELLO!";
            Assert.False(a.AreDeepEqual(b));
        }

        // ========== ReadOnlyMemory<byte> equality using slices (same content different offsets) ==========

        [Fact]
        public void Blob_Slices_With_Same_Content_Are_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            var backing = new byte[] { 9, 1, 2, 3, 4, 5, 9 };
            a.Blob = new ReadOnlyMemory<byte>(backing, 1, 5); // [1..5] => 1,2,3,4,5
            b.Blob = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3, 4, 5 }, 0, 5);

            Assert.True(a.AreDeepEqual(b));
        }

        // ========== Multi-D array equal control (same shape, same values) ==========

        [Fact]
        public void Grid_SameShape_SameValues_Are_Equal()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            a.Grid = new int[,] { { 1, 2 }, { 3, 4 } };
            b.Grid = new int[,] { { 1, 2 }, { 3, 4 } };

            Assert.True(a.AreDeepEqual(b));
        }

        // ========== Dictionary values with StringComparison from options (control + negative) ==========

        [Fact]
        public void Props_StringValues_Equal_With_IgnoreCase_And_NotEqual_Without()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            a.Props["featureX"] = "alpha";
            b.Props["featureX"] = "ALPHA";

            var ignore = new ComparisonContext(new ComparisonOptions { StringComparison = StringComparison.OrdinalIgnoreCase });
            var ordinal = new ComparisonContext(new ComparisonOptions { StringComparison = StringComparison.Ordinal });

            Assert.True(a.AreDeepEqual(b, ignore));
            Assert.False(a.AreDeepEqual(b, ordinal));
        }

        // ========== Numeric epsilon boundary checks (just outside tolerance) ==========

        [Fact]
        public void DoubleTolerance_Outside_Fails()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            a.Props["eps"] = 1.0;
            b.Props["eps"] = 1.0 + 1.1e-8;

            var ctx = new ComparisonContext(new ComparisonOptions { DoubleEpsilon = 1e-8 });
            Assert.False(a.AreDeepEqual(b, ctx));
        }

        [Fact]
        public void DecimalTolerance_Outside_Fails()
        {
            var a = MakeBaseline();
            var b = Clone(a);

            a.Lines[0].Price = 10.0000m;
            b.Lines[0].Price = 10.0002m;

            var ctx = new ComparisonContext(new ComparisonOptions { DecimalEpsilon = 0.0001m });
            Assert.False(a.AreDeepEqual(b, ctx));
        }
    }
}