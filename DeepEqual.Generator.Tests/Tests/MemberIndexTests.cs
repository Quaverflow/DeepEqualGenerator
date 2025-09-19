using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Tests;

[DeepComparable(GenerateDiff = true, GenerateDelta = true, StableMemberIndex = StableMemberIndexMode.Off)]
public sealed class OrdinalHost
{
    public int A { get; set; }
    public int B { get; set; }
    public int C { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]public sealed class StableHost
{
    public int A { get; set; }
    public int B { get; set; }
    public int C { get; set; }
}

public class MemberIndexTests
{
    [Fact]
    public void Ordinal_Index_Uses_Declaration_Order()
    {
        var baseObj = new OrdinalHost { A = 1, B = 2, C = 3 };

        var modA = new OrdinalHost { A = 10, B = 2, C = 3 };
        var docA = OrdinalHostDeepOps.ComputeDelta(baseObj, modA);
        var idxA = docA.Operations.Single(o => o.Kind == DeltaKind.SetMember).MemberIndex;

        var modB = new OrdinalHost { A = 1, B = 20, C = 3 };
        var docB = OrdinalHostDeepOps.ComputeDelta(baseObj, modB);
        var idxB = docB.Operations.Single(o => o.Kind == DeltaKind.SetMember).MemberIndex;

        var modC = new OrdinalHost { A = 1, B = 2, C = 30 };
        var docC = OrdinalHostDeepOps.ComputeDelta(baseObj, modC);
        var idxC = docC.Operations.Single(o => o.Kind == DeltaKind.SetMember).MemberIndex;

        Assert.True(idxA < idxB && idxB < idxC);
    }

    [Fact]
    public void Stable_Index_Is_Not_Ordinal_Declaration_Order()
    {
        var baseStable = new StableHost { A = 1, B = 2, C = 3 };
        var baseOrd = new OrdinalHost { A = 1, B = 2, C = 3 };

        var modStable = new StableHost { A = 1, B = 200, C = 3 };
        var docStable = StableHostDeepOps.ComputeDelta(baseStable, modStable);
        var idxStableB = docStable.Operations.Single(o => o.Kind == DeltaKind.SetMember).MemberIndex;

        var modOrd = new OrdinalHost { A = 1, B = 200, C = 3 };
        var docOrd = OrdinalHostDeepOps.ComputeDelta(baseOrd, modOrd);
        var idxOrdB = docOrd.Operations.Single(o => o.Kind == DeltaKind.SetMember).MemberIndex;

        Assert.NotEqual(idxOrdB, idxStableB);
    }

    [Fact]
    public void Auto_Mode_Behaves_As_Stable_When_Delta_Enabled()
    {
        var baseStable = new StableHost { A = 1, B = 2, C = 3 };

        var modStable1 = new StableHost { A = 1, B = 200, C = 3 };
        var doc1 = StableHostDeepOps.ComputeDelta(baseStable, modStable1);
        var idx1 = doc1.Operations.Single(o => o.Kind == DeltaKind.SetMember).MemberIndex;

        var modStable2 = new StableHost { A = 1, B = 201, C = 3 };
        var doc2 = StableHostDeepOps.ComputeDelta(baseStable, modStable2);
        var idx2 = doc2.Operations.Single(o => o.Kind == DeltaKind.SetMember).MemberIndex;

        Assert.Equal(idx1, idx2);
    }
}
