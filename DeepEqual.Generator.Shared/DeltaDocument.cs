using System;
using System.Collections.Generic;
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
    internal readonly List<DeltaOp> Ops = [];
    public IReadOnlyList<DeltaOp> Operations => Ops;
    public bool IsEmpty => Ops.Count == 0;

    internal void Clear()
    {
        Ops.Clear();
    }

    internal static DeltaDocument Rent(int initialCapacity)
    {
        var d = Rent();
        if (d.Ops.Capacity < initialCapacity) d.Ops.Capacity = initialCapacity;
        return d;
    }

    internal static DeltaDocument Rent()
    {
        var p = _pool;
        if (p is not null && p.Count > 0)
        {
            return p.Pop();
        }

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

public readonly struct DeltaOp
{
    public readonly int MemberIndex;
    public readonly DeltaKind Kind;
    public readonly int Index;
    public readonly object? Key;
    public readonly object? Value;
    public readonly DeltaDocument? Nested;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DeltaOp(int memberIndex, DeltaKind kind, int index, object? key, object? value, DeltaDocument? nested)
    {
        MemberIndex = memberIndex;
        Kind = kind;
        Index = index;
        Key = key;
        Value = value;
        Nested = nested;
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
    public void WriteReplaceObject(object? newValue) =>
        Document.Ops.Add(new DeltaOp(-1, DeltaKind.ReplaceObject, -1, null, newValue, null));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteSetMember(int memberIndex, object? value) =>
        Document.Ops.Add(new DeltaOp(memberIndex, DeltaKind.SetMember, -1, null, value, null));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteNestedMember(int memberIndex, DeltaDocument nested) =>
        Document.Ops.Add(new DeltaOp(memberIndex, DeltaKind.NestedMember, -1, null, null, nested));

    public NestedMemberScope BeginNestedMember(int memberIndex, out DeltaWriter nestedWriter)
    {
        var nested = DeltaDocument.Rent(initialCapacity: 8);        nestedWriter = new DeltaWriter(nested);
        return new NestedMemberScope(this, memberIndex, nested);
    }

    public DictNestedScope BeginDictNested(int memberIndex, object key, out DeltaWriter nestedWriter)
    {
        var nested = DeltaDocument.Rent(initialCapacity: 8);
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
            if (doc is null)
            {
                return;
            }

            if (doc.IsEmpty)
            {
                DeltaDocument.Return(doc);
            }
            else
            {
                _parent.WriteNestedMember(_memberIndex, doc);
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
            if (doc is null)
            {
                return;
            }

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
///     Reader for a delta document.
/// </summary>
public struct DeltaReader(DeltaDocument? doc)
{
    private readonly DeltaDocument _doc = doc ?? DeltaDocument.Empty;
    private int _pos = 0;

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
        {
            if (op.MemberIndex == memberIndex) action(op);
        }
    }
    public IEnumerable<DeltaOp> EnumerateAll()
    {
        return _doc.Operations;
    }

    public IEnumerable<DeltaOp> EnumerateMember(int memberIndex)
    {
        foreach (var op in _doc.Operations)
            if (op.MemberIndex == memberIndex)
            {
                yield return op;
            }
    }

    public void Reset()
    {
        _pos = 0;
    }
}

/// <summary>
///     Delta helper algorithms used by the generated code.
/// </summary>
public static class DeltaHelpers
{
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
            {
                writer.WriteSeqReplaceAt(memberIndex, ai, right[ai]);
            }
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
            {
                writer.WriteSeqReplaceAt(memberIndex, ai, right[ai]);
            }
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


    public static void ComputeDictDelta<TKey, TValue>(
        IDictionary<TKey, TValue>? left,
        IDictionary<TKey, TValue>? right,
        int memberIndex,
        ref DeltaWriter writer,
        bool nestedValues,
        ComparisonContext context)
        where TKey : notnull
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

        foreach (var kv in left)
            if (!right.ContainsKey(kv.Key))
            {
                writer.WriteDictRemove(memberIndex, kv.Key);
            }

        foreach (var kv in right)
        {
            if (!left.TryGetValue(kv.Key, out var lv))
            {
                writer.WriteDictSet(memberIndex, kv.Key, kv.Value);
                continue;
            }

            if (default(DefaultElementComparer<TValue>).Invoke(lv, kv.Value, context))
            {
                continue;
            }

            if (nestedValues && lv is object lo && kv.Value is object ro)
            {
                var tL = lo.GetType();
                var tR = ro.GetType();
                if (ReferenceEquals(tL, tR))
                {
                    var scope = writer.BeginDictNested(memberIndex, kv.Key, out var w);
                    GeneratedHelperRegistry.ComputeDeltaSameType(tL, lo, ro, context, ref w);
                    var had = !w.Document.IsEmpty;
                    scope.Dispose();
                    if (!had)
                    {
                        writer.WriteDictSet(memberIndex, kv.Key, kv.Value);
                    }

                    continue;
                }
            }

            writer.WriteDictSet(memberIndex, kv.Key, kv.Value);
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
        if (ReferenceEquals(left, right))
        {
            return;
        }

        if (left is null || right is null)
        {
            writer.WriteSetMember(memberIndex, right);
            return;
        }

        foreach (var kv in left)
            if (!right.ContainsKey(kv.Key))
            {
                writer.WriteDictRemove(memberIndex, kv.Key);
            }

        foreach (var kv in right)
        {
            if (!left.TryGetValue(kv.Key, out var lval))
            {
                writer.WriteDictSet(memberIndex, kv.Key, kv.Value);
                continue;
            }

            if (!nestedValues)
            {
                if (!default(DefaultElementComparer<TValue>).Invoke(lval, kv.Value, context))
                {
                    writer.WriteDictSet(memberIndex, kv.Key, kv.Value);
                }

                continue;
            }

            var lo = (object?)lval;
            var ro = (object?)kv.Value;
            if (ReferenceEquals(lo, ro))
            {
                continue;
            }

            if (lo is null || ro is null)
            {
                if (!EqualityComparer<TValue>.Default.Equals(lval, kv.Value))
                {
                    writer.WriteDictSet(memberIndex, kv.Key, kv.Value);
                }

                continue;
            }

            var tL = lo.GetType();
            var tR = ro.GetType();
            if (!ReferenceEquals(tL, tR))
            {
                writer.WriteDictSet(memberIndex, kv.Key, kv.Value);
                continue;
            }

            if (ComparisonHelpers.DeepComparePolymorphic(lo, ro, context))
            {
                continue;
            }

            var scope = writer.BeginDictNested(memberIndex, kv.Key, out var w);
            GeneratedHelperRegistry.ComputeDeltaSameType(tL, lo, ro, context, ref w);
            var had = !w.Document.IsEmpty;
            scope.Dispose();
            if (!had)
            {
                writer.WriteDictSet(memberIndex, kv.Key, kv.Value);
            }
        }
    }

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
                            object? obj = slot!;
                            var subReader = new DeltaReader(op.Nested!);
                            GeneratedHelperRegistry.TryApplyDeltaSameType(obj.GetType(), ref obj, ref subReader);
                            slot = (TValue)obj!;
                        }
                        return;
                    }
            }
            return;
        }

               if (target is IDictionary<TKey, TValue> { IsReadOnly: false } map)
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
                            object? obj = oldVal;
                            var subReader = new DeltaReader(op.Nested!);
                            GeneratedHelperRegistry.TryApplyDeltaSameType(obj.GetType(), ref obj, ref subReader);
                            map[k] = (TValue)obj!;
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
                    if (clone.TryGetValue(k, out var oldVal) && oldVal is not null)
                    {
                        object? obj = oldVal;
                        var subReader = new DeltaReader(op.Nested!);
                        GeneratedHelperRegistry.TryApplyDeltaSameType(obj.GetType(), ref obj, ref subReader);
                        clone[k] = (TValue)obj!;
                    }
                    break;
                }
        }

        target = clone;
    }
}
