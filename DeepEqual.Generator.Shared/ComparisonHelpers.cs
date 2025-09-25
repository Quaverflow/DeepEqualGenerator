using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DeepEqual.Generator.Shared;

public static class ComparisonHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreEqualStrings(string? a, string? b, ComparisonContext? context)
    {
        var comp = context?.Options.StringComparison ?? StringComparison.Ordinal;
        if (comp == StringComparison.Ordinal) return a == b;
        return string.Equals(a, b, comp);
    }

    public static bool ArraysEqual<T>(T[]? a, T[]? b)
    {
        if (ReferenceEquals(a, b)) return true;

        if (a is null || b is null) return false;

        if (a.Length != b.Length) return false;

        return a.SequenceEqual(b);
    }

    public static bool AreEqualEnum<T>(T a, T b) where T : struct, Enum
    {
        return EqualityComparer<T>.Default.Equals(a, b);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreEqualDateTime(DateTime a, DateTime b)
    {
        return a.Kind == b.Kind && a.Ticks == b.Ticks;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreEqualSingle(float a, float b, ComparisonContext? context)
    {
        if (float.IsNaN(a) || float.IsNaN(b))
            return context?.Options.TreatNaNEqual == true && float.IsNaN(a) && float.IsNaN(b);

        var eps = context?.Options.FloatEpsilon ?? 0f;
        if (eps <= 0f) return a.Equals(b);

        return Math.Abs(a - b) <= eps;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreEqualDouble(double a, double b, ComparisonContext? context)
    {
        if (double.IsNaN(a) || double.IsNaN(b))
            return context?.Options.TreatNaNEqual == true && double.IsNaN(a) && double.IsNaN(b);

        var eps = context?.Options.DoubleEpsilon ?? 0.0;
        if (eps <= 0.0) return a.Equals(b);

        return Math.Abs(a - b) <= eps;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreEqualDecimal(decimal a, decimal b, ComparisonContext? context)
    {
        var eps = context?.Options.DecimalEpsilon ?? 0m;
        if (eps <= 0m) return a.Equals(b);

        return Math.Abs(a - b) <= eps;
    }

    public static bool AreEqualArrayRank1<TElement, TComparer>(TElement[]? a, TElement[]? b, TComparer comparer,
        ComparisonContext context)
        where TComparer : IElementComparer<TElement>
    {
        if (ReferenceEquals(a, b)) return true;

        if (a is null || b is null) return false;

        if (a.Length != b.Length) return false;

        for (var i = 0; i < a.Length; i++)
            if (!comparer.Invoke(a[i], b[i], context))
                return false;

        return true;
    }

    public static bool AreEqualArray<TElement, TComparer>(Array? a, Array? b, TComparer comparer,
        ComparisonContext context)
        where TComparer : IElementComparer<TElement>
    {
        if (ReferenceEquals(a, b)) return true;

        if (a is null || b is null) return false;

        if (a.Rank != b.Rank) return false;

        for (var d = 0; d < a.Rank; d++)
            if (a.GetLength(d) != b.GetLength(d))
                return false;

        if (a.Length == 0) return true;

        var indices = new int[a.Rank];
        while (true)
        {
            var va = (TElement)a.GetValue(indices)!;
            var vb = (TElement)b.GetValue(indices)!;
            if (!comparer.Invoke(va, vb, context)) return false;

            var dim = a.Rank - 1;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsHashFriendlyType(Type t)
    {
        // Types with good EqualityComparer<T>.Default and stable GetHashCode
        return t.IsPrimitive
               || t.IsEnum
               || t == typeof(Guid)
               || t == typeof(DateTime)
               || t == typeof(DateTimeOffset)
               || t == typeof(TimeSpan)
               || t == typeof(decimal);
    }

    public static bool AreEqualSequencesOrdered<T, TComparer>(
        IEnumerable<T>? a, IEnumerable<T>? b, TComparer comparer, ComparisonContext context)
        where TComparer : IElementComparer<T>
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;

        if (a is T[] aa && b is T[] bb)
            return AreEqualArrayRank1<T, TComparer>(aa, bb, comparer, context);

        if (a is IList<T> la && b is IList<T> lb)
        {
            if (la.Count != lb.Count) return false;
            for (var i = 0; i < la.Count; i++)
                if (!comparer.Invoke(la[i], lb[i], context))
                    return false;
            return true;
        }

        if (a is IReadOnlyList<T> ra && b is IReadOnlyList<T> rb)
        {
            if (ra.Count != rb.Count) return false;
            for (var i = 0; i < ra.Count; i++)
                if (!comparer.Invoke(ra[i], rb[i], context))
                    return false;
            return true;
        }

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

    public static bool AreEqualSequencesUnordered<T, TComparer>(
        IEnumerable<T>? a, IEnumerable<T>? b, TComparer comparer, ComparisonContext context)
        where TComparer : IElementComparer<T>
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;

        // Cheap count checks when available
        if (a is ICollection<T> ca && b is ICollection<T> cb)
        {
            if (ca.Count != cb.Count) return false;
            if (ca.Count == 0) return true;

            // Prefer O(n) multiset comparison when we can get a good hash-based comparer
            if (typeof(T) == typeof(string))
            {
                var sc = ComparisonHelpers.GetStringComparer(context);
                return AreEqualSequencesUnorderedHash((IEnumerable<string>)(object)a, (IEnumerable<string>)(object)b, (IEqualityComparer<string>)sc);
            }

            if (IsHashFriendlyType(typeof(T)))
            {
                // Use EqualityComparer<T>.Default which is fast for primitives/enums/guids/DateTime/decimal/etc.
                return AreEqualSequencesUnorderedHash((IEnumerable<T>)a, (IEnumerable<T>)b, EqualityComparer<T>.Default);
            }

            if (a is IList<T> la && b is IList<T> lb)
                return AreEqualUnorderedIList(la, lb, comparer, context);

            if (a is IReadOnlyList<T> ra && b is IReadOnlyList<T> rb)
                return AreEqualUnorderedOrList(ra, rb, comparer, context);
        }

        // Fallback: materialize to lists and do O(n^2) matching
        var listA = a as List<T> ?? new List<T>(a);
        var listB = b as List<T> ?? new List<T>(b);
        return AreEqualUnordered(listA, listB, comparer, context);
    }

    // Add inside public static class ComparisonHelpers

    private static bool AreEqualUnorderedIList<T, TComparer>(
        IList<T> a, IList<T> b, TComparer comparer, ComparisonContext context)
        where TComparer : IElementComparer<T>
    {
        if (a.Count != b.Count) return false;
        if (a.Count == 0) return true;

        var matched = new bool[b.Count];
        for (var i = 0; i < a.Count; i++)
        {
            var found = false;
            var ai = a[i];
            for (var j = 0; j < b.Count; j++)
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

    private static bool AreEqualUnorderedOrList<T, TComparer>(
        IReadOnlyList<T> a, IReadOnlyList<T> b, TComparer comparer, ComparisonContext context)
        where TComparer : IElementComparer<T>
    {
        if (a.Count != b.Count) return false;
        if (a.Count == 0) return true;

        var matched = new bool[b.Count];
        for (var i = 0; i < a.Count; i++)
        {
            var found = false;
            var ai = a[i];
            for (var j = 0; j < b.Count; j++)
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
    public static bool AreEqualArrayUnordered<TElement, TComparer>(
        Array? a, Array? b, TComparer comparer, ComparisonContext context)
        where TComparer : IElementComparer<TElement>
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Length != b.Length) return false;

        if (a.Rank == 1 && b.Rank == 1)
        {
            // Hash-based multiset when element type is hash-friendly (or string with context comparer)
            var elemT = typeof(TElement);
            if (elemT == typeof(string))
            {
                var ec = (IEqualityComparer<string>)GetStringComparer(context);
                var ea = a as string[] ?? a.Cast<object?>().Select(o => (string?)o).ToArray()!;
                var eb = b as string[] ?? b.Cast<object?>().Select(o => (string?)o).ToArray()!;
                // null-aware: treat nulls as keys too
                var counts = new Dictionary<string?, int>(new NullAwareStringComparer(ec));
                foreach (var s in ea) counts[s] = counts.TryGetValue(s, out var n) ? n + 1 : 1;
                foreach (var s in eb)
                {
                    if (!counts.TryGetValue(s, out var n)) return false;
                    if (n == 1) counts.Remove(s); else counts[s] = n - 1;
                }
                return counts.Count == 0;
            }

            if (IsHashFriendlyType(elemT))
            {
                // Fast path using EqualityComparer<T>.Default
                var counts = new Dictionary<TElement, int>(EqualityComparer<TElement>.Default);
                for (var i = 0; i < a.Length; i++)
                    counts[(TElement)a.GetValue(i)!] = counts.TryGetValue((TElement)a.GetValue(i)!, out var n) ? n + 1 : 1;
                for (var i = 0; i < b.Length; i++)
                {
                    var v = (TElement)b.GetValue(i)!;
                    if (!counts.TryGetValue(v, out var n)) return false;
                    if (n == 1) counts.Remove(v); else counts[v] = n - 1;
                }
                return counts.Count == 0;
            }

            // Fallback O(n^2)
            var len = a.Length;
            var matched = new bool[len];
            for (var i = 0; i < len; i++)
            {
                var ai = (TElement)a.GetValue(i)!;
                var found = false;
                for (var j = 0; j < len; j++)
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

        // Non-1D arrays: fall back to list conversion
        var listA = new List<TElement>(a.Length);
        var listB = new List<TElement>(b.Length);
        foreach (var o in a) listA.Add((TElement)o!);
        foreach (var o in b) listB.Add((TElement)o!);
        return AreEqualUnordered(listA, listB, comparer, context);
    }

    public static bool AreEqualSequencesUnorderedHash<T>(IEnumerable<T>? a, IEnumerable<T>? b,
        IEqualityComparer<T> equalityComparer) where T : notnull
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

            if (n == 1)
                counts.Remove(item);
            else
                counts[item] = n - 1;
        }

        return counts.Count == 0;
    }

    public static bool AreEqualReadOnlyMemory<T, TComparer>(ReadOnlyMemory<T> a, ReadOnlyMemory<T> b,
        TComparer comparer, ComparisonContext context)
        where TComparer : IElementComparer<T>
    {
        if (a.Length != b.Length) return false;

        var sa = a.Span;
        var sb = b.Span;
        for (var i = 0; i < sa.Length; i++)
            if (!comparer.Invoke(sa[i], sb[i], context))
                return false;

        return true;
    }

    public static bool AreEqualMemory<T, TComparer>(Memory<T> a, Memory<T> b, TComparer comparer,
        ComparisonContext context)
        where TComparer : IElementComparer<T>
    {
        if (a.Length != b.Length) return false;

        var sa = a.Span;
        var sb = b.Span;
        for (var i = 0; i < sa.Length; i++)
            if (!comparer.Invoke(sa[i], sb[i], context))
                return false;

        return true;
    }

    private static bool AreEqualUnordered<T, TComparer>(List<T> a, List<T> b, TComparer comparer,
        ComparisonContext context)
        where TComparer : IElementComparer<T>
    {
        if (a.Count != b.Count) return false;

        if (a.Count == 0) return true;

        var matched = new bool[b.Count];
        for (var i = 0; i < a.Count; i++)
        {
            var found = false;
            for (var j = 0; j < b.Count; j++)
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

    public static bool AreEqualDictionariesAny<TKey, TValue, TValueComparer>(
        object? a, object? b, TValueComparer comparer, ComparisonContext context)
        where TValueComparer : IElementComparer<TValue>
        where TKey : notnull
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;

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

        if (a is ReadOnlyDictionary<TKey, TValue> rda &&
            b is ReadOnlyDictionary<TKey, TValue> rdb)
        {
            if (rda.Count != rdb.Count) return false;
            foreach (KeyValuePair<TKey, TValue> kv in rda)
            {
                if (!rdb.TryGetValue(kv.Key, out var bv)) return false;
                if (!comparer.Invoke(kv.Value, bv, context)) return false;
            }

            return true;
        }

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
            if (ReferenceEquals(tl, tr))
            {
                if (GeneratedHelperRegistry.TryCompareSameType(tl, ol, orr, context, out var eqv))
                    return eqv;
            }
            // Value types: fall back to default equality (no cycles possible)
            return EqualityComparer<T>.Default.Equals(left, right);
        }
        else
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;

            object ol = left;
            object orr = right;
            var tl = ol.GetType();
            var tr = orr.GetType();
            if (!ReferenceEquals(tl, tr))
            {
                // Cross-type numeric comparison (e.g., int vs long, int vs double) when both are numeric boxes.
                if (IsNumericBox(ol) && IsNumericBox(orr))
                {
                    var da = ol is decimal mad ? (double)mad : Convert.ToDouble(ol);
                    var db = orr is decimal mbd ? (double)mbd : Convert.ToDouble(orr);
                    return AreEqualDouble(da, db, context);
                }
                return false;
            }

            // Prefer generated same-type comparers for user/domain types.
            if (GeneratedHelperRegistry.TryCompareSameType(tl, ol, orr, context, out var eqv)) return eqv;

            // Known container shapes — cycles can occur; guard at this level.
            // IDictionary<string, object?>
            if (ol is IDictionary<string, object?> sdictA && orr is IDictionary<string, object?> sdictB)
            {
                if (!context.Enter(ol, orr)) return true;
                try { return AreEqualStringObjectDictionaryPolymorphic(sdictA, sdictB, context); }
                finally { context.Exit(ol, orr); }
            }

            // Non-generic IDictionary
            if (ol is IDictionary dictA && orr is IDictionary dictB)
            {
                if (!context.Enter(ol, orr)) return true;
                try { return AreEqualNonGenericDictionaryPolymorphic(dictA, dictB, context); }
                finally { context.Exit(ol, orr); }
            }

            // Arrays
            if (ol is Array arrA && orr is Array arrB)
            {
                if (arrA.Rank != arrB.Rank) return false;
                if (!context.Enter(ol, orr)) return true;
                try
                {
                    return AreEqualArray<object?, DeepPolymorphicElementComparer<object?>>(arrA, arrB,
                        new DeepPolymorphicElementComparer<object?>(), context);
                }
                finally { context.Exit(ol, orr); }
            }

            // IEnumerable (ordered semantics by default)
            if (ol is IEnumerable seqA && orr is IEnumerable seqB)
            {
                if (!context.Enter(ol, orr)) return true;
                try { return AreEqualNonGenericSequenceOrderedPolymorphic(seqA, seqB, context); }
                finally { context.Exit(ol, orr); }
            }

            // Value-like fallback with options (strings/float/double/decimal with tolerances, etc.)
            if (EqualsValueLike(ol, orr, context)) return true;

            // Last resort
            return ol.Equals(orr);
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
        if (ReferenceEquals(a, b)) return true;

        if (a is null || b is null) return false;

        var ta = a.GetType();
        var tb = b.GetType();
        if (ta != tb) return false;

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

    private sealed class NullAwareStringComparer : IEqualityComparer<string?>
    {
        private readonly IEqualityComparer<string> _inner;
        public NullAwareStringComparer(IEqualityComparer<string> inner) => _inner = inner;
        public bool Equals(string? x, string? y)
        {
            if (x is null || y is null) return x is null && y is null;
            return _inner.Equals(x, y);
        }
        public int GetHashCode(string? obj) => obj is null ? 0 : _inner.GetHashCode(obj);
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

    // --- Helpers for polymorphic container comparison (cycle-aware via caller) ---
    private static bool AreEqualStringObjectDictionaryPolymorphic(
        IDictionary<string, object?> a,
        IDictionary<string, object?> b,
        ComparisonContext ctx)
    {
        if (a.Count != b.Count) return false;
        foreach (var kv in a)
        {
            if (!b.TryGetValue(kv.Key, out var bv)) return false;
            if (!DeepComparePolymorphic(kv.Value, bv, ctx)) return false;
        }
        return true;
    }

    private static bool AreEqualNonGenericDictionaryPolymorphic(
        IDictionary a,
        IDictionary b,
        ComparisonContext ctx)
    {
        if (a.Count != b.Count) return false;
        foreach (DictionaryEntry de in a)
        {
            if (!b.Contains(de.Key)) return false;
            var rv = b[de.Key];
            if (!DeepComparePolymorphic(de.Value, rv, ctx)) return false;
        }
        return true;
    }

    private static bool AreEqualNonGenericSequenceOrderedPolymorphic(
        IEnumerable a,
        IEnumerable b,
        ComparisonContext ctx)
    {
        var ea = a.GetEnumerator();
        var eb = b.GetEnumerator();
        try
        {
            while (true)
            {
                var ma = ea.MoveNext();
                var mb = eb.MoveNext();
                if (ma != mb) return false;
                if (!ma) return true;
                if (!DeepComparePolymorphic(ea.Current, eb.Current, ctx)) return false;
            }
        }
        finally
        {
            (ea as IDisposable)?.Dispose();
            (eb as IDisposable)?.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNumericBox(object o)
    {
        return o is byte or sbyte or short or ushort or int or uint or long or ulong
            or float or double or decimal;
    }
}
