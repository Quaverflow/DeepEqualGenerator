using DeepEqual;
using DeepEqual.RewrittenTests.Domain;
using Xunit;
using static DeepEqual.RewrittenTests.UnifiedFixture;

namespace DeepEqual.RewrittenTests;

public class EqualityTests
{
    [Fact]
    public void Primitives_And_Datetime_Equal_When_Identical()
    {
        var a = MakeBaseline();
        var b = Clone(a);

        Assert.True(a.AreDeepEqual(b));
    }

    [Fact]
    public void Ordered_Collections_Are_Ordered_By_Default()
    {
        var a = MakeBaseline();
        var b = Clone(a);

        // Reorder Widgets (ordered by default)
        var w = b.Widgets[0];
        b.Widgets.RemoveAt(0);
        b.Widgets.Add(w);

        Assert.False(a.AreDeepEqual(b));
    }

    [Fact]
    public void Unordered_Keyed_Lines_By_Sku_Are_Order_Insensitive()
    {
        var a = MakeBaseline();
        var b = Clone(a);

        // Swap A and C
        var l0 = b.Lines[0];
        b.Lines.RemoveAt(0);
        b.Lines.Add(l0);

        Assert.True(a.AreDeepEqual(b)); // unordered keyed by Sku -> equal
    }

    [Fact]
    public void Unordered_Keyed_Lines_With_Changed_Qty_Are_Not_Equal()
    {
        var a = MakeBaseline();
        var b = Clone(a);

        // Change quantity for Sku "B"
        var lineB = b.Lines.Find(l => l.Sku == "B")!;
        lineB.Qty++;

        Assert.False(a.AreDeepEqual(b));
    }

    [Fact]
    public void Polymorphic_Interface_SameType_SameValues_Are_Equal()
    {
        var a = MakeBaseline();
        var b = Clone(a);
        a.Shape = new Circle { Radius = 3 };
        b.Shape = new Circle { Radius = 3 };

        Assert.True(a.AreDeepEqual(b));
    }

    [Fact]
    public void Polymorphic_Interface_Different_Runtime_Types_Not_Equal()
    {
        var a = MakeBaseline();
        var b = Clone(a);
        a.Shape = new Circle { Radius = 3 };
        b.Shape = new Square { Side = 3 };

        Assert.False(a.AreDeepEqual(b));
    }
}