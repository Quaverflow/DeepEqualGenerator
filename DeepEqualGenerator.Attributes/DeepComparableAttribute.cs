using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DeepEqual.Generator.Attributes;

public enum CompareKind
{
    Deep,
    Shallow, 
    Reference, 
    Skip       
}

/// <summary>
/// Put this on any class/struct you want a separate DeepEqual class generated for (root entry points).
/// Nested types are included by default; you do NOT have to annotate them.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class DeepComparableAttribute : Attribute
{
    /// <summary>Compare IEnumerable&lt;T&gt; as unordered (multiset) by default at the type level.</summary>
    public bool OrderInsensitiveCollections { get; set; }

}

/// <summary>
/// Per-member or per-type override. Apply to:
///  - a property/field (to control that member), or
///  - a class/struct (to set the default for that type wherever it appears).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class DeepCompareAttribute : Attribute
{
    public CompareKind Kind { get; set; } = CompareKind.Deep;

    /// <summary>
    /// When the target is a sequence (array or IEnumerable&lt;T&gt;), choose unordered matching (multiset).
    /// If not set, the generator falls back to the parent type's default or ordered.
    /// </summary>
    public bool OrderInsensitive { get; set; } = true;
}

/// <summary>Tracks visited object pairs to break cycles in reference graphs.</summary>
public sealed class ComparisonContext
{
    private readonly HashSet<ObjectPair> _visited = new(ObjectPair.ReferenceComparer.Instance);

    public bool Enter(object left, object right) => _visited.Add(new ObjectPair(left, right));
    public void Exit(object left, object right) => _visited.Remove(new ObjectPair(left, right));

    private readonly struct ObjectPair(object left, object right)
    {
        private object Left { get; } = left;
        private object Right { get; } = right;

        public sealed class ReferenceComparer : IEqualityComparer<ObjectPair>
        {
            public static readonly ReferenceComparer Instance = new();
            public bool Equals(ObjectPair x, ObjectPair y)
                => ReferenceEquals(x.Left, y.Left) && ReferenceEquals(x.Right, y.Right);
            public int GetHashCode(ObjectPair p)
            {
                unchecked
                {
                    var a = RuntimeHelpers.GetHashCode(p.Left);
                    var b = RuntimeHelpers.GetHashCode(p.Right);
                    return (a * 397) ^ b;
                }
            }
        }
    }
}

/// <summary>Shared comparison primitives; written for clarity and AOT-friendliness.</summary>
public static class ComparisonHelpers
{
    public static bool AreEqualStrings(string? left, string? right)
        => string.Equals(left, right, StringComparison.Ordinal);

    public static bool AreEqualEnum<T>(T left, T right) where T : struct, Enum
        => left.Equals(right);

    public static bool AreEqualValue<T>(T left, T right) where T : struct
        => left.Equals(right);

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