using DeepEqual.Generator.Shared;
using DeepEqual.Generator.Tests.Models;

namespace DeepEqual.Generator.Tests.Tests;

public class ExtraEdgeTests
{
    [Fact]
    public void Double_NaN_And_SignedZero_Options()
    {
        var a = new DoubleWeird { D = double.NaN };
        var b = new DoubleWeird { D = double.NaN };
        var allow = new ComparisonOptions { TreatNaNEqual = true };
        var deny = new ComparisonOptions { TreatNaNEqual = false };
        Assert.True(DoubleWeirdDeepEqual.AreDeepEqual(a, b, allow));
        Assert.False(DoubleWeirdDeepEqual.AreDeepEqual(a, b, deny));

        var z1 = new DoubleWeird { D = +0.0 };
        var z2 = new DoubleWeird { D = -0.0 };
        var strict = new ComparisonOptions { DoubleEpsilon = 0.0 };
        Assert.True(DoubleWeirdDeepEqual.AreDeepEqual(z1, z2, strict));
    }

    [Fact]
    public void Float_Uses_Single_Epsilon()
    {
        var a = new FloatHolder { F = 1.000001f };
        var b = new FloatHolder { F = 1.000002f };
        var loose = new ComparisonOptions { FloatEpsilon = 0.00001f };
        var strict = new ComparisonOptions { FloatEpsilon = 0f };
        Assert.True(FloatHolderDeepEqual.AreDeepEqual(a, b, loose));
        Assert.False(FloatHolderDeepEqual.AreDeepEqual(a, b, strict));
    }

    [Fact]
    public void String_Does_Not_Normalize_Combining_Marks()
    {
        var nfc = new StringNfcNfd { S = "é" };
        var nfd = new StringNfcNfd { S = "e\u0301" };
        var opts = new ComparisonOptions { StringComparison = StringComparison.Ordinal };
        Assert.False(StringNfcNfdDeepEqual.AreDeepEqual(nfc, nfd, opts));
    }

    [Fact]
    public void Collections_With_Nulls_Are_Handled()
    {
        var a = new ObjList { Items = [1, null, new[] { "x" }] };
        var b = new ObjList { Items = [1, null, new[] { "x" }] };
        var c = new ObjList { Items = [1, null, new[] { "y" }] };
        Assert.True(ObjListDeepEqual.AreDeepEqual(a, b));
        Assert.False(ObjListDeepEqual.AreDeepEqual(a, c));
    }

    [Fact]
    public void Polymorphism_Inside_Collections()
    {
        var a = new ZooList { Animals = [new Cat { Age = 2, Name = "Paws" }, new Cat { Age = 5, Name = "Mews" }] };
        var b = new ZooList { Animals = [new Cat { Age = 2, Name = "Paws" }, new Cat { Age = 5, Name = "Mews" }] };
        var c = new ZooList { Animals = [new Cat { Age = 2, Name = "Paws" }, new Cat { Age = 5, Name = "Mewz" }] };
        Assert.True(ZooListDeepEqual.AreDeepEqual(a, b));
        Assert.False(ZooListDeepEqual.AreDeepEqual(a, c));
    }

    [DeepComparable] public sealed class BucketItem { public string K { get; init; } = ""; public int V { get; init; } }
    [DeepComparable]
    public sealed class Bucketed
    {
        [DeepCompare(OrderInsensitive = true, KeyMembers = [nameof(BucketItem.K)])]
        public List<BucketItem> Items { get; init; } = [];
    }

    [Fact]
    public void Keyed_Unordered_SameCounts_But_DeepValue_Diff_Is_False()
    {
        var a = new Bucketed { Items = [new() { K = "a", V = 1 }, new() { K = "a", V = 2 }] };
        var b = new Bucketed { Items = [new() { K = "a", V = 2 }, new() { K = "a", V = 1 }] };
        var c = new Bucketed { Items = [new() { K = "a", V = 1 }, new() { K = "a", V = 99 }] };
        Assert.True(BucketedDeepEqual.AreDeepEqual(a, b));
        Assert.False(BucketedDeepEqual.AreDeepEqual(a, c));
    }

    [DeepComparable] public sealed class DictShapeA { public Dictionary<string, int> Map { get; init; } = new(); }
    public sealed class CustomDict : Dictionary<string, int> { }     [DeepComparable] public sealed class DictShapeB { public CustomDict Map { get; init; } = new(); }

    [Fact]
    public void Dictionary_Fallback_Mixed_Shapes_Work()
    {
        var a = new DictShapeA { Map = new() { ["x"] = 1, ["y"] = 2 } };
        var b = new DictShapeB { Map = new CustomDict { ["y"] = 2, ["x"] = 1 } };
        Assert.True(DictShapeADeepEqual.AreDeepEqual(a, new DictShapeA { Map = new() { ["x"] = 1, ["y"] = 2 } }));
        Assert.True(DictShapeBDeepEqual.AreDeepEqual(b, new DictShapeB { Map = new CustomDict { ["x"] = 1, ["y"] = 2 } }));
    }

    [Fact]
    public void Symmetry_And_Repeatability()
    {
        var a = new ObjList { Items = ["a", "b"] };
        var b = new ObjList { Items = ["a", "b"] };
        Assert.True(ObjListDeepEqual.AreDeepEqual(a, b));
        Assert.True(ObjListDeepEqual.AreDeepEqual(b, a));
        Assert.True(ObjListDeepEqual.AreDeepEqual(a, b));     }
}