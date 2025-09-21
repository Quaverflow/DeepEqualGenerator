using System.Dynamic;
using DeepEqual.Generator.Tests.Models;

namespace DeepEqual.Generator.Tests.Tests;

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

        var refLeft = new Item { X = 3, Name = "z" };
        var refRightDifferentRefSameValues = new Item { X = 3, Name = "z" };

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

        b.ValShallow = shallowLeft;
        Assert.False(MemberKindContainerDeepEqual.AreDeepEqual(a, b));

        b.ValReference = refLeft;
        Assert.True(MemberKindContainerDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Type_Level_Shallow_On_Member_Type()
    {
        var a = new ContainerWithTypeLevelShallow { Child = new TypeLevelShallowChild { V = 5 } };
        var b = new ContainerWithTypeLevelShallow { Child = new TypeLevelShallowChild { V = 5 } };
        Assert.False(ContainerWithTypeLevelShallowDeepEqual.AreDeepEqual(a, b));
        Assert.True(ContainerWithTypeLevelShallowDeepEqual.AreDeepEqual(a, a));
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
        var b = new MemoryHolder
            { Buf = new Memory<byte>(bytes1.ToArray()), RBuf = new ReadOnlyMemory<byte>(bytes2.ToArray()) };
        var c = new MemoryHolder { Buf = new Memory<byte>(bytes3), RBuf = new ReadOnlyMemory<byte>(bytes2) };

        Assert.True(MemoryHolderDeepEqual.AreDeepEqual(a, b));
        Assert.False(MemoryHolderDeepEqual.AreDeepEqual(a, c));
    }

    [Fact]
    public void Memory_And_ReadOnlyMemory_Slices_And_Defaults()
    {
        var base1 = new byte[] { 0, 1, 2, 3, 4, 5 };
        var base2 = new byte[] { 0, 1, 2, 3, 4, 5 };
        var base3 = new byte[] { 0, 1, 9, 3, 4, 5 };

        var a = new MemoryHolder
            { Buf = new Memory<byte>(base1).Slice(2, 2), RBuf = new ReadOnlyMemory<byte>(base1).Slice(1, 3) };
        var b = new MemoryHolder
            { Buf = new Memory<byte>(base2).Slice(2, 2), RBuf = new ReadOnlyMemory<byte>(base2).Slice(1, 3) };
        var c = new MemoryHolder
            { Buf = new Memory<byte>(base3).Slice(2, 2), RBuf = new ReadOnlyMemory<byte>(base3).Slice(1, 3) };

        Assert.True(MemoryHolderDeepEqual.AreDeepEqual(a, b));
        Assert.False(MemoryHolderDeepEqual.AreDeepEqual(a, c));

        var d = new MemoryHolder { Buf = default, RBuf = default };
        var e = new MemoryHolder { Buf = default, RBuf = default };
        Assert.True(MemoryHolderDeepEqual.AreDeepEqual(d, e));
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