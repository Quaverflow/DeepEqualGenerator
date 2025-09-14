using System.Globalization;
using DeepEqual.Generator.Shared;
using DeepEqual.Generator.Tests.Models;

public class CultureSensitiveStringTests
{
    [Fact]
    public void Turkish_Case_Insensitive_Compare_Uses_CurrentCulture()
    {
        var prev = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            var A = new StringHolder { Value = "I" };               var B = new StringHolder { Value = "ı" };   
            var opts = new ComparisonOptions { StringComparison = StringComparison.CurrentCultureIgnoreCase };
            Assert.True(StringHolderDeepEqual.AreDeepEqual(A, B, opts));

            var ordinal = new ComparisonOptions { StringComparison = StringComparison.OrdinalIgnoreCase };
            Assert.False(StringHolderDeepEqual.AreDeepEqual(A, B, ordinal));
        }
        finally
        {
            CultureInfo.CurrentCulture = prev;
        }
    }
}