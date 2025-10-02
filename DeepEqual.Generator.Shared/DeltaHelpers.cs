using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeepEqual.Generator.Shared;

/// <summary>
///     Delta helper algorithms used by the generated code.
/// </summary>
public static class DeltaHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ApplyListOpCloneIfNeeded<T>(ref object? target, in DeltaOp op)
    {
        if (target is List<T> list)
        {
            var comparer = EqualityComparer<T>.Default;

            switch (op.Kind)
            {
                case DeltaKind.SeqReplaceAt:
                    {
                        if ((uint)op.Index < (uint)list.Count)
                            list[op.Index] = (T)op.Value!;
                        return;
                    }

                case DeltaKind.SeqAddAt:
                    {
                        int idx = op.Index;
                        if (idx < 0) idx = 0;
                        var value = (T)op.Value!;
                        int count = list.Count;

                        if ((uint)idx <= (uint)count)
                        {
                            // Replay safety: if the target slot already equals the value, no-op
                            if ((uint)idx < (uint)count &&
                                comparer.Equals(list[idx], value))
                            {
                                return;
                            }

                            // (A) If this add would start at an existing [v,v] pair, no-op
                            if (idx + 1 < count &&
                                comparer.Equals(list[idx], value) &&
                                comparer.Equals(list[idx + 1], value))
                            {
                                return;
                            }

                            // (B) If appending and the list already ends with v, no-op
                            if (idx == count && count > 0 &&
                                comparer.Equals(list[count - 1], value))
                            {
                                return;
                            }

                            // (C) Replays recorded with old length: idx == count-1 and that slot already == v
                            if (idx == count - 1 && idx >= 0 &&
                                comparer.Equals(list[idx], value))
                            {
                                return;
                            }

                            // (D) No-triples guard around the boundary:
                            // if left neighbor and current are already [v,v], inserting here would create a triple
                            if (idx > 0 && idx < count &&
                                comparer.Equals(list[idx - 1], value) &&
                                comparer.Equals(list[idx], value))
                            {
                                return;
                            }

                            if (count + 1 > list.Capacity)
                                list.Capacity = Math.Max(list.Capacity * 2, count + 1);

                            if (idx == count)
                                list.Add(value);
                            else
                                list.Insert(idx, value);
                        }
                        else
                        {
                            // idx beyond end: clamp to end; avoid duplicate when last already equals v
                            if (count == 0 || !comparer.Equals(list[count - 1], value))
                                list.Add(value);
                        }
                        return;
                    }

                case DeltaKind.SeqRemoveAt:
                    {
                        // New contract: expected element is required for idempotency
                        if (op.Value is null)
                            throw new InvalidOperationException("SeqRemoveAt requires an expected element value (op.Value) for idempotency.");

                        if ((uint)op.Index < (uint)list.Count)
                        {
                            var expected = (T)op.Value!;
                            if (comparer.Equals(list[op.Index], expected))
                                list.RemoveAt(op.Index);
                            // else: no-op (already removed or index now points to a different element)
                        }
                        return;
                    }

                case DeltaKind.SeqNestedAt:
                    {
                        int idx = op.Index;
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
            var comparer = EqualityComparer<T>.Default;

            switch (op.Kind)
            {
                case DeltaKind.SeqReplaceAt:
                    {
                        if ((uint)op.Index < (uint)ilist.Count)
                            ilist[op.Index] = (T)op.Value!;
                        return;
                    }

                case DeltaKind.SeqAddAt:
                    {
                        int idx = op.Index;
                        if (idx < 0) idx = 0;
                        var value = (T)op.Value!;
                        int count = ilist.Count;

                        if ((uint)idx <= (uint)count)
                        {
                            if ((uint)idx < (uint)count &&
                                comparer.Equals(ilist[idx], value))
                            {
                                return;
                            }

                            if (idx + 1 < count &&
                                comparer.Equals(ilist[idx], value) &&
                                comparer.Equals(ilist[idx + 1], value))
                            {
                                return;
                            }

                            if (idx == count && count > 0 &&
                                comparer.Equals(ilist[count - 1], value))
                            {
                                return;
                            }

                            if (idx == count - 1 && idx >= 0 &&
                                comparer.Equals(ilist[idx], value))
                            {
                                return;
                            }

                            if (idx > 0 && idx < count &&
                                comparer.Equals(ilist[idx - 1], value) &&
                                comparer.Equals(ilist[idx], value))
                            {
                                return;
                            }

                            if (idx == count)
                                ilist.Add(value);
                            else
                                ilist.Insert(idx, value);
                        }
                        else
                        {
                            // idx beyond end: clamp to end; avoid duplicate when last already equals v
                            if (count == 0 || !comparer.Equals(ilist[count - 1], value))
                                ilist.Insert(count, value);
                        }
                        return;
                    }

                case DeltaKind.SeqRemoveAt:
                    {
                        // New contract: expected element is required for idempotency
                        if (op.Value is null)
                            throw new InvalidOperationException("SeqRemoveAt requires an expected element value (op.Value) for idempotency.");

                        if ((uint)op.Index < (uint)ilist.Count)
                        {
                            var expected = (T)op.Value!;
                            if (comparer.Equals(ilist[op.Index], expected))
                                ilist.RemoveAt(op.Index);
                        }
                        return;
                    }

                case DeltaKind.SeqNestedAt:
                    {
                        int idx = op.Index;
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

        // clone path (IReadOnlyList / IEnumerable -> clone)
        var comparerClone = EqualityComparer<T>.Default;
        var addsHint = op.Kind == DeltaKind.SeqAddAt ? 1 : 0;

        List<T> clone;
        if (target is IReadOnlyList<T> ro)
        {
            var capacity = ro.Count + addsHint;
            clone = new List<T>(capacity);
            for (int i = 0; i < ro.Count; i++) clone.Add(ro[i]);
        }
        else if (target is ICollection<T> collection)
        {
            var capacity = collection.Count + addsHint;
            clone = new List<T>(capacity);
            foreach (var e in collection) clone.Add(e);
        }
        else if (target is IEnumerable<T> seq)
        {
            clone = addsHint > 0 ? new List<T>(addsHint) : new List<T>();
            foreach (var e in seq) clone.Add(e);
        }
        else
        {
            clone = addsHint > 0 ? new List<T>(addsHint) : new List<T>();
        }

        switch (op.Kind)
        {
            case DeltaKind.SeqReplaceAt:
                {
                    if ((uint)op.Index < (uint)clone.Count)
                        clone[op.Index] = (T)op.Value!;
                    break;
                }

            case DeltaKind.SeqAddAt:
                {
                    int ai = op.Index;
                    if (ai < 0) ai = 0;
                    if (ai > clone.Count) ai = clone.Count;

                    var v = (T)op.Value!;

                    // Replay safety: if target slot already equals v, no-op
                    if (ai < clone.Count &&
                        comparerClone.Equals(clone[ai], v))
                        break;

                    // Append de-dupe
                    if (ai == clone.Count && clone.Count > 0 &&
                        comparerClone.Equals(clone[clone.Count - 1], v))
                        break;

                    // Optional: avoid triples within one pass
                    if (ai > 0 && ai < clone.Count &&
                        comparerClone.Equals(clone[ai - 1], v) &&
                        comparerClone.Equals(clone[ai], v))
                        break;

                    clone.Insert(ai, v);
                    break;
                }

            case DeltaKind.SeqRemoveAt:
                {
                    // New contract: expected element is required
                    if (op.Value is null)
                        throw new InvalidOperationException("SeqRemoveAt requires an expected element value (op.Value).");

                    if ((uint)op.Index < (uint)clone.Count)
                    {
                        var expected = (T)op.Value!;
                        if (comparerClone.Equals(clone[op.Index], expected))
                            clone.RemoveAt(op.Index); // idempotent: only remove if it still matches
                    }
                    break;
                }

            case DeltaKind.SeqNestedAt:
                {
                    int idx = op.Index;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryApplyListSeqAddFastLane<T>(ref object? target, ReadOnlySpan<DeltaOp> ops)
    {
        if (ops.Length == 0)
            return false;

        var expectedMember = ops[0].MemberIndex;
        if (expectedMember < 0)
            return false;

        List<T>? list;

        if (target is List<T> existing)
        {
            list = existing;
        }
        else if (target is null)
        {
            list = new List<T>(ops.Length);
            target = list;
        }
        else
        {
            return false;
        }

        var baseSpan = CollectionsMarshal.AsSpan(list);
        var baseCount = baseSpan.Length;
        var comparer = EqualityComparer<T>.Default;
        var isRefType = !typeof(T).IsValueType;

        var currentCount = baseCount;
        var prevIndex = -1;
        var ensureCapacityDone = false;

        var hasLast = baseCount > 0;
        T lastValue = hasLast ? baseSpan[baseCount - 1] : default!;

        for (var i = 0; i < ops.Length; i++)
        {
            ref readonly var op = ref ops[i];

            if (op.Kind != DeltaKind.SeqAddAt || op.MemberIndex != expectedMember)
                return false;

            var rawIndex = op.Index;
            if (rawIndex < 0)
                return false;

            if (rawIndex <= prevIndex)
                return false;

            prevIndex = rawIndex;

            var value = (T)op.Value!;

            if (rawIndex < baseCount)
            {
                var existingValue = baseSpan[rawIndex];
                bool equal;
                if (isRefType)
                {
                    equal = ReferenceEquals(existingValue, value) || comparer.Equals(existingValue, value);
                }
                else
                {
                    equal = comparer.Equals(existingValue, value);
                }

                if (!equal)
                    return false;

                if (rawIndex == baseCount - 1)
                {
                    lastValue = existingValue;
                    hasLast = true;
                }

                continue;
            }

            if (rawIndex != currentCount)
                return false;

            if (!ensureCapacityDone)
            {
                list.EnsureCapacity(currentCount + (ops.Length - i));
                baseSpan = CollectionsMarshal.AsSpan(list);
                ensureCapacityDone = true;
            }

            if (hasLast)
            {
                bool same = isRefType
                    ? ReferenceEquals(lastValue, value) || comparer.Equals(lastValue, value)
                    : comparer.Equals(lastValue, value);
                if (same)
                    continue;
            }

            list.Add(value);
            currentCount++;
            lastValue = value;
            hasLast = true;
        }

        target = list;
        return true;
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

        var leftList = left;
        var rightList = right;

        var leftAccessor = new ListReadAccessor<T>(leftList);
        var rightAccessor = new ListReadAccessor<T>(rightList);

        var la = leftList.Count;
        var lb = rightList.Count;

        if (la == 0)
        {
            if (lb == 0)
                return;

            for (var i = 0; i < lb; i++)
                writer.WriteSeqAddAt(memberIndex, i, rightAccessor[i]);
            return;
        }

        if (lb == 0)
        {
            for (var i = la - 1; i >= 0; i--)
                writer.WriteSeqRemoveAt(memberIndex, i, leftAccessor[i]);
            return;
        }

        var elementComparer = new ListElementComparer<T>(context);

        var prefix = 0;
        var maxPrefix = Math.Min(la, lb);
        while (prefix < maxPrefix && elementComparer.AreEqual(leftAccessor[prefix], rightAccessor[prefix]))
            prefix++;

        var suffix = 0;
        var maxSuffix = Math.Min(la - prefix, lb - prefix);
        while (suffix < maxSuffix &&
               elementComparer.AreEqual(leftAccessor[la - 1 - suffix], rightAccessor[lb - 1 - suffix]))
            suffix++;

        var ra = la - prefix - suffix;
        var rb = lb - prefix - suffix;
        if (ra == 0 && rb == 0)
            return;

        if (rb >= ra && ra > 0)
        {
            var addBudget = rb - ra;
            var chosenK = -1;

            for (var k = addBudget; k >= 0; k--)
            {
                var match = true;
                for (var i = 0; i < ra; i++)
                {
                    if (!elementComparer.AreEqual(leftAccessor[prefix + i], rightAccessor[prefix + k + i]))
                    {
                        match = false;
                        break;
                    }
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
                    writer.WriteSeqAddAt(memberIndex, prefix + i, rightAccessor[prefix + i]);

                var alignedLen = ra;
                var tailAdds = addBudget - chosenK;
                for (var i = 0; i < tailAdds; i++)
                {
                    var insertIndex = prefix + chosenK + alignedLen + i;
                    writer.WriteSeqAddAt(memberIndex, insertIndex, rightAccessor[insertIndex]);
                }

                return;
            }
        }

        var common = Math.Min(ra, rb);

        for (var i = 0; i < common; i++)
        {
            var ai = prefix + i;
            var leftValue = leftAccessor[ai];
            var rightValue = rightAccessor[ai];

            if (!elementComparer.AreEqual(leftValue, rightValue))
            {
                var lo = (object?)leftValue;
                var ro = (object?)rightValue;

                if (lo is not null && ro is not null && ReferenceEquals(lo.GetType(), ro.GetType()))
                {
                    var scope = writer.BeginSeqNestedAt(memberIndex, ai, out var w);
                    GeneratedHelperRegistry.ComputeDeltaSameType(lo.GetType(), lo, ro, context, ref w);
                    var had = !w.Document.IsEmpty;
                    scope.Dispose();

                    if (!had) writer.WriteSeqReplaceAt(memberIndex, ai, rightValue);
                }
                else
                {
                    writer.WriteSeqReplaceAt(memberIndex, ai, rightValue);
                }
            }
        }

        if (ra > rb)
        {
            for (var i = ra - 1; i >= rb; i--)
            {
                var idx = prefix + i;
                writer.WriteSeqRemoveAt(memberIndex, idx, leftAccessor[idx]);
            }
        }
        else if (rb > ra)
        {
            for (var i = ra; i < rb; i++)
                writer.WriteSeqAddAt(memberIndex, prefix + i, rightAccessor[prefix + i]);
        }
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

        int na = left.Count;
        int nb = right.Count;

        if (na == 0)
        {
            if (nb == 0)
                return;

            for (int i = 0; i < nb; i++)
                writer.WriteSeqAddAt(memberIndex, i, right[i]);
            return;
        }

        if (nb == 0)
        {
            for (int i = na - 1; i >= 0; i--)
                writer.WriteSeqRemoveAt(memberIndex, i, left[i]);
            return;
        }

        // -------- SINGLE-INSERT FAST PATH (pick earliest feasible k) --------
        // Find the smallest k in [0..na] such that:
        // left[0..k-1] == right[0..k-1] AND left[k..na-1] == right[k+1..nb-1]
        if (nb == na + 1)
        {
            for (int k = 0; k <= na; k++)
            {
                // head must match up to k-1
                bool headMatches = true;
                for (int i = 0; i < k; i++)
                {
                    if (!comparer.Invoke(left[i], right[i], context))
                    {
                        headMatches = false;
                        break;
                    }
                }
                if (!headMatches) continue;

                // tail must match with +1 shift
                bool tailMatches = true;
                for (int i = k; i < na; i++)
                {
                    if (!comparer.Invoke(left[i], right[i + 1], context))
                    {
                        tailMatches = false;
                        break;
                    }
                }

                if (tailMatches)
                {
                    // If right[k] equals left[k], insert AFTER the existing element (k+1) for replay idempotency
                    int insertIndex = k;
                    if (k < na && comparer.Invoke(left[k], right[k], context))
                        insertIndex = k + 1;

                    writer.WriteSeqAddAt(memberIndex, insertIndex, right[insertIndex]);
                    return;
                }
            }
            // not a pure insert -> fall through
        }
        // -------------------------------------------------------------------

        // Original prefix/suffix trimming
        int prefix = 0;
        int maxPrefix = Math.Min(na, nb);
        while (prefix < maxPrefix && comparer.Invoke(left[prefix], right[prefix], context)) prefix++;

        int suffix = 0;
        int maxSuffix = Math.Min(na - prefix, nb - prefix);
        while (suffix < maxSuffix && comparer.Invoke(left[na - 1 - suffix], right[nb - 1 - suffix], context)) suffix++;

        int ra = na - prefix - suffix;
        int rb = nb - prefix - suffix;

        // Duplicate-aware alignment (existing logic)
        if (rb >= ra && ra > 0)
        {
            int addBudget = rb - ra;
            int chosenK = -1;

            for (int k = addBudget; k >= 0; k--)
            {
                bool match = true;
                for (int i = 0; i < ra; i++)
                {
                    if (!comparer.Invoke(left[prefix + i], right[prefix + k + i], context))
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    chosenK = k;
                    break;
                }
            }

            if (chosenK >= 0)
            {
                for (int i = 0; i < chosenK; i++)
                    writer.WriteSeqAddAt(memberIndex, prefix + i, right[prefix + i]);

                int alignedLen = ra;
                int tailAdds = addBudget - chosenK;
                for (int i = 0; i < tailAdds; i++)
                {
                    int insertIndex = prefix + chosenK + alignedLen + i;
                    writer.WriteSeqAddAt(memberIndex, insertIndex, right[insertIndex]);
                }

                return;
            }
        }

        int common = Math.Min(ra, rb);

        for (int i = 0; i < common; i++)
        {
            int ai = prefix + i;
            if (!comparer.Invoke(left[ai], right[ai], context))
                writer.WriteSeqReplaceAt(memberIndex, ai, right[ai]);
        }

        if (ra > rb)
        {
            for (var i = ra - 1; i >= rb; i--)
                writer.WriteSeqRemoveAt(memberIndex, prefix + i, left[prefix + i]);   // expected value
        }
        else if (rb > ra)
        {
            for (int i = ra; i < rb; i++)
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

        // Duplicate-aware alignment (prefer largest k)
        if (rb >= ra && ra > 0)
        {
            var addBudget = rb - ra;
            var chosenK = -1;

            for (var k = addBudget; k >= 0; k--)
            {
                var match = true;
                for (var i = 0; i < ra; i++)
                {
                    if (!areEqual(left[prefix + i], right[prefix + k + i]))
                    {
                        match = false;
                        break;
                    }
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
            if (!areEqual(left[ai], right[ai])) writer.WriteSeqReplaceAt(memberIndex, ai, right[ai]);
        }

        if (ra > rb)
        {
            for (var i = ra - 1; i >= rb; i--)
                writer.WriteSeqRemoveAt(memberIndex, prefix + i, left[prefix + i]);   // expected
        }
        else if (rb > ra)
        {
            for (var i = ra; i < rb; i++)
                writer.WriteSeqAddAt(memberIndex, prefix + i, right[prefix + i]);
        }
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

        var leftCount = left.Count;
        var rightCount = right.Count;

        if (leftCount == 0 && rightCount == 0)
            return;

        var leftOrder = leftCount == 0 ? Array.Empty<TKey>() : new TKey[leftCount];
        var leftLookup = new Dictionary<TKey, (int Index, T Value)>(leftCount);

        for (int i = 0; i < leftCount; i++)
        {
            var value = left[i];
            var key = keySelector(value);
            leftOrder[i] = key;
            leftLookup[key] = (i, value);
        }

        var rightKeys = rightCount == 0 ? Array.Empty<TKey>() : new TKey[rightCount];
        var rightKeySet = new HashSet<TKey>(EqualityComparer<TKey>.Default);

        for (int i = 0; i < rightCount; i++)
        {
            var key = keySelector(right[i]);
            rightKeys[i] = key;
            rightKeySet.Add(key);
        }

        var elementComparer = new ListElementComparer<T>(context);

        // Removals (preserve original ordering)
        for (int i = 0; i < leftOrder.Length; i++)
        {
            var key = leftOrder[i];
            if (!rightKeySet.Contains(key))
            {
                var entry = leftLookup[key];
                writer.WriteSeqRemoveAt(memberIndex, entry.Index, entry.Value);
            }
        }

        // Adds + nested updates (right-side ordering)
        for (int i = 0; i < rightKeys.Length; i++)
        {
            var key = rightKeys[i];
            var rightValue = right[i];

            if (!leftLookup.TryGetValue(key, out var leftEntry))
            {
                writer.WriteSeqAddAt(memberIndex, i, rightValue);
                continue;
            }

            var leftValue = leftEntry.Value;
            if (elementComparer.AreEqual(leftValue, rightValue))
                continue;

            var lo = (object?)leftValue;
            var ro = (object?)rightValue;

            if (lo is not null && ro is not null && ReferenceEquals(lo.GetType(), ro.GetType()))
            {
                var scope = writer.BeginSeqNestedAt(memberIndex, leftEntry.Index, out var nestedWriter);
                GeneratedHelperRegistry.ComputeDeltaSameType(lo.GetType(), lo, ro, context, ref nestedWriter);
                var had = !nestedWriter.Document.IsEmpty;
                scope.Dispose();
                if (!had) writer.WriteSeqReplaceAt(memberIndex, leftEntry.Index, rightValue);
            }
            else
            {
                writer.WriteSeqReplaceAt(memberIndex, leftEntry.Index, rightValue);
            }
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
                        // *** IMPORTANT: do not create missing keys on DictNested ***
                        var k = (TKey)op.Key!;
                        if (md.TryGetValue(k, out var existing) && existing is not null)
                        {
                            object? cur = existing!;
                            ApplyNestedDictOrSameType(ref cur, op.Nested!);
                            md[k] = (TValue)cur!;
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
                        // Only mutate when present; do not materialize missing keys
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

        // clone path (read-only or null): do NOT materialize missing keys on DictNested
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

            // Any IReadOnlyDictionary<,> — don't mutate via nested delta
            var ifaces = curVal?.GetType().GetInterfaces();
            if (ifaces != null)
            {
                foreach (var it in ifaces)
                {
                    if (it.IsGenericType && it.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
                    {
                        // no-op
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



    private struct ListElementComparer<TElement>
    {
        private readonly ComparisonContext _context;
        private readonly bool _isValueType;
        private readonly bool _isSealedReference;
        private readonly Func<TElement, TElement, ComparisonContext, bool>? _typedComparer;
        private readonly Func<object, object, ComparisonContext, bool>? _sameTypeComparer;
        private Type? _cachedRuntimeType;
        private Func<object, object, ComparisonContext, bool>? _cachedRuntimeComparer;
        private bool _cachedRuntimeHasComparer;

        public ListElementComparer(ComparisonContext context)
        {
            _context = context;

            var elementType = typeof(TElement);
            _isValueType = elementType.IsValueType;
            _isSealedReference = !_isValueType && elementType.IsSealed;

            if (GeneratedHelperRegistry.TryGetTypedComparer<TElement>(out var typed))
            {
                _typedComparer = typed;
                _sameTypeComparer = null;
            }
            else if (GeneratedHelperRegistry.TryGetComparerSameType(elementType, out var comparer))
            {
                _typedComparer = null;
                _sameTypeComparer = comparer;
            }
            else
            {
                _typedComparer = null;
                _sameTypeComparer = null;
            }

            _cachedRuntimeType = null;
            _cachedRuntimeComparer = null;
            _cachedRuntimeHasComparer = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AreEqual(TElement left, TElement right)
        {
            if (_isValueType)
            {
                if (_typedComparer is not null)
                    return _typedComparer(left, right, _context);

                if (_sameTypeComparer is not null)
                {
                    object lo = left!;
                    object ro = right!;
                    return _sameTypeComparer(lo, ro, _context);
                }

                return EqualityComparer<TElement>.Default.Equals(left, right);
            }

            if (ReferenceEquals(left, right))
                return true;

            if (left is null || right is null)
                return false;

            if (_isSealedReference)
            {
                if (_typedComparer is not null)
                    return _typedComparer(left, right, _context);

                if (_sameTypeComparer is not null)
                    return _sameTypeComparer(left, right, _context);

                if (EqualityComparer<TElement>.Default.Equals(left, right))
                    return true;

                return ComparisonHelpers.DeepComparePolymorphic(left, right, _context);
            }

            object ol = left;
            object orr = right;
            var runtimeType = ol.GetType();
            if (!ReferenceEquals(runtimeType, orr.GetType()))
                return ComparisonHelpers.DeepComparePolymorphic(left, right, _context);

            if (ReferenceEquals(runtimeType, typeof(TElement)))
            {
                if (_typedComparer is not null)
                    return _typedComparer(left, right, _context);

                if (_sameTypeComparer is not null)
                    return _sameTypeComparer(ol, orr, _context);
            }

            if (!ReferenceEquals(runtimeType, _cachedRuntimeType))
            {
                _cachedRuntimeHasComparer = GeneratedHelperRegistry.TryGetComparerSameType(runtimeType, out _cachedRuntimeComparer);
                _cachedRuntimeType = runtimeType;
            }

            if (_cachedRuntimeHasComparer && _cachedRuntimeComparer is not null)
                return _cachedRuntimeComparer(ol, orr, _context);

            if (EqualityComparer<TElement>.Default.Equals(left, right))
                return true;

            return ComparisonHelpers.DeepComparePolymorphic(left, right, _context);
        }
    }




    private readonly ref struct ListReadAccessor<TElement>
    {
        private readonly ReadOnlySpan<TElement> _span;
        private readonly IList<TElement>? _list;
        private readonly bool _hasSpan;

        public ListReadAccessor(IList<TElement> source)
        {
            if (source is List<TElement> list)
            {
                _span = CollectionsMarshal.AsSpan(list);
                _list = null;
                _hasSpan = true;
            }
            else if (source is TElement[] array)
            {
                _span = array;
                _list = null;
                _hasSpan = true;
            }
            else
            {
                _span = default;
                _list = source;
                _hasSpan = false;
            }
        }

        public TElement this[int index] => _hasSpan ? _span[index] : _list![index];
    }

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
