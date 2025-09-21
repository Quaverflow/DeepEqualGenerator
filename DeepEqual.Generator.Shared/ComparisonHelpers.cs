using System;
using System.Collections.Generic;
using System.Linq;

namespace DeepEqual.Generator.Shared;

public static class ComparisonHelpers
{
    public static bool AreEqualStrings(string? a, string? b, ComparisonContext? context)
    {
        var comp = context?.Options.StringComparison ?? StringComparison.Ordinal;
        if (comp == StringComparison.Ordinal) return a == b;
        return string.Equals(a, b, comp);
    }

    public static bool ArraysEqual<T>(T[]? a, T[]? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        if (a.Length != b.Length)
        {
            return false;
        }

        return a.SequenceEqual(b);
    }

    public static bool AreEqualEnum<T>(T a, T b) where T : struct, Enum
    {
        return EqualityComparer<T>.Default.Equals(a, b);
    }

    public static bool AreEqualDateTime(DateTime a, DateTime b)
    {
        return a.Kind == b.Kind && a.Ticks == b.Ticks;
    }

    public static bool AreEqualDateTimeOffset(DateTimeOffset a, DateTimeOffset b)
    {
        return a.Offset == b.Offset && a.UtcTicks == b.UtcTicks;
    }

    public static bool AreEqualDateOnly(DateOnly a, DateOnly b)
    {
        return a.DayNumber == b.DayNumber;
    }

    public static bool AreEqualTimeOnly(TimeOnly a, TimeOnly b)
    {
        return a.Ticks == b.Ticks;
    }

    public static bool AreEqualSingle(float a, float b, ComparisonContext? context)
    {
        if (float.IsNaN(a) || float.IsNaN(b))
        {
            return context?.Options.TreatNaNEqual == true && float.IsNaN(a) && float.IsNaN(b);
        }

        var eps = context?.Options.FloatEpsilon ?? 0f;
        if (eps <= 0f)
        {
            return a.Equals(b);
        }

        return Math.Abs(a - b) <= eps;
    }

    public static bool AreEqualDouble(double a, double b, ComparisonContext? context)
    {
        if (double.IsNaN(a) || double.IsNaN(b))
        {
            return context?.Options.TreatNaNEqual == true && double.IsNaN(a) && double.IsNaN(b);
        }

        var eps = context?.Options.DoubleEpsilon ?? 0.0;
        if (eps <= 0.0)
        {
            return a.Equals(b);
        }

        return Math.Abs(a - b) <= eps;
    }

    public static bool AreEqualDecimal(decimal a, decimal b, ComparisonContext? context)
    {
        var eps = context?.Options.DecimalEpsilon ?? 0m;
        if (eps <= 0m)
        {
            return a.Equals(b);
        }

        return Math.Abs(a - b) <= eps;
    }

    public static bool AreEqualArrayRank1<TElement, TComparer>(TElement[]? a, TElement[]? b, TComparer comparer,
        ComparisonContext context)
        where TComparer : IElementComparer<TElement>
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        if (a.Length != b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
            if (!comparer.Invoke(a[i], b[i], context))
            {
                return false;
            }

        return true;
    }

    public static bool AreEqualArray<TElement, TComparer>(Array? a, Array? b, TComparer comparer,
        ComparisonContext context)
        where TComparer : IElementComparer<TElement>
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        if (a.Rank != b.Rank)
        {
            return false;
        }

        for (var d = 0; d < a.Rank; d++)
            if (a.GetLength(d) != b.GetLength(d))
            {
                return false;
            }

        if (a.Length == 0)
        {
            return true;
        }

        var indices = new int[a.Rank];
        while (true)
        {
            var va = (TElement)a.GetValue(indices)!;
            var vb = (TElement)b.GetValue(indices)!;
            if (!comparer.Invoke(va, vb, context))
            {
                return false;
            }

            var dim = a.Rank - 1;
            while (dim >= 0)
            {
                indices[dim]++;
                if (indices[dim] < a.GetLength(dim))
                {
                    break;
                }

                indices[dim] = 0;
                dim--;
            }

            if (dim < 0)
            {
                break;
            }
        }

        return true;
    }

    // FILE: DeepEqual.Generator.Shared/ComparisonHelpers.cs

    public static bool AreEqualSequencesOrdered<T, TComparer>(
        IEnumerable<T>? a, IEnumerable<T>? b, TComparer comparer, ComparisonContext context)
        where TComparer : IElementComparer<T>
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;

        // Fast paths: avoid interface enumeration/boxing
        if (a is T[] aa && b is T[] bb)
            return AreEqualArrayRank1<T, TComparer>(aa, bb, comparer, context);

        if (a is IList<T> la && b is IList<T> lb)
        {
            if (la.Count != lb.Count) return false;
            for (int i = 0; i < la.Count; i++)
                if (!comparer.Invoke(la[i], lb[i], context)) return false;
            return true;
        }

        if (a is IReadOnlyList<T> ra && b is IReadOnlyList<T> rb)
        {
            if (ra.Count != rb.Count) return false;
            for (int i = 0; i < ra.Count; i++)
                if (!comparer.Invoke(ra[i], rb[i], context)) return false;
            return true;
        }

        // Fallback: general enumerators
        using var ea = a.GetEnumerator();
        using var eb = b.GetEnumerator();
        while (true)
        {
            var ma = ea.MoveNext();
            var mb = eb.MoveNext();
            if (ma != mb) return false;
            if (!ma) return true;
            if (!comparer.Invoke(ea.Current, eb.Current, context)) return false;
        }
    }

    // FILE: DeepEqual.Generator.Shared/ComparisonHelpers.cs

    public static bool AreEqualSequencesUnordered<T, TComparer>(
        IEnumerable<T>? a, IEnumerable<T>? b, TComparer comparer, ComparisonContext context)
        where TComparer : IElementComparer<T>
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;

        // If both have a count, short‑circuit unequal sizes
        if (a is ICollection<T> ca && b is ICollection<T> cb)
        {
            if (ca.Count != cb.Count) return false;
            if (ca.Count == 0) return true;

            // No‑copy list fast paths
            if (a is IList<T> la && b is IList<T> lb)
                return AreEqualUnorderedIList(la, lb, comparer, context);

            if (a is IReadOnlyList<T> ra && b is IReadOnlyList<T> rb)
                return AreEqualUnorderedOrList(ra, rb, comparer, context);
        }

        // Fallback: previous list copy behavior (preserves semantics)
        var listA = a as List<T> ?? new List<T>(a);
        var listB = b as List<T> ?? new List<T>(b);
        return AreEqualUnordered(listA, listB, comparer, context);
    }

    // No‑copy unordered matcher for IList<T>
    private static bool AreEqualUnorderedIList<T, TComparer>(
        IList<T> a, IList<T> b, TComparer comparer, ComparisonContext context)
        where TComparer : IElementComparer<T>
    {
        if (a.Count != b.Count) return false;
        if (a.Count == 0) return true;

        var matched = new bool[b.Count];
        for (int i = 0; i < a.Count; i++)
        {
            bool found = false;
            var ai = a[i];
            for (int j = 0; j < b.Count; j++)
            {
                if (matched[j]) continue;
                if (comparer.Invoke(ai, b[j], context))
                {
                    matched[j] = true;
                    found = true;
                    break;
                }
            }
            if (!found) return false;
        }
        return true;
    }

    // No‑copy unordered matcher for IReadOnlyList<T>
    private static bool AreEqualUnorderedOrList<T, TComparer>(
        IReadOnlyList<T> a, IReadOnlyList<T> b, TComparer comparer, ComparisonContext context)
        where TComparer : IElementComparer<T>
    {
        if (a.Count != b.Count) return false;
        if (a.Count == 0) return true;

        var matched = new bool[b.Count];
        for (int i = 0; i < a.Count; i++)
        {
            bool found = false;
            var ai = a[i];
            for (int j = 0; j < b.Count; j++)
            {
                if (matched[j]) continue;
                if (comparer.Invoke(ai, b[j], context))
                {
                    matched[j] = true;
                    found = true;
                    break;
                }
            }
            if (!found) return false;
        }
        return true;
    }

    // FILE: DeepEqual.Generator.Shared/ComparisonHelpers.cs

    public static bool AreEqualArrayUnordered<TElement, TComparer>(
        Array? a, Array? b, TComparer comparer, ComparisonContext context)
        where TComparer : IElementComparer<TElement>
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Length != b.Length) return false;

        // Rank‑1 no‑copy fast path
        if (a.Rank == 1 && b.Rank == 1)
        {
            var len = a.Length;
            var matched = new bool[len];
            for (int i = 0; i < len; i++)
            {
                var ai = (TElement)a.GetValue(i)!;
                bool found = false;
                for (int j = 0; j < len; j++)
                {
                    if (matched[j]) continue;
                    var bj = (TElement)b.GetValue(j)!;
                    if (comparer.Invoke(ai, bj, context))
                    {
                        matched[j] = true;
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }
            return true;
        }

        // Fallback for multi‑dimensional arrays: preserve previous behavior
        var listA = new List<TElement>(a.Length);
        var listB = new List<TElement>(b.Length);
        foreach (var o in a) listA.Add((TElement)o!);
        foreach (var o in b) listB.Add((TElement)o!);
        return AreEqualUnordered(listA, listB, comparer, context);
    }

    public static bool AreEqualSequencesUnorderedHash<T>(IEnumerable<T>? a, IEnumerable<T>? b,
        IEqualityComparer<T> equalityComparer) where T : notnull
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        if (a is ICollection<T> ca && b is ICollection<T> cb && ca.Count != cb.Count)
        {
            return false;
        }

        var counts = new Dictionary<T, int>(equalityComparer);
        foreach (var item in a)
        {
            counts.TryGetValue(item, out var n);
            counts[item] = n + 1;
        }

        foreach (var item in b)
        {
            if (!counts.TryGetValue(item, out var n))
            {
                return false;
            }

            if (n == 1)
            {
                counts.Remove(item);
            }
            else
            {
                counts[item] = n - 1;
            }
        }

        return counts.Count == 0;
    }

    public static bool AreEqualReadOnlyMemory<T, TComparer>(ReadOnlyMemory<T> a, ReadOnlyMemory<T> b,
        TComparer comparer, ComparisonContext context)
        where TComparer : IElementComparer<T>
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        var sa = a.Span;
        var sb = b.Span;
        for (var i = 0; i < sa.Length; i++)
            if (!comparer.Invoke(sa[i], sb[i], context))
            {
                return false;
            }

        return true;
    }

    public static bool AreEqualMemory<T, TComparer>(Memory<T> a, Memory<T> b, TComparer comparer,
        ComparisonContext context)
        where TComparer : IElementComparer<T>
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        var sa = a.Span;
        var sb = b.Span;
        for (var i = 0; i < sa.Length; i++)
            if (!comparer.Invoke(sa[i], sb[i], context))
            {
                return false;
            }

        return true;
    }

    private static bool AreEqualUnordered<T, TComparer>(List<T> a, List<T> b, TComparer comparer,
        ComparisonContext context)
        where TComparer : IElementComparer<T>
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        if (a.Count == 0)
        {
            return true;
        }

        var matched = new bool[b.Count];
        for (var i = 0; i < a.Count; i++)
        {
            var found = false;
            for (var j = 0; j < b.Count; j++)
            {
                if (matched[j])
                {
                    continue;
                }

                if (comparer.Invoke(a[i], b[j], context))
                {
                    matched[j] = true;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    public static bool AreEqualDictionariesAny<TKey, TValue, TValueComparer>(
        object? a, object? b, TValueComparer comparer, ComparisonContext context)
        where TValueComparer : IElementComparer<TValue>
        where TKey : notnull
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;

        // 1) Concrete fast-paths — avoid interface enumeration boxing
        if (a is Dictionary<TKey, TValue> da && b is Dictionary<TKey, TValue> db)
        {
            if (da.Count != db.Count) return false;
            foreach (KeyValuePair<TKey, TValue> kv in da)
            {
                if (!db.TryGetValue(kv.Key, out var bv)) return false;
                if (!comparer.Invoke(kv.Value, bv, context)) return false;
            }
            return true;
        }

        // Optional: if you use ReadOnlyDictionary<TKey,TValue>
        if (a is System.Collections.ObjectModel.ReadOnlyDictionary<TKey, TValue> rda &&
            b is System.Collections.ObjectModel.ReadOnlyDictionary<TKey, TValue> rdb)
        {
            if (rda.Count != rdb.Count) return false;
            foreach (KeyValuePair<TKey, TValue> kv in rda)
            {
                if (!rdb.TryGetValue(kv.Key, out var bv)) return false;
                if (!comparer.Invoke(kv.Value, bv, context)) return false;
            }
            return true;
        }

        // 2) Interface fast-paths
        if (a is IReadOnlyDictionary<TKey, TValue> roa && b is IReadOnlyDictionary<TKey, TValue> rob)
        {
            if (roa.Count != rob.Count) return false;
            // IMPORTANT: iterate 'roa' via the interface, but avoid LINQ and do TryGetValue on 'rob'
            foreach (var kv in roa)
            {
                if (!rob.TryGetValue(kv.Key, out var bv)) return false;
                if (!comparer.Invoke(kv.Value, bv, context)) return false;
            }
            return true;
        }

        if (a is IDictionary<TKey, TValue> rwa && b is IDictionary<TKey, TValue> rwb)
        {
            if (rwa.Count != rwb.Count) return false;
            foreach (var kv in rwa)
            {
                if (!rwb.TryGetValue(kv.Key, out var bv)) return false;
                if (!comparer.Invoke(kv.Value, bv, context)) return false;
            }
            return true;
        }

        // 3) Last resort: if either side is not a compatible dictionary type, fall back
        return Equals(a, b);
    }

    public static bool DeepComparePolymorphic<T>(T left, T right, ComparisonContext context)
    {
        if (typeof(T).IsValueType)
        {
            object ol = left!;
            object orr = right!;
            var tl = ol.GetType();
            var tr = orr.GetType();
            if (tl == tr)
            {
                if (GeneratedHelperRegistry.TryCompareSameType(tl, ol, orr, context, out var eqv))
                {
                    return eqv;
                }
            }

            return EqualityComparer<T>.Default.Equals(left, right);
        }
        else
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null)
            {
                return false;
            }

            object ol = left;
            object orr = right;
            var tl = ol.GetType();
            var tr = orr.GetType();
            if (tl != tr)
            {
                return false;
            }

            if (GeneratedHelperRegistry.TryCompareSameType(tl, ol, orr, context, out var eqv))
            {
                return eqv;
            }

            return Equals(left, right);
        }
    }

    public static IEqualityComparer<string> GetStringComparer(ComparisonContext? context)
    {
        var sc = context?.Options.StringComparison ?? StringComparison.Ordinal;
        return sc switch
        {
            StringComparison.Ordinal => StringComparer.Ordinal,
            StringComparison.OrdinalIgnoreCase => StringComparer.OrdinalIgnoreCase,
            StringComparison.InvariantCulture => StringComparer.InvariantCulture,
            StringComparison.InvariantCultureIgnoreCase => StringComparer.InvariantCultureIgnoreCase,
            StringComparison.CurrentCulture => StringComparer.CurrentCulture,
            StringComparison.CurrentCultureIgnoreCase => StringComparer.CurrentCultureIgnoreCase,
            _ => StringComparer.Ordinal
        };
    }

    public static bool EqualsValueLike(object? a, object? b, ComparisonContext? context)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        var ta = a.GetType();
        var tb = b.GetType();
        if (ta != tb)
        {
            return false;
        }

        switch (a)
        {
            case string sa:
                return AreEqualStrings(sa, (string)b, context);
            case float fa:
                return AreEqualSingle(fa, (float)b, context);
            case double da:
                return AreEqualDouble(da, (double)b, context);
            case decimal ma:
                return AreEqualDecimal(ma, (decimal)b, context);
            default:
                return a.Equals(b);
        }
    }

    public sealed class StrictDateTimeComparer : IEqualityComparer<DateTime>
    {
        public static readonly StrictDateTimeComparer Instance = new();

        public bool Equals(DateTime x, DateTime y)
        {
            return x.Kind == y.Kind && x.Ticks == y.Ticks;
        }

        public int GetHashCode(DateTime obj)
        {
            return HashCode.Combine((int)obj.Kind, obj.Ticks);
        }
    }

    public sealed class StrictDateTimeOffsetComparer : IEqualityComparer<DateTimeOffset>
    {
        public static readonly StrictDateTimeOffsetComparer Instance = new();

        public bool Equals(DateTimeOffset x, DateTimeOffset y)
        {
            return x.Offset == y.Offset && x.UtcTicks == y.UtcTicks;
        }

        public int GetHashCode(DateTimeOffset obj)
        {
            return HashCode.Combine(obj.Offset, obj.UtcTicks);
        }
    }
}