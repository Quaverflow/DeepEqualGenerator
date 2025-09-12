namespace Tests;

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
            Tags = ["red", "blue", "red"],
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
            Tags = ["red", "red", "blue"],
            Measurements = [1, 2, 3],
            Endpoint = new Uri("https://api.example.com/x"),
            Ignored = new { x = 999 },
            Blob = a.Blob 
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