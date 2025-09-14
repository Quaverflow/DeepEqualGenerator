using System;
using System.Collections.Generic;
using Xunit;
using DeepEqual.Generator.Shared;

namespace DeepEqual.Tests3
{
    [DeepComparable]
    public struct SimpleStruct
    {
        public int Id { get; set; }
        public DateTime WhenUtc { get; set; }
    }

    [DeepComparable]
    public sealed class DateTimeHolder
    {
        public DateTime When { get; set; }
    }

    [DeepComparable]
    public sealed class DateTimeOffsetHolder
    {
        public DateTimeOffset When { get; set; }
    }

    [DeepComparable]
    public sealed class NullableDateTimeHolder
    {
        public DateTime? When { get; set; }
    }

    [DeepComparable]
    public sealed class CollectionsOfTimeHolder
    {
        public DateTime[] Snapshots { get; set; } = Array.Empty<DateTime>();
        public List<DateTime> Events { get; set; } = new();
        public Dictionary<string, DateTimeOffset> Index { get; set; } = new();
    }

    public enum Color
    {
        Red = 1,
        Blue = 2
    }

    [DeepComparable]
    public sealed class EnumHolder
    {
        public Color Shade { get; set; }
    }

    [DeepComparable]
    public sealed class GuidHolder
    {
        public Guid Id { get; set; }
    }

    [DeepComparable]
    public sealed class DateOnlyTimeOnlyHolder
    {
        public DateOnly D { get; set; }
        public TimeOnly T { get; set; }
    }

    [DeepComparable]
    public sealed class Person
    {
        public string? Name { get; set; }
        public int Age { get; set; }
    }

    [DeepComparable]
    public sealed class StringHolder
    {
        public string? Value { get; set; }
    }

    [DeepComparable]
    public sealed class NullableStringHolder
    {
        public string? Value { get; set; }
    }

    [DeepComparable]
    public sealed class MemberKindContainer
    {
        public Item ValDeep { get; set; } = new();
        [DeepCompare(Kind = CompareKind.Shallow)]
        public Item ValShallow { get; set; } = new();
        [DeepCompare(Kind = CompareKind.Reference)]
        public Item ValReference { get; set; } = new();
        [DeepCompare(Kind = CompareKind.Skip)]
        public Item ValSkipped { get; set; } = new();
    }

    [DeepComparable]
    public sealed class Item
    {
        public int X { get; set; }
        public string? Name { get; set; }
    }

    [DeepCompare(Members = new[] { "A", "B" })]
    [DeepComparable]
    public sealed class OnlySomeMembers
    {
        public int A { get; set; }
        public int B { get; set; }
        public int C { get; set; }
    }

    [DeepCompare(IgnoreMembers = new[] { "C" })]
    [DeepComparable]
    public sealed class IgnoreSomeMembers
    {
        public int A { get; set; }
        public int B { get; set; }
        public int C { get; set; }
    }

    [DeepComparable]
    public sealed class ShallowChild
    {
        public int V { get; set; }
    }

    [DeepCompare(Kind = CompareKind.Shallow)]
    [DeepComparable]
    public sealed class TypeLevelShallowChild
    {
        public int V { get; set; }
    }

    [DeepComparable]
    public sealed class ContainerWithTypeLevelShallow
    {
        public TypeLevelShallowChild? Child { get; set; }
    }

    [DeepComparable(IncludeInternals = true)]
    public sealed class WithInternalsIncluded
    {
        public int Shown { get; set; }
        internal int Hidden { get; set; }
    }

    [DeepComparable]
    public sealed class WithInternalsExcluded
    {
        public int Shown { get; set; }
        internal int Hidden { get; set; }
    }

    [DeepComparable]
    public sealed class ObjectHolder
    {
        public object? Any { get; set; }
        public ChildRef Known { get; set; } = new();
    }

    [DeepComparable]
    public sealed class ChildRef
    {
        public int Value { get; set; }
    }

    public sealed class Unregistered
    {
        public int V { get; set; }
    }

    [DeepComparable]
    public sealed class CycleNode
    {
        public int Id { get; set; }
        public CycleNode? Next { get; set; }
    }

    [DeepComparable]
    public sealed class WithOrderInsensitiveMember
    {
        [DeepCompare(OrderInsensitive = true)]
        public List<int> Values { get; set; } = new();
    }

    [DeepComparable(OrderInsensitiveCollections = true)]
    public sealed class RootOrderInsensitiveCollections
    {
        public List<string> Names { get; set; } = new();
        public List<Person> People { get; set; } = new();
        [DeepCompare(OrderInsensitive = false)]
        public List<int> ForcedOrdered { get; set; } = new();
    }

    [DeepComparable]
    public sealed class RootOrderSensitiveCollections
    {
        public List<string> Names { get; set; } = new();
    }

    [DeepComparable]
    public sealed class Tag
    {
        public string Id { get; set; } = "";
    }

    [DeepComparable(OrderInsensitiveCollections = true)]
    public sealed class TagAsElementDefaultUnordered
    {
        public string Label { get; set; } = "";
    }

    [DeepComparable]
    public sealed class RootWithElementTypeDefaultUnordered
    {
        public List<TagAsElementDefaultUnordered> Tags { get; set; } = new();
    }

    [DeepComparable]
    public sealed class MultiDimArrayHolder
    {
        public int[,] Matrix { get; set; } = new int[0, 0];
    }

    [DeepComparable]
    public sealed class DictionaryHolder
    {
        public Dictionary<int, Person> Map { get; set; } = new();
    }

    public class StructTests
    {
        [Fact]
        public void Structs_With_Different_DateTimeKind_Fail()
        {
            var a = new SimpleStruct { WhenUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
            var b = new SimpleStruct { WhenUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Local) };
            Assert.False(SimpleStructDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void DateTime_Strict_Cases()
        {
            var tU1 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var tU2 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            Assert.True(DateTimeHolderDeepEqual.AreDeepEqual(new DateTimeHolder { When = tU1 }, new DateTimeHolder { When = tU2 }));

            var tKindDiff = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Local);
            Assert.False(DateTimeHolderDeepEqual.AreDeepEqual(new DateTimeHolder { When = tU1 }, new DateTimeHolder { When = tKindDiff }));

            var tTickDiff = new DateTime(2025, 1, 1, 12, 0, 1, DateTimeKind.Utc);
            Assert.False(DateTimeHolderDeepEqual.AreDeepEqual(new DateTimeHolder { When = tU1 }, new DateTimeHolder { When = tTickDiff }));
        }

        [Fact]
        public void DateTimeOffset_Strict_Cases()
        {
            var dt1 = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.FromHours(1));
            var dt2 = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.FromHours(1));
            Assert.True(DateTimeOffsetHolderDeepEqual.AreDeepEqual(new DateTimeOffsetHolder { When = dt1 }, new DateTimeOffsetHolder { When = dt2 }));

            var dtoOff = dt1.ToOffset(TimeSpan.FromHours(2));
            Assert.False(DateTimeOffsetHolderDeepEqual.AreDeepEqual(new DateTimeOffsetHolder { When = dt1 }, new DateTimeOffsetHolder { When = dtoOff }));
        }

        [Fact]
        public void Nullable_DateTime_Cases()
        {
            Assert.True(NullableDateTimeHolderDeepEqual.AreDeepEqual(new NullableDateTimeHolder { When = null }, new NullableDateTimeHolder { When = null }));
            Assert.False(NullableDateTimeHolderDeepEqual.AreDeepEqual(new NullableDateTimeHolder { When = null }, new NullableDateTimeHolder { When = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }));
            Assert.False(NullableDateTimeHolderDeepEqual.AreDeepEqual(new NullableDateTimeHolder { When = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }, new NullableDateTimeHolder { When = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Local) }));
        }

        [Fact]
        public void Enum_Equality_By_Value()
        {
            var a = new EnumHolder { Shade = Color.Blue };
            var b = new EnumHolder { Shade = Color.Blue };
            var c = new EnumHolder { Shade = Color.Red };
            Assert.True(EnumHolderDeepEqual.AreDeepEqual(a, b));
            Assert.False(EnumHolderDeepEqual.AreDeepEqual(a, c));
        }

        [Fact]
        public void Guid_Equality_By_Value()
        {
            var g = Guid.NewGuid();
            var a = new GuidHolder { Id = g };
            var b = new GuidHolder { Id = g };
            var c = new GuidHolder { Id = Guid.NewGuid() };
            Assert.True(GuidHolderDeepEqual.AreDeepEqual(a, b));
            Assert.False(GuidHolderDeepEqual.AreDeepEqual(a, c));
        }

        [Fact]
        public void DateOnly_TimeOnly_Strict()
        {
            var a = new DateOnlyTimeOnlyHolder { D = new DateOnly(2025, 1, 1), T = new TimeOnly(12, 0, 0) };
            var b = new DateOnlyTimeOnlyHolder { D = new DateOnly(2025, 1, 1), T = new TimeOnly(12, 0, 0) };
            var c = new DateOnlyTimeOnlyHolder { D = new DateOnly(2025, 1, 2), T = new TimeOnly(12, 0, 0) };
            var d = new DateOnlyTimeOnlyHolder { D = new DateOnly(2025, 1, 1), T = new TimeOnly(12, 0, 1) };
            Assert.True(DateOnlyTimeOnlyHolderDeepEqual.AreDeepEqual(a, b));
            Assert.False(DateOnlyTimeOnlyHolderDeepEqual.AreDeepEqual(a, c));
            Assert.False(DateOnlyTimeOnlyHolderDeepEqual.AreDeepEqual(a, d));
        }
    }

    public class RefTypeTests
    {
        [Fact]
        public void Strings_Case_Sensitive_By_Default()
        {
            var a = new StringHolder { Value = "Hello" };
            var b = new StringHolder { Value = "Hello" };
            var c = new StringHolder { Value = "hello" };
            Assert.True(StringHolderDeepEqual.AreDeepEqual(a, b));
            Assert.False(StringHolderDeepEqual.AreDeepEqual(a, c));
        }

        [Fact]
        public void Nullable_String_Null_Handling()
        {
            var n1 = new NullableStringHolder { Value = null };
            var n2 = new NullableStringHolder { Value = null };
            var v = new NullableStringHolder { Value = "x" };
            Assert.True(NullableStringHolderDeepEqual.AreDeepEqual(n1, n2));
            Assert.False(NullableStringHolderDeepEqual.AreDeepEqual(n1, v));
        }

        [Fact]
        public void Deep_Shallow_Reference_Skip_Semantics()
        {
            var deepLeft = new Item { X = 1, Name = "x" };
            var deepRight = new Item { X = 1, Name = "x" };

            var shallowLeft = new Item { X = 2, Name = "y" };
            var shallowRightSameValues = new Item { X = 2, Name = "y" };
            var shallowSameRef = shallowLeft;

            var refLeft = new Item { X = 3, Name = "z" };
            var refRightDifferentRefSameValues = new Item { X = 3, Name = "z" };
            var refSameRef = refLeft;

            var skipLeft = new Item { X = 9, Name = "q" };
            var skipRightDifferent = new Item { X = 123, Name = "QQ" };

            var a = new MemberKindContainer
            {
                ValDeep = deepLeft,
                ValShallow = shallowLeft,
                ValReference = refLeft,
                ValSkipped = skipLeft
            };
            var b = new MemberKindContainer
            {
                ValDeep = deepRight,
                ValShallow = shallowRightSameValues,
                ValReference = refRightDifferentRefSameValues,
                ValSkipped = skipRightDifferent
            };

            Assert.False(MemberKindContainerDeepEqual.AreDeepEqual(a, b));

            b.ValShallow = shallowSameRef;
            Assert.False(MemberKindContainerDeepEqual.AreDeepEqual(a, b));

            b.ValReference = refSameRef;
            Assert.True(MemberKindContainerDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void Type_Level_Shallow_On_Member_Type()
        {
            var a = new ContainerWithTypeLevelShallow { Child = new TypeLevelShallowChild { V = 5 } };
            var b = new ContainerWithTypeLevelShallow { Child = new TypeLevelShallowChild { V = 5 } };
            Assert.False(ContainerWithTypeLevelShallowDeepEqual.AreDeepEqual(a, b));
            var same = a;
            Assert.True(ContainerWithTypeLevelShallowDeepEqual.AreDeepEqual(a, same));
        }

        [Fact]
        public void Include_Internals_Controls_Visibility()
        {
            var inc1 = new WithInternalsIncluded { Shown = 1, Hidden = 2 };
            var inc2 = new WithInternalsIncluded { Shown = 1, Hidden = 99 };
            Assert.False(WithInternalsIncludedDeepEqual.AreDeepEqual(inc1, inc2));

            var exc1 = new WithInternalsExcluded { Shown = 1, Hidden = 2 };
            var exc2 = new WithInternalsExcluded { Shown = 1, Hidden = 99 };
            Assert.True(WithInternalsExcludedDeepEqual.AreDeepEqual(exc1, exc2));
        }

        [Fact]
        public void Only_Members_Included_Or_Ignored_Are_Respected()
        {
            var i1 = new OnlySomeMembers { A = 1, B = 2, C = 777 };
            var i2 = new OnlySomeMembers { A = 1, B = 2, C = -1 };
            Assert.True(OnlySomeMembersDeepEqual.AreDeepEqual(i1, i2));

            var j1 = new IgnoreSomeMembers { A = 7, B = 8, C = 100 };
            var j2 = new IgnoreSomeMembers { A = 7, B = 8, C = -100 };
            Assert.True(IgnoreSomeMembersDeepEqual.AreDeepEqual(j1, j2));

            var j3 = new IgnoreSomeMembers { A = 7, B = 9, C = 100 };
            Assert.False(IgnoreSomeMembersDeepEqual.AreDeepEqual(j1, j3));
        }

        [Fact]
        public void Object_Member_Uses_Registry_When_Runtime_Type_Is_Registered()
        {
            var a = new ObjectHolder { Known = new ChildRef { Value = 10 }, Any = new ChildRef { Value = 10 } };
            var b = new ObjectHolder { Known = new ChildRef { Value = 10 }, Any = new ChildRef { Value = 10 } };
            Assert.True(ObjectHolderDeepEqual.AreDeepEqual(a, b));

            var c = new ObjectHolder { Known = new ChildRef { Value = 10 }, Any = new ChildRef { Value = 99 } };
            Assert.False(ObjectHolderDeepEqual.AreDeepEqual(a, c));
        }

        [Fact]
        public void Object_Member_Falls_Back_When_Runtime_Type_Is_Not_Registered()
        {
            var a = new ObjectHolder { Known = new ChildRef { Value = 1 }, Any = new Unregistered { V = 1 } };
            var b = new ObjectHolder { Known = new ChildRef { Value = 1 }, Any = new Unregistered { V = 1 } };
            Assert.False(ObjectHolderDeepEqual.AreDeepEqual(a, b));
            var shared = new Unregistered { V = 1 };
            var c = new ObjectHolder { Known = new ChildRef { Value = 1 }, Any = shared };
            var d = new ObjectHolder { Known = new ChildRef { Value = 1 }, Any = shared };
            Assert.True(ObjectHolderDeepEqual.AreDeepEqual(c, d));
        }

        [Fact]
        public void Cycle_Tracking_Prevents_Stack_Overflow_And_Compares_Correctly()
        {
            var a1 = new CycleNode { Id = 1 };
            var a2 = new CycleNode { Id = 2 };
            a1.Next = a2;
            a2.Next = a1;

            var b1 = new CycleNode { Id = 1 };
            var b2 = new CycleNode { Id = 2 };
            b1.Next = b2;
            b2.Next = b1;

            Assert.True(CycleNodeDeepEqual.AreDeepEqual(a1, b1));

            b2.Id = 99;
            Assert.False(CycleNodeDeepEqual.AreDeepEqual(a1, b1));
        }
    }

    public class CollectionsTests
    {
        [Fact]
        public void Array_Ordered_By_Default()
        {
            var a = new RootOrderSensitiveCollections { Names = new List<string> { "a", "b" } };
            var b = new RootOrderSensitiveCollections { Names = new List<string> { "b", "a" } };
            Assert.False(RootOrderSensitiveCollectionsDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void Member_Level_OrderInsensitive_For_Array_Or_List()
        {
            var a = new WithOrderInsensitiveMember { Values = new List<int> { 1, 2, 2, 3 } };
            var b = new WithOrderInsensitiveMember { Values = new List<int> { 2, 3, 2, 1 } };
            Assert.True(WithOrderInsensitiveMemberDeepEqual.AreDeepEqual(a, b));
            var c = new WithOrderInsensitiveMember { Values = new List<int> { 1, 2, 3 } };
            Assert.False(WithOrderInsensitiveMemberDeepEqual.AreDeepEqual(a, c));
        }

        [Fact]
        public void Root_Level_OrderInsensitive_Applies_To_Collections()
        {
            var a = new RootOrderInsensitiveCollections
            {
                Names = new List<string> { "x", "y", "y" },
                People = new List<Person> { new Person { Name = "p1", Age = 1 }, new Person { Name = "p2", Age = 2 } },
                ForcedOrdered = new List<int> { 1, 2, 3 }
            };
            var b = new RootOrderInsensitiveCollections
            {
                Names = new List<string> { "y", "x", "y" },
                People = new List<Person> { new Person { Name = "p2", Age = 2 }, new Person { Name = "p1", Age = 1 } },
                ForcedOrdered = new List<int> { 1, 2, 3 } // keep same order here
            };
            Assert.True(RootOrderInsensitiveCollectionsDeepEqual.AreDeepEqual(a, b));

            Assert.False(RootOrderInsensitiveCollectionsDeepEqual.AreDeepEqual(
                new RootOrderInsensitiveCollections { ForcedOrdered = new List<int> { 1, 2, 3 } },
                new RootOrderInsensitiveCollections { ForcedOrdered = new List<int> { 3, 2, 1 } }));
        }

        [Fact]
        public void Element_Type_OrderInsensitive_Default_Is_Respected()
        {
            var a = new RootWithElementTypeDefaultUnordered
            {
                Tags = new List<TagAsElementDefaultUnordered>
                {
                    new TagAsElementDefaultUnordered { Label = "A" },
                    new TagAsElementDefaultUnordered { Label = "B" }
                }
            };
            var b = new RootWithElementTypeDefaultUnordered
            {
                Tags = new List<TagAsElementDefaultUnordered>
                {
                    new TagAsElementDefaultUnordered { Label = "B" },
                    new TagAsElementDefaultUnordered { Label = "A" }
                }
            };
            Assert.True(RootWithElementTypeDefaultUnorderedDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void MultiDimensional_Arrays_Are_Compared_By_Shape_And_Values()
        {
            var m1 = new MultiDimArrayHolder { Matrix = new int[2, 2] { { 1, 2 }, { 3, 4 } } };
            var m2 = new MultiDimArrayHolder { Matrix = new int[2, 2] { { 1, 2 }, { 3, 4 } } };
            var m3 = new MultiDimArrayHolder { Matrix = new int[2, 2] { { 1, 2 }, { 4, 3 } } };
            var m4 = new MultiDimArrayHolder { Matrix = new int[2, 3] { { 1, 2, 0 }, { 3, 4, 0 } } };
            Assert.True(MultiDimArrayHolderDeepEqual.AreDeepEqual(m1, m2));
            Assert.False(MultiDimArrayHolderDeepEqual.AreDeepEqual(m1, m3));
            Assert.False(MultiDimArrayHolderDeepEqual.AreDeepEqual(m1, m4));
        }

        [Fact]
        public void Dictionaries_Use_Deep_Compare_For_Values()
        {
            var a = new DictionaryHolder
            {
                Map = new Dictionary<int, Person>
                {
                    [1] = new Person { Name = "A", Age = 10 },
                    [2] = new Person { Name = "B", Age = 20 }
                }
            };
            var b = new DictionaryHolder
            {
                Map = new Dictionary<int, Person>
                {
                    [2] = new Person { Name = "B", Age = 20 },
                    [1] = new Person { Name = "A", Age = 10 }
                }
            };
            var c = new DictionaryHolder
            {
                Map = new Dictionary<int, Person>
                {
                    [1] = new Person { Name = "A", Age = 10 },
                    [2] = new Person { Name = "B", Age = 99 }
                }
            };
            Assert.True(DictionaryHolderDeepEqual.AreDeepEqual(a, b));
            Assert.False(DictionaryHolderDeepEqual.AreDeepEqual(a, c));
        }
    }
}
