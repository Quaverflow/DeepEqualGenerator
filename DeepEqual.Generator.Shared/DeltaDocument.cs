using DeepEqual.Generator.Shared;
using System;
using System.Collections.Generic;

namespace DeepEqual.Generator.Shared;

/// <summary>
/// In-memory, transport-neutral representation of a delta (patch).
/// This design is intentionally simple and does not prescribe an on-the-wire format.
/// </summary>
public sealed class DeltaDocument
{
    internal readonly List<DeltaOp> Ops = new();
    public IReadOnlyList<DeltaOp> Operations => Ops;
    public bool IsEmpty => Ops.Count == 0;

    public static readonly DeltaDocument Empty = new();
}

public enum DeltaKind
{
    ReplaceObject = 0,

    SetMember = 1,
    NestedMember = 2,

    SeqReplaceAt = 10,
    SeqAddAt = 11,
    SeqRemoveAt = 12,

    DictSet = 20,
    DictRemove = 21,
    DictNested = 22
}

public readonly record struct DeltaOp(
    int MemberIndex,
    DeltaKind Kind,
    int Index,
    object? Key,
    object? Value,
    DeltaDocument? Nested);

/// <summary>
/// Writer used by generated helpers to append operations.
/// </summary>
public ref struct DeltaWriter
{
    private DeltaDocument _doc;

    public DeltaWriter(DeltaDocument doc)
    {
        _doc = doc ?? throw new ArgumentNullException(nameof(doc));
    }

    public DeltaDocument Document => _doc;

    public void WriteReplaceObject(object? newValue)
        => _doc.Ops.Add(new DeltaOp(-1, DeltaKind.ReplaceObject, -1, null, newValue, null));

    public void WriteSetMember(int memberIndex, object? value)
        => _doc.Ops.Add(new DeltaOp(memberIndex, DeltaKind.SetMember, -1, null, value, null));

    public void WriteNestedMember(int memberIndex, DeltaDocument nested)
        => _doc.Ops.Add(new DeltaOp(memberIndex, DeltaKind.NestedMember, -1, null, null, nested));

    public void WriteSeqReplaceAt(int memberIndex, int index, object? value)
        => _doc.Ops.Add(new DeltaOp(memberIndex, DeltaKind.SeqReplaceAt, index, null, value, null));

    public void WriteSeqAddAt(int memberIndex, int index, object? value)
        => _doc.Ops.Add(new DeltaOp(memberIndex, DeltaKind.SeqAddAt, index, null, value, null));

    public void WriteSeqRemoveAt(int memberIndex, int index)
        => _doc.Ops.Add(new DeltaOp(memberIndex, DeltaKind.SeqRemoveAt, index, null, null, null));

    public void WriteDictSet(int memberIndex, object key, object? value)
        => _doc.Ops.Add(new DeltaOp(memberIndex, DeltaKind.DictSet, -1, key, value, null));

    public void WriteDictRemove(int memberIndex, object key)
        => _doc.Ops.Add(new DeltaOp(memberIndex, DeltaKind.DictRemove, -1, key, null, null));

    public void WriteDictNested(int memberIndex, object key, DeltaDocument nested)
        => _doc.Ops.Add(new DeltaOp(memberIndex, DeltaKind.DictNested, -1, key, null, nested));
}

/// <summary>
/// Reader wrapper to iterate over a delta document.
/// </summary>
public struct DeltaReader
{
    private readonly DeltaDocument _doc;
    private int _pos;

    public DeltaReader(DeltaDocument doc)
    {
        _doc = doc ?? DeltaDocument.Empty;
        _pos = 0;
    }

    public bool TryRead(out DeltaOp op)
    {
        if (_pos < _doc.Ops.Count)
        {
            op = _doc.Ops[_pos++];
            return true;
        }
        op = default;
        return false;
    }

    public IEnumerable<DeltaOp> EnumerateAll() => _doc.Operations;

    public IEnumerable<DeltaOp> EnumerateMember(int memberIndex)
    {
        foreach (var op in _doc.Operations)
        {
            if (op.MemberIndex == memberIndex) yield return op;
        }
    }

    public void Reset() => _pos = 0;
}
public static class DeltaHelpers
{
    public static void ComputeListDelta<T>(
        IList<T>? left, IList<T>? right, int memberIndex, ref DeltaWriter writer,
        Func<T, T, bool> areEqual)
    {
        if (ReferenceEquals(left, right)) return;
        if (left is null || right is null) { writer.WriteSetMember(memberIndex, right); return; }

        int lCount = left.Count, rCount = right.Count;

        int prefix = 0;
        while (prefix < lCount && prefix < rCount && areEqual(left[prefix], right[prefix])) prefix++;

        int suffix = 0;
        while (suffix + prefix < lCount && suffix + prefix < rCount &&
               areEqual(left[lCount - 1 - suffix], right[rCount - 1 - suffix]))
            suffix++;

        int lRemain = lCount - prefix - suffix;
        int rRemain = rCount - prefix - suffix;

        int common = Math.Min(lRemain, rRemain);
        for (int i = 0; i < common; i++)
        {
            if (!areEqual(left[prefix + i], right[prefix + i]))
                writer.WriteSeqReplaceAt(memberIndex, prefix + i, right[prefix + i]);
        }

        for (int i = lRemain - 1; i >= common; i--)
            writer.WriteSeqRemoveAt(memberIndex, prefix + i);

        for (int i = common; i < rRemain; i++)
            writer.WriteSeqAddAt(memberIndex, prefix + i, right[prefix + i]);
    }

    public static void ComputeDictDelta<TKey, TValue>(
        IDictionary<TKey, TValue>? left, IDictionary<TKey, TValue>? right, int memberIndex, ref DeltaWriter writer,
        bool nestedValues, ComparisonContext context)
        where TKey : notnull
    {
        if (ReferenceEquals(left, right)) return;
        if (left is null || right is null) { writer.WriteSetMember(memberIndex, right); return; }

        foreach (var kv in left)
        {
            if (!right.ContainsKey(kv.Key))
                writer.WriteDictRemove(memberIndex, kv.Key!);
        }

        foreach (var kv in right)
        {
            if (!left.TryGetValue(kv.Key, out var lv))
            {
                writer.WriteDictSet(memberIndex, kv.Key!, kv.Value);
                continue;
            }

            if (default(DefaultElementComparer<TValue>).Invoke(lv, kv.Value, context))
                continue;

            if (nestedValues && lv is object lo && kv.Value is object ro)
            {
                var tL = lo.GetType();
                var tR = ro.GetType();
                if (ReferenceEquals(tL, tR))
                {
                    var sub = new DeltaDocument();
                    var w = new DeltaWriter(sub);
                    GeneratedHelperRegistry.ComputeDeltaSameType(tL, lo, ro, context, ref w);
                    if (!sub.IsEmpty)
                    {
                        writer.WriteDictNested(memberIndex, kv.Key!, sub);
                        continue;
                    }
                }
            }

            writer.WriteDictSet(memberIndex, kv.Key!, kv.Value);
        }
    }

    /// <summary>
    /// Computes a granular delta between two <see cref="IReadOnlyDictionary{TKey,TValue}"/> instances,
    /// emitting DictRemove/DictSet/DictNested operations as appropriate.
    /// </summary>
    /// <remarks>
    /// This is the non-mutating counterpart to <c>ComputeDictDelta</c> used for <c>IReadOnlyDictionary&lt;TKey,TValue&gt;</c>.
    /// It performs two linear passes (removals, then adds/changes), allocates zero intermediate collections,
    /// and attempts nested value deltas only when requested via <paramref name="nestedValues"/>.
    /// </remarks>
    public static void ComputeReadOnlyDictDelta<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue>? left,
        IReadOnlyDictionary<TKey, TValue>? right,
        int memberIndex,
        ref DeltaWriter writer,
        bool nestedValues,
        ComparisonContext context)
        where TKey : notnull
    {
        if (ReferenceEquals(left, right)) return;
        if (left is null || right is null)
        {
            writer.WriteSetMember(memberIndex, right);
            return;
        }

        foreach (var kv in left)
        {
            if (!right.ContainsKey(kv.Key))
                writer.WriteDictRemove(memberIndex, kv.Key!);
        }

        foreach (var kv in right)
        {
            if (!left.TryGetValue(kv.Key, out var lval))
            {
                writer.WriteDictSet(memberIndex, kv.Key!, kv.Value);
                continue;
            }

            if (!nestedValues)
            {
                if (default(DefaultElementComparer<TValue>).Invoke(lval, kv.Value, context))
                    continue;
            }

            var lo = (object?)lval;
            var ro = (object?)kv.Value;

            if (ReferenceEquals(lo, ro)) continue;

            if (lo is null || ro is null)
            {
                if (!EqualityComparer<TValue>.Default.Equals(lval, kv.Value))
                    writer.WriteDictSet(memberIndex, kv.Key!, kv.Value);
                continue;
            }

            var tL = lo.GetType();
            var tR = ro.GetType();
            if (!ReferenceEquals(tL, tR))
            {
                writer.WriteDictSet(memberIndex, kv.Key!, kv.Value);
                continue;
            }

            if (ComparisonHelpers.DeepComparePolymorphic(lo, ro, context))
                continue;

            var sub = new DeltaDocument();
            var w = new DeltaWriter(sub);
            GeneratedHelperRegistry.ComputeDeltaSameType(tL, lo, ro, context, ref w);

            if (!sub.IsEmpty)
            {
                writer.WriteDictNested(memberIndex, kv.Key!, sub);
            }
            else
            {
                writer.WriteDictSet(memberIndex, kv.Key!, kv.Value);
            }
        }
    }

    /// <summary>
    /// Applies a single dictionary operation (DictSet/DictRemove/DictNested) to a target that may be an
    /// <see cref="IDictionary{TKey,TValue}"/> or an <see cref="IReadOnlyDictionary{TKey,TValue}"/> or null.
    /// </summary>
    /// <remarks>
    /// If the target is mutable (<c>IDictionary&lt;TKey,TValue&gt;</c>), it is mutated in place.
    /// Otherwise, a new <c>Dictionary&lt;TKey,TValue&gt;</c> is materialized, updated, and written back via <paramref name="target"/>.
    /// This guarantees the op is never ignored for read-only map instances.
    /// </remarks>
    public static void ApplyDictOpCloneIfNeeded<TKey, TValue>(ref object? target, in DeltaOp op)
     where TKey : notnull
    {
        if (target is IDictionary<TKey, TValue> md && !md.IsReadOnly)
        {
            switch (op.Kind)
            {
                case DeltaKind.DictSet:
                    md[(TKey)op.Key!] = (TValue)op.Value!;
                    return;

                case DeltaKind.DictRemove:
                    md.Remove((TKey)op.Key!);
                    return;

                case DeltaKind.DictNested:
                    {
                        var k = (TKey)op.Key!;
                        if (md.TryGetValue(k, out var oldVal))
                        {
                            object? obj = oldVal;
                            var subReader = new DeltaReader(op.Nested!);
                            GeneratedHelperRegistry.TryApplyDeltaSameType(obj!.GetType(), ref obj, ref subReader);
                            md[k] = (TValue)obj!;
                        }
                        return;
                    }
            }

            return;
        }

        var ro = target as IReadOnlyDictionary<TKey, TValue>;
        var clone = ro is null ? new Dictionary<TKey, TValue>() : new Dictionary<TKey, TValue>(ro);

        switch (op.Kind)
        {
            case DeltaKind.DictSet:
                clone[(TKey)op.Key!] = (TValue)op.Value!;
                break;

            case DeltaKind.DictRemove:
                clone.Remove((TKey)op.Key!);
                break;

            case DeltaKind.DictNested:
                {
                    var k = (TKey)op.Key!;
                    if (clone.TryGetValue(k, out var oldVal))
                    {
                        object? obj = oldVal;
                        var subReader = new DeltaReader(op.Nested!);
                        GeneratedHelperRegistry.TryApplyDeltaSameType(obj!.GetType(), ref obj, ref subReader);
                        clone[k] = (TValue)obj!;
                    }
                    break;
                }
        }

        target = clone;
    }

    /// <summary>
    /// Computes a granular delta between two <see cref="IList{T}"/> instances using a struct comparer and writes sequence operations.
    /// </summary>
    /// <remarks>
    /// This overload avoids per-call delegate allocations by taking a value-type comparer that implements <see cref="IElementComparer{T}"/>.
    /// The algorithm emits prefix/suffix-preserving replace/add/remove operations. When either side is <c>null</c>, it emits a shallow <c>SetMember</c>.
    /// </remarks>
    public static void ComputeListDelta<T, TComparer>(
        IList<T>? left,
        IList<T>? right,
        int memberIndex,
        ref DeltaWriter writer,
        TComparer comparer,
        ComparisonContext context)
        where TComparer : struct, IElementComparer<T>
    {
        if (ReferenceEquals(left, right))
        {
            return;
        }

        if (left is null || right is null)
        {
            writer.WriteSetMember(memberIndex, right);
            return;
        }

        var la = left.Count;
        var lb = right.Count;

        var prefix = 0;
        var maxPrefix = Math.Min(la, lb);
        while (prefix < maxPrefix && comparer.Invoke(left[prefix], right[prefix], context))
        {
            prefix++;
        }

        var suffix = 0;
        var maxSuffix = Math.Min(la - prefix, lb - prefix);
        while (suffix < maxSuffix && comparer.Invoke(left[la - 1 - suffix], right[lb - 1 - suffix], context))
        {
            suffix++;
        }

        var ra = la - prefix - suffix;
        var rb = lb - prefix - suffix;

        for (var i = 0; i < Math.Min(ra, rb); i++)
        {
            var ai = prefix + i;
            if (!comparer.Invoke(left[ai], right[ai], context))
            {
                writer.WriteSeqReplaceAt(memberIndex, ai, right[ai]);
            }
        }

        if (ra > rb)
        {
            for (var i = ra - 1; i >= rb; i--)
            {
                writer.WriteSeqRemoveAt(memberIndex, prefix + i);
            }
        }
        else if (rb > ra)
        {
            for (var i = ra; i < rb; i++)
            {
                var ai = prefix + i;
                writer.WriteSeqAddAt(memberIndex, ai, right[ai]);
            }
        }
    }
}