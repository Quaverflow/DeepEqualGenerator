using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
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


    public class BaseEntity
    {
        public string? BaseId { get; set; }
    }

    [DeepComparable(IncludeBaseMembers = true)]
    public sealed class DerivedWithBaseIncluded : BaseEntity
    {
        public string? Name { get; set; }
    }

    [DeepComparable(IncludeBaseMembers = false)]
    public sealed class DerivedWithBaseExcluded : BaseEntity
    {
        public string? Name { get; set; }
    }

    public sealed class CustomerK
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }

    [DeepComparable]
    public sealed class CustomersKeyed
    {
        [DeepCompare(OrderInsensitive = true, KeyMembers = new[] { "Id" })]
        public List<CustomerK> Customers { get; set; } = new();
    }

    [DeepComparable]
    public sealed class CustomersUnkeyed
    {
        [DeepCompare(OrderInsensitive = true)]
        public List<Person> People { get; set; } = new();
    }

    public sealed class CaseInsensitiveStringComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode(string obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj);
    }

    [DeepComparable]
    public sealed class CustomComparerHolder
    {
        [DeepCompare(ComparerType = typeof(CaseInsensitiveStringComparer))]
        public string? Code { get; set; }
    }

    public sealed class DoubleEpsComparer : IEqualityComparer<double>
    {
        private readonly double _eps;
        public DoubleEpsComparer() : this(1e-6) { }
        public DoubleEpsComparer(double eps) { _eps = eps; }
        public bool Equals(double x, double y) => Math.Abs(x - y) <= _eps || (double.IsNaN(x) && double.IsNaN(y));
        public int GetHashCode(double obj) => 0;
    }

    [DeepComparable]
    public sealed class NumericWithComparer
    {
        [DeepCompare(ComparerType = typeof(DoubleEpsComparer))]
        public double Value { get; set; }
    }

    [DeepComparable]
    public sealed class MemoryHolder
    {
        public Memory<byte> Buf { get; set; }
        public ReadOnlyMemory<byte> RBuf { get; set; }
    }

    [DeepComparable]
    public sealed class DynamicHolder
    {
        public IDictionary<string, object?> Data { get; set; } = new ExpandoObject();
    }


    [DeepComparable] public sealed class ObjArr { public object? Any { get; init; } }

    public interface IAnimal { int Age { get; } }

    [DeepComparable] public sealed class Cat : IAnimal { public int Age { get; init; } public string Name { get; init; } = ""; }

    [DeepComparable] public sealed class Zoo { public IAnimal? Animal { get; init; } }

    [DeepComparable] public sealed class ArrayHolder { public object? Any { get; init; } }

    [DeepComparable]
    public sealed class KeyedBag
    {
        [DeepCompare(OrderInsensitive = true, KeyMembers = new[] { nameof(Item.Name) })]
        public List<Item> Items { get; init; } = new();
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

        [Fact]
        public void Base_Members_Included_And_Excluded()
        {
            var a = new DerivedWithBaseIncluded { BaseId = "B1", Name = "X" };
            var b = new DerivedWithBaseIncluded { BaseId = "B1", Name = "X" };
            Assert.True(DerivedWithBaseIncludedDeepEqual.AreDeepEqual(a, b));

            var bDiff = new DerivedWithBaseIncluded { BaseId = "DIFF", Name = "X" };
            Assert.False(DerivedWithBaseIncludedDeepEqual.AreDeepEqual(a, bDiff));

            var c = new DerivedWithBaseExcluded { BaseId = "B1", Name = "X" };
            var d = new DerivedWithBaseExcluded { BaseId = "DIFF", Name = "X" };
            Assert.True(DerivedWithBaseExcludedDeepEqual.AreDeepEqual(c, d));
        }

        [Fact]
        public void Custom_Comparer_On_Member_Works()
        {
            var a = new CustomComparerHolder { Code = "ABc" };
            var b = new CustomComparerHolder { Code = "abc" };
            Assert.True(CustomComparerHolderDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void Numeric_Custom_Comparer_Works()
        {
            var a = new NumericWithComparer { Value = 1.0000001 };
            var b = new NumericWithComparer { Value = 1.0000002 };
            Assert.True(NumericWithComparerDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void Memory_And_ReadOnlyMemory_Are_Compared_By_Content()
        {
            var bytes1 = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
            var bytes2 = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
            var bytes3 = Enumerable.Range(0, 32).Select(i => (byte)(i + 1)).ToArray();

            var a = new MemoryHolder { Buf = new Memory<byte>(bytes1), RBuf = new ReadOnlyMemory<byte>(bytes2) };
            var b = new MemoryHolder { Buf = new Memory<byte>(bytes1.ToArray()), RBuf = new ReadOnlyMemory<byte>(bytes2.ToArray()) };
            var c = new MemoryHolder { Buf = new Memory<byte>(bytes3), RBuf = new ReadOnlyMemory<byte>(bytes2) };

            Assert.True(MemoryHolderDeepEqual.AreDeepEqual(a, b));
            Assert.False(MemoryHolderDeepEqual.AreDeepEqual(a, c));
        }

        [Fact]
        public void Dynamics_Expando_Missing_And_Nested_Diff()
        {
            dynamic e1 = new ExpandoObject();
            e1.id = 1;
            e1.name = "x";
            e1.arr = new[] { 1, 2, 3 };
            e1.map = new Dictionary<string, object?> { ["k"] = 1, ["z"] = new[] { "p", "q" } };

            dynamic e2 = new ExpandoObject();
            e2.id = 1;
            e2.name = "x";
            e2.arr = new[] { 1, 2, 3 };
            e2.map = new Dictionary<string, object?> { ["k"] = 1, ["z"] = new[] { "p", "q" } };

            var a = new DynamicHolder { Data = (IDictionary<string, object?>)e1 };
            var b = new DynamicHolder { Data = (IDictionary<string, object?>)e2 };
            Assert.True(DynamicHolderDeepEqual.AreDeepEqual(a, b));

            ((IDictionary<string, object?>)e2).Remove("name");
            Assert.False(DynamicHolderDeepEqual.AreDeepEqual(a, b));

            ((IDictionary<string, object?>)e2)["name"] = "x";
            ((Dictionary<string, object?>)e2.map)["z"] = new[] { "p", "Q" };
            Assert.False(DynamicHolderDeepEqual.AreDeepEqual(a, b));
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
                ForcedOrdered = new List<int> { 1, 2, 3 }
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

        [Fact]
        public void Unordered_List_Of_Objects_Without_Keys_Still_Equal_When_Swapped()
        {
            var a = new CustomersUnkeyed
            {
                People = new List<Person>
                {
                    new Person { Name = "p1", Age = 1 },
                    new Person { Name = "p2", Age = 2 }
                }
            };
            var b = new CustomersUnkeyed
            {
                People = new List<Person>
                {
                    new Person { Name = "p2", Age = 2 },
                    new Person { Name = "p1", Age = 1 }
                }
            };
            Assert.True(CustomersUnkeyedDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void Unordered_List_With_KeyMembers_Matches_By_Key()
        {
            var a = new CustomersKeyed
            {
                Customers = new List<CustomerK>
                {
                    new CustomerK { Id = "a", Name = "alice" },
                    new CustomerK { Id = "b", Name = "bob" }
                }
            };
            var b = new CustomersKeyed
            {
                Customers = new List<CustomerK>
                {
                    new CustomerK { Id = "b", Name = "bob" },
                    new CustomerK { Id = "a", Name = "alice" }
                }
            };
            Assert.True(CustomersKeyedDeepEqual.AreDeepEqual(a, b));

            var c = new CustomersKeyed
            {
                Customers = new List<CustomerK>
                {
                    new CustomerK { Id = "a", Name = "alice" },
                    new CustomerK { Id = "b", Name = "BOB!" }
                }
            };
            Assert.False(CustomersKeyedDeepEqual.AreDeepEqual(a, c));

            var d = new CustomersKeyed
            {
                Customers = new List<CustomerK>
                {
                    new CustomerK { Id = "a", Name = "alice" }
                }
            };
            Assert.False(CustomersKeyedDeepEqual.AreDeepEqual(a, d));
        }


        [Fact]
        public void Object_Array_Uses_Structural_Equality()
        {
            var a = new ObjArr { Any = new[] { 1, 2, 3 } };
            var b = new ObjArr { Any = new[] { 1, 2, 3 } };
            var c = new ObjArr { Any = new[] { 1, 2, 4 } };
            Assert.True(ObjArrDeepEqual.AreDeepEqual(a, b));
            Assert.False(ObjArrDeepEqual.AreDeepEqual(a, c));
        }

        [Fact]
        public void Interface_Property_Uses_Runtime_Type()
        {
            var a = new Zoo { Animal = new Cat { Age = 3, Name = "Paws" } };
            var b = new Zoo { Animal = new Cat { Age = 3, Name = "Paws" } };
            var c = new Zoo { Animal = new Cat { Age = 3, Name = "Claws" } };
            Assert.True(ZooDeepEqual.AreDeepEqual(a, b));
            Assert.False(ZooDeepEqual.AreDeepEqual(a, c));
        }

        [Fact]
        public void Jagged_Vs_Multidimensional_Arrays_NotEqual()
        {
            var jagged = new int[][] { new[] { 1, 2 }, new[] { 3, 4 } };
            var multi = new int[2, 2] { { 1, 2 }, { 3, 4 } };
            var a = new ArrayHolder { Any = jagged };
            var b = new ArrayHolder { Any = multi };
            Assert.False(ArrayHolderDeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void Unordered_Keyed_With_Duplicates_Must_Match_PerBucket_Count_And_Values()
        {
            var A = new KeyedBag { Items = new() { new() { Name = "x", X = 1 }, new() { Name = "x", X = 2 }, new() { Name = "y", X = 9 } } };
            var B = new KeyedBag { Items = new() { new() { Name = "x", X = 2 }, new() { Name = "y", X = 9 }, new() { Name = "x", X = 1 } } };
            var C = new KeyedBag { Items = new() { new() { Name = "x", X = 1 }, new() { Name = "y", X = 9 } } };             Assert.True(KeyedBagDeepEqual.AreDeepEqual(A, B));
            Assert.False(KeyedBagDeepEqual.AreDeepEqual(A, C));
        }

        [Fact]
        public void String_And_Number_Options_Are_Respected()
        {
            var a = new OptsHolder { S = "Hello", D = 1.0000001, M = 1.00005m };
            var b = new OptsHolder { S = "hello", D = 1.0000002, M = 1.00001m };

            var loose = new ComparisonOptions
            {
                StringComparison = StringComparison.OrdinalIgnoreCase,
                DoubleEpsilon = 1e-6,
                DecimalEpsilon = 0.0001m
            };
            Assert.True(OptsHolderDeepEqual.AreDeepEqual(a, b, loose));

            var strict = new ComparisonOptions
            {
                StringComparison = StringComparison.Ordinal,
                DoubleEpsilon = 0.0,
                DecimalEpsilon = 0m
            };
            Assert.False(OptsHolderDeepEqual.AreDeepEqual(a, b, strict));
        }


        [DeepComparable] public sealed class OptsHolder { public string? S { get; init; } public double D { get; init; } public decimal M { get; init; } }
    }
}
[DeepComparable] public sealed class FloatHolder { public float F { get; init; } }
[DeepComparable] public sealed class DoubleWeird { public double D { get; init; } }
[DeepComparable] public sealed class StringNfcNfd { public string? S { get; init; } }
[DeepComparable] public sealed class ObjList { public List<object?> Items { get; init; } = new(); }
public interface IAnimal { int Age { get; } }
[DeepComparable] public sealed class Cat : IAnimal { public int Age { get; init; } public string Name { get; init; } = ""; }
[DeepComparable] public sealed class ZooList { public List<IAnimal> Animals { get; init; } = new(); }

public class ExtraEdgeTests
{
    [Fact]
    public void Double_NaN_And_SignedZero_Options()
    {
        var a = new DoubleWeird { D = double.NaN };
        var b = new DoubleWeird { D = double.NaN };
        var allow = new ComparisonOptions { TreatNaNEqual = true };
        var deny = new ComparisonOptions { TreatNaNEqual = false };
        Assert.True(DoubleWeirdDeepEqual.AreDeepEqual(a, b, allow));
        Assert.False(DoubleWeirdDeepEqual.AreDeepEqual(a, b, deny));

        var z1 = new DoubleWeird { D = +0.0 };
        var z2 = new DoubleWeird { D = -0.0 };
        var strict = new ComparisonOptions { DoubleEpsilon = 0.0 };
        Assert.True(DoubleWeirdDeepEqual.AreDeepEqual(z1, z2, strict));
    }

    [Fact]
    public void Float_Uses_Single_Epsilon()
    {
        var a = new FloatHolder { F = 1.000001f };
        var b = new FloatHolder { F = 1.000002f };
        var loose = new ComparisonOptions { FloatEpsilon = 0.00001f };
        var strict = new ComparisonOptions { FloatEpsilon = 0f };
        Assert.True(FloatHolderDeepEqual.AreDeepEqual(a, b, loose));
        Assert.False(FloatHolderDeepEqual.AreDeepEqual(a, b, strict));
    }

    [Fact]
    public void String_Does_Not_Normalize_Combining_Marks()
    {
        var nfc = new StringNfcNfd { S = "é" };
        var nfd = new StringNfcNfd { S = "e\u0301" };
        var opts = new ComparisonOptions { StringComparison = StringComparison.Ordinal };
        Assert.False(StringNfcNfdDeepEqual.AreDeepEqual(nfc, nfd, opts));
    }

    [Fact]
    public void Collections_With_Nulls_Are_Handled()
    {
        var a = new ObjList { Items = new() { 1, null, new[] { "x" } } };
        var b = new ObjList { Items = new() { 1, null, new[] { "x" } } };
        var c = new ObjList { Items = new() { 1, null, new[] { "y" } } };
        Assert.True(ObjListDeepEqual.AreDeepEqual(a, b));
        Assert.False(ObjListDeepEqual.AreDeepEqual(a, c));
    }

    [Fact]
    public void Polymorphism_Inside_Collections()
    {
        var A = new ZooList { Animals = new() { new Cat { Age = 2, Name = "Paws" }, new Cat { Age = 5, Name = "Mews" } } };
        var B = new ZooList { Animals = new() { new Cat { Age = 2, Name = "Paws" }, new Cat { Age = 5, Name = "Mews" } } };
        var C = new ZooList { Animals = new() { new Cat { Age = 2, Name = "Paws" }, new Cat { Age = 5, Name = "Mewz" } } };
        Assert.True(ZooListDeepEqual.AreDeepEqual(A, B));
        Assert.False(ZooListDeepEqual.AreDeepEqual(A, C));
    }

    [DeepComparable] public sealed class BucketItem { public string K { get; init; } = ""; public int V { get; init; } }
    [DeepComparable]
    public sealed class Bucketed
    {
        [DeepCompare(OrderInsensitive = true, KeyMembers = new[] { nameof(BucketItem.K) })]
        public List<BucketItem> Items { get; init; } = new();
    }

    [Fact]
    public void Keyed_Unordered_SameCounts_But_DeepValue_Diff_Is_False()
    {
        var A = new Bucketed { Items = new() { new() { K = "a", V = 1 }, new() { K = "a", V = 2 } } };
        var B = new Bucketed { Items = new() { new() { K = "a", V = 2 }, new() { K = "a", V = 1 } } };
        var C = new Bucketed { Items = new() { new() { K = "a", V = 1 }, new() { K = "a", V = 99 } } };
        Assert.True(BucketedDeepEqual.AreDeepEqual(A, B));
        Assert.False(BucketedDeepEqual.AreDeepEqual(A, C));
    }

    [DeepComparable] public sealed class DictShapeA { public Dictionary<string, int> Map { get; init; } = new(); }
    public sealed class CustomDict : Dictionary<string, int> { } // implements IDictionary<,>
    [DeepComparable] public sealed class DictShapeB { public CustomDict Map { get; init; } = new(); }

    [Fact]
    public void Dictionary_Fallback_Mixed_Shapes_Work()
    {
        var a = new DictShapeA { Map = new() { ["x"] = 1, ["y"] = 2 } };
        var b = new DictShapeB { Map = new CustomDict { ["y"] = 2, ["x"] = 1 } };
        Assert.True(DictShapeADeepEqual.AreDeepEqual(a, new DictShapeA { Map = new() { ["x"] = 1, ["y"] = 2 } }));
        Assert.True(DictShapeBDeepEqual.AreDeepEqual(b, new DictShapeB { Map = new CustomDict { ["x"] = 1, ["y"] = 2 } }));
    }

    [Fact]
    public void Symmetry_And_Repeatability()
    {
        var a = new ObjList { Items = new() { "a", "b" } };
        var b = new ObjList { Items = new() { "a", "b" } };
        Assert.True(ObjListDeepEqual.AreDeepEqual(a, b));
        Assert.True(ObjListDeepEqual.AreDeepEqual(b, a));
        Assert.True(ObjListDeepEqual.AreDeepEqual(a, b)); // repeat
    }
}
