using System;
using System.Collections;
using System.Collections.Generic;

namespace DeepEqual.Generator.Shared;

public interface IElementComparer<in T>
{
    bool Invoke(T left, T right, ComparisonContext context);
}

public static class ComparisonHelpers
{
    public static bool AreEqualStrings(string? a, string? b, ComparisonContext context)
    {
        var comp = context?.Options?.StringComparison ?? StringComparison.Ordinal;
        return string.Equals(a, b, comp);
    }

    public static bool AreEqualEnum<T>(T a, T b) where T : struct, Enum => EqualityComparer<T>.Default.Equals(a, b);

    public static bool AreEqualDateTime(DateTime a, DateTime b) => a.Kind == b.Kind && a.Ticks == b.Ticks;

    public static bool AreEqualDateTimeOffset(DateTimeOffset a, DateTimeOffset b) => a.Offset == b.Offset && a.UtcTicks == b.UtcTicks;

    public static bool AreEqualDateOnly(DateOnly a, DateOnly b) => a.DayNumber == b.DayNumber;

    public static bool AreEqualTimeOnly(TimeOnly a, TimeOnly b) => a.Ticks == b.Ticks;

    public static bool AreEqualSingle(float a, float b, ComparisonContext context)
    {
        if (float.IsNaN(a) || float.IsNaN(b)) return context?.Options?.TreatNaNEqual == true && float.IsNaN(a) && float.IsNaN(b);
        var eps = context?.Options?.FloatEpsilon ?? 0f;
        if (eps <= 0f) return a.Equals(b);
        return Math.Abs(a - b) <= eps;
    }

    public static bool AreEqualDouble(double a, double b, ComparisonContext context)
    {
        if (double.IsNaN(a) || double.IsNaN(b)) return context?.Options?.TreatNaNEqual == true && double.IsNaN(a) && double.IsNaN(b);
        var eps = context?.Options?.DoubleEpsilon ?? 0.0;
        if (eps <= 0.0) return a.Equals(b);
        return Math.Abs(a - b) <= eps;
    }

    public static bool AreEqualDecimal(decimal a, decimal b, ComparisonContext context)
    {
        var eps = context?.Options?.DecimalEpsilon ?? 0m;
        if (eps <= 0m) return a.Equals(b);
        return Math.Abs(a - b) <= eps;
    }

    public static bool AreEqualArrayRank1<TElement, TComparer>(TElement[]? a, TElement[]? b, TComparer comparer, ComparisonContext context)
        where TComparer : IElementComparer<TElement>
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (!comparer.Invoke(a[i], b[i], context)) return false;
        }
        return true;
    }

    public static bool AreEqualArray<TElement, TComparer>(Array? a, Array? b, TComparer comparer, ComparisonContext context)
        where TComparer : IElementComparer<TElement>
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Rank != b.Rank) return false;
        for (int d = 0; d < a.Rank; d++)
        {
            if (a.GetLength(d) != b.GetLength(d)) return false;
        }
        if (a.Length == 0) return true;
        var indices = new int[a.Rank];
        while (true)
        {
            var va = (TElement)a.GetValue(indices)!;
            var vb = (TElement)b.GetValue(indices)!;
            if (!comparer.Invoke(va, vb, context)) return false;
            int dim = a.Rank - 1;
            while (dim >= 0)
            {
                indices[dim]++;
                if (indices[dim] < a.GetLength(dim)) break;
                indices[dim] = 0;
                dim--;
            }
            if (dim < 0) break;
        }
        return true;
    }

    public static bool AreEqualArrayUnordered<TElement, TComparer>(Array? a, Array? b, TComparer comparer, ComparisonContext context)
        where TComparer : IElementComparer<TElement>
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Length != b.Length) return false;
        var listA = new List<TElement>(a.Length);
        var listB = new List<TElement>(b.Length);
        foreach (var o in a) listA.Add((TElement)o!);
        foreach (var o in b) listB.Add((TElement)o!);
        return AreEqualUnordered(listA, listB, comparer, context);
    }

    public static bool AreEqualSequencesOrdered<T, TComparer>(IEnumerable<T>? a, IEnumerable<T>? b, TComparer comparer, ComparisonContext context)
        where TComparer : IElementComparer<T>
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        using var ea = a.GetEnumerator();
        using var eb = b.GetEnumerator();
        while (true)
        {
            bool ma = ea.MoveNext();
            bool mb = eb.MoveNext();
            if (ma != mb) return false;
            if (!ma) return true;
            if (!comparer.Invoke(ea.Current, eb.Current, context)) return false;
        }
    }

    public static bool AreEqualSequencesUnordered<T, TComparer>(IEnumerable<T>? a, IEnumerable<T>? b, TComparer comparer, ComparisonContext context)
        where TComparer : IElementComparer<T>
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a is ICollection<T> ca && b is ICollection<T> cb && ca.Count != cb.Count) return false;
        var listA = new List<T>(a);
        var listB = new List<T>(b);
        return AreEqualUnordered(listA, listB, comparer, context);
    }

    public static bool AreEqualSequencesUnorderedHash<T>(IEnumerable<T>? a, IEnumerable<T>? b, IEqualityComparer<T> equalityComparer)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a is ICollection<T> ca && b is ICollection<T> cb && ca.Count != cb.Count) return false;
        var counts = new Dictionary<T, int>(equalityComparer);
        foreach (var item in a)
        {
            counts.TryGetValue(item, out var n);
            counts[item] = n + 1;
        }
        foreach (var item in b)
        {
            if (!counts.TryGetValue(item, out var n)) return false;
            if (n == 1) counts.Remove(item);
            else counts[item] = n - 1;
        }
        return counts.Count == 0;
    }

    public static bool AreEqualReadOnlyMemory<T, TComparer>(ReadOnlyMemory<T> a, ReadOnlyMemory<T> b, TComparer comparer, ComparisonContext context)
        where TComparer : IElementComparer<T>
    {
        if (a.Length != b.Length) return false;
        var sa = a.Span;
        var sb = b.Span;
        for (int i = 0; i < sa.Length; i++)
        {
            if (!comparer.Invoke(sa[i], sb[i], context)) return false;
        }
        return true;
    }

    public static bool AreEqualMemory<T, TComparer>(Memory<T> a, Memory<T> b, TComparer comparer, ComparisonContext context)
        where TComparer : IElementComparer<T>
    {
        if (a.Length != b.Length) return false;
        var sa = a.Span;
        var sb = b.Span;
        for (int i = 0; i < sa.Length; i++)
        {
            if (!comparer.Invoke(sa[i], sb[i], context)) return false;
        }
        return true;
    }

    private static bool AreEqualUnordered<T, TComparer>(List<T> a, List<T> b, TComparer comparer, ComparisonContext context)
        where TComparer : IElementComparer<T>
    {
        if (a.Count != b.Count) return false;
        if (a.Count == 0) return true;
        var matched = new bool[b.Count];
        for (int i = 0; i < a.Count; i++)
        {
            bool found = false;
            for (int j = 0; j < b.Count; j++)
            {
                if (matched[j]) continue;
                if (comparer.Invoke(a[i], b[j], context))
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

    public static bool AreEqualDictionariesAny<TKey, TValue, TValueComparer>(object? a, object? b, TValueComparer comparer, ComparisonContext context)
        where TValueComparer : IElementComparer<TValue>
        where TKey : notnull
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a is IReadOnlyDictionary<TKey, TValue> roa && b is IReadOnlyDictionary<TKey, TValue> rob)
        {
            if (roa.Count != rob.Count) return false;
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
        return object.Equals(a, b);
    }

    public static bool DeepComparePolymorphic<T>(T left, T right, ComparisonContext context)
    {
        if (typeof(T).IsValueType)
        {
            object ol = left!;
            object orr = right!;
            var tl = ol.GetType();
            var tr = orr.GetType();
            if (tl == tr && GeneratedHelperRegistry.TryCompareSameType(tl, ol, orr, context, out var eqv)) return eqv;
            return EqualityComparer<T>.Default.Equals(left, right);
        }
        else
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            object ol = left!;
            object orr = right!;
            var tl = ol.GetType();
            var tr = orr.GetType();
            if (tl != tr) return false;
            if (GeneratedHelperRegistry.TryCompareSameType(tl, ol, orr, context, out var eqv)) return eqv;
            return object.Equals(left, right);
        }
    }

    public static IEqualityComparer<string> GetStringComparer(ComparisonContext context)
    {
        var sc = context?.Options?.StringComparison ?? StringComparison.Ordinal;
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

    public sealed class StrictDateTimeComparer : IEqualityComparer<DateTime>
    {
        public static readonly StrictDateTimeComparer Instance = new StrictDateTimeComparer();
        public bool Equals(DateTime x, DateTime y) => x.Kind == y.Kind && x.Ticks == y.Ticks;
        public int GetHashCode(DateTime obj) => HashCode.Combine((int)obj.Kind, obj.Ticks);
    }

    public sealed class StrictDateTimeOffsetComparer : IEqualityComparer<DateTimeOffset>
    {
        public static readonly StrictDateTimeOffsetComparer Instance = new StrictDateTimeOffsetComparer();
        public bool Equals(DateTimeOffset x, DateTimeOffset y) => x.Offset == y.Offset && x.UtcTicks == y.UtcTicks;
        public int GetHashCode(DateTimeOffset obj) => HashCode.Combine(obj.Offset, obj.UtcTicks);
    }
}
