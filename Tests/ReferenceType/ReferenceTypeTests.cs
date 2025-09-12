using DeepEqual.Generator.Shared;
using DeepEqual.Tests;
using Tests.ReferenceType.Models;
using Tests.ValueType.Models;

namespace Tests;

public class ReferenceTypeTests
{
    [Fact]
    public void External_Types_Fallback_To_Equals()
    {
        var (a, b) = Fixtures.EqualAggregates();
        Assert.True(BigAggregateDeepEqual.AreDeepEqual(a, b));

        b.Endpoint = new Uri("https://api.example.com/y");
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Sanity_Generated_Class_Is_Callable()
    {
        var (a, b) = Fixtures.EqualAggregates();
        Assert.True(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Deep_Includes_All_Public_Members_By_Default()
    {
        var (a, b) = Fixtures.WithOneDifference(x => x.Title = "Beta");
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Deep_Contains_Collections_Dictionaries_Arrays()
    {
        var (a, b) = Fixtures.WithOneDifference(x => x.Measurements = [1, 2, 4]);
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));

        (a, b) = Fixtures.WithOneDifference(x => x.People[1].Name = "Bobby");
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));

        (a, b) = Fixtures.WithOneDifference(x => x.ByName["Alice"].Role = Role.Lead);
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Skip_Ignores_Member()
    {
        var (a, b) = Fixtures.EqualAggregates();
        b.Ignored = new object();
        Assert.True(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Reference_Uses_ReferenceEquality()
    {
        var (a, b) = Fixtures.EqualAggregates();
        b.Blob = [1, 2, 3]; 
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));
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
        var (a, b) = Fixtures.EqualAggregates();
        b.People.Reverse();
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Unordered_Member_Uses_Multiset_Semantics()
    {
        var (a, b) = Fixtures.EqualAggregates();
        b.Tags = ["blue", "red", "red"];
        Assert.True(BigAggregateDeepEqual.AreDeepEqual(a, b));

        b.Tags = ["red", "blue"]; 
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Root_Default_OrderInsensitive_Applies_When_Not_Overridden()
    {
        var r1 = new RootUnordered { Items = [1, 2, 2, 3] };
        var r2 = new RootUnordered { Items = [2, 3, 2, 1] };
        Assert.True(RootUnorderedDeepEqual.AreDeepEqual(r1, r2));

        var o1 = new RootUnordered { Ordered = [1, 2, 3] };
        var o2 = new RootUnordered { Ordered = [3, 2, 1] };
        Assert.False(RootUnorderedDeepEqual.AreDeepEqual(o1, o2));
    }

    [Fact]
    public void Dictionaries_Compare_Keys_And_Deep_Values()
    {
        var (a, b) = Fixtures.EqualAggregates();
        b.ByName["Bob"].Role = Role.Lead;
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));

        (a, b) = Fixtures.EqualAggregates();
        b.ByName.Remove("Carol");
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Arrays_Are_Compared_Elementwise()
    {
        var (a, b) = Fixtures.EqualAggregates();
        b.Measurements = [1, 2, 3, 4];
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Nulls_Are_Handled()
    {
        var (a, b) = Fixtures.EqualAggregates();
        b.Endpoint = null;
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));

        (a, b) = Fixtures.EqualAggregates();
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
        var (a, b) = Fixtures.EqualAggregates();

        a.Root.Manager = a.People.Last();
        b.Root.Manager = b.People.Last();

        Assert.True(BigAggregateDeepEqual.AreDeepEqual(a, b));

        b.Root.Manager!.Name = "Changed";
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Generic_Node_Is_Compared_Deeply()
    {
        var (a, b) = Fixtures.EqualNodes();
        var h1 = new GenericHolder { Node = a };
        var h2 = new GenericHolder { Node = b };
        Assert.True(GenericHolderDeepEqual.AreDeepEqual(h1, h2));

        b.Children[1].Value = 99;
        Assert.False(GenericHolderDeepEqual.AreDeepEqual(h1, h2));
    }

    [Fact]
    public void Deep_Null_In_Nested_Path_Is_Handled()
    {
        var (a, b) = Fixtures.EqualAggregates();
        a.Root.Manager = null;
        b.Root.Manager = null;
        Assert.True(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Deep_Null_Mismatch_Fails()
    {
        var (a, b) = Fixtures.EqualAggregates();
        a.Root.Manager = null;
        b.Root.Manager = b.Root;
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Member_Override_Wins_Over_Type_And_Root()
    {
        var x = new OrderDefaultRoot { Ordered = [1, 2, 3] };
        var y = new OrderDefaultRoot { Ordered = [3, 2, 1] };
        Assert.True(OrderDefaultRootDeepEqual.AreDeepEqual(x, y));

        var a = new ForceOrderedType { Items = [1, 2, 3] };
        var c = new ForceOrderedType { Items = [3, 2, 1] };
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
        var a = new SchemaHolder
        {
            A1 = new SampleA { X = 1, Y = 2, Z = 0 }
        };
        var b = new SchemaHolder
        {
            A1 = new SampleA { X = 9, Y = 2, Z = 0 }
        };

        Assert.False(SchemaHolderDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void TypeLevel_IgnoreMembers_Schema_Ignores_Listed_Members()
    {
        var a = new SchemaHolder
        {
            B1 = new SampleB { X = 10, Z = 100 }
        };
        var b = new SchemaHolder
        {
            B1 = new SampleB { X = 10, Z = 999 }
        };

        Assert.True(SchemaHolderDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void TypeLevel_IgnoreMembers_Schema_Still_Compares_Other_Members()
    {
        var a = new SchemaHolder
        {
            B1 = new SampleB { X = 10, Z = 100 }
        };
        var b = new SchemaHolder
        {
            B1 = new SampleB { X = 11, Z = 100 }
        };

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
        var c = new Dictionary<string, object?>
        {
            ["a"] = new Dictionary<string, object?>
            {
                ["b"] = new Dictionary<string, object?>
                {
                    ["c"] = 155,
                    ["d"] = new[] { 1, 2, 3 }
                }
            }
        };
        Assert.True(DynamicRootDeepEqual.AreDeepEqual(new DynamicRoot { Data = a }, new DynamicRoot { Data = b }));
        Assert.False(DynamicRootDeepEqual.AreDeepEqual(new DynamicRoot { Data = a }, new DynamicRoot { Data = c }));
    }

    [Fact]
    public void Expando_is_compared_as_string_object_map()
    {
        dynamic ea = new System.Dynamic.ExpandoObject();
        ea.name = "alice"; ea.age = 30;

        dynamic eb = new System.Dynamic.ExpandoObject();
        eb.name = "alice"; eb.age = 30;

        var ra = new DynamicRoot { Data = ea };
        var rb = new DynamicRoot { Data = eb };
        Assert.True(DynamicRootDeepEqual.AreDeepEqual(ra, rb));

        eb.age = 31;
        Assert.False(DynamicRootDeepEqual.AreDeepEqual(ra, rb));
    }
}


[DeepComparable]
public partial class DynamicRoot
{
    public object? Data { get; set; }
}