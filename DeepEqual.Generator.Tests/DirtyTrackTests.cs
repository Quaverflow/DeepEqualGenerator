#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using DeepEqual;
using DeepEqual.Generator.Shared;
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
