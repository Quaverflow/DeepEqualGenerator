using DeepEqual;
using DeepEqual.Generator.Shared;
using DeepEqual.RewrittenTests.Domain;
using Xunit;
using static DeepEqual.RewrittenTests.UnifiedFixture;

namespace DeepEqual.RewrittenTests;

public class PrecedenceAndOptionsTests
{
    [Fact]
    public void Ordered_Vs_Unordered_Lists_Are_Respected()
    {
        var a = MakeBaseline();
        var b = Clone(a);

        // Widgets ordered: swap -> not equal
        var w = b.Widgets[0];
        b.Widgets.RemoveAt(0);
        b.Widgets.Add(w);
        Assert.False(a.AreDeepEqual(b));

        // Lines unordered keyed: swap -> equal
        a = MakeBaseline();
        b = Clone(a);
        var l = b.Lines[0];
        b.Lines.RemoveAt(0);
        b.Lines.Add(l);
        Assert.True(a.AreDeepEqual(b));
    }

    [Fact]
    public void Epsilon_And_StringComparison_Can_Be_Passed_Via_Context()
    {
        var a = MakeBaseline();
        var b = Clone(a);

        // Numerical within tolerance
        b.Props["threshold"] = 0.7500000001;

        var opts = new ComparisonOptions
        {
            DoubleEpsilon = 1e-8,
            StringComparison = System.StringComparison.OrdinalIgnoreCase
        };
        var ctx = new ComparisonContext(opts);

        Assert.True(a.AreDeepEqual(b, ctx));
    }
}