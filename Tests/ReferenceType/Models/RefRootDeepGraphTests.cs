namespace DeepEqual.Tests;

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