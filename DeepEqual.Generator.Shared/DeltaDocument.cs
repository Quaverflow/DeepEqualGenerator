using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DeepEqual.Generator.Shared;

/// <summary>
/// In-memory, transport-neutral representation of a delta (patch).
/// </summary>
public sealed class DeltaDocument
{
    internal readonly List<DeltaOp> Ops = new();
    public IReadOnlyList<DeltaOp> Operations => Ops;
    public bool IsEmpty => Ops.Count == 0;

    public static readonly DeltaDocument Empty = new();

    internal void Clear() => Ops.Clear();

    // Tiny thread-local pool for nested scopes
    [ThreadStatic] private static Stack<DeltaDocument>? _pool;

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
/// Provides zero-alloc nested scopes for user objects and dict values.
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

    /// <summary>Begin a nested user-object scope. Writes nothing if empty; else emits a single NestedMember.</summary>
    public NestedMemberScope BeginNestedMember(int memberIndex, out DeltaWriter nestedWriter)
    {
        var nested = DeltaDocument.Rent();
        nestedWriter = new DeltaWriter(nested);
        return new NestedMemberScope(this, memberIndex, nested);
    }

    /// <summary>Begin a nested dictionary-value scope. Writes nothing if empty; else emits DictNested.</summary>
    public DictNestedScope BeginDictNested(int memberIndex, object key, out DeltaWriter nestedWriter)
    {
        var nested = DeltaDocument.Rent();
        nestedWriter = new DeltaWriter(nested);
        return new DictNestedScope(this, memberIndex, key, nested);
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
            {
                DeltaDocument.Return(doc);
            }
            else
            {
                _parent.WriteNestedMember(_memberIndex, doc);
                // Ownership transferred; do not return to pool
            }

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
            {
                DeltaDocument.Return(doc);
            }
            else
            {
                _parent.WriteDictNested(_memberIndex, _key, doc);
            }

            _nested = null;
        }
    }
}

/// <summary>
/// Reader for a delta document.
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
            if (op.MemberIndex == memberIndex) yield return op;
    }

    public void Reset() => _pos = 0;
}

/// <summary>
/// Delta helper algorithms used by the generated code.
/// </summary>
public static class DeltaHelpers
{
    // ----------------------------
    // IList<T> granular (order-sensitive)
    // ----------------------------

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
        if (left is null || right is null) { writer.WriteSetMember(memberIndex, right); return; }

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
            if (!comparer.Invoke(left[ai], right[ai], context))
                writer.WriteSeqReplaceAt(memberIndex, ai, right[ai]);
        }

        if (ra > rb)
        {
            for (var i = ra - 1; i >= rb; i--)
                writer.WriteSeqRemoveAt(memberIndex, prefix + i);
        }
        else if (rb > ra)
        {
            for (var i = ra; i < rb; i++)
                writer.WriteSeqAddAt(memberIndex, prefix + i, right[prefix + i]);
        }
    }

    public static void ComputeListDelta<T>(
        IList<T>? left,
        IList<T>? right,
        int memberIndex,
        ref DeltaWriter writer,
        Func<T, T, bool> areEqual)
    {
        if (ReferenceEquals(left, right)) return;
        if (left is null || right is null) { writer.WriteSetMember(memberIndex, right); return; }

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
            if (!areEqual(left[ai], right[ai]))
                writer.WriteSeqReplaceAt(memberIndex, ai, right[ai]);
        }

        if (ra > rb)
        {
            for (var i = ra - 1; i >= rb; i--)
                writer.WriteSeqRemoveAt(memberIndex, prefix + i);
        }
        else if (rb > ra)
        {
            for (var i = ra; i < rb; i++)
                writer.WriteSeqAddAt(memberIndex, prefix + i, right[prefix + i]);
        }
    }

    // ----------------------------
    // IDictionary<TKey,TValue> with optional nested value deltas
    // ----------------------------

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
        if (left is null || right is null) { writer.WriteSetMember(memberIndex, right); return; }

        foreach (var kv in left)
            if (!right.ContainsKey(kv.Key))
                writer.WriteDictRemove(memberIndex, kv.Key!);

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
                    var scope = writer.BeginDictNested(memberIndex, kv.Key!, out var w);
                    GeneratedHelperRegistry.ComputeDeltaSameType(tL, lo, ro, context, ref w);
                    var had = !w.Document.IsEmpty;
                    scope.Dispose(); // emits if had==true
                    if (!had)
                        writer.WriteDictSet(memberIndex, kv.Key!, kv.Value);
                    continue;
                }
            }

            writer.WriteDictSet(memberIndex, kv.Key!, kv.Value);
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
        if (left is null || right is null) { writer.WriteSetMember(memberIndex, right); return; }

        foreach (var kv in left)
            if (!right.ContainsKey(kv.Key))
                writer.WriteDictRemove(memberIndex, kv.Key!);

        foreach (var kv in right)
        {
            if (!left.TryGetValue(kv.Key, out var lval))
            {
                writer.WriteDictSet(memberIndex, kv.Key!, kv.Value);
                continue;
            }

            // If not nesting, we only need to check equality and emit DictSet on change.
            if (!nestedValues)
            {
                if (!default(DefaultElementComparer<TValue>).Invoke(lval, kv.Value, context))
                    writer.WriteDictSet(memberIndex, kv.Key!, kv.Value);
                continue;
            }

            // Nested value path
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

            var scope = writer.BeginDictNested(memberIndex, kv.Key!, out var w);
            GeneratedHelperRegistry.ComputeDeltaSameType(tL, lo, ro, context, ref w);
            var had = !w.Document.IsEmpty;
            scope.Dispose(); // emits if had==true
            if (!had)
                writer.WriteDictSet(memberIndex, kv.Key!, kv.Value);
        }
    }

    // ----------------------------
    // Apply helpers for dicts
    // ----------------------------

    public static void ApplyDictOpCloneIfNeeded<TKey, TValue>(ref object? target, in DeltaOp op)
        where TKey : notnull
    {
        if (target is IDictionary<TKey, TValue> md && !md.IsReadOnly)
        {
            switch (op.Kind)
            {
                case DeltaKind.DictSet: md[(TKey)op.Key!] = (TValue)op.Value!; return;
                case DeltaKind.DictRemove: md.Remove((TKey)op.Key!); return;
                case DeltaKind.DictNested:
                    {
                        var k = (TKey)op.Key!;
                        if (md.TryGetValue(k, out var oldVal) && oldVal is not null)
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

        // Clone-readonly path — comparer is not preserved (IReadOnlyDictionary doesn’t expose it).
        var ro = target as IReadOnlyDictionary<TKey, TValue>;
        var clone = ro is null ? new Dictionary<TKey, TValue>() : new Dictionary<TKey, TValue>(ro);

        switch (op.Kind)
        {
            case DeltaKind.DictSet: clone[(TKey)op.Key!] = (TValue)op.Value!; break;
            case DeltaKind.DictRemove: clone.Remove((TKey)op.Key!); break;
            case DeltaKind.DictNested:
                {
                    var k = (TKey)op.Key!;
                    if (clone.TryGetValue(k, out var oldVal) && oldVal is not null)
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
}
