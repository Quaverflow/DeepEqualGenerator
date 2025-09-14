using System.Globalization;
using DeepEqual.Generator.Shared;
using DeepEqual.Generator.Tests.Models;

namespace DeepEqual.Generator.Tests.Tests;

public class CultureSensitiveStringTests
{
    [Fact]
    public void Turkish_Case_Insensitive_Compare_Uses_CurrentCulture()
    {
        var prev = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            var a = new StringHolder { Value = "I" };               var b = new StringHolder { Value = "ı" };   
            var opts = new ComparisonOptions { StringComparison = StringComparison.CurrentCultureIgnoreCase };
            Assert.True(StringHolderDeepEqual.AreDeepEqual(a, b, opts));

            var ordinal = new ComparisonOptions { StringComparison = StringComparison.OrdinalIgnoreCase };
            Assert.False(StringHolderDeepEqual.AreDeepEqual(a, b, ordinal));
        }
        finally
        {
            CultureInfo.CurrentCulture = prev;
        }
    }
}