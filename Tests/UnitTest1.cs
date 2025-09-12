using DeepEqual.Generator.Attributes;

namespace Tests;

[DeepComparable(OrderInsensitiveCollections = false)]
public class BigAggregate
{
    public string Title { get; set; } = "";
    public int Version { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }

    public Person Root { get; set; } = new();

    public List<Person> People { get; set; } = [];

    [DeepCompare(OrderInsensitive = true)]
    public List<string> Tags { get; set; } = [];

    public Dictionary<string, Person> ByName { get; set; } = new(StringComparer.Ordinal);

    public int[] Measurements { get; set; } = [];

    public Uri? Endpoint { get; set; }

    [DeepCompare(Kind = CompareKind.Skip)]
    public object? Ignored { get; set; }

    [DeepCompare(Kind = CompareKind.Reference)]
    public byte[]? Blob { get; set; }
}

[DeepComparable]
public class Person
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Role Role { get; set; }

    public Person? Manager { get; set; }
    public List<Person> Reports { get; set; } = [];

    private int SecretScore { get; set; }

    public void SetSecret(int s) => SecretScore = s;
}

public enum Role { Unknown, Dev, Lead, Manager }

public class Node<T>
{
    public T Value { get; set; } = default!;
    public List<Node<T>> Children { get; set; } = [];
    public Node<T>? Parent { get; set; }
}

[DeepComparable]
public class Holder
{
    public ThirdPartyLike Third { get; set; } = new();
}

[DeepComparable(OrderInsensitiveCollections = true)]
public class RootUnordered
{
    public List<int> Items { get; set; } = [];
    [DeepCompare(OrderInsensitive = false)]
    public List<int> Ordered { get; set; } = [];
}

[DeepCompare(Kind = CompareKind.Shallow)]
public class ThirdPartyLike
{
    public string Payload { get; set; } = "";
    public override bool Equals(object? obj) => obj is ThirdPartyLike t && t.Payload == Payload;
    public override int GetHashCode() => Payload.GetHashCode();
}
[DeepComparable]
public class GenericHolder
{
    public Node<int> Node { get; set; } = new();
}


[DeepComparable(OrderInsensitiveCollections = true)]
public class OrderDefaultRoot { public List<int> Ordered { get; set; } = new(); }


[DeepCompare(OrderInsensitive = false)]
[DeepComparable]
public class ForceOrderedType { public List<int> Items { get; set; } = new(); }

internal static class Fixtures
{
    public static (BigAggregate A, BigAggregate B) EqualAggregates()
    {
        var (root, people, byName) = Org();
        var a = new BigAggregate
        {
            Title = "Alpha",
            Version = 1,
            GeneratedAt = new DateTimeOffset(2025, 9, 12, 9, 0, 0, TimeSpan.Zero),
            Root = root,
            People = people,
            ByName = byName,
            Tags = ["red", "blue", "red"], // multiset
            Measurements = [1, 2, 3],
            Endpoint = new Uri("https://api.example.com/x"),
            Ignored = new { x = 1 },
            Blob = [1, 2, 3]
        };

        var (root2, people2, byName2) = Org();
        var b = new BigAggregate
        {
            Title = "Alpha",
            Version = 1,
            GeneratedAt = new DateTimeOffset(2025, 9, 12, 9, 0, 0, TimeSpan.Zero),
            Root = root2,
            People = people2,
            ByName = byName2,
            Tags = ["red", "red", "blue"], // same multiset, different order
            Measurements = [1, 2, 3],
            Endpoint = new Uri("https://api.example.com/x"),
            Ignored = new { x = 999 }, // ignored anyway
            Blob = a.Blob // same reference (Reference compare)
        };

        return (a, b);
    }

    public static (BigAggregate A, BigAggregate B) WithOneDifference(Action<BigAggregate> mutateB)
    {
        var (a, b) = EqualAggregates();
        mutateB(b);
        return (a, b);
    }

    private static Guid GuidFrom(string s)
    {
        // not cryptographic; just reproducible
        var bytes = System.Text.Encoding.UTF8.GetBytes(s);
        Span<byte> guid = stackalloc byte[16];
        for (var i = 0; i < guid.Length; i++) guid[i] = (byte)(bytes[i % bytes.Length] + i * 31);
        return new Guid(guid);
    }

    private static (Person root, List<Person> people, Dictionary<string, Person> byName) Org()
    {
        var alice = new Person { Id = GuidFrom("Alice"), Name = "Alice", Role = Role.Manager };
        var bob = new Person { Id = GuidFrom("Bob"), Name = "Bob", Role = Role.Dev, Manager = alice };
        var carol = new Person { Id = GuidFrom("Carol"), Name = "Carol", Role = Role.Lead, Manager = alice };
        alice.Reports.AddRange([bob, carol]);

        // Optional cycle
        carol.Manager = alice;

        alice.SetSecret(42);
        bob.SetSecret(7);
        carol.SetSecret(9);

        List<Person> people = [alice, bob, carol];
        var byName = new Dictionary<string, Person>(StringComparer.Ordinal)
        {
            ["Alice"] = alice,
            ["Bob"] = bob,
            ["Carol"] = carol
        };
        return (alice, people, byName);
    }

    public static (Node<int> A, Node<int> B) EqualNodes()
    {
        var a = new Node<int> { Value = 1 };
        var a1 = new Node<int> { Value = 2, Parent = a };
        var a2 = new Node<int> { Value = 3, Parent = a };
        a.Children.AddRange([a1, a2]);

        var b = new Node<int> { Value = 1 };
        var b1 = new Node<int> { Value = 2, Parent = b };
        var b2 = new Node<int> { Value = 3, Parent = b };
        b.Children.AddRange([b1, b2]);

        return (a, b);
    }
}

public class Tests
{
    [Fact]
    public void External_Types_Fallback_To_Equals()
    {
        var (a, b) = Fixtures.EqualAggregates();
        Assert.True(BigAggregateDeepEqual.AreDeepEqual(a, b));

        // Different Uri -> Equals false
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
        Assert.True(BigAggregateDeepEqual.AreDeepEqual(a, b)); // ignored member
    }

    [Fact]
    public void Reference_Uses_ReferenceEquality()
    {
        var (a, b) = Fixtures.EqualAggregates();
        b.Blob = [1, 2, 3]; // identical content, different reference
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
        // People is ordered by default
        b.People.Reverse();
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Unordered_Member_Uses_Multiset_Semantics()
    {
        var (a, b) = Fixtures.EqualAggregates();
        b.Tags = ["blue", "red", "red"]; // same multiset
        Assert.True(BigAggregateDeepEqual.AreDeepEqual(a, b));

        b.Tags = ["red", "blue"]; // different multiplicity
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Root_Default_OrderInsensitive_Applies_When_Not_Overridden()
    {
        // Create a root with default unordered collections
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

        // Create deeper cycle: make Root.Manager point to last report on both graphs
        a.Root.Manager = a.People.Last();
        b.Root.Manager = b.People.Last();

        Assert.True(BigAggregateDeepEqual.AreDeepEqual(a, b));

        // Break symmetry in the cycle
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
        b.Root.Manager = b.Root; // non-null
        Assert.False(BigAggregateDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Member_Override_Wins_Over_Type_And_Root()
    {
        var x = new OrderDefaultRoot { Ordered = new() { 1, 2, 3 } };
        var y = new OrderDefaultRoot { Ordered = new() { 3, 2, 1 } };
        Assert.True(OrderDefaultRootDeepEqual.AreDeepEqual(x, y)); // root says unordered

        var a = new ForceOrderedType { Items = new() { 1, 2, 3 } };
        var c = new ForceOrderedType { Items = new() { 3, 2, 1 } };
        Assert.False(ForceOrderedTypeDeepEqual.AreDeepEqual(a, c)); // type forces ordered
    }
}
