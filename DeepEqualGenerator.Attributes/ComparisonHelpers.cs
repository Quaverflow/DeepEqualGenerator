using System;
using System.Collections.Generic;

namespace DeepEqual.Generator.Shared;

/// <summary>Shared comparison primitives; written for clarity and AOT-friendliness.</summary>
public static class ComparisonHelpers
{
    public static bool AreEqualStrings(string? left, string? right)
        => string.Equals(left, right, StringComparison.Ordinal);

    public static bool AreEqualEnum<T>(T left, T right) where T : struct, Enum
        => left.Equals(right);

    public static bool AreEqualValue<T>(T left, T right) where T : struct
        => left.Equals(right);

    public static bool AreEqualDateTime(DateTime left, DateTime right)
        => left.Kind == right.Kind && left.Ticks == right.Ticks;

    public static bool AreEqualDateTimeOffset(DateTimeOffset left, DateTimeOffset right)
        => left.Ticks == right.Ticks && left.Offset == right.Offset;

    // New well-knowns
    public static bool AreEqualDateOnly(DateOnly left, DateOnly right)
        => left.DayNumber == right.DayNumber;

    public static bool AreEqualTimeOnly(TimeOnly left, TimeOnly right)
        => left.Ticks == right.Ticks;

    public static bool AreEqualUri(Uri? left, Uri? right)
        => ReferenceEquals(left, right) || left is not null && right is not null && left.Equals(right);

    public static bool AreEqualMemory<T>(Memory<T> left, Memory<T> right)
        => left.Span.SequenceEqual(right.Span);

    public static bool AreEqualReadOnlyMemory<T>(ReadOnlyMemory<T> left, ReadOnlyMemory<T> right)
        => left.Span.SequenceEqual(right.Span);

    /// <summary>Ordered sequence equality with deep element comparison.</summary>
    public static bool AreEqualSequencesOrdered<T>(
        IEnumerable<T>? left,
        IEnumerable<T>? right,
        Func<T, T, ComparisonContext, bool> elementComparer,
        ComparisonContext context)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;

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
    /// Favor ordered where possible; switch to unordered only when you must.
    /// </summary>
    public static bool AreEqualSequencesUnordered<T>(
        IEnumerable<T>? left,
        IEnumerable<T>? right,
        Func<T, T, ComparisonContext, bool> elementComparer,
        ComparisonContext context)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;

        var rightList = right as IList<T> ?? [.. right];
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
        {
            if (!t) return false;
        }

        return true;
    }

    /// <summary>
    /// Array equality (ordered) with shape validation (rank + per-dimension lengths).
    /// Works for both 1-D (SZ arrays) and multi-dimensional arrays.
    /// </summary>
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
        {
            if (left.GetLength(d) != right.GetLength(d)) return false;
        }

        var el = left.GetEnumerator();
        var er = right.GetEnumerator();
        while (true)
        {
            bool ml = el.MoveNext(), mr = er.MoveNext();
            if (ml != mr) return false;
            if (!ml) return true;

            if (!elementComparer((T)el.Current!, (T)er.Current!, context))
                return false;
        }
    }

    /// <summary>
    /// Array equality (unordered/multiset). Validates total element count and
    /// ignores shape. O(n^2) but covers all array shapes without allocations.
    /// </summary>
    public static bool AreEqualArrayUnordered<T>(
        Array? left,
        Array? right,
        Func<T, T, ComparisonContext, bool> elementComparer,
        ComparisonContext context)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;

        if (left.Length != right.Length) return false;

        // Materialize right as a list for marking matches.
        var rightItems = new List<T>(right.Length);
        var er = right.GetEnumerator();
        while (er.MoveNext()) rightItems.Add((T)er.Current!);

        var matched = new bool[rightItems.Count];
        var el = left.GetEnumerator();
        while (el.MoveNext())
        {
            var lv = (T)el.Current!;
            var found = false;
            for (int i = 0; i < rightItems.Count; i++)
            {
                if (matched[i]) continue;
                if (elementComparer(lv, rightItems[i], context))
                {
                    matched[i] = true;
                    found = true;
                    break;
                }
            }
            if (!found) return false;
        }

        for (int i = 0; i < matched.Length; i++)
            if (!matched[i]) return false;

        return true;
    }

    /// <summary>Dictionary equality: matching keys, deep compare values.</summary>
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

    /// <summary>
    /// Dictionary equality for any IEnumerable&lt;KeyValuePair&lt;TKey,TValue&gt;&gt; sources.
    /// Accepts IDictionary&lt;,&gt; and IReadOnlyDictionary&lt;,&gt; transparently by coercing internally.
    /// </summary>
    public static bool AreEqualDictionaries<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>>? left,
        IEnumerable<KeyValuePair<TKey, TValue>>? right,
        Func<TValue, TValue, ComparisonContext, bool> valueComparer,
        ComparisonContext context) where TKey : notnull
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;

        var ld = ToDictionary(left);
        var rd = ToDictionary(right);

        if (ld.Count != rd.Count) return false;

        foreach (var kv in ld)
        {
            if (!rd.TryGetValue(kv.Key, out var rv)) return false;
            if (!valueComparer(kv.Value, rv, context)) return false;
        }
        return true;

        static Dictionary<TKey, TValue> ToDictionary(IEnumerable<KeyValuePair<TKey, TValue>> src)
        {
            if (src is IDictionary<TKey, TValue> id) return new Dictionary<TKey, TValue>(id);
            var d = new Dictionary<TKey, TValue>();
            foreach (var kv in src) d[kv.Key] = kv.Value;
            return d;
        }
    }
}
