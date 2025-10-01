#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using DeepEqual;
using DeepEqual.Generator.Shared;
using Newtonsoft.Json;
using Xunit;

namespace DeepEqual.RewrittenTests;

public sealed class DirtyTrackTests
{
    private static DirtyTrackedModel MakeBaseline()
    {
        var child = new DirtyTrackedChild();
        child.Initialize(5);

        var model = new DirtyTrackedModel();
        model.Initialize(
            a: 10,
            name: "baseline",
            numbers: new[] { 1, 2, 3 },
            map: new Dictionary<string, int> { ["alpha"] = 1, ["beta"] = 2 },
            child: child);

        return model;
    }

    private static DeltaDocument Delta(DirtyTrackedModel left, DirtyTrackedModel right, ComparisonContext? ctx = null)
        => left.ComputeDeepDelta(right, ctx);

    private static DeltaDocument DeltaNullable(DirtyTrackedModel? left, DirtyTrackedModel? right, ComparisonContext? ctx = null)
        => left.ComputeDeepDelta(right, ctx);

    private static DeltaOp[] RootOps(DeltaDocument doc)
        => new DeltaReader(doc).AsSpan().ToArray();

    private static IEnumerable<DeltaOp> EnumerateDeep(DeltaDocument doc)
    {
        var stack = new Stack<DeltaDocument>();
        stack.Push(doc);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            var ops = new DeltaReader(current).AsSpan().ToArray();
            foreach (var op in ops)
            {
                yield return op;
                if (op.Nested is not null)
                    stack.Push(op.Nested);
            }
        }
    }

    [Fact]
    public void FastPath_PrimitiveChange_EmitsSingleSet()
    {
        var left = MakeBaseline();
        var right = left.Clone();
        right.A += 5;

        var doc = Delta(left, right);
        var ops = RootOps(doc);

        Assert.Collection(ops, op => Assert.Equal(DeltaKind.SetMember, op.Kind));
        Assert.False(right.__HasAnyDirty());

        var applied = left.Clone().ApplyDeepDelta(doc)!;
        Assert.True(applied.AreDeepEqual(right));
    }

    [Fact]
    public void FastPath_ListMutation_ProducesSeqOps()
    {
        var left = MakeBaseline();
        var right = left.Clone();
        right.ReplaceNumber(0, 99);
        right.AddNumber(123);

        var doc = Delta(left, right);
        var seqOps = EnumerateDeep(doc)
            .Where(op => op.Kind is DeltaKind.SeqAddAt or DeltaKind.SeqReplaceAt)
            .ToList();

        Assert.NotEmpty(seqOps);
        Assert.False(right.__HasAnyDirty());

        var applied = left.Clone().ApplyDeepDelta(doc)!;
        Assert.True(applied.AreDeepEqual(right));
    }

    [Fact]
    public void FastPath_DictionaryMutation_ProducesDictOps()
    {
        var left = MakeBaseline();
        var right = left.Clone();
        right.SetMap("gamma", 7);
        right.RemoveMap("alpha");

        var doc = Delta(left, right);
        var dictOps = EnumerateDeep(doc)
            .Where(op => op.Kind is DeltaKind.DictSet or DeltaKind.DictRemove)
            .ToList();

        Assert.Equal(2, dictOps.Count);
        Assert.False(right.__HasAnyDirty());

        var applied = left.Clone().ApplyDeepDelta(doc)!;
        Assert.True(applied.AreDeepEqual(right));
    }

    [Fact]
    public void FastPath_NestedChild_ProducesNestedMember()
    {
        var left = MakeBaseline();
        var right = left.Clone();
        right.Child.Score += 10;
        right.MarkChildDirty();

        var doc = Delta(left, right);
        var nested = RootOps(doc).Single(op => op.Kind == DeltaKind.NestedMember);
        Assert.NotNull(nested.Nested);
        Assert.Contains(EnumerateDeep(nested.Nested!), op => op.Kind == DeltaKind.SetMember);
        Assert.False(right.__HasAnyDirty());

        var applied = left.Clone().ApplyDeepDelta(doc)!;
        Assert.True(applied.AreDeepEqual(right));
    }

    [Fact]
    public void ValidateDirty_SuppressesFalsePositive()
    {
        var left = MakeBaseline();
        var right = left.Clone();
        right.ForceDirtyA();

        var fastDoc = Delta(left, right);
        Assert.Single(RootOps(fastDoc));

        var ctx = new ComparisonContext(new ComparisonOptions { ValidateDirtyOnEmit = true });
        var validated = Delta(left, right, ctx);
        Assert.Empty(RootOps(validated));
        Assert.False(right.__HasAnyDirty());
    }

    [Fact]
    public void ApplyDelta_ClearsDirtyBits_OnTarget()
    {
        var baseline = MakeBaseline();
        var mutated = baseline.Clone();
        mutated.A += 3;
        mutated.SetMap("delta", 99);
        mutated.Child.Score += 1;
        mutated.MarkChildDirty();

        var doc = Delta(baseline, mutated);

        var target = baseline.Clone();
        target.ForceDirtyA();
        target.MarkChildDirty();
        Assert.True(target.__HasAnyDirty());

        target = target.ApplyDeepDelta(doc)!;

        Assert.False(target.__HasAnyDirty());
        Assert.True(target.AreDeepEqual(mutated));
    }

    [Fact]
    public void Initialization_SuppressesDirtyMarks()
    {
        var model = new DirtyTrackedModel();
        model.Initialize(
            a: 42,
            name: "init",
            numbers: new[] { 1, 2, 3 },
            map: new Dictionary<string, int> { ["alpha"] = 1 },
            child: new DirtyTrackedChild());

        Assert.False(model.__HasAnyDirty());
        Assert.False(model.__TryPopNextDirty(out _));
    }

    [Fact]
    public void DirtyFlags_DeduplicateRepeatedMarks()
    {
        var baseline = MakeBaseline();
        var right = baseline.Clone();
        right.ForceDirtyA();
        right.ForceDirtyA();

        var bits = new List<int>();
        while (right.__TryPopNextDirty(out var bit))
            bits.Add(bit);

        Assert.Equal(new[] { DirtyTrackedModel.__Bit_A }, bits);
        Assert.False(right.__HasAnyDirty());
    }

    [Fact]
    public void DirtyFlags_PopInAscendingOrder()
    {
        var mutated = MakeBaseline().Clone();
        mutated.ForceDirtyA();
        mutated.Name = "order-check";
        mutated.AddNumber(999);
        mutated.MarkChildDirty();

        var popped = new List<int>();
        while (mutated.__TryPopNextDirty(out var bit))
            popped.Add(bit);

        var expected = new[]
        {
            DirtyTrackedModel.__Bit_A,
            DirtyTrackedModel.__Bit_Name,
            DirtyTrackedModel.__Bit_Numbers,
            DirtyTrackedModel.__Bit_Child
        }.OrderBy(x => x).ToArray();

        Assert.Equal(expected, popped.ToArray());
        Assert.False(mutated.__HasAnyDirty());
    }

    [Fact]
    public void DirtyTrack_MixedMutations_EmitsOnlyMutatedMembers()
    {
        var left = MakeBaseline();
        var right = left.Clone();
        right.A += 7;
        right.Name = "mutated";
        right.ReplaceNumber(0, 77);
        right.AddNumber(500);
        right.SetMap("gamma", 9);
        right.RemoveMap("alpha");
        right.Child.Score += 3;
        right.MarkChildDirty();

        var doc = Delta(left, right);
        var rootOps = RootOps(doc);
        var expectedMembers = new HashSet<int>
        {
            DirtyTrackedModel.__Bit_A,
            DirtyTrackedModel.__Bit_Name,
            DirtyTrackedModel.__Bit_Numbers,
            DirtyTrackedModel.__Bit_Map,
            DirtyTrackedModel.__Bit_Child
        };

        Assert.NotEmpty(rootOps);
        Assert.All(rootOps, op => Assert.Contains(op.MemberIndex, expectedMembers));
        foreach (var member in expectedMembers)
            Assert.Contains(rootOps, op => op.MemberIndex == member);

        Assert.False(right.__HasAnyDirty());
        var applied = left.Clone().ApplyDeepDelta(doc)!;
        Assert.True(applied.AreDeepEqual(right));
    }

    [Fact]
    public void ValidateDirtyOnEmit_StillProducesDelta()
    {
        var left = MakeBaseline();
        var right = left.Clone();
        right.Name = "validated";
        right.ReplaceNumber(1, 404);
        right.Child.Score += 11;

        var ctx = new ComparisonContext(new ComparisonOptions { ValidateDirtyOnEmit = true });
        var doc = Delta(left, right, ctx);

        var rootOps = RootOps(doc);
        Assert.Contains(rootOps, op => op.MemberIndex == DirtyTrackedModel.__Bit_Name);

        var seqOps = EnumerateDeep(doc)
            .Where(op => op.MemberIndex == DirtyTrackedModel.__Bit_Numbers && op.Kind is DeltaKind.SeqReplaceAt)
            .ToList();
        Assert.NotEmpty(seqOps);

        var nestedChild = EnumerateDeep(doc)
            .FirstOrDefault(op => op.MemberIndex == DirtyTrackedChild.__Bit_Score && op.Kind == DeltaKind.SetMember);
        Assert.NotNull(nestedChild);

        Assert.False(right.__HasAnyDirty());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ApplyDeepDelta_IdempotentAcrossShapes(bool validate)
    {
        var baseline = MakeBaseline();
        var mutated = baseline.Clone();
        mutated.A += 4;
        mutated.Name = "idempotent";
        mutated.ReplaceNumber(1, 200);
        mutated.AddNumber(77);
        mutated.SetMap("beta", 200);
        mutated.SetMap("omega", 500);
        mutated.RemoveMap("alpha");
        mutated.Child.Score += 3;
        mutated.MarkChildDirty();

        ComparisonContext? ctx = validate ? new ComparisonContext(new ComparisonOptions { ValidateDirtyOnEmit = true }) : null;
        var delta = Delta(baseline, mutated, ctx);

        var rootOps = RootOps(delta);
        Assert.Contains(rootOps, op => op.MemberIndex == DirtyTrackedModel.__Bit_A && op.Kind == DeltaKind.SetMember);
        Assert.Contains(rootOps, op => op.MemberIndex == DirtyTrackedModel.__Bit_Name && op.Kind == DeltaKind.SetMember);
        Assert.Contains(rootOps, op => op.MemberIndex == DirtyTrackedModel.__Bit_Numbers);
        Assert.Contains(rootOps, op => op.MemberIndex == DirtyTrackedModel.__Bit_Map);
        Assert.Contains(rootOps, op => op.MemberIndex == DirtyTrackedModel.__Bit_Child && op.Kind == DeltaKind.NestedMember);

        var seqOps = EnumerateDeep(delta)
            .Where(op => op.Kind is DeltaKind.SeqAddAt or DeltaKind.SeqReplaceAt)
            .ToList();
        Assert.Contains(seqOps, op => op.Kind == DeltaKind.SeqReplaceAt);
        Assert.Contains(seqOps, op => op.Kind == DeltaKind.SeqAddAt);

        var dictOps = EnumerateDeep(delta)
            .Where(op => op.Kind is DeltaKind.DictSet or DeltaKind.DictRemove)
            .ToList();
        Assert.NotEmpty(dictOps);

        var target = baseline.Clone();
        target = target.ApplyDeepDelta(delta)!;
        Assert.True(target.AreDeepEqual(mutated));
        Assert.False(target.__HasAnyDirty());

        target = target.ApplyDeepDelta(delta)!;
        Assert.True(target.AreDeepEqual(mutated),"mutated: " + JsonConvert.SerializeObject(mutated) + " baseline " + JsonConvert.SerializeObject(target));
        Assert.False(target.__HasAnyDirty());
        Assert.Equal(mutated.Numbers, target.Numbers);
        Assert.Equal(mutated.Map, target.Map);

        var second = baseline.Clone();
        second = second.ApplyDeepDelta(delta)!;
        Assert.True(second.AreDeepEqual(mutated));
        Assert.False(second.__HasAnyDirty());

        mutated = mutated.ApplyDeepDelta(delta)!;
        Assert.True(mutated.AreDeepEqual(target));
        Assert.False(mutated.__HasAnyDirty());
    }

    public static IEnumerable<object[]> DirtyBitShapeCases()
    {
        yield return new object[]
        {
            (Action<DirtyTrackedModel>)(model => model.A += 1),
            (Action<DeltaDocument>)(doc =>
            {
                var root = RootOps(doc);
                var op = Assert.Single(root);
                Assert.Equal(DirtyTrackedModel.__Bit_A, op.MemberIndex);
                Assert.Equal(DeltaKind.SetMember, op.Kind);
            })
        };

        yield return new object[]
        {
            (Action<DirtyTrackedModel>)(model => model.Name = "shape-name"),
            (Action<DeltaDocument>)(doc =>
            {
                var root = RootOps(doc);
                var op = Assert.Single(root);
                Assert.Equal(DirtyTrackedModel.__Bit_Name, op.MemberIndex);
                Assert.Equal(DeltaKind.SetMember, op.Kind);
            })
        };

        yield return new object[]
        {
            (Action<DirtyTrackedModel>)(model =>
            {
                model.ReplaceNumber(0, 44);
                model.AddNumber(99);
            }),
            (Action<DeltaDocument>)(doc =>
            {
                var root = RootOps(doc);
                Assert.Contains(root, op => op.MemberIndex == DirtyTrackedModel.__Bit_Numbers);
                var seqOps = EnumerateDeep(doc)
                    .Where(op => op.Kind is DeltaKind.SeqAddAt or DeltaKind.SeqReplaceAt)
                    .ToList();
                Assert.Contains(seqOps, op => op.Kind == DeltaKind.SeqReplaceAt);
                Assert.Contains(seqOps, op => op.Kind == DeltaKind.SeqAddAt);
            })
        };

        yield return new object[]
        {
            (Action<DirtyTrackedModel>)(model =>
            {
                model.SetMap("theta", 60);
                model.RemoveMap("alpha");
            }),
            (Action<DeltaDocument>)(doc =>
            {
                Assert.Contains(RootOps(doc), op => op.MemberIndex == DirtyTrackedModel.__Bit_Map);
                var dictOps = EnumerateDeep(doc)
                    .Where(op => op.Kind is DeltaKind.DictSet or DeltaKind.DictRemove)
                    .ToList();
                Assert.Equal(2, dictOps.Count);
            })
        };

        yield return new object[]
        {
            (Action<DirtyTrackedModel>)(model =>
            {
                model.Child.Score += 5;
                model.MarkChildDirty();
            }),
            (Action<DeltaDocument>)(doc =>
            {
                var nested = Assert.Single(RootOps(doc), op => op.MemberIndex == DirtyTrackedModel.__Bit_Child);
                Assert.Equal(DeltaKind.NestedMember, nested.Kind);
                Assert.NotNull(nested.Nested);
                Assert.Contains(EnumerateDeep(nested.Nested!), op => op.MemberIndex == DirtyTrackedChild.__Bit_Score && op.Kind == DeltaKind.SetMember);
            })
        };
    }

    [Theory]
    [MemberData(nameof(DirtyBitShapeCases))]
    public void DirtyBitFastPath_EmitsAndClearsPerShape(Action<DirtyTrackedModel> mutate, Action<DeltaDocument> assertOps)
    {
        var left = MakeBaseline();
        var right = left.Clone();
        mutate(right);

        var delta = Delta(left, right);
        assertOps(delta);

        Assert.False(right.__HasAnyDirty());
        Assert.False(right.__TryPopNextDirty(out _));
    }

    [Fact]
    public void ValidateDirtyOnEmit_ClearsStalePrimitiveBit()
    {
        var left = MakeBaseline();
        var right = left.Clone();
        right.ForceDirtyA();

        var ctx = new ComparisonContext(new ComparisonOptions { ValidateDirtyOnEmit = true });
        var delta = Delta(left, right, ctx);

        Assert.True(delta.IsEmpty);
        Assert.False(right.__HasAnyDirty());
        Assert.False(right.__TryPopNextDirty(out _));
    }

    [Fact]
    public void ValidateDirtyOnEmit_ClearsStaleChildBit()
    {
        var left = MakeBaseline();
        var right = left.Clone();
        right.Child.Score += 5;
        right.Child.Score = left.Child.Score;
        right.MarkChildDirty();

        var ctx = new ComparisonContext(new ComparisonOptions { ValidateDirtyOnEmit = true });
        var delta = Delta(left, right, ctx);

        Assert.True(delta.IsEmpty);
        Assert.False(right.__HasAnyDirty());
        Assert.False(right.__TryPopNextDirty(out _));
        Assert.False(right.Child.__HasAnyDirty());
        Assert.False(right.Child.__TryPopNextDirty(out _));
    }

    [Fact]
    public void ValidateDirtyOnEmit_DetectsUnmarkedNestedChange()
    {
        var left = MakeBaseline();
        var right = left.Clone();
        right.Child.Score += 3;

        var ctx = new ComparisonContext(new ComparisonOptions { ValidateDirtyOnEmit = true });
        var delta = Delta(left, right, ctx);

        var nested = Assert.Single(RootOps(delta), op => op.MemberIndex == DirtyTrackedModel.__Bit_Child);
        Assert.NotNull(nested.Nested);
        Assert.Contains(EnumerateDeep(nested.Nested!), op => op.MemberIndex == DirtyTrackedChild.__Bit_Score && op.Kind == DeltaKind.SetMember);

        Assert.False(right.Child.__HasAnyDirty());
        Assert.False(right.Child.__TryPopNextDirty(out _));
    }

    [Fact]
    public void ValidateDirtyOnEmit_ClearsRevertedListChange()
    {
        var left = MakeBaseline();
        var right = left.Clone();
        right.ReplaceNumber(2, 444);
        right.ReplaceNumber(2, left.Numbers[2]);
        right.__MarkDirty(DirtyTrackedModel.__Bit_Numbers);

        var ctx = new ComparisonContext(new ComparisonOptions { ValidateDirtyOnEmit = true });
        var delta = Delta(left, right, ctx);

        Assert.True(delta.IsEmpty);
        Assert.False(right.__HasAnyDirty());
        Assert.False(right.__TryPopNextDirty(out _));
    }
    [Fact]
    public void ApplyDeepDelta_ListOperationsAcrossPositions()
    {
        var baseline = MakeBaseline();
        var stage1 = baseline.Clone();
        stage1.Numbers.Insert(0, -5);
        stage1.__MarkDirty(DirtyTrackedModel.__Bit_Numbers);

        var delta1 = Delta(baseline, stage1);
        var addsFront = EnumerateDeep(delta1)
            .Where(op => op.Kind == DeltaKind.SeqAddAt)
            .Select(op => op.Index)
            .ToList();
        Assert.Contains(0, addsFront);

        var target = baseline.Clone();
        target = target.ApplyDeepDelta(delta1)!;
        Assert.Equal(stage1.Numbers, target.Numbers);

        var stage2 = stage1.Clone();
        stage2.Numbers.Insert(stage2.Numbers.Count, 42);
        stage2.__MarkDirty(DirtyTrackedModel.__Bit_Numbers);

        var delta2 = Delta(stage1, stage2);
        var addsBack = EnumerateDeep(delta2)
            .Where(op => op.Kind == DeltaKind.SeqAddAt)
            .Select(op => op.Index)
            .ToList();
        Assert.Contains(stage1.Numbers.Count, addsBack);

        target = target.ApplyDeepDelta(delta2)!;
        Assert.Equal(stage2.Numbers, target.Numbers);

        var stage3 = stage2.Clone();
        stage3.Numbers.Insert(2, 77);
        stage3.__MarkDirty(DirtyTrackedModel.__Bit_Numbers);

        var delta3 = Delta(stage2, stage3);
        var addsMid = EnumerateDeep(delta3)
            .Where(op => op.Kind == DeltaKind.SeqAddAt)
            .Select(op => op.Index)
            .ToList();
        Assert.Contains(2, addsMid);

        target = target.ApplyDeepDelta(delta3)!;
        Assert.Equal(stage3.Numbers, target.Numbers);

        var stage4 = stage3.Clone();
        stage4.Numbers.RemoveAt(stage4.Numbers.Count - 1);
        stage4.__MarkDirty(DirtyTrackedModel.__Bit_Numbers);

        var delta4 = Delta(stage3, stage4);
        Assert.Contains(EnumerateDeep(delta4), op => op.Kind == DeltaKind.SeqRemoveAt);

        target = target.ApplyDeepDelta(delta4)!;
        Assert.Equal(stage4.Numbers, target.Numbers);
        Assert.False(target.__HasAnyDirty());

        target = target.ApplyDeepDelta(delta4)!;
        Assert.Equal(stage4.Numbers, target.Numbers);
        Assert.False(target.__HasAnyDirty());

        var stage5 = stage4.Clone();
        stage5.Numbers[1] = 555;
        stage5.__MarkDirty(DirtyTrackedModel.__Bit_Numbers);

        var delta5 = Delta(stage4, stage5);
        Assert.Contains(EnumerateDeep(delta5), op => op.Kind == DeltaKind.SeqReplaceAt && op.Index == 1);

        target = target.ApplyDeepDelta(delta5)!;
        Assert.Equal(stage5.Numbers, target.Numbers);

        target = target.ApplyDeepDelta(delta5)!;
        Assert.Equal(stage5.Numbers, target.Numbers);
        Assert.False(target.__HasAnyDirty());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ApplyDeepDelta_ListDuplicateInsertIdempotent(bool validate)
    {
        var baseline = MakeBaseline();
        var mutated = baseline.Clone();
        mutated.Numbers.Insert(1, baseline.Numbers[1]);
        mutated.__MarkDirty(DirtyTrackedModel.__Bit_Numbers);

        ComparisonContext? ctx = validate ? new ComparisonContext(new ComparisonOptions { ValidateDirtyOnEmit = true }) : null;
        var delta = Delta(baseline, mutated, ctx);

        var adds = EnumerateDeep(delta)
            .Where(op => op.Kind == DeltaKind.SeqAddAt)
            .ToList();
        Assert.True(adds.Any(op => op.Index == 1), JsonConvert.SerializeObject(adds));
        Assert.Single(adds.Where(op => op.Index == 1));

        var target = baseline.Clone();
        target = target.ApplyDeepDelta(delta)!;
        Assert.Equal(mutated.Numbers, target.Numbers);

        target = target.ApplyDeepDelta(delta)!;
        Assert.Equal(mutated.Numbers, target.Numbers);
        Assert.False(target.__HasAnyDirty());
    }

    [Fact]
    public void ApplyDeepDelta_ListReplaceSameValue_Idempotent()
    {
        var baseline = MakeBaseline();
        var target = baseline.Clone();

        // Simulate a delta that replaces an element with the same value it already holds.
        var seqDoc = new DeltaDocument();
        seqDoc.Ops.Add(new DeltaOp(
            DirtyTrackedModel.__Bit_Numbers,
            DeltaKind.SeqReplaceAt,
            index: 1,
            key: null,
            value: baseline.Numbers[1],
            nested: null));

        var delta = new DeltaDocument();
        delta.Ops.Add(new DeltaOp(
            DirtyTrackedModel.__Bit_Numbers,
            DeltaKind.NestedMember,
            index: 0,
            key: null,
            value: null,
            nested: seqDoc));

        target = target.ApplyDeepDelta(delta)!;
        Assert.True(target.AreDeepEqual(baseline));
        Assert.False(target.__HasAnyDirty());

        target = target.ApplyDeepDelta(delta)!;
        Assert.True(target.AreDeepEqual(baseline));
        Assert.False(target.__HasAnyDirty());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ApplyDeepDelta_DictionaryOperations_Idempotent(bool validate)
    {
        var baseline = MakeBaseline();
        var mutated = baseline.Clone();
        mutated.SetMap("alpha", 100);
        mutated.SetMap("gamma", 3);
        mutated.RemoveMap("beta");

        ComparisonContext? ctx = validate ? new ComparisonContext(new ComparisonOptions { ValidateDirtyOnEmit = true }) : null;
        var delta = Delta(baseline, mutated, ctx);

        var dictOps = EnumerateDeep(delta)
            .Where(op => op.Kind is DeltaKind.DictSet or DeltaKind.DictRemove)
            .ToList();
        Assert.Contains(dictOps, op => op.Kind == DeltaKind.DictSet && Equals(op.Key, "alpha"));
        Assert.Contains(dictOps, op => op.Kind == DeltaKind.DictSet && Equals(op.Key, "gamma"));
        Assert.Contains(dictOps, op => op.Kind == DeltaKind.DictRemove && Equals(op.Key, "beta"));
        Assert.Single(dictOps.Where(op => op.Kind == DeltaKind.DictSet && Equals(op.Key, "alpha")));

        var target = baseline.Clone();
        target = target.ApplyDeepDelta(delta)!;
        Assert.Equal(mutated.Map, target.Map);

        target = target.ApplyDeepDelta(delta)!;
        Assert.Equal(mutated.Map, target.Map);
        Assert.False(target.__HasAnyDirty());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ApplyDeepDelta_MixedMutationsOnPartialTarget(bool validate)
    {
        var baseline = MakeBaseline();
        var mutated = baseline.Clone();
        mutated.A += 12;
        mutated.Name = "mixed-partial";
        mutated.ReplaceNumber(0, 123);
        mutated.AddNumber(888);
        mutated.SetMap("beta", 222);
        mutated.SetMap("delta", 444);
        mutated.RemoveMap("alpha");
        mutated.Child.Score += 9;
        mutated.MarkChildDirty();

        ComparisonContext? ctx = validate ? new ComparisonContext(new ComparisonOptions { ValidateDirtyOnEmit = true }) : null;
        var delta = Delta(baseline, mutated, ctx);

        var rootOps = RootOps(delta);
        Assert.Contains(rootOps, op => op.MemberIndex == DirtyTrackedModel.__Bit_A);
        Assert.Contains(rootOps, op => op.MemberIndex == DirtyTrackedModel.__Bit_Name);
        Assert.Contains(rootOps, op => op.MemberIndex == DirtyTrackedModel.__Bit_Numbers);
        Assert.Contains(rootOps, op => op.MemberIndex == DirtyTrackedModel.__Bit_Map);
        Assert.Contains(rootOps, op => op.MemberIndex == DirtyTrackedModel.__Bit_Child);

        var partial = baseline.Clone();
        partial.A = mutated.A;
        partial.Name = mutated.Name;

        partial = partial.ApplyDeepDelta(delta)!;
        Assert.True(partial.AreDeepEqual(mutated));
        Assert.False(partial.__HasAnyDirty());

        partial.A = baseline.A;
        partial.Name = baseline.Name;
        partial.Numbers[0] = baseline.Numbers[0];
        partial.Numbers.RemoveAt(partial.Numbers.Count - 1);
        partial.__MarkDirty(DirtyTrackedModel.__Bit_Numbers);
        partial.Map["beta"] = baseline.Map["beta"];
        partial.Map["alpha"] = baseline.Map["alpha"];
        partial.Map.Remove("delta");
        partial.__MarkDirty(DirtyTrackedModel.__Bit_Map);
        partial.Child.Score = baseline.Child.Score;
        partial.MarkChildDirty();

        partial = partial.ApplyDeepDelta(delta)!;
        Assert.True(partial.AreDeepEqual(mutated));
        Assert.False(partial.__HasAnyDirty());
    }
    [Fact]
    public void InitializeAndClone_DoNotLeakDirtyBits()
    {
        var model = new DirtyTrackedModel();
        model.Initialize(
            a: 0,
            name: "init",
            numbers: Array.Empty<int>(),
            map: null,
            child: null);

        Assert.False(model.__HasAnyDirty());
        Assert.False(model.__TryPopNextDirty(out _));

        model.AddNumber(5);
        model.SetMap("x", 1);
        var clone = model.Clone();
        Assert.False(clone.__HasAnyDirty());
        Assert.False(clone.__TryPopNextDirty(out _));
        Assert.False(clone.Child.__HasAnyDirty());

        var readOnlyNumbers = Array.AsReadOnly(new[] { 4, 5 });
        var readOnlyMap = new Dictionary<string, int> { ["r"] = 9 };
        clone.Initialize(9, "reset", readOnlyNumbers, readOnlyMap, new DirtyTrackedChild());
        Assert.False(clone.__HasAnyDirty());
        Assert.False(clone.__TryPopNextDirty(out _));
        Assert.False(clone.Child.__HasAnyDirty());
    }

    [Fact]
    public void ComputeDeepDelta_IdenticalInstances_Empty()
    {
        var left = MakeBaseline();
        var right = left.Clone();

        var delta = Delta(left, right);
        Assert.True(delta.IsEmpty);

        var applied = left.Clone().ApplyDeepDelta(delta)!;
        Assert.True(applied.AreDeepEqual(right));
        Assert.False(applied.__HasAnyDirty());
    }

    [Fact]
    public void ComputeDeepDelta_NullToNull_EmptyDocument()
    {
        DirtyTrackedModel? left = null;
        DirtyTrackedModel? right = null;

        var delta = DeltaNullable(left, right);
        Assert.True(delta.IsEmpty);

        left = left.ApplyDeepDelta(delta);
        Assert.Null(left);
    }

    [Fact]
    public void DirtyTrackedWide_NoDirtyBits_DeltaEmpty()
    {
        var left = new DirtyTrackedWide();
        var right = new DirtyTrackedWide();

        var delta = left.ComputeDeepDelta(right);
        Assert.True(delta.IsEmpty);

        var applied = left.ApplyDeepDelta(delta);
        Assert.NotNull(applied);
        Assert.False(applied!.__HasAnyDirty());
    }

    [Fact]
    public void ApplyDeepDelta_SequentialDeltasMaintainState()
    {
        var baseline = MakeBaseline();
        var step1 = baseline.Clone();
        step1.Name = "stage-1";
        step1.AddNumber(99);
        step1.SetMap("gamma", 5);
        step1.Child.Score += 1;
        step1.MarkChildDirty();

        var step2 = step1.Clone();
        step2.A += 10;
        step2.ReplaceNumber(0, 404);
        step2.RemoveMap("beta");
        step2.Child.Score += 2;
        step2.MarkChildDirty();

        var delta1 = Delta(baseline, step1);
        var delta2 = Delta(step1, step2);

        var target = baseline.Clone();
        target = target.ApplyDeepDelta(delta1)!;
        Assert.True(target.AreDeepEqual(step1));
        Assert.False(target.__HasAnyDirty());

        target = target.ApplyDeepDelta(delta2)!;
        Assert.True(target.AreDeepEqual(step2));
        Assert.False(target.__HasAnyDirty());

        var fresh = baseline.Clone();
        fresh = fresh.ApplyDeepDelta(delta1)!;
        fresh = fresh.ApplyDeepDelta(delta2)!;
        Assert.True(fresh.AreDeepEqual(step2));
        Assert.False(fresh.__HasAnyDirty());

        var merged = Delta(baseline, step2);
        var direct = baseline.Clone().ApplyDeepDelta(merged)!;
        Assert.True(direct.AreDeepEqual(step2));
    }

    [Fact]
    public void ApplyDeepDelta_EmptyDocumentNoOp()
    {
        var model = MakeBaseline();
        var clone = model.Clone();

        var applied = clone.ApplyDeepDelta(DeltaDocument.Empty)!;
        Assert.True(applied.AreDeepEqual(clone));
        Assert.False(applied.__HasAnyDirty());
    }

    [Fact]
    public void ApplyDeepDelta_MalformedDocumentIgnored()
    {
        var model = MakeBaseline();
        var target = model.Clone();

        var malformed = new DeltaDocument();
        malformed.Ops.Add(new DeltaOp(999, DeltaKind.SetMember, 0, null, 123, null));
        malformed.Ops.Add(new DeltaOp(DirtyTrackedModel.__Bit_Numbers, DeltaKind.SeqRemoveAt, 5, null, null, null));

        var applied = target.ApplyDeepDelta(malformed)!;
        Assert.True(applied.AreDeepEqual(model));
        Assert.False(applied.__HasAnyDirty());
    }

    [Fact]
    public void ThreadSafeDirtyBits_NoLostBitsUnderConcurrency()
    {
        var model = new ThreadSafeDirtyTrackedModel();
        model.Initialize(0, 0);

        var captured = new ConcurrentBag<int>();
        using var barrier = new Barrier(3);

        var producer1 = Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < 500; i++)
                model.Value = i;
        });

        var producer2 = Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < 500; i++)
                model.Other = i;
        });

        var consumer = Task.Run(() =>
        {
            barrier.SignalAndWait();
            var idleSpins = 0;
            while (!producer1.IsCompleted || !producer2.IsCompleted || model.__HasAnyDirty())
            {
                if (model.__TryPopNextDirty(out var bit))
                {
                    captured.Add(bit);
                    idleSpins = 0;
                }
                else
                {
                    idleSpins++;
                    if (idleSpins % 32 == 0)
                        Thread.Yield();
                }
            }
        });

        Task.WaitAll(producer1, producer2, consumer);

        while (model.__TryPopNextDirty(out var bit))
            captured.Add(bit);

        Assert.False(model.__HasAnyDirty());
        Assert.NotEmpty(captured);
        Assert.All(captured, bit =>
            Assert.True(bit == ThreadSafeDirtyTrackedModel.__Bit_Value || bit == ThreadSafeDirtyTrackedModel.__Bit_Other));
    }

    [Fact]
    public void DirtyBitArray_MultipleHighBits_PopAscendingAndClear()
    {
        var wide = new DirtyTrackedWide();
        wide.__MarkDirty(DirtyTrackedWide.__Bit_F65);
        wide.__MarkDirty(DirtyTrackedWide.__Bit_F68);
        wide.SetHighField(456);

        var bits = new List<int>();
        while (wide.__TryPopNextDirty(out var bit))
            bits.Add(bit);

        var expected = new[]
        {
            DirtyTrackedWide.__Bit_F65,
            DirtyTrackedWide.__Bit_F68,
            DirtyTrackedWide.__Bit_F70
        }.OrderBy(x => x).ToArray();

        Assert.Equal(expected, bits.ToArray());
        Assert.False(wide.__HasAnyDirty());
    }

    [Fact]
    public void DirtyBitArray_SupportsHighIndices()
    {
        var wide = new DirtyTrackedWide();
        wide.SetHighField(123);

        Assert.True(wide.__HasAnyDirty());

        var bits = new List<int>();
        while (wide.__TryPopNextDirty(out var bit))
            bits.Add(bit);

        Assert.Contains(DirtyTrackedWide.__Bit_F70, bits);
        Assert.False(wide.__HasAnyDirty());
    }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true, StableMemberIndex = StableMemberIndexMode.Auto)]
[DeltaTrack]
public partial class DirtyTrackedModel
{
    private bool _suppress;
    private int _a;
    private string _name = string.Empty;
    private DirtyTrackedChild _child = new();

    public DirtyTrackedModel()
    {
        Numbers = new List<int>();
        Map = new Dictionary<string, int>(StringComparer.Ordinal);
    }

    public int A
    {
        get => _a;
        set
        {
            if (_a != value)
            {
                _a = value;
                if (!_suppress) __MarkDirty(__Bit_A);
            }
        }
    }

    public string Name
    {
        get => _name;
        set
        {
            if (!string.Equals(_name, value, StringComparison.Ordinal))
            {
                _name = value;
                if (!_suppress) __MarkDirty(__Bit_Name);
            }
        }
    }

    public List<int> Numbers { get; }
    public Dictionary<string, int> Map { get; }

    public DirtyTrackedChild Child
    {
        get => _child;
        set
        {
            var newChild = value ?? new DirtyTrackedChild();
            if (!ReferenceEquals(_child, newChild))
            {
                _child = newChild;
                if (!_suppress) __MarkDirty(__Bit_Child);
            }
        }
    }

    public void Initialize(int a, string name, IEnumerable<int>? numbers, IEnumerable<KeyValuePair<string, int>>? map, DirtyTrackedChild? child)
    {
        _suppress = true;
        _a = a;
        _name = name;

        Numbers.Clear();
        if (numbers is not null)
            Numbers.AddRange(numbers);

        Map.Clear();
        if (map is not null)
            foreach (var kv in map)
                Map[kv.Key] = kv.Value;

        _child = child ?? new DirtyTrackedChild();
        _suppress = false;
    }

    public DirtyTrackedModel Clone()
    {
        var clone = new DirtyTrackedModel();
        clone.Initialize(_a, _name, Numbers, Map, _child.Clone());
        return clone;
    }

    public void AddNumber(int value)
    {
        Numbers.Add(value);
        if (!_suppress) __MarkDirty(__Bit_Numbers);
    }

    public void ReplaceNumber(int index, int value)
    {
        Numbers[index] = value;
        if (!_suppress) __MarkDirty(__Bit_Numbers);
    }

    public void SetMap(string key, int value)
    {
        Map[key] = value;
        if (!_suppress) __MarkDirty(__Bit_Map);
    }

    public void RemoveMap(string key)
    {
        Map.Remove(key);
        if (!_suppress) __MarkDirty(__Bit_Map);
    }

    public void MarkChildDirty()
    {
        if (!_suppress) __MarkDirty(__Bit_Child);
    }

    public void ForceDirtyA()
    {
        if (!_suppress) __MarkDirty(__Bit_A);
    }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true, StableMemberIndex = StableMemberIndexMode.Auto)]
[DeltaTrack]
public partial class DirtyTrackedChild
{
    private bool _suppress;
    private int _score;

    public int Score
    {
        get => _score;
        set
        {
            if (_score != value)
            {
                _score = value;
                if (!_suppress) __MarkDirty(__Bit_Score);
            }
        }
    }

    public void Initialize(int score)
    {
        _suppress = true;
        _score = score;
        _suppress = false;
    }

    public DirtyTrackedChild Clone()
    {
        var clone = new DirtyTrackedChild();
        clone.Initialize(_score);
        return clone;
    }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true, StableMemberIndex = StableMemberIndexMode.Auto)]
[DeltaTrack]
public partial class DirtyTrackedWide
{
    public int F00;
    public int F01;
    public int F02;
    public int F03;
    public int F04;
    public int F05;
    public int F06;
    public int F07;
    public int F08;
    public int F09;
    public int F10;
    public int F11;
    public int F12;
    public int F13;
    public int F14;
    public int F15;
    public int F16;
    public int F17;
    public int F18;
    public int F19;
    public int F20;
    public int F21;
    public int F22;
    public int F23;
    public int F24;
    public int F25;
    public int F26;
    public int F27;
    public int F28;
    public int F29;
    public int F30;
    public int F31;
    public int F32;
    public int F33;
    public int F34;
    public int F35;
    public int F36;
    public int F37;
    public int F38;
    public int F39;
    public int F40;
    public int F41;
    public int F42;
    public int F43;
    public int F44;
    public int F45;
    public int F46;
    public int F47;
    public int F48;
    public int F49;
    public int F50;
    public int F51;
    public int F52;
    public int F53;
    public int F54;
    public int F55;
    public int F56;
    public int F57;
    public int F58;
    public int F59;
    public int F60;
    public int F61;
    public int F62;
    public int F63;
    public int F64;
    public int F65;
    public int F66;
    public int F67;
    public int F68;
    public int F69;
    public int F70;

    public void SetHighField(int value)
    {
        F70 = value;
        __MarkDirty(__Bit_F70);
    }
}











[DeepComparable(GenerateDiff = true, GenerateDelta = true, StableMemberIndex = StableMemberIndexMode.Auto)]
[DeltaTrack(ThreadSafe = true)]
public partial class ThreadSafeDirtyTrackedModel
{
    private bool _suppress;
    private int _value;
    private int _other;

    public int Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                if (!_suppress) __MarkDirty(__Bit_Value);
            }
        }
    }

    public int Other
    {
        get => _other;
        set
        {
            if (_other != value)
            {
                _other = value;
                if (!_suppress) __MarkDirty(__Bit_Other);
            }
        }
    }

    public void Initialize(int value, int other)
    {
        _suppress = true;
        _value = value;
        _other = other;
        _suppress = false;
    }

    public ThreadSafeDirtyTrackedModel Clone()
    {
        var clone = new ThreadSafeDirtyTrackedModel();
        clone.Initialize(_value, _other);
        return clone;
    }

    public void TouchValue()
    {
        if (!_suppress) __MarkDirty(__Bit_Value);
    }

    public void TouchOther()
    {
        if (!_suppress) __MarkDirty(__Bit_Other);
    }
}
