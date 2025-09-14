using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using DeepEqual.Generator.Shared;
using Xunit;

namespace DeepEqual.Tests;

public sealed class RefTests
{
    
    [Fact]
    public void External_Types_Fallback_To_Equals()
    {
        var (a, b) = EqualAggregates();
        Assert.True(BigAggregateDeepEqual.AreDeepEqual(a, b));

        b.Endpoint = new Uri("https://api.example.com/y");
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Sanity_Generated_Class_Is_Callable()
    {
        var (a, b) = EqualAggregates();
        Assert.True(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Deep_Includes_All_Public_Members_By_Default()
    {
        var (a, b) = WithOneDifference(x => x.Title = "Beta");
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Deep_Contains_Collections_Dictionaries_Arrays()
    {
        var (a, b) = WithOneDifference(x => x.Measurements = new[] { 1, 2, 4 });
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));

        (a, b) = WithOneDifference(x => x.People[1].Name = "Bobby");
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));

        (a, b) = WithOneDifference(x => x.ByName["Alice"].Role = Role.Lead);
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Skip_Ignores_Member()
    {
        var (a, b) = EqualAggregates();
        b.Ignored = new object();
        Assert.True(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Reference_Uses_ReferenceEquality()
    {
        var (a, b) = EqualAggregates();
        b.Blob = new byte[] { 1, 2, 3 };         Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Shallow_On_Type_Is_Used_Globally()
    {
        var t1 = new ThirdPartyLike { Payload = "X" };
        var t2 = new ThirdPartyLike { Payload = "X" };
        Assert.True(Equals(t1, t2)); 
        var h1 = new Holder { Third = t1 };
        var h2 = new Holder { Third = t2 };
        Assert.True(HolderDeepEqual.AreDeepEqual(h1, h2));

        h2.Third = new ThirdPartyLike { Payload = "Y" };
        Assert.False(HolderDeepEqual.AreDeepEqual(h1, h2));
    }

    [Fact]
    public void Ordered_Sequences_Must_Match_Positionally()
    {
        var (a, b) = EqualAggregates();
        b.People.Reverse();
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Unordered_Member_Uses_Multiset_Semantics()
    {
        var (a, b) = EqualAggregates();
        b.Tags = new List<string> { "blue", "red", "red" };         Assert.True(BigAggregateDeepEqual.AreDeepEqual(a, b));

        b.Tags = new List<string> { "red", "blue" };         Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Root_Default_OrderInsensitive_Applies_When_Not_Overridden()
    {
        var r1 = new RootUnordered { Items = new List<int> { 1, 2, 2, 3 } };
        var r2 = new RootUnordered { Items = new List<int> { 2, 3, 2, 1 } };
        Assert.True(RootUnorderedDeepEqual.AreDeepEqual(r1, r2));

        var o1 = new RootUnordered { Ordered = new List<int> { 1, 2, 3 } };
        var o2 = new RootUnordered { Ordered = new List<int> { 3, 2, 1 } };
        Assert.False(RootUnorderedDeepEqual.AreDeepEqual(o1, o2));
    }

    [Fact]
    public void Dictionaries_Compare_Keys_And_Deep_Values()
    {
        var (a, b) = EqualAggregates();
        b.ByName["Bob"].Role = Role.Lead;
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));

        (a, b) = EqualAggregates("James");
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Arrays_Are_Compared_Elementwise()
    {
        var (a, b) = EqualAggregates();
        b.Measurements = new[] { 1, 2, 3, 4 };
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Nulls_Are_Handled()
    {
        var (a, b) = EqualAggregates();
        b.Endpoint = null;
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));

        (a, b) = EqualAggregates();
        a.Endpoint = null; b.Endpoint = null;
        Assert.True(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Structs_And_Enums_Compare_By_Value()
    {
        var p1 = new Person { Name = "X", Role = Role.Dev };
        var p2 = new Person { Name = "X", Role = Role.Dev };
        p1.SetSecret(5); p2.SetSecret(5);
        Assert.True(PersonDeepEqual.AreDeepEqual(p1, p2));

        p2.Role = Role.Lead;
        Assert.False(PersonDeepEqual.AreDeepEqual(p1, p2));
    }

    [Fact]
    public void Cycles_Terminate_And_Compare_Correctly()
    {
        var (a, b) = EqualAggregates();

                a.People[0].Friend = a.People[1];
        a.People[1].Friend = a.People[0];
        b.People[0].Friend = b.People[1];
        b.People[1].Friend = b.People[0];

        a.Root.Manager = a.People[^1];
        b.Root.Manager = b.People[^1];

        Assert.True(BigAggregateDeepEqual.AreDeepEqual(a, b));

        b.Root.Manager!.Name = "Changed";
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Generic_Node_Is_Compared_Deeply()
    {
        var a = SampleTree();
        var b = SampleTree();
        var h1 = new GenericHolder { Node = a };
        var h2 = new GenericHolder { Node = b };
        Assert.True(GenericHolderDeepEqual.AreDeepEqual(h1, h2));

        b.Children[1].Value = 99;
        Assert.False(GenericHolderDeepEqual.AreDeepEqual(h1, h2));
    }

    [Fact]
    public void Deep_Null_In_Nested_Path_Is_Handled()
    {
        var (a, b) = EqualAggregates();
        a.Root.Manager = null;
        b.Root.Manager = null;
        Assert.True(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Deep_Null_Mismatch_Fails()
    {
        var (a, b) = EqualAggregates();
        a.Root.Manager = null;
        b.Root.Manager = b.Root.Manager;         Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Member_Override_Wins_Over_Type_And_Root()
    {
        var x = new OrderDefaultRoot { Ordered = new List<int> { 1, 2, 3 } };
        var y = new OrderDefaultRoot { Ordered = new List<int> { 3, 2, 1 } };
        Assert.True(OrderDefaultRootDeepEqual.AreDeepEqual(x, y));

        var a = new ForceOrderedType { Items = new List<int> { 1, 2, 3 } };
        var c = new ForceOrderedType { Items = new List<int> { 3, 2, 1 } };
        Assert.False(ForceOrderedTypeDeepEqual.AreDeepEqual(a, c));
    }

    
    [Fact]
    public void TypeLevel_Members_Schema_Only_Compares_Listed_Members()
    {
        var a = new SchemaHolder
        {
            A1 = new SampleA { X = 1, Y = 2, Z = 999 },
            B1 = new SampleB { X = 7, Z = 5 }
        };
        var b = new SchemaHolder
        {
            A1 = new SampleA { X = 1, Y = 2, Z = -1 },
            B1 = new SampleB { X = 7, Z = 5 }
        };

        Assert.True(SchemaHolderDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void TypeLevel_Members_Schema_Detects_Change_In_Included_Fields()
    {
        var a = new SchemaHolder { A1 = new SampleA { X = 1, Y = 2, Z = 0 } };
        var b = new SchemaHolder { A1 = new SampleA { X = 9, Y = 2, Z = 0 } };
        Assert.False(SchemaHolderDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void TypeLevel_IgnoreMembers_Schema_Ignores_Listed_Members()
    {
        var a = new SchemaHolder { B1 = new SampleB { X = 10, Z = 100 } };
        var b = new SchemaHolder { B1 = new SampleB { X = 10, Z = 999 } };
        Assert.True(SchemaHolderDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void TypeLevel_IgnoreMembers_Schema_Still_Compares_Other_Members()
    {
        var a = new SchemaHolder { B1 = new SampleB { X = 10, Z = 100 } };
        var b = new SchemaHolder { B1 = new SampleB { X = 11, Z = 100 } };
        Assert.False(SchemaHolderDeepEqual.AreDeepEqual(a, b));
    }

    
    [Fact]
    public void Deep_nested_string_keyed_maps_compare_recursively()
    {
        var a = new Dictionary<string, object?>
        {
            ["a"] = new Dictionary<string, object?>
            {
                ["b"] = new Dictionary<string, object?>
                {
                    ["c"] = 123,
                    ["d"] = new[] { 1, 2, 3 }
                }
            }
        };
        var b = new Dictionary<string, object?>
        {
            ["a"] = new Dictionary<string, object?>
            {
                ["b"] = new Dictionary<string, object?>
                {
                    ["c"] = 123,
                    ["d"] = new[] { 1, 2, 3 }
                }
            }
        };
        var ra = new DynamicRoot { Data = a };
        var rb = new DynamicRoot { Data = b };
        Assert.True(DynamicRootDeepEqual.AreDeepEqual(ra, rb));

        (((Dictionary<string, object?>)((Dictionary<string, object?>)b["a"]!)["b"]!)).Remove("d");
        Assert.False(DynamicRootDeepEqual.AreDeepEqual(ra, rb));
    }

    [Fact]
    public void Expando_is_compared_as_string_object_map()
    {
        dynamic ea = new ExpandoObject(); ea.name = "alice"; ea.age = 30;
        dynamic eb = new ExpandoObject(); eb.name = "alice"; eb.age = 30;

        var ra = new DynamicRoot { Data = ea };
        var rb = new DynamicRoot { Data = eb };
        Assert.True(DynamicRootDeepEqual.AreDeepEqual(ra, rb));

        eb.age = 31;
        Assert.False(DynamicRootDeepEqual.AreDeepEqual(ra, rb));
    }

    [Fact]
    public void Generic_NonString_Keyed_Dictionaries_Are_Compared_Dynamically()
    {
        var a = new DynamicRoot { Data = new Dictionary<int, object?> { [1] = "one", [2] = new[] { 1, 2 } } };
        var b = new DynamicRoot { Data = new Dictionary<int, object?> { [1] = "one", [2] = new[] { 1, 2 } } };
        Assert.True(DynamicRootDeepEqual.AreDeepEqual(a, b));

        ((Dictionary<int, object?>)b.Data!)[2] = new[] { 1, 2, 3 };
        Assert.False(DynamicRootDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Object_Typed_Member_Uses_StronglyTyped_Helper_When_Possible()
    {
        var a = new ObjectHolder { Value = new Person { Name = "X", Role = Role.Dev } };
        var b = new ObjectHolder { Value = new Person { Name = "X", Role = Role.Dev } };
        Assert.True(ObjectHolderDeepEqual.AreDeepEqual(a, b));

        ((Person)b.Value!).Role = Role.Lead;
        Assert.False(ObjectHolderDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void ObjectArray_Ordered_Elements_DeepCompare()
    {
        var a = new ObjectArrayHolder { Items = new object?[] { 1, "x", new Person { Name = "Z", Role = Role.Dev } } };
        var b = new ObjectArrayHolder { Items = new object?[] { 1, "x", new Person { Name = "Z", Role = Role.Dev } } };
        Assert.True(ObjectArrayHolderDeepEqual.AreDeepEqual(a, b));

        ((Person)b.Items![2]!).Role = Role.Lead;
        Assert.False(ObjectArrayHolderDeepEqual.AreDeepEqual(a, b));
    }

    
    [Fact]
    public void IncludeInternals_True_Enables_Deep_Compare_Of_Internal_Types()
    {
        var a = new InternalsOnRoot { Data = new InternalData { X = 5 } };
        var b = new InternalsOnRoot { Data = new InternalData { X = 5 } };
        Assert.True(InternalsOnRootDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void IncludeInternals_False_Falls_Back_To_Shallow_On_Internal_Types()
    {
        var a = new InternalsOffRoot { Data = new InternalData { X = 5 } };
        var b = new InternalsOffRoot { Data = new InternalData { X = 5 } };
        Assert.False(InternalsOffRootDeepEqual.AreDeepEqual(a, b));     }

    
    [Fact]
    public void HashSet_Unordered_When_Requested()
    {
        var a = new SetHolder { Tags = new HashSet<string>(StringComparer.Ordinal) { "a", "b", "c" } };
        var b = new SetHolder { Tags = new HashSet<string>(StringComparer.Ordinal) { "c", "b", "a" } };
        Assert.True(SetHolderDeepEqual.AreDeepEqual(a, b));

        b.Tags!.Remove("a");
        Assert.False(SetHolderDeepEqual.AreDeepEqual(a, b));
    }

    
    [Fact]
    public void DateOnly_TimeOnly_ReferenceHolder()
    {
        var a = new DateOnlyTimeOnlyRefHolder { Day = new DateOnly(2025, 1, 1), Time = new TimeOnly(12, 0) };
        var b = new DateOnlyTimeOnlyRefHolder { Day = new DateOnly(2025, 1, 1), Time = new TimeOnly(12, 0) };
        Assert.True(DateOnlyTimeOnlyRefHolderDeepEqual.AreDeepEqual(a, b));

        b.Time = new TimeOnly(12, 0, 1);
        Assert.False(DateOnlyTimeOnlyRefHolderDeepEqual.AreDeepEqual(a, b));
    }

    
    private static (BigAggregate a, BigAggregate b) EqualAggregates(string diff = "Carol")
    {
        var p1a = new Person { Name = "Alice", Role = Role.Dev };
        var p2a = new Person { Name = "Bob", Role = Role.Dev };
        var p3a = new Person { Name = "Carol", Role = Role.Lead };

        var p1b = new Person { Name = "Alice", Role = Role.Dev };
        var p2b = new Person { Name = "Bob", Role = Role.Dev };
        var p3b = new Person { Name = diff, Role = Role.Lead };

        var a = new BigAggregate
        {
            Title = "Alpha",
            Measurements = new[] { 1, 2, 3 },
            People = new List<Person> { p1a, p2a, p3a },
            ByName = new Dictionary<string, Person> { ["Alice"] = p1a, ["Bob"] = p2a, ["Carol"] = p3a },
            Endpoint = new Uri("https://api.example.com/x"),
            Tags = new List<string> { "red", "blue", "red" },
            Blob = new byte[] { 1, 2, 3 },
            Ignored = null,
            Root = new RootNode
            {
                Manager = new Person
                {
                    Name = "Put",
                    Role = Role.Dev,
                    Friend = new Person
                    {
                        Name = "Nat",
                        Role = Role.Lead,
                    }
                }
            }
        };
        var b = new BigAggregate
        {
            Title = "Alpha",
            Measurements = new[] { 1, 2, 3 },
            People = new List<Person> { p1b, p2b, p3b },
            ByName = new Dictionary<string, Person> { ["Alice"] = p1b, ["Bob"] = p2b, ["Carol"] = p3b },
            Endpoint = new Uri("https://api.example.com/x"),
            Tags = new List<string> { "red", "red", "blue" },
            Blob = a.Blob,             Ignored = new object(),
            Root = new RootNode
            {
                Manager = new Person
                {
                    Name = "Put",
                    Role = Role.Dev,
                    Friend = new Person
                    {
                        Name = "Nat",
                        Role = Role.Lead,
                    }
                }
            }
        };
        return (a, b);
    }

    private static (BigAggregate a, BigAggregate b) WithOneDifference(Action<BigAggregate> changeB)
    {
        var (a, b) = EqualAggregates();
        changeB(b);
        return (a, b);
    }

    private static Node<int> SampleTree()
    {
        return new Node<int>
        {
            Value = 10,
            Children =
            {
                new Node<int> { Value = 5 },
                new Node<int> { Value = 7 }
            }
        };
    }
}

/* ----------------------- Models for ref tests ---------------------------- */

public enum Role { Dev, Lead }

[DeepComparable]
public sealed class Person
{
    public string Name { get; set; } = "";
    public Role Role { get; set; }
    public Person? Friend { get; set; }

    private int _secret;
    public void SetSecret(int x) => _secret = x;
}

[DeepComparable(OrderInsensitiveCollections = true)]
public sealed class RootUnordered
{
    public List<int>? Items { get; set; }

    [DeepCompare(OrderInsensitive = false)]
    public List<int>? Ordered { get; set; }
}

[DeepComparable(OrderInsensitiveCollections = true)]
public sealed class OrderDefaultRoot
{
    [DeepCompare(OrderInsensitive = true)]
    public List<int>? Ordered { get; set; }
}

[DeepComparable(OrderInsensitiveCollections = false)]
public sealed class ForceOrderedType
{
    public List<int>? Items { get; set; }
}

[DeepComparable]
public sealed class Holder
{
    public ThirdPartyLike? Third { get; set; }
}

[DeepCompare(Kind = CompareKind.Shallow)]
public sealed class ThirdPartyLike
{
    public string? Payload { get; set; }

    public override bool Equals(object? obj)
        => obj is ThirdPartyLike t && string.Equals(Payload, t.Payload, StringComparison.Ordinal);

    public override int GetHashCode() => Payload?.GetHashCode() ?? 0;
}

[DeepComparable]
public sealed class RootNode
{
    public Person? Manager { get; set; }
}

[DeepComparable]
public sealed class BigAggregate
{
    public string Title { get; set; } = "";
    public int[] Measurements { get; set; } = Array.Empty<int>();
    public List<Person> People { get; set; } = new();
    public IReadOnlyDictionary<string, Person> ByName { get; set; } = new Dictionary<string, Person>();
    public Uri? Endpoint { get; set; }

    [DeepCompare(OrderInsensitive = true)]
    public List<string> Tags { get; set; } = new();

    [DeepCompare(Kind = CompareKind.Reference)]
    public byte[]? Blob { get; set; }

    [DeepCompare(Kind = CompareKind.Skip)]
    public object? Ignored { get; set; }

    public RootNode Root { get; set; } = new();
}

[DeepComparable]
public sealed class SchemaHolder
{
    public SampleA? A1 { get; set; }
    public SampleB? B1 { get; set; }
}

[DeepCompare(Members = new[] { "X", "Y" })]
public sealed class SampleA
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; } }

[DeepCompare(IgnoreMembers = new[] { "Z" })]
public sealed class SampleB
{
    public int X { get; set; }
    public int Z { get; set; } }

[DeepComparable]
public sealed class DynamicRoot
{
    public object? Data { get; set; }
}

[DeepComparable]
public sealed class ObjectHolder
{
    public object? Value { get; set; }
}

[DeepComparable]
public sealed class ObjectArrayHolder
{
    public object?[]? Items { get; set; }
}

[DeepComparable(IncludeInternals = true)]
internal sealed class InternalsOnRoot
{
    public InternalData Data { get; set; } = new();
}

[DeepComparable] internal sealed class InternalsOffRoot
{
    public InternalData Data { get; set; } = new();
}

internal sealed class InternalData
{
    public int X { get; set; }
}

[DeepComparable]
public sealed class SetHolder
{
    [DeepCompare(OrderInsensitive = true)]
    public HashSet<string>? Tags { get; set; }
}

[DeepComparable]
public sealed class DateOnlyTimeOnlyRefHolder
{
    public DateOnly Day { get; set; }
    public TimeOnly Time { get; set; }
}

[DeepComparable]
public sealed class GenericHolder
{
    public Node<int>? Node { get; set; }
}

[DeepComparable]
public sealed class Node<T>
{
    public T? Value { get; set; }
    public List<Node<T>> Children { get; set; } = new();
}
