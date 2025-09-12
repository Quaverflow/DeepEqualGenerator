using DeepEqual.Generator.Shared;

namespace DeepEqual.Tests
{

    [DeepComparable]
    public class RefRoot
    {
        public string Title { get; set; } = "";
        public RefChild Child { get; set; } = new RefChild();
        public List<RefChild> Items { get; set; } = new();
    }

    public class RefChild
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }

    public sealed class RefRootDeepGraphTests
    {
        [Fact]
        public void Deep_walks_unannotated_reference_child()
        {
            var a = new RefRoot
            {
                Title = "A",
                Child = new RefChild { Name = "X", Count = 1 }
            };
            var b = new RefRoot
            {
                Title = "A",
                Child = new RefChild { Name = "X", Count = 1 }
            };

            Assert.True(RefRootDeepEqual.AreDeepEqual(a, b));

            b.Child.Count = 2;
            Assert.False(RefRootDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void Deep_walks_unannotated_reference_children_in_collections()
        {
            var a = new RefRoot
            {
                Items = new List<RefChild>
                {
                    new RefChild { Name = "n1", Count = 1 },
                    new RefChild { Name = "n2", Count = 2 }
                }
            };
            var b = new RefRoot
            {
                Items = new List<RefChild>
                {
                    new RefChild { Name = "n1", Count = 1 },
                    new RefChild { Name = "n2", Count = 2 }
                }
            };

            Assert.True(RefRootDeepEqual.AreDeepEqual(a, b));

            b.Items[1].Name = "changed";
            Assert.False(RefRootDeepEqual.AreDeepEqual(a, b));
        }
    }


    [DeepComparable]
    public class StructRoot
    {
        public string Label { get; set; } = "";
        public StructChild Value { get; set; }  // unannotated struct
        public StructChild? Maybe { get; set; } // Nullable<T> deep unwrap
        public StructChild[] Array { get; set; } = [];
    }

    // NOTE: no DeepComparable here
    public struct StructChild
    {
        public int A { get; set; }
        public double B { get; set; }
    }

    public sealed class StructRootDeepGraphTests
    {
        [Fact]
        public void Deep_walks_unannotated_struct_child()
        {
            var a = new StructRoot { Value = new StructChild { A = 1, B = 2.0 } };
            var b = new StructRoot { Value = new StructChild { A = 1, B = 2.0 } };

            Assert.True(StructRootDeepEqual.AreDeepEqual(a, b));

            b.Value = new StructChild { A = 1, B = 3.14 }; // deep difference in struct
            Assert.False(StructRootDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void Deep_walks_nullable_struct_child()
        {
            var a = new StructRoot { Maybe = new StructChild { A = 5, B = 6 } };
            var b = new StructRoot { Maybe = new StructChild { A = 5, B = 6 } };

            Assert.True(StructRootDeepEqual.AreDeepEqual(a, b));

            b.Maybe = new StructChild { A = 7, B = 6 };
            Assert.False(StructRootDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void Deep_walks_struct_children_in_arrays()
        {
            var a = new StructRoot
            {
                Array = new[] { new StructChild { A = 1, B = 1 }, new StructChild { A = 2, B = 2 } }
            };
            var b = new StructRoot
            {
                Array = new[] { new StructChild { A = 1, B = 1 }, new StructChild { A = 2, B = 2 } }
            };

            Assert.True(StructRootDeepEqual.AreDeepEqual(a, b));

            b.Array[1] = new StructChild { A = 2, B = 3 };
            Assert.False(StructRootDeepEqual.AreDeepEqual(a, b));
        }
    }

    public sealed class SelfBox
    {
        public SelfStruct Value;
    }

    public struct SelfStruct
    {
        public int X { get; set; }
        public SelfBox? Next { get; set; }
    }

    [DeepComparable]
    public class SelfStructRoot
    {
        public SelfStruct Payload { get; set; }
    }


    public sealed class SelfStructSafetyTests
    {
        [Fact]
        public void Deep_walk_handles_struct_self_cycles_via_reference_indirection()
        {
            var boxA = new SelfBox { Value = new SelfStruct { X = 2, Next = null } };
            var a = new SelfStructRoot { Payload = new SelfStruct { X = 1, Next = boxA } };
            boxA.Value = new SelfStruct { X = 2, Next = new SelfBox { Value = a.Payload } };

            var boxB = new SelfBox { Value = new SelfStruct { X = 2, Next = null } };
            var b = new SelfStructRoot { Payload = new SelfStruct { X = 1, Next = boxB } };
            boxB.Value = new SelfStruct { X = 2, Next = new SelfBox { Value = b.Payload } };

            Assert.True(SelfStructRootDeepEqual.AreDeepEqual(a, b));

            boxB.Value = new SelfStruct { X = 3, Next = boxB.Value.Next }; // tweak X
            Assert.False(SelfStructRootDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void Nullable_like_behavior_via_indirection_still_deep_compares()
        {
            var a = new SelfStructRoot { Payload = new SelfStruct { X = 10, Next = null } };
            var b = new SelfStructRoot { Payload = new SelfStruct { X = 10, Next = null } };
            Assert.True(SelfStructRootDeepEqual.AreDeepEqual(a, b));

            b.Payload = new SelfStruct { X = 11, Next = null };
            Assert.False(SelfStructRootDeepEqual.AreDeepEqual(a, b));
        }
    }

    [DeepCompare(Members = new[] { "Name" })]
    public class SchemaChild
    {
        public string Name { get; set; } = "";
        public int Ignored { get; set; }
    }

    [DeepComparable]
    public class SchemaRoot
    {
        public SchemaChild Child { get; set; } = new();
    }

    public sealed class SchemaTypeLevelTests
    {
        [Fact]
        public void Type_level_members_schema_applies_to_unannotated_child()
        {
            var a = new SchemaRoot { Child = new SchemaChild { Name = "A", Ignored = 1 } };
            var b = new SchemaRoot { Child = new SchemaChild { Name = "A", Ignored = 999 } };

            Assert.True(SchemaRootDeepEqual.AreDeepEqual(a, b));

            b.Child.Name = "B";
            Assert.False(SchemaRootDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void Type_level_ignore_schema_applies_to_unannotated_child()
        {
            var a = new SchemaRootIgnore { Child = new SchemaChildIgnore { X = 10, Z = 111 } };
            var b = new SchemaRootIgnore { Child = new SchemaChildIgnore { X = 10, Z = 222 } };

            Assert.True(SchemaRootIgnoreDeepEqual.AreDeepEqual(a, b));

            b.Child.X = 11;
            Assert.False(SchemaRootIgnoreDeepEqual.AreDeepEqual(a, b));
        }

        [DeepCompare(IgnoreMembers = new[] { "Z" })]
        public class SchemaChildIgnore
        {
            public int X { get; set; }
            public int Z { get; set; }
        }

        [DeepComparable]
        public class SchemaRootIgnore
        {
            public SchemaChildIgnore Child { get; set; } = new();
        }
    }
}
