namespace DeepEqual.Tests;

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