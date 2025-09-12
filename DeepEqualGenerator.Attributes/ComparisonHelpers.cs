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

        var rightList = right as IList<T> ?? [..right];
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
}