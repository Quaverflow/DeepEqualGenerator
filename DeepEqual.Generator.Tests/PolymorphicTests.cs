using DeepEqual;
using DeepEqual.RewrittenTests.Domain;
using Xunit;
using static DeepEqual.RewrittenTests.UnifiedFixture;

namespace DeepEqual.RewrittenTests;

public class PolymorphicTests
{
    [Fact]
    public void Polymorphic_Square_Vs_Circle_Not_Equal()
    {
        var a = MakeBaseline();
        var b = Clone(a);
        a.Shape = new Square { Side = 2 };
        b.Shape = new Circle { Radius = 2 };
        Assert.False(a.AreDeepEqual(b));
    }

    [Fact]
    public void Polymorphic_SameType_SameValues_Equal()
    {
        var a = MakeBaseline();
        var b = Clone(a);
        a.Shape = new Square { Side = 2 };
        b.Shape = new Square { Side = 2 };
        Assert.True(a.AreDeepEqual(b));
    }
}