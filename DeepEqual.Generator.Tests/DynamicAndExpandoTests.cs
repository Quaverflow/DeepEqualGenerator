using System.Collections.Generic;
using System.Dynamic;
using DeepEqual;
using Xunit;
using static DeepEqual.RewrittenTests.UnifiedFixture;

namespace DeepEqual.RewrittenTests;

public class DynamicAndExpandoTests
{
    [Fact]
    public void Expando_Nested_Change_Is_Detected()
    {
        var a = MakeBaseline();
        var b = Clone(a);

        var dict = (IDictionary<string, object?>)b.Expando;
        var nested = (IDictionary<string, object?>)dict["nested"]!;
        nested["flag"] = false; // flip

        Assert.False(a.AreDeepEqual(b));
    }

    [Fact]
    public void Dictionary_Object_Polymorphic_Differs_On_Value()
    {
        var a = MakeBaseline();
        var b = Clone(a);

        ((Dictionary<string, object?>)b.Props["child"]!)["sub"] = 321;

        Assert.False(a.AreDeepEqual(b));
    }
}