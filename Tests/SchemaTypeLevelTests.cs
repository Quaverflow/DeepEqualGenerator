using DeepEqual.Generator.Shared;

namespace DeepEqual.Tests;

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