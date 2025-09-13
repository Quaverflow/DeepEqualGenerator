// DeepEqual.Generator.Shared/ComparisonHelpers.cs
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;

namespace DeepEqual.Generator.Shared;

/// <summary>Shared comparison primitives; optimized but clear. Includes list/dictionary fast paths and count prechecks.</summary>
public static class ComparisonHelpers
{
    // -------- simple values --------

    public static bool AreEqualStrings(string? left, string? right)
        => string.Equals(left, right, StringComparison.Ordinal);

    public static bool AreEqualEnum<T>(T left, T right) where T : struct, Enum
        => left.Equals(right);

    public static bool AreEqualDateTime(DateTime left, DateTime right)
        => left.Kind == right.Kind && left.Ticks == right.Ticks;

    public static bool AreEqualDateTimeOffset(DateTimeOffset left, DateTimeOffset right)
        => left.Ticks == right.Ticks && left.Offset == right.Offset;

    public static bool AreEqualDateOnly(DateOnly left, DateOnly right) => left.DayNumber == right.DayNumber;
    public static bool AreEqualTimeOnly(TimeOnly left, TimeOnly right) => left.Ticks == right.Ticks;

    // -------- sequences (ordered/unordered) --------

    private static bool TryGetNonEnumeratingCount<T>(IEnumerable<T>? seq, out int count)
    {
        switch (seq)
        {
            case ICollection<T> c: count = c.Count; return true;
            case IReadOnlyCollection<T> rc: count = rc.Count; return true;
            case ICollection nc: count = nc.Count; return true;
            default: count = 0; return false;
        }
    }

    /// <summary>Ordered sequence equality with deep element comparison. Fast-paths IList/IReadOnlyList indexers.</summary>
    public static bool AreEqualSequencesOrdered<T>(
        IEnumerable<T>? left,
        IEnumerable<T>? right,
        Func<T, T, ComparisonContext, bool> elementComparer,
        ComparisonContext context)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;

        // length precheck
        if (TryGetNonEnumeratingCount(left, out var lc) &&
            TryGetNonEnumeratingCount(right, out var rc) &&
            lc != rc)
            return false;

        // IList<T>
        if (left is IList<T> la && right is IList<T> lb)
        {
            int n = la.Count;
            if (n != lb.Count) return false;
            for (int i = 0; i < n; i++)
                if (!elementComparer(la[i], lb[i], context)) return false;
            return true;
        }

        // IReadOnlyList<T>
        if (left is IReadOnlyList<T> rla && right is IReadOnlyList<T> rlb)
        {
            int n = rla.Count;
            if (n != rlb.Count) return false;
            for (int i = 0; i < n; i++)
                if (!elementComparer(rla[i], rlb[i], context)) return false;
            return true;
        }

        // general enumerator path
        using var el = left.GetEnumerator();
        using var er = right.GetEnumerator();

        while (true)
        {
            var ml = el.MoveNext();
            var mr = er.MoveNext();
            if (ml != mr) return false;
            if (!ml) return true;

            if (!elementComparer(el.Current, er.Current, context))
                return false;
        }
    }

    /// <summary>
    /// Unordered (multiset) sequence equality using deep element comparison.
    /// O(n^2) worst-case but correct without requiring a hash/comparer for T.
    /// </summary>
    public static bool AreEqualSequencesUnordered<T>(
        IEnumerable<T>? left,
        IEnumerable<T>? right,
        Func<T, T, ComparisonContext, bool> elementComparer,
        ComparisonContext context)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;

        // length precheck
        if (TryGetNonEnumeratingCount(left, out var lc) &&
            TryGetNonEnumeratingCount(right, out var rc) &&
            lc != rc)
            return false;

        var rightList = right as IList<T> ?? new List<T>(right);
        var matched = new bool[rightList.Count];

        foreach (var item in left)
        {
            var found = false;
            for (var i = 0; i < rightList.Count; i++)
            {
                if (matched[i]) continue;
                if (elementComparer(item, rightList[i], context))
                {
                    matched[i] = true;
                    found = true;
                    break;
                }
            }
            if (!found) return false;
        }

        foreach (var t in matched)
            if (!t) return false;

        return true;
    }

    // -------- arrays --------

    public static bool AreEqualArrayRank1<T>(
        T[]? left,
        T[]? right,
        Func<T, T, ComparisonContext, bool> elementComparer,
        ComparisonContext context)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        if (left.Length != right.Length) return false;

        for (int i = 0; i < left.Length; i++)
            if (!elementComparer(left[i], right[i], context)) return false;

        return true;
    }

    public static bool AreEqualArray<T>(
        Array? left,
        Array? right,
        Func<T, T, ComparisonContext, bool> elementComparer,
        ComparisonContext context)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        if (left.Rank != right.Rank) return false;

        for (int d = 0; d < left.Rank; d++)
            if (left.GetLength(d) != right.GetLength(d)) return false;

        var ea = left.GetEnumerator();
        var eb = right.GetEnumerator();
        while (true)
        {
            bool ma = ea.MoveNext();
            bool mb = eb.MoveNext();
            if (ma != mb) return false;
            if (!ma) return true;

            if (!elementComparer((T)ea.Current!, (T)eb.Current!, context))
                return false;
        }
    }

    public static bool AreEqualArrayUnordered<T>(
        Array? left,
        Array? right,
        Func<T, T, ComparisonContext, bool> elementComparer,
        ComparisonContext context)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;

        // compare total element counts first
        int Count(Array a)
        {
            int c = 1;
            for (int d = 0; d < a.Rank; d++) c *= a.GetLength(d);
            return c;
        }
        if (left.Rank != right.Rank) return false;
        if (Count(left) != Count(right)) return false;

        // flatten right to a list, then multiset match
        var rightList = new List<T>(Count(right));
        foreach (var item in right) rightList.Add((T)item!);
        var matched = new bool[rightList.Count];

        int idx = 0;
        foreach (var item in left)
        {
            var x = (T)item!;
            var found = false;
            for (int i = 0; i < rightList.Count; i++)
            {
                if (matched[i]) continue;
                if (elementComparer(x, rightList[i], context))
                {
                    matched[i] = true;
                    found = true;
                    break;
                }
            }
            if (!found) return false;
            idx++;
        }
        foreach (var m in matched) if (!m) return false;
        return true;
    }

    // -------- dictionaries --------

    /// <summary>Typed dictionary fast path (IDictionary).</summary>
    public static bool AreEqualDictionaries<TKey, TValue>(
        IDictionary<TKey, TValue>? left,
        IDictionary<TKey, TValue>? right,
        Func<TValue, TValue, ComparisonContext, bool> valueComparer,
        ComparisonContext context) where TKey : notnull
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        if (left.Count != right.Count) return false;

        foreach (var x in left)
        {
            if (!right.TryGetValue(x.Key, out var rv)) return false;
            if (!valueComparer(x.Value, rv, context)) return false;
        }
        return true;
    }

    /// <summary>Typed dictionary fast path (IReadOnlyDictionary).</summary>
    public static bool AreEqualReadOnlyDictionaries<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue>? left,
        IReadOnlyDictionary<TKey, TValue>? right,
        Func<TValue, TValue, ComparisonContext, bool> valueComparer,
        ComparisonContext context) where TKey : notnull
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        if (left.Count != right.Count) return false;

        foreach (var x in left)
        {
            if (!right.TryGetValue(x.Key, out var rv)) return false;
            if (!valueComparer(x.Value, rv, context)) return false;
        }
        return true;
    }

    /// <summary>Flexible entry that tries IDictionary and IReadOnlyDictionary before falling back to enumerable KVPs (may copy).</summary>
    public static bool AreEqualDictionariesAny<TKey, TValue>(
        object? left,
        object? right,
        Func<TValue, TValue, ComparisonContext, bool> valueComparer,
        ComparisonContext context) where TKey : notnull
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;

        if (left is IDictionary<TKey, TValue> ld && right is IDictionary<TKey, TValue> rd)
            return AreEqualDictionaries(ld, rd, valueComparer, context);

        if (left is IReadOnlyDictionary<TKey, TValue> lrd && right is IReadOnlyDictionary<TKey, TValue> rrd)
            return AreEqualReadOnlyDictionaries(lrd, rrd, valueComparer, context);

        // Fallback: treat as sequences (will materialize right side to a map once)
        var le = left as IEnumerable<KeyValuePair<TKey, TValue>>;
        var re = right as IEnumerable<KeyValuePair<TKey, TValue>>;
        return AreEqualDictionaryEnumerables(le, re, valueComparer, context);
    }

    /// <summary>Enumerable KVP fallback (builds a lookup for the right side once).</summary>
    public static bool AreEqualDictionaryEnumerables<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>>? left,
        IEnumerable<KeyValuePair<TKey, TValue>>? right,
        Func<TValue, TValue, ComparisonContext, bool> valueComparer,
        ComparisonContext context) where TKey : notnull
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;

        var rmap = new Dictionary<TKey, TValue>();
        int rcount = 0;
        foreach (var kv in right) { rmap[kv.Key] = kv.Value; rcount++; }

        int lcount = 0;
        foreach (var kv in left)
        {
            lcount++;
            if (!rmap.TryGetValue(kv.Key, out var rv)) return false;
            if (!valueComparer(kv.Value, rv, context)) return false;
        }
        return lcount == rcount;
    }
}
