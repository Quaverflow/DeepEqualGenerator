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
                    {
                        if ((uint)op.Index < (uint)list.Count)
                            list[op.Index] = (T)op.Value!;
                        return;
                    }

                case DeltaKind.SeqAddAt:
                    {
                        var idx = op.Index;
                        var value = (T)op.Value!;

                        if ((uint)idx <= (uint)list.Count)
                        {
                            if ((uint)idx < (uint)list.Count && EqualityComparer<T>.Default.Equals(list[idx], value))
                                return;

                            if (list.Count + 1 > list.Capacity)
                                list.Capacity = Math.Max(list.Capacity * 2, list.Count + 1);

                            if (idx == list.Count)
                            {
                                list.Add(value);
                            }
                            else
                            {
                                list.Insert(idx, value);
                            }
                        }
                        else
                        {
                            // out-of-range adds are clamped to end (ignore index)
                            list.Add(value);
                        }

                        return;
                    }

                case DeltaKind.SeqRemoveAt:
                    {
                        if ((uint)op.Index < (uint)list.Count)
                            list.RemoveAt(op.Index);
                        return;
                    }

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
                    {
                        if ((uint)op.Index < (uint)ilist.Count)
                            ilist[op.Index] = (T)op.Value!;
                        return;
                    }

                case DeltaKind.SeqAddAt:
                    {
                        var idx = op.Index;
                        var value = (T)op.Value!;

                        if ((uint)idx <= (uint)ilist.Count)
                        {
                            if ((uint)idx < (uint)ilist.Count && EqualityComparer<T>.Default.Equals(ilist[idx], value))
                                return;

                            ilist.Insert(idx, value);
                        }
                        else
                        {
                            // clamp to end
                            ilist.Insert(ilist.Count, value);
                        }

                        return;
                    }

                case DeltaKind.SeqRemoveAt:
                    {
                        if ((uint)op.Index < (uint)ilist.Count)
                            ilist.RemoveAt(op.Index);
                        return;
                    }

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
                {
                    if ((uint)op.Index < (uint)clone.Count)
                        clone[op.Index] = (T)op.Value!;
                    break;
                }

            case DeltaKind.SeqAddAt:
                {
                    var ai = op.Index;
                    if (ai < 0) ai = 0;
                    if (ai > clone.Count) ai = clone.Count;
                    clone.Insert(ai, (T)op.Value!);
                    break;
                }

            case DeltaKind.SeqRemoveAt:
                {
                    if ((uint)op.Index < (uint)clone.Count)
                        clone.RemoveAt(op.Index);
                    break;
                }

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

        // Duplicate-aware alignment (prefer largest k to capture pre-inserts)
        if (rb >= ra && ra > 0)
        {
            var addBudget = rb - ra;
            var chosenK = -1;

            for (var k = addBudget; k >= 0; k--)
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
