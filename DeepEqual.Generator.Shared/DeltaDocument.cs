using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeepEqual.Generator.Shared;

/// <summary>
///     In-memory, transport-neutral representation of a delta (patch).
/// </summary>
public sealed class DeltaDocument
{
    public static readonly DeltaDocument Empty = new();

    [ThreadStatic] private static Stack<DeltaDocument>? _pool;
    public readonly List<DeltaOp> Ops = [];
    public IReadOnlyList<DeltaOp> Operations => Ops;
    public bool IsEmpty => Ops.Count == 0;

    internal void Clear()
    {
        Ops.Clear();
    }

    public static DeltaDocument Rent(int initialCapacity)
    {
        var d = Rent();
        if (d.Ops.Capacity < initialCapacity) d.Ops.Capacity = initialCapacity;
        return d;
    }

    internal static DeltaDocument Rent()
    {
        var p = _pool;
        if (p is not null && p.Count > 0) return p.Pop();

        return new DeltaDocument();
    }

    internal static void Return(DeltaDocument doc)
    {
        doc.Clear();
        (_pool ??= new Stack<DeltaDocument>(4)).Push(doc);
    }
}

public enum DeltaKind
{
    ReplaceObject = 0,

    SetMember = 1,
    NestedMember = 2,

    SeqReplaceAt = 10,
    SeqAddAt = 11,
    SeqRemoveAt = 12,
    SeqNestedAt = 13,
    DictSet = 20,
    DictRemove = 21,
    DictNested = 22
}

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly struct DeltaOp(
    int memberIndex,
    DeltaKind kind,
    int index,
    object? key,
    object? value,
    DeltaDocument? nested)
{
    public readonly int MemberIndex = memberIndex;
    public readonly DeltaKind Kind = kind;
    public readonly int Index = index;
    public readonly object? Key = key;
    public readonly object? Value = value;
    public readonly DeltaDocument? Nested = nested;
}

public ref struct SeqNestedScope
{
    private DeltaWriter _parent;
    private readonly int _memberIndex;
    private readonly int _index;
    private DeltaDocument? _nested;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal SeqNestedScope(DeltaWriter parent, int memberIndex, int index, DeltaDocument nested)
    {
        _parent = parent;
        _memberIndex = memberIndex;
        _index = index;
        _nested = nested;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        var doc = _nested;
        if (doc is null) return;

        if (doc.IsEmpty)
            DeltaDocument.Return(doc);
        else
            _parent.WriteSeqNestedAt(_memberIndex, _index, doc);

        _nested = null;
    }
}

/// <summary>
///     Writer used by generated helpers to append operations.
///     Provides zero-alloc nested scopes for user objects and dict values.
/// </summary>
public ref struct DeltaWriter(DeltaDocument doc)
{
    public DeltaDocument Document { get; } = doc ?? throw new ArgumentNullException(nameof(doc));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteSeqNestedAt(int memberIndex, int index, DeltaDocument nested)
    {
        Document.Ops.Add(new DeltaOp(memberIndex, DeltaKind.SeqNestedAt, index, null, null, nested));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SeqNestedScope BeginSeqNestedAt(int memberIndex, int index, out DeltaWriter nestedWriter)
    {
        var nested = DeltaDocument.Rent(8);
        nestedWriter = new DeltaWriter(nested);
        return new SeqNestedScope(this, memberIndex, index, nested);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteReplaceObject(object? newValue)
    {
        Document.Ops.Add(new DeltaOp(-1, DeltaKind.ReplaceObject, -1, null, newValue, null));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteSetMember(int memberIndex, object? value)
    {
        Document.Ops.Add(new DeltaOp(memberIndex, DeltaKind.SetMember, -1, null, value, null));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteNestedMember(int memberIndex, DeltaDocument nested)
    {
        Document.Ops.Add(new DeltaOp(memberIndex, DeltaKind.NestedMember, -1, null, null, nested));
    }

    public NestedMemberScope BeginNestedMember(int memberIndex, out DeltaWriter nestedWriter)
    {
        var nested = DeltaDocument.Rent(8);
        nestedWriter = new DeltaWriter(nested);
        return new NestedMemberScope(this, memberIndex, nested);
    }

    public DictNestedScope BeginDictNested(int memberIndex, object key, out DeltaWriter nestedWriter)
    {
        var nested = DeltaDocument.Rent(8);
        nestedWriter = new DeltaWriter(nested);
        return new DictNestedScope(this, memberIndex, key, nested);
    }

    public void WriteSeqReplaceAt(int memberIndex, int index, object? value)
    {
        Document.Ops.Add(new DeltaOp(memberIndex, DeltaKind.SeqReplaceAt, index, null, value, null));
    }

    public void WriteSeqAddAt(int memberIndex, int index, object? value)
    {
        Document.Ops.Add(new DeltaOp(memberIndex, DeltaKind.SeqAddAt, index, null, value, null));
    }

    public void WriteSeqRemoveAt(int memberIndex, int index)
    {
        Document.Ops.Add(new DeltaOp(memberIndex, DeltaKind.SeqRemoveAt, index, null, null, null));
    }

    public void WriteDictSet(int memberIndex, object key, object? value)
    {
        Document.Ops.Add(new DeltaOp(memberIndex, DeltaKind.DictSet, -1, key, value, null));
    }

    public void WriteDictRemove(int memberIndex, object key)
    {
        Document.Ops.Add(new DeltaOp(memberIndex, DeltaKind.DictRemove, -1, key, null, null));
    }

    public void WriteDictNested(int memberIndex, object key, DeltaDocument nested)
    {
        Document.Ops.Add(new DeltaOp(memberIndex, DeltaKind.DictNested, -1, key, null, nested));
    }

    public ref struct NestedMemberScope
    {
        private DeltaWriter _parent;
        private readonly int _memberIndex;
        private DeltaDocument? _nested;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal NestedMemberScope(DeltaWriter parent, int memberIndex, DeltaDocument nested)
        {
            _parent = parent;
            _memberIndex = memberIndex;
            _nested = nested;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            var doc = _nested;
            if (doc is null) return;

            if (doc.IsEmpty)
                DeltaDocument.Return(doc);
            else
                _parent.WriteNestedMember(_memberIndex, doc);

            _nested = null;
        }
    }

    public ref struct DictNestedScope
    {
        private DeltaWriter _parent;
        private readonly int _memberIndex;
        private readonly object _key;
        private DeltaDocument? _nested;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal DictNestedScope(DeltaWriter parent, int memberIndex, object key, DeltaDocument nested)
        {
            _parent = parent;
            _memberIndex = memberIndex;
            _key = key;
            _nested = nested;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            var doc = _nested;
            if (doc is null) return;

            if (doc.IsEmpty)
                DeltaDocument.Return(doc);
            else
                _parent.WriteDictNested(_memberIndex, _key, doc);

            _nested = null;
        }
    }
}

/// <summary>
///     Reader for a delta document.
/// </summary>
public struct DeltaReader(DeltaDocument? doc)
{
    private readonly DeltaDocument _doc = doc ?? DeltaDocument.Empty;
    private int _pos = 0;

    public ReadOnlySpan<DeltaOp> AsSpan()
    {
        return CollectionsMarshal.AsSpan(_doc.Ops);
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

    public void ForEachMember(int memberIndex, Action<DeltaOp> action)
    {
        foreach (var op in _doc.Operations)
            if (op.MemberIndex == memberIndex)
                action(op);
    }

    public IEnumerable<DeltaOp> EnumerateAll()
    {
        return _doc.Operations;
    }

    public IEnumerable<DeltaOp> EnumerateMember(int memberIndex)
    {
        foreach (var op in _doc.Operations)
            if (op.MemberIndex == memberIndex)
                yield return op;
    }

    public void Reset()
    {
        _pos = 0;
    }
}

/// <summary>
///     Delta helper algorithms used by the generated code.
/// </summary>
/// <summary>
///     Delta helper algorithms used by the generated code.
/// </summary>
public static class DeltaHelpers
{
    // ------------------------------------------------------------
    // SEQUENCES (LISTS)
    // ------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ApplyListOpCloneIfNeeded<T>(ref object? target, in DeltaOp op)
    {
        if (target is List<T> list)
        {
            switch (op.Kind)
            {
                case DeltaKind.SeqReplaceAt:
                    list[op.Index] = (T)op.Value!;
                    return;

                case DeltaKind.SeqAddAt:
                    if (list.Count + 1 > list.Capacity)
                        list.Capacity = Math.Max(list.Capacity * 2, list.Count + 1);
                    list.Insert(op.Index, (T)op.Value!);
                    return;

                case DeltaKind.SeqRemoveAt:
                    list.RemoveAt(op.Index);
                    return;

                case DeltaKind.SeqNestedAt:
                    {
                        var idx = op.Index;
                        if ((uint)idx < (uint)list.Count)
                        {
                            var cur = list[idx];
                            if (cur is not null)
                            {
                                object? obj = cur!;
                                var subReader = new DeltaReader(op.Nested!);
                                GeneratedHelperRegistry.TryApplyDeltaSameType(obj.GetType(), ref obj, ref subReader);
                                list[idx] = (T)obj!;
                            }
                        }

                        return;
                    }
            }

            return;
        }

        if (target is IList<T> ilist && !ilist.IsReadOnly)
        {
            switch (op.Kind)
            {
                case DeltaKind.SeqReplaceAt:
                    ilist[op.Index] = (T)op.Value!;
                    return;

                case DeltaKind.SeqAddAt:
                    ilist.Insert(op.Index, (T)op.Value!);
                    return;

                case DeltaKind.SeqRemoveAt:
                    ilist.RemoveAt(op.Index);
                    return;

                case DeltaKind.SeqNestedAt:
                    {
                        var idx = op.Index;
                        if ((uint)idx < (uint)ilist.Count)
                        {
                            var cur = ilist[idx];
                            if (cur is not null)
                            {
                                object? obj = cur!;
                                var subReader = new DeltaReader(op.Nested!);
                                GeneratedHelperRegistry.TryApplyDeltaSameType(obj.GetType(), ref obj, ref subReader);
                                ilist[idx] = (T)obj!;
                            }
                        }

                        return;
                    }
            }

            return;
        }

        // clone path (target not a mutable list)
        List<T> clone;
        if (target is IReadOnlyList<T> ro)
        {
            clone = new List<T>(ro.Count);
            for (var i = 0; i < ro.Count; i++) clone.Add(ro[i]);
        }
        else if (target is IEnumerable<T> seq)
        {
            clone = new List<T>();
            foreach (var e in seq) clone.Add(e);
        }
        else
        {
            clone = new List<T>();
        }

        switch (op.Kind)
        {
            case DeltaKind.SeqReplaceAt:
                clone[op.Index] = (T)op.Value!;
                break;

            case DeltaKind.SeqAddAt:
                clone.Insert(op.Index, (T)op.Value!);
                break;

            case DeltaKind.SeqRemoveAt:
                clone.RemoveAt(op.Index);
                break;

            case DeltaKind.SeqNestedAt:
                {
                    var idx = op.Index;
                    if ((uint)idx < (uint)clone.Count)
                    {
                        var cur = clone[idx];
                        if (cur is not null)
                        {
                            object? obj = cur!;
                            var subReader = new DeltaReader(op.Nested!);
                            GeneratedHelperRegistry.TryApplyDeltaSameType(obj.GetType(), ref obj, ref subReader);
                            clone[idx] = (T)obj!;
                        }
                    }

                    break;
                }
        }

        target = clone;
    }

    public static void ComputeListDeltaNested<T>(
        IList<T>? left,
        IList<T>? right,
        int memberIndex,
        ref DeltaWriter writer,
        ComparisonContext context)
    {
        if (ReferenceEquals(left, right))
            return;

        if (left is null || right is null)
        {
            writer.WriteSetMember(memberIndex, right);
            return;
        }

        var la = left.Count;
        var lb = right.Count;

        var prefix = 0;
        var maxPrefix = Math.Min(la, lb);
        while (prefix < maxPrefix && ComparisonHelpers.DeepComparePolymorphic(left[prefix], right[prefix], context))
            prefix++;

        var suffix = 0;
        var maxSuffix = Math.Min(la - prefix, lb - prefix);
        while (suffix < maxSuffix &&
               ComparisonHelpers.DeepComparePolymorphic(
                   left[la - 1 - suffix],
                   right[lb - 1 - suffix],
                   context))
            suffix++;

        var ra = la - prefix - suffix;
        var rb = lb - prefix - suffix;
        if (ra == 0 && rb == 0)
            return;

        if (rb >= ra && ra > 0)
        {
            var addBudget = rb - ra;
            var chosenK = -1;

            for (var k = 0; k <= addBudget; k++)
            {
                var match = true;
                for (var i = 0; i < ra; i++)
                    if (!ComparisonHelpers.DeepComparePolymorphic(
                            left[prefix + i],
                            right[prefix + k + i],
                            context))
                    {
                        match = false;
                        break;
                    }

                if (match)
                {
                    chosenK = k;
                    break;
                }
            }

            if (chosenK >= 0)
            {
                for (var i = 0; i < chosenK; i++)
                    writer.WriteSeqAddAt(memberIndex, prefix + i, right[prefix + i]);

                var alignedLen = ra;
                var tailAdds = addBudget - chosenK;
                for (var i = 0; i < tailAdds; i++)
                {
                    var insertIndex = prefix + chosenK + alignedLen + i;
                    writer.WriteSeqAddAt(memberIndex, insertIndex, right[insertIndex]);
                }

                return;
            }
        }

        var common = Math.Min(ra, rb);

        for (var i = 0; i < common; i++)
        {
            var ai = prefix + i;
            if (!ComparisonHelpers.DeepComparePolymorphic(left[ai], right[ai], context))
            {
                var lo = (object?)left[ai];
                var ro = (object?)right[ai];

                if (lo is not null && ro is not null && ReferenceEquals(lo.GetType(), ro.GetType()))
                {
                    var scope = writer.BeginSeqNestedAt(memberIndex, ai, out var w);
                    GeneratedHelperRegistry.ComputeDeltaSameType(lo.GetType(), lo, ro, context, ref w);
                    var had = !w.Document.IsEmpty;
                    scope.Dispose();

                    if (!had) writer.WriteSeqReplaceAt(memberIndex, ai, right[ai]);
                }
                else
                {
                    writer.WriteSeqReplaceAt(memberIndex, ai, right[ai]);
                }
            }
        }

        if (ra > rb)
            for (var i = ra - 1; i >= rb; i--)
                writer.WriteSeqRemoveAt(memberIndex, prefix + i);
        else if (rb > ra)
            for (var i = ra; i < rb; i++)
                writer.WriteSeqAddAt(memberIndex, prefix + i, right[prefix + i]);
    }

    public static void ComputeListDelta<T, TComparer>(
        IList<T>? left,
        IList<T>? right,
        int memberIndex,
        ref DeltaWriter writer,
        TComparer comparer,
        ComparisonContext context)
        where TComparer : struct, IElementComparer<T>
    {
        if (ReferenceEquals(left, right)) return;

        if (left is null || right is null)
        {
            writer.WriteSetMember(memberIndex, right);
            return;
        }

        var la = left.Count;
        var lb = right.Count;

        var prefix = 0;
        var maxPrefix = Math.Min(la, lb);
        while (prefix < maxPrefix && comparer.Invoke(left[prefix], right[prefix], context)) prefix++;

        var suffix = 0;
        var maxSuffix = Math.Min(la - prefix, lb - prefix);
        while (suffix < maxSuffix && comparer.Invoke(left[la - 1 - suffix], right[lb - 1 - suffix], context)) suffix++;

        var ra = la - prefix - suffix;
        var rb = lb - prefix - suffix;
        var common = Math.Min(ra, rb);

        for (var i = 0; i < common; i++)
        {
            var ai = prefix + i;
            if (!comparer.Invoke(left[ai], right[ai], context)) writer.WriteSeqReplaceAt(memberIndex, ai, right[ai]);
        }

        if (ra > rb)
            for (var i = ra - 1; i >= rb; i--)
                writer.WriteSeqRemoveAt(memberIndex, prefix + i);
        else if (rb > ra)
            for (var i = ra; i < rb; i++)
                writer.WriteSeqAddAt(memberIndex, prefix + i, right[prefix + i]);
    }

    public static void ComputeListDelta<T>(
        IList<T>? left,
        IList<T>? right,
        int memberIndex,
        ref DeltaWriter writer,
        Func<T, T, bool> areEqual)
    {
        if (ReferenceEquals(left, right)) return;

        if (left is null || right is null)
        {
            writer.WriteSetMember(memberIndex, right);
            return;
        }

        var la = left.Count;
        var lb = right.Count;

        var prefix = 0;
        var maxPrefix = Math.Min(la, lb);
        while (prefix < maxPrefix && areEqual(left[prefix], right[prefix])) prefix++;

        var suffix = 0;
        var maxSuffix = Math.Min(la - prefix, lb - prefix);
        while (suffix < maxSuffix && areEqual(left[la - 1 - suffix], right[lb - 1 - suffix])) suffix++;

        var ra = la - prefix - suffix;
        var rb = lb - prefix - suffix;
        var common = Math.Min(ra, rb);

        for (var i = 0; i < common; i++)
        {
            var ai = prefix + i;
            if (!areEqual(left[ai], right[ai])) writer.WriteSeqReplaceAt(memberIndex, ai, right[ai]);
        }

        if (ra > rb)
            for (var i = ra - 1; i >= rb; i--)
                writer.WriteSeqRemoveAt(memberIndex, prefix + i);
        else if (rb > ra)
            for (var i = ra; i < rb; i++)
                writer.WriteSeqAddAt(memberIndex, prefix + i, right[prefix + i]);
    }
    // =============================
    // LIST DELTA (keyed / order-insensitive)
    // =============================
    public static void ComputeKeyedListDeltaNested<T, TKey>(
        IList<T>? left,
        IList<T>? right,
        int memberIndex,
        ref DeltaWriter writer,
        Func<T, TKey> keySelector,
        ComparisonContext context)
        where TKey : notnull
    {
        if (ReferenceEquals(left, right)) return;
        if (left is null || right is null) { writer.WriteSetMember(memberIndex, right); return; }

        var lMap = new Dictionary<TKey, T>(left.Count);
        var rMap = new Dictionary<TKey, T>(right.Count);

        for (int i = 0; i < left.Count; i++) lMap[keySelector(left[i])] = left[i];
        for (int i = 0; i < right.Count; i++) rMap[keySelector(right[i])] = right[i];

        // Removals (by index from LEFT)
        foreach (var k in lMap.Keys)
        {
            if (!rMap.ContainsKey(k))
            {
                int idx = IndexOf(left, keySelector, k);
                if (idx >= 0) writer.WriteSeqRemoveAt(memberIndex, idx);
            }
        }

        // Adds + nested updates (by key)
        foreach (var kv in rMap)
        {
            var k = kv.Key;
            var rv = kv.Value;

            if (!lMap.TryGetValue(k, out var lv))
            {
                // Add at the position it appears in RIGHT (advisory)
                int idx = IndexOf(right, keySelector, k);
                writer.WriteSeqAddAt(memberIndex, idx < 0 ? right.Count : idx, rv);
            }
            else
            {
                // Same key present in both: diff in-place
                if (!ComparisonHelpers.DeepComparePolymorphic(lv, rv, context))
                {
                    int li = IndexOf(left, keySelector, k);
                    if (li >= 0)
                    {
                        var scope = writer.BeginSeqNestedAt(memberIndex, li, out var w);
                        GeneratedHelperRegistry.ComputeDeltaSameType(lv!.GetType(), lv!, rv!, context, ref w);
                        var had = !w.Document.IsEmpty;
                        scope.Dispose();
                        if (!had) writer.WriteSeqReplaceAt(memberIndex, li, rv);
                    }
                }
            }
        }

        // Reorder-only differences are intentionally ignored (no ops)

        static int IndexOf(IList<T> list, Func<T, TKey> sel, TKey key)
        {
            for (int i = 0; i < list.Count; i++)
                if (EqualityComparer<TKey>.Default.Equals(sel(list[i]), key))
                    return i;
            return -1;
        }
    }

    // ------------------------------------------------------------
    // DICTIONARIES
    // ------------------------------------------------------------

    public static void ComputeDictDelta<TKey, TValue>(
        IDictionary<TKey, TValue>? left,
        IDictionary<TKey, TValue>? right,
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

        // removals (avoid double hashing)
        foreach (var kv in left)
        {
            if (!right.TryGetValue(kv.Key, out _))
                writer.WriteDictRemove(memberIndex, kv.Key);
        }

        // adds / updates
        foreach (var kv in right)
        {
            var k = kv.Key;
            var rv = kv.Value;

            if (!left.TryGetValue(k, out var lv))
            {
                writer.WriteDictSet(memberIndex, k, rv);
                continue;
            }

            // equal? skip
            if (default(DefaultElementComparer<TValue>).Invoke(lv, rv, context)) continue;

            if (nestedValues)
            {
                var lo = (object?)lv;
                var ro = (object?)rv;

                // 1) Expando / IDictionary<string, object?>
                if (lo is IDictionary<string, object?> lDict && ro is IDictionary<string, object?> rDict)
                {
                    var scope = writer.BeginDictNested(memberIndex, k, out var w);
                    ComputeDictDelta<string, object?>(lDict, rDict, /*memberIndex*/ 0, ref w, /*nestedValues*/ true, context);
                    var had = !w.Document.IsEmpty;
                    scope.Dispose();
                    if (!had) writer.WriteDictSet(memberIndex, k, rv);
                    continue;
                }

                // 2) Any IReadOnlyDictionary<,> (held as object)
                if (IsReadOnlyDictionary(lo, out var lro) && IsReadOnlyDictionary(ro, out var rro))
                {
                    var scope = writer.BeginDictNested(memberIndex, k, out var w);
                    ComputeReadOnlyDictDeltaUntyped(lro!, rro!, /*memberIndex*/ 0, ref w, /*nestedValues*/ true, context);
                    var had = !w.Document.IsEmpty;
                    scope.Dispose();
                    if (!had) writer.WriteDictSet(memberIndex, k, rv);
                    continue;
                }

                // 3) Same runtime type with generated helper
                if (lo is not null && ro is not null && ReferenceEquals(lo.GetType(), ro.GetType()))
                {
                    var scope = writer.BeginDictNested(memberIndex, k, out var w);
                    GeneratedHelperRegistry.ComputeDeltaSameType(lo.GetType(), lo, ro, context, ref w);
                    var had = !w.Document.IsEmpty;
                    scope.Dispose();
                    if (!had) writer.WriteDictSet(memberIndex, k, rv);
                    continue;
                }
            }

            // fallback: set
            writer.WriteDictSet(memberIndex, k, rv);
        }
    }

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

        // removals
        foreach (var kv in left)
            if (!right.TryGetValue(kv.Key, out _))
                writer.WriteDictRemove(memberIndex, kv.Key);

        // adds / updates
        foreach (var kv in right)
        {
            var k = kv.Key;
            var rv = kv.Value;

            if (!left.TryGetValue(k, out var lval))
            {
                writer.WriteDictSet(memberIndex, k, rv);
                continue;
            }

            if (!nestedValues)
            {
                if (!default(DefaultElementComparer<TValue>).Invoke(lval, rv, context))
                    writer.WriteDictSet(memberIndex, k, rv);
                continue;
            }

            var lo = (object?)lval;
            var ro = (object?)rv;

            if (ReferenceEquals(lo, ro)) continue;

            if (lo is null || ro is null)
            {
                if (!EqualityComparer<TValue>.Default.Equals(lval, rv))
                    writer.WriteDictSet(memberIndex, k, rv);
                continue;
            }

            // 1) Expando / IDictionary<string, object?>
            if (lo is IDictionary<string, object?> lDict && ro is IDictionary<string, object?> rDict)
            {
                var scope = writer.BeginDictNested(memberIndex, k, out var w);
                ComputeDictDelta<string, object?>(lDict, rDict, /*memberIndex*/ 0, ref w, /*nestedValues*/ true, context);
                var had = !w.Document.IsEmpty;
                scope.Dispose();
                if (!had) writer.WriteDictSet(memberIndex, k, rv);
                continue;
            }

            // 2) Any IReadOnlyDictionary<,> (held as object)
            if (IsReadOnlyDictionary(lo, out var lro) && IsReadOnlyDictionary(ro, out var rro))
            {
                var scope = writer.BeginDictNested(memberIndex, k, out var w);
                ComputeReadOnlyDictDeltaUntyped(lro!, rro!, /*memberIndex*/ 0, ref w, /*nestedValues*/ true, context);
                var had = !w.Document.IsEmpty;
                scope.Dispose();
                if (!had) writer.WriteDictSet(memberIndex, k, rv);
                continue;
            }

            // 3) Same-type generated helper
            var tL = lo.GetType();
            var tR = ro.GetType();
            if (!ReferenceEquals(tL, tR))
            {
                writer.WriteDictSet(memberIndex, k, rv);
                continue;
            }

            if (ComparisonHelpers.DeepComparePolymorphic(lo, ro, context)) continue;

            var scope2 = writer.BeginDictNested(memberIndex, k, out var w2);
            GeneratedHelperRegistry.ComputeDeltaSameType(tL, lo, ro, context, ref w2);
            var had2 = !w2.Document.IsEmpty;
            scope2.Dispose();
            if (!had2) writer.WriteDictSet(memberIndex, k, rv);
        }
    }

    /// <summary>
    /// Untyped variant for any IReadOnlyDictionary&lt;TKey,TValue&gt; held as object.
    /// Builds dict ops directly into <paramref name="writer"/> at <paramref name="memberIndex"/>.
    /// </summary>
    public static void ComputeReadOnlyDictDeltaUntyped(
        object leftRO,
        object rightRO,
        int memberIndex,
        ref DeltaWriter writer,
        bool nestedValues,
        ComparisonContext context)
    {
        if (leftRO is null || rightRO is null) return;

        var rightMap = new Dictionary<object, object?>(EqualityComparer<object>.Default);
        foreach (var (rk, rv) in RoEnumerate(rightRO)) rightMap[rk] = rv;

        // removals
        foreach (var (lk, _) in RoEnumerate(leftRO))
            if (!rightMap.ContainsKey(lk)) writer.WriteDictRemove(memberIndex, lk);

        // adds / updates
        foreach (var (rk, rv) in RoEnumerate(rightRO))
        {
            if (!RoTryGetValue(leftRO, rk, out var lv))
            {
                writer.WriteDictSet(memberIndex, rk, rv);
                continue;
            }

            if (ComparisonHelpers.DeepComparePolymorphic(lv, rv, context))
                continue;

            if (nestedValues)
            {
                if (lv is IDictionary<string, object?> lDict && rv is IDictionary<string, object?> rDict)
                {
                    var scope = writer.BeginDictNested(memberIndex, rk, out var w);
                    ComputeDictDelta<string, object?>(lDict, rDict, /*memberIndex*/ 0, ref w, /*nestedValues*/ true, context);
                    var had = !w.Document.IsEmpty;
                    scope.Dispose();
                    if (!had) writer.WriteDictSet(memberIndex, rk, rv);
                    continue;
                }

                if (IsReadOnlyDictionary(lv, out var lro) && IsReadOnlyDictionary(rv, out var rro))
                {
                    var scope = writer.BeginDictNested(memberIndex, rk, out var w);
                    ComputeReadOnlyDictDeltaUntyped(lro!, rro!, /*memberIndex*/ 0, ref w, /*nestedValues*/ true, context);
                    var had = !w.Document.IsEmpty;
                    scope.Dispose();
                    if (!had) writer.WriteDictSet(memberIndex, rk, rv);
                    continue;
                }
            }

            writer.WriteDictSet(memberIndex, rk, rv);
        }

        // local helpers
        static IEnumerable<(object key, object? value)> RoEnumerate(object ro)
        {
            var e = ro as IEnumerable ?? throw new InvalidOperationException("Not an IEnumerable");
            foreach (var kv in e)
            {
                var t = kv!.GetType();
                var k = t.GetProperty("Key")!.GetValue(kv)!;
                var v = t.GetProperty("Value")!.GetValue(kv);
                yield return (k, v);
            }
        }

        static bool RoTryGetValue(object ro, object key, out object? value)
        {
            var m = ro.GetType().GetMethod("TryGetValue");
            var args = new object?[] { key, null };
            var ok = (bool)(m?.Invoke(ro, args) ?? false);
            value = ok ? args[1] : null;
            return ok;
        }
    }

    // ------------------------------------------------------------
    // APPLY — DICTIONARY OPS
    // ------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ApplyDictOpCloneIfNeeded<TKey, TValue>(ref object? target, in DeltaOp op)
        where TKey : notnull
    {
        if (target is Dictionary<TKey, TValue> md)
        {
            switch (op.Kind)
            {
                case DeltaKind.DictSet:
                    {
                        ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(md, (TKey)op.Key!, out _);
                        slot = (TValue)op.Value!;
                        return;
                    }
                case DeltaKind.DictRemove:
                    {
                        md.Remove((TKey)op.Key!);
                        return;
                    }
                case DeltaKind.DictNested:
                    {
                        var k = (TKey)op.Key!;
                        ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(md, k, out var existed);
                        if (existed && slot is not null)
                        {
                            object? cur = slot!;
                            ApplyNestedDictOrSameType(ref cur, op.Nested!);
                            slot = (TValue)cur!;
                        }
                        return;
                    }
            }

            return;
        }

        if (target is IDictionary<TKey, TValue> map && !map.IsReadOnly)
        {
            switch (op.Kind)
            {
                case DeltaKind.DictSet:
                    map[(TKey)op.Key!] = (TValue)op.Value!;
                    return;

                case DeltaKind.DictRemove:
                    map.Remove((TKey)op.Key!);
                    return;

                case DeltaKind.DictNested:
                    {
                        var k = (TKey)op.Key!;
                        if (map.TryGetValue(k, out var oldVal) && oldVal is not null)
                        {
                            object? cur = oldVal;
                            ApplyNestedDictOrSameType(ref cur, op.Nested!);
                            map[k] = (TValue)cur!;
                        }
                        return;
                    }
            }

            return;
        }

        // clone path (read-only or null)
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
                    if (clone.TryGetValue(k, out var oldVal) && oldVal is not null)
                    {
                        object? cur = oldVal;
                        ApplyNestedDictOrSameType(ref cur, op.Nested!);
                        clone[k] = (TValue)cur!;
                    }
                    break;
                }
        }

        target = clone;

        // ------------ local ------------
        static void ApplyNestedDictOrSameType(ref object? curVal, DeltaDocument nestedDoc)
        {
            // Expando / IDictionary<string, object?>
            if (curVal is IDictionary<string, object?> rw)
            {
                object? inner = rw;
                var ops = new DeltaReader(nestedDoc).AsSpan();
                for (int i = 0; i < ops.Length; i++)
                {
                    ref readonly var nop = ref ops[i];
                    ApplyDictOpCloneIfNeeded<string, object?>(ref inner, in nop);
                }

                // preserve ExpandoObject if that was the original
                if (curVal is ExpandoObject)
                {
                    var src = (IDictionary<string, object?>)inner!;
                    var exp = new ExpandoObject();
                    var dst = (IDictionary<string, object?>)exp;
                    dst.Clear();
                    foreach (var kv in src) dst[kv.Key] = kv.Value;
                    curVal = exp;
                }
                else
                {
                    curVal = inner!;
                }
                return;
            }

            // Any IReadOnlyDictionary<,> — we don't mutate (could add a reflection-based apply if ever needed)
            var ifaces = curVal?.GetType().GetInterfaces();
            if (ifaces != null)
            {
                foreach (var it in ifaces)
                {
                    if (it.IsGenericType && it.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
                    {
                        // no-op; user would need a generated helper to mutate inner RO maps
                        return;
                    }
                }
            }

            // Fallback: same-type generated helper
            if (curVal is not null)
            {
                var t = curVal.GetType();
                var subReader = new DeltaReader(nestedDoc);
                GeneratedHelperRegistry.TryApplyDeltaSameType(t, ref curVal, ref subReader);
            }
        }
    }

    // ------------------------------------------------------------
    // PRIVATE HELPERS
    // ------------------------------------------------------------

    private static bool IsReadOnlyDictionary(object? o, out object? roDict)
    {
        roDict = null;
        if (o is null) return false;
        foreach (var it in o.GetType().GetInterfaces())
        {
            if (it.IsGenericType && it.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
            {
                roDict = o;
                return true;
            }
        }
        return false;
    }
}
