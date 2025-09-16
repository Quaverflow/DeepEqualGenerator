using DeepEqual.Generator.Shared;
using System;
using System.Collections.Generic;

namespace DeepEqual.Generator.Shared
{
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
        // -------- Ordered collections (IList<T>) --------
        // Emits minimal ops using common prefix/suffix trim + middle replace/add/remove.
        public static void ComputeListDelta<T>(
            IList<T>? left, IList<T>? right, int memberIndex, ref DeltaWriter writer,
            IEqualityComparer<T>? cmp = null)
        {
            cmp ??= EqualityComparer<T>.Default;

            if (ReferenceEquals(left, right)) return;
            if (left is null || right is null) { writer.WriteSetMember(memberIndex, right); return; }

            int lCount = left.Count, rCount = right.Count;
            int prefix = 0;
            while (prefix < lCount && prefix < rCount && cmp.Equals(left[prefix], right[prefix])) prefix++;

            int suffix = 0;
            while (suffix + prefix < lCount && suffix + prefix < rCount &&
                   cmp.Equals(left[lCount - 1 - suffix], right[rCount - 1 - suffix]))
                suffix++;

            int lRemain = lCount - prefix - suffix;
            int rRemain = rCount - prefix - suffix;

            int common = Math.Min(lRemain, rRemain);
            for (int i = 0; i < common; i++)
            {
                var li = left[prefix + i];
                var ri = right[prefix + i];
                if (!cmp.Equals(li, ri))
                    writer.WriteSeqReplaceAt(memberIndex, prefix + i, ri);
            }

            // Removes (from left’s remaining)
            for (int i = common - 1; i >= 0; i--) { /* keep index monotonic for adds */ }
            for (int i = lRemain - 1; i >= common; i--)
                writer.WriteSeqRemoveAt(memberIndex, prefix + i);

            // Adds (from right’s remaining)
            for (int i = common; i < rRemain; i++)
                writer.WriteSeqAddAt(memberIndex, prefix + i, right[prefix + i]);
        }

        // -------- Dictionaries (IDictionary<TKey,TValue>) --------
        // If nestedValues=true and values are reference types: compute nested deltas
        public static void ComputeDictDelta<TKey, TValue>(
            IDictionary<TKey, TValue>? left, IDictionary<TKey, TValue>? right, int memberIndex, ref DeltaWriter writer,
            bool nestedValues, ComparisonContext context)
            where TKey : notnull
        {
            if (ReferenceEquals(left, right)) return;
            if (left is null || right is null) { writer.WriteSetMember(memberIndex, right); return; }

            // Removals
            foreach (var kv in left)
            {
                if (!right.ContainsKey(kv.Key))
                    writer.WriteDictRemove(memberIndex, kv.Key!);
            }

            // Adds / changes
            foreach (var kv in right)
            {
                if (!left.TryGetValue(kv.Key, out var lv))
                {
                    writer.WriteDictSet(memberIndex, kv.Key!, kv.Value);
                    continue;
                }

                if (EqualityComparer<TValue>.Default.Equals(lv, kv.Value))
                    continue;

                if (nestedValues && lv is object lo && kv.Value is object ro)
                {
                    var tL = lo.GetType();
                    var tR = ro.GetType();
                    if (ReferenceEquals(tL, tR))
                    {
                        var sub = new DeltaDocument();
                        var w = new DeltaWriter(sub);
                        GeneratedHelperRegistry.ComputeDeltaSameType(tL, lo, ro, ref w);
                        if (!sub.IsEmpty)
                        {
                            writer.WriteDictNested(memberIndex, kv.Key!, sub);
                            continue;
                        }
                    }
                }

                // fallback: shallow set
                writer.WriteDictSet(memberIndex, kv.Key!, kv.Value);
            }
        }
    }
}