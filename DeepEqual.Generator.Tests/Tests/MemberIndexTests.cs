
using System.Linq;
using DeepEqual.Generator.Shared;
using Xunit;

namespace DeepEqual.Generator.Tests.Tests;

[DeepComparable(GenerateDiff = true, GenerateDelta = true, StableMemberIndex = StableMemberIndexMode.Off)]
public sealed class OrdinalHost
{
    public int A { get; set; }
    public int B { get; set; }
    public int C { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)] // Auto => stable when delta enabled
public sealed class StableHost
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

        var docA = new DeltaDocument(); var wA = new DeltaWriter(docA);
        var modA = new OrdinalHost { A = 10, B = 2, C = 3 };
        OrdinalHostDeepOps.ComputeDelta(baseObj, modA, ref wA);
        var idxA = docA.Operations.Single(o => o.Kind == DeltaKind.SetMember).MemberIndex;

        var docB = new DeltaDocument(); var wB = new DeltaWriter(docB);
        var modB = new OrdinalHost { A = 1, B = 20, C = 3 };
        OrdinalHostDeepOps.ComputeDelta(baseObj, modB, ref wB);
        var idxB = docB.Operations.Single(o => o.Kind == DeltaKind.SetMember).MemberIndex;

        var docC = new DeltaDocument(); var wC = new DeltaWriter(docC);
        var modC = new OrdinalHost { A = 1, B = 2, C = 30 };
        OrdinalHostDeepOps.ComputeDelta(baseObj, modC, ref wC);
        var idxC = docC.Operations.Single(o => o.Kind == DeltaKind.SetMember).MemberIndex;

        Assert.True(idxA < idxB && idxB < idxC);
    }

    [Fact]
    public void Stable_Index_Is_Not_Ordinal_Declaration_Order()
    {
        var baseStable = new StableHost { A = 1, B = 2, C = 3 };
        var baseOrd = new OrdinalHost { A = 1, B = 2, C = 3 };

        var docStable = new DeltaDocument(); var wS = new DeltaWriter(docStable);
        var modStable = new StableHost { A = 1, B = 200, C = 3 };
        StableHostDeepOps.ComputeDelta(baseStable, modStable, ref wS);
        var idxStableB = docStable.Operations.Single(o => o.Kind == DeltaKind.SetMember).MemberIndex;

        var docOrd = new DeltaDocument(); var wO = new DeltaWriter(docOrd);
        var modOrd = new OrdinalHost { A = 1, B = 200, C = 3 };
        OrdinalHostDeepOps.ComputeDelta(baseOrd, modOrd, ref wO);
        var idxOrdB = docOrd.Operations.Single(o => o.Kind == DeltaKind.SetMember).MemberIndex;

        Assert.NotEqual(idxOrdB, idxStableB);
    }

    [Fact]
    public void Auto_Mode_Behaves_As_Stable_When_Delta_Enabled()
    {
        var baseStable = new StableHost { A = 1, B = 2, C = 3 };
        var doc1 = new DeltaDocument(); var w1 = new DeltaWriter(doc1);
        var modStable = new StableHost { A = 1, B = 200, C = 3 };
        StableHostDeepOps.ComputeDelta(baseStable, modStable, ref w1);
        var idx1 = doc1.Operations.Single(o => o.Kind == DeltaKind.SetMember).MemberIndex;

        var doc2 = new DeltaDocument(); var w2 = new DeltaWriter(doc2);
        var modStable2 = new StableHost { A = 1, B = 201, C = 3 };
        StableHostDeepOps.ComputeDelta(baseStable, modStable2, ref w2);
        var idx2 = doc2.Operations.Single(o => o.Kind == DeltaKind.SetMember).MemberIndex;

        Assert.Equal(idx1, idx2);
    }
}