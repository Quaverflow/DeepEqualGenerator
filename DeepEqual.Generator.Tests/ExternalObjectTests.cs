using System.Drawing;
using DeepEqual.Generator.Shared;
using DeepEqual.Generator.Tests;

[assembly:
    ExternalDeepComparable(typeof(ListHolder1), GenerateDiff = true, GenerateDelta = true, CycleTracking = false)]
[assembly:
    ExternalDeepComparable(typeof(ArrayHolder1), GenerateDiff = true, GenerateDelta = true, CycleTracking = false)]
[assembly:
    ExternalDeepComparable(typeof(DictHolder1), GenerateDiff = true, GenerateDelta = true, CycleTracking = false)]
[assembly:
    ExternalDeepComparable(typeof(MixedHolder), GenerateDiff = true, GenerateDelta = true, CycleTracking = false)]
[assembly: ExternalDeepCompare(typeof(ListHolder1), "Points.X")]
[assembly: ExternalDeepCompare(typeof(ArrayHolder1), "Points.X")]
[assembly: ExternalDeepCompare(typeof(DictHolder1), "PointsById<Value>.Y")]
[assembly: ExternalDeepCompare(typeof(MixedHolder), "Routes<Value>.X")]

namespace DeepEqual.Generator.Tests;

public sealed class ListHolder1
{
    public List<Point> Points { get; set; } = [];
}

public sealed class ArrayHolder1
{
    public Point[] Points { get; set; } = [];
}

public sealed class DictHolder1
{
    public Dictionary<int, Point> PointsById { get; set; } = new();
}

public sealed class MixedHolder
{
    public Dictionary<int, List<Point>> Routes { get; set; } = new();
    public string Name { get; set; } = "";
}

public class ExternalAttributes_Collections_Tests
{
    [Fact]
    public void ListHolder1_Delta_RoundTrips()
    {
        var before = new ListHolder1 { Points = [new Point(1, 1), new Point(2, 2), new Point(3, 3)] };
        var after = new ListHolder1 { Points = [new Point(1, 1), new Point(9, 2), new Point(3, 3)] };

        var doc = ListHolder1DeepOps.ComputeDelta(before, after);
        Assert.False(doc.IsEmpty);

        ListHolder1DeepOps.ApplyDelta(ref before, doc);
        Assert.True(ListHolder1DeepEqual.AreDeepEqual(before, after));
    }

    [Fact]
    public void ArrayHolder1_Delta_RoundTrips()
    {
        var before = new ArrayHolder1 { Points = [new Point(1, 1), new Point(2, 2)] };
        var after = new ArrayHolder1 { Points = [new Point(1, 1), new Point(7, 2)] };

        var doc = ArrayHolder1DeepOps.ComputeDelta(before, after);
        Assert.False(doc.IsEmpty);

        ArrayHolder1DeepOps.ApplyDelta(ref before, doc);
        Assert.True(ArrayHolder1DeepEqual.AreDeepEqual(before, after));
    }

    [Fact]
    public void DictHolder1_Delta_RoundTrips_With_Value_Path()
    {
        var before = new DictHolder1
        {
            PointsById = new Dictionary<int, Point>
            {
                [1] = new(10, 10),
                [2] = new(20, 20)
            }
        };
        var after = new DictHolder1
        {
            PointsById = new Dictionary<int, Point>
            {
                [1] = new(10, 10),
                [2] = new(99, 20), [3] = new(30, 30)
            }
        };

        var doc = DictHolder1DeepOps.ComputeDelta(before, after);
        Assert.False(doc.IsEmpty);

        DictHolder1DeepOps.ApplyDelta(ref before, doc);
        Assert.True(DictHolder1DeepEqual.AreDeepEqual(before, after));
    }

    [Fact]
    public void MixedHolder_Nested_Dict_List_Element_Path_RoundTrips()
    {
        var before = new MixedHolder
        {
            Name = "alpha",
            Routes = new Dictionary<int, List<Point>>
            {
                [1] = [new Point(1, 1), new Point(2, 2)],
                [2] = [new Point(3, 3)]
            }
        };
        var after = new MixedHolder
        {
            Name = "alpha",
            Routes = new Dictionary<int, List<Point>>
            {
                [1] = [new Point(1, 1), new Point(9, 2)], [2] = [new Point(3, 3), new Point(4, 4)]
            }
        };

        var doc = MixedHolderDeepOps.ComputeDelta(before, after);
        Assert.False(doc.IsEmpty);

        MixedHolderDeepOps.ApplyDelta(ref before, doc);
        Assert.True(MixedHolderDeepEqual.AreDeepEqual(before, after));
    }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true, StableMemberIndex = StableMemberIndexMode.On)]
public sealed class IndexProbeV1
{
    public int A { get; set; }
    public string? B { get; set; }
    public List<int>? C { get; set; }
    public Dictionary<string, string>? D { get; set; }
}

// V2 = V1 + new member E (append-only expectation)
[DeepComparable(GenerateDiff = true, GenerateDelta = true, StableMemberIndex = StableMemberIndexMode.On)]
public sealed class IndexProbeV2
{
    public int A { get; set; }
    public string? B { get; set; }
    public List<int>? C { get; set; }
    public Dictionary<string, string>? D { get; set; }
    public string? E { get; set; }
}

public sealed class StableMemberIndexingTests
{
    private static int ProbeMemberIndex_A()
    {
        var a = new IndexProbeV1 { A = 1 };
        var b = new IndexProbeV1 { A = 2 };
        var doc = IndexProbeV1DeepOps.ComputeDelta(a, b);
        return doc.Operations.Single(o => o is { Kind: DeltaKind.SetMember, Value: int }).MemberIndex;
    }

    private static int ProbeMemberIndex_B()
    {
        var a = new IndexProbeV1 { B = "x" };
        var b = new IndexProbeV1 { B = "y" };
        var doc = IndexProbeV1DeepOps.ComputeDelta(a, b);
        return doc.Operations.Single(o => o is { Kind: DeltaKind.SetMember, Value: string }).MemberIndex;
    }

    private static int ProbeMemberIndex_C_SeqReplaceOrSet()
    {
        var a = new IndexProbeV1 { C = [1, 2, 3] };
        var b = new IndexProbeV1 { C = [1, 9, 3] };
        var doc = IndexProbeV1DeepOps.ComputeDelta(a, b);

        var seq = doc.Operations.FirstOrDefault(o => o.Kind == DeltaKind.SeqReplaceAt
                                                     || o.Kind == DeltaKind.SeqAddAt
                                                     || o.Kind == DeltaKind.SeqRemoveAt);
        if (seq.Kind is DeltaKind.SeqReplaceAt or DeltaKind.SeqAddAt or DeltaKind.SeqRemoveAt) return seq.MemberIndex;

        return doc.Operations.Single(o => o is { Kind: DeltaKind.SetMember, Value: List<int> }).MemberIndex;
    }

    private static int ProbeMemberIndex_D_DictSet()
    {
        var a = new IndexProbeV1 { D = new Dictionary<string, string> { ["k"] = "1" } };
        var b = new IndexProbeV1 { D = new Dictionary<string, string> { ["k"] = "2" } };
        var doc = IndexProbeV1DeepOps.ComputeDelta(a, b);

        var dict = doc.Operations.FirstOrDefault(o =>
            o.Kind == DeltaKind.DictSet || o.Kind == DeltaKind.DictRemove || o.Kind == DeltaKind.DictNested);
        if (dict.Kind is DeltaKind.DictSet or DeltaKind.DictRemove or DeltaKind.DictNested) return dict.MemberIndex;

        return doc.Operations.Single(o => o is { Kind: DeltaKind.SetMember, Value: Dictionary<string, string> })
            .MemberIndex;
    }

    private static int ProbeMemberIndex_D_DictAddRemoveShape()
    {
        var a = new IndexProbeV1 { D = new Dictionary<string, string> { ["x"] = "1" } };
        var b = new IndexProbeV1 { D = new Dictionary<string, string> { ["y"] = "2" } };
        var doc = IndexProbeV1DeepOps.ComputeDelta(a, b);
        var dict = doc.Operations.First(o => o.Kind is DeltaKind.DictSet or DeltaKind.DictRemove);
        return dict.MemberIndex;
    }

    private static (int A, int B, int C, int D) ProbeAllV1()
    {
        return (ProbeMemberIndex_A(), ProbeMemberIndex_B(), ProbeMemberIndex_C_SeqReplaceOrSet(),
            ProbeMemberIndex_D_DictSet());
    }

    private static int ProbeV2_Index_A()
    {
        var a = new IndexProbeV2 { A = 1 };
        var b = new IndexProbeV2 { A = 2 };
        var doc = IndexProbeV2DeepOps.ComputeDelta(a, b);
        return doc.Operations.Single(o => o is { Kind: DeltaKind.SetMember, Value: int }).MemberIndex;
    }

    private static int ProbeV2_Index_B()
    {
        var a = new IndexProbeV2 { B = "x" };
        var b = new IndexProbeV2 { B = "y" };
        var doc = IndexProbeV2DeepOps.ComputeDelta(a, b);
        return doc.Operations.Single(o => o is { Kind: DeltaKind.SetMember, Value: string }).MemberIndex;
    }

    private static int ProbeV2_Index_E()
    {
        var a = new IndexProbeV2 { E = "old" };
        var b = new IndexProbeV2 { E = "new" };
        var doc = IndexProbeV2DeepOps.ComputeDelta(a, b);
        return doc.Operations.Single(o => o is { Kind: DeltaKind.SetMember, Value: string }).MemberIndex;
    }

    [Fact]
    public void StableIndices_AreDeterministic_WithinType()
    {
        var (a1, b1, c1, d1) = ProbeAllV1();
        var (a2, b2, c2, d2) = ProbeAllV1();

        Assert.Equal(a1, a2);
        Assert.Equal(b1, b2);
        Assert.Equal(c1, c2);
        Assert.Equal(d1, d2);

        var set = new[] { a1, b1, c1, d1 }.ToHashSet();
        Assert.Equal(4, set.Count);
    }

    [Fact]
    public void StableIndices_SequenceOrSetMember_ShareSameMemberIndex()
    {
        var d_valueChange = ProbeMemberIndex_D_DictSet();
        var d_addRemoveShape = ProbeMemberIndex_D_DictAddRemoveShape();

        Assert.Equal(d_valueChange, d_addRemoveShape);
    }

    [Fact]
    public void AppendOnly_NewMember_Comes_Last()
    {
        var (aV1, bV1, cV1, dV1) = ProbeAllV1();

        var aV2 = ProbeV2_Index_A();
        var bV2 = ProbeV2_Index_B();
        var eV2 = ProbeV2_Index_E();

        Assert.Equal(aV1, aV2);
        Assert.Equal(bV1, bV2);

        var maxV1 = new[] { aV1, bV1, cV1, dV1 }.Max();
        Assert.True(eV2 > maxV1, $"Expected E index ({eV2}) to be greater than max v1 index ({maxV1}).");
    }

    [Fact]
    public void ApplyDelta_StillFunctional_WithStableIndices()
    {
        var left = new IndexProbeV1
        {
            A = 1,
            B = "x",
            C = [1, 2, 3],
            D = new Dictionary<string, string> { ["k"] = "v1" }
        };
        var right = new IndexProbeV1
        {
            A = 2,
            B = "y",
            C = [1, 9, 3],
            D = new Dictionary<string, string> { ["k"] = "v2" }
        };

        var doc = IndexProbeV1DeepOps.ComputeDelta(left, right);

        var target = new IndexProbeV1
        {
            A = 1,
            B = "x",
            C = [1, 2, 3],
            D = new Dictionary<string, string> { ["k"] = "v1" }
        };
        IndexProbeV1DeepOps.ApplyDelta(ref target, doc);

        Assert.True(IndexProbeV1DeepEqual.AreDeepEqual(right, target));
    }
}