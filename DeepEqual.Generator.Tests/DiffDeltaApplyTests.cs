using System.Text.Json;
using DeepEqual;
using DeepEqual.Generator.Shared;
using Newtonsoft.Json;
using Xunit;
using static DeepEqual.RewrittenTests.UnifiedFixture;

namespace DeepEqual.RewrittenTests;

public class DiffDeltaApplyTests
{
    [Fact]
    public void ComputeDelta_Then_ApplyDelta_Brings_A_To_B()
    {
        var a = MakeBaseline();
        var b = Clone(a);

        // Change several spots
        //b.Customer.Name = "Grace";
        //b.Bytes[0] = 99;
        //b.Widgets[1].Count++;
        b.Lines.Find(x => x.Sku == "B")!.Qty = 42;
        System.Diagnostics.Debug.WriteLine($"DeepOps @ {typeof(DeepEqual.DeepOps).Assembly.Location}");
        System.Diagnostics.Debug.WriteLine($"Registry @ {typeof(DeepEqual.Generator.Shared.GeneratedHelperRegistry).Assembly.Location}");
        System.Diagnostics.Debug.WriteLine($"Domain  @ {typeof(DeepEqual.RewrittenTests.Domain.Order).Assembly.Location}");
        System.Diagnostics.Debug.WriteLine($"Has OrderDeepOps type? " +
                                           (typeof(DeepEqual.RewrittenTests.Domain.Order).Assembly
                                               .GetType("DeepEqual.RewrittenTests.Domain.OrderDeepOps") != null));

        System.Runtime.CompilerServices.RuntimeHelpers
            .RunClassConstructor(typeof(DeepEqual.RewrittenTests.Domain.OrderDeepOps).TypeHandle);
        var delta = a.ComputeDeepDelta(b);
        var r = JsonConvert.SerializeObject(delta);
        // For reference types, ApplyDeepDelta returns the updated instance
        a = a.ApplyDeepDelta(delta);

        Assert.True(a.AreDeepEqual(b), r);
    }

    [Fact]
    public void GetDeepDiff_Reports_Has_When_Different()
    {
        var a = MakeBaseline();
        var b = Clone(a);
        b.Lines[0].Qty++;

        var (has, diff) = a.GetDeepDiff(b);
        Assert.True(has);
        Assert.NotNull(diff);
    }
}