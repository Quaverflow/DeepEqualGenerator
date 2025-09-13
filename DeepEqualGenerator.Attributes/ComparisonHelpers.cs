// DeepEqual.Generator.Shared/ComparisonHelpers.cs
#nullable enable
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeepEqual.Generator.Shared;

/// <summary>
/// Shared comparison primitives used by the generated code.
/// Includes span-based fast paths for arrays/lists, pooled buffers for unordered compares,
/// and optimized dictionary equality that iterates the smaller side first.
/// </summary>
public static class ComparisonHelpers
{
    // ---------------- simple values ----------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreEqualStrings(string? left, string? right)
        => string.Equals(left, right, StringComparison.Ordinal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreEqualEnum<T>(T left, T right) where T : struct, Enum
        => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreEqualDateTime(DateTime left, DateTime right)
        => left.Kind == right.Kind && left.Ticks == right.Ticks;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreEqualDateTimeOffset(DateTimeOffset left, DateTimeOffset right)
        => left.Ticks == right.Ticks && left.Offset == right.Offset;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreEqualDateOnly(DateOnly left, DateOnly right)
        => left.DayNumber == right.DayNumber;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreEqualTimeOnly(TimeOnly left, TimeOnly right)
        => left.Ticks == right.Ticks;

    // ---------------- sequence helpers ----------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetNonEnumeratingCount<T>(IEnumerable<T>? seq, out int count)
    {
        switch (seq)
        {
            case ICollection<T> c:
                count = c.Count; return true;
            case IReadOnlyCollection<T> rc:
                count = rc.Count; return true;
            case ICollection nc:
                count = nc.Count; return true;
            default:
                count = 0; return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetSpan<T>(IEnumerable<T> seq, out ReadOnlySpan<T> span)
    {
        if (seq is T[] arr) { span = arr; return true; }
#if NET5_0_OR_GREATER
        if (seq is List<T> list) { span = CollectionsMarshal.AsSpan(list); return true; }
#endif
        span = default;
        return false;
    }

    /// <summary>
    /// Ordered sequence equality with deep element comparison.
    /// Fast paths: O(1) Count precheck, T[]/List&lt;T&gt; span/indexer loops, SIMD for byte[].
    /// </summary>
    public static bool AreEqualSequencesOrdered<T>(
        IEnumerable<T>? left,
        IEnumerable<T>? right,
        Func<T, T, ComparisonContext, bool> elementComparer,
        ComparisonContext context)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;

        // O(1) Count precheck (when available)
        if (TryGetNonEnumeratingCount(left, out var lc) &&
            TryGetNonEnumeratingCount(right, out var rc) &&
            lc != rc)
        {
            return false;
        }

        // Span/List fast path (both sides must expose a span)
        if (TryGetSpan(left, out var ls) && TryGetSpan(right, out var rs))
        {
            if (ls.Length != rs.Length) return false;

            if (typeof(T) == typeof(byte))
            {
                // byte[] vs byte[]
                if (left is byte[] lx && right is byte[] rb)
                {
                    return lx.AsSpan().SequenceEqual(rb);
                }

#if NET5_0_OR_GREATER
                if (left is List<byte> lbl && right is List<byte> rbl)
                {
                    return CollectionsMarshal.AsSpan(lbl).SequenceEqual(CollectionsMarshal.AsSpan(rbl));
                }
#endif
            }
            for (int i = 0; i < ls.Length; i++)
            {
                if (!elementComparer(ls[i], rs[i], context)) return false;
            }
            return true;
        }

        // IList<T> fast path
        if (left is IList<T> la && right is IList<T> lb)
        {
            int n = la.Count;
            if (n != lb.Count) return false;
            for (int i = 0; i < n; i++)
                if (!elementComparer(la[i], lb[i], context)) return false;
            return true;
        }

        // IReadOnlyList<T> fast path
        if (left is IReadOnlyList<T> rla && right is IReadOnlyList<T> rlb)
        {
            int n = rla.Count;
            if (n != rlb.Count) return false;
            for (int i = 0; i < n; i++)
                if (!elementComparer(rla[i], rlb[i], context)) return false;
            return true;
        }

        // Enumerator fallback
        using var el = left.GetEnumerator();
        using var er = right.GetEnumerator();
        while (true)
        {
            var ml = el.MoveNext();
            var mr = er.MoveNext();
            if (ml != mr) return false;
            if (!ml) return true;
            if (!elementComparer(el.Current, er.Current, context)) return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsHashFriendly<T>()
    {
        var t = typeof(T);
        return t.IsPrimitive || t.IsEnum ||
               t == typeof(string) ||
               t == typeof(Guid) ||
               t == typeof(DateTime) ||
               t == typeof(DateTimeOffset);
    }

    /// <summary>
    /// Unordered (multiset) sequence equality.
    /// - Hash-multiset fast path for primitives/enums/string/Guid/DateTime/DateTimeOffset (no delegate invokes).
    /// - Pooled O(n²) matcher for deep comparers (no permanent allocations).
    /// </summary>
    public static bool AreEqualSequencesUnordered<T>(
        IEnumerable<T>? left,
        IEnumerable<T>? right,
        Func<T, T, ComparisonContext, bool> elementComparer,
        ComparisonContext context)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;

        if (TryGetNonEnumeratingCount(left, out var lc) &&
            TryGetNonEnumeratingCount(right, out var rc) &&
            lc != rc)
        {
            return false;
        }

        // Hash-multiset for "cheap" values
        if (IsHashFriendly<T>())
        {
            int capacity = 0;
            if (TryGetNonEnumeratingCount(right, out var rc2)) capacity = rc2;

            var counts = capacity > 0
                ? new Dictionary<T, int>(capacity, EqualityComparer<T>.Default)
                : new Dictionary<T, int>(EqualityComparer<T>.Default);

            foreach (var x in right)
            {
                if (counts.TryGetValue(x, out var c)) counts[x] = c + 1;
                else counts[x] = 1;
            }

            foreach (var x in left)
            {
                if (!counts.TryGetValue(x, out var c)) return false;
                if (c == 1) counts.Remove(x);
                else counts[x] = c - 1;
            }

            return counts.Count == 0;
        }

        // Deep comparer path: pool buffers, O(n²) matching
        var poolT = ArrayPool<T>.Shared;
        var poolB = ArrayPool<bool>.Shared;

        // materialize right into a pooled array
        T[] rArr;
        int n = 0;

        if (right is ICollection<T> rc3)
        {
            rArr = poolT.Rent(rc3.Count);
            foreach (var x in rc3) rArr[n++] = x;
        }
        else
        {
            rArr = poolT.Rent(16);
            foreach (var x in right)
            {
                if (n == rArr.Length)
                {
                    var newArr = poolT.Rent(n * 2);
                    Array.Copy(rArr, newArr, n);
                    poolT.Return(rArr, clearArray: false);
                    rArr = newArr;
                }
                rArr[n++] = x;
            }
        }

        var matched = poolB.Rent(n);
        Array.Clear(matched, 0, n);

        try
        {
            foreach (var lx in left)
            {
                bool found = false;
                for (int i = 0; i < n; i++)
                {
                    if (matched[i]) continue;
                    if (elementComparer(lx, rArr[i], context))
                    {
                        matched[i] = true;
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }

            for (int i = 0; i < n; i++)
                if (!matched[i]) return false;

            return true;
        }
        finally
        {
            poolB.Return(matched, clearArray: true);
            poolT.Return(rArr, clearArray: false);
        }
    }

    // ---------------- arrays ----------------

    /// <summary>Rank-1 array equality with indexer loop; SIMD for byte[].</summary>
    public static bool AreEqualArrayRank1<T>(
        T[]? left,
        T[]? right,
        Func<T, T, ComparisonContext, bool> elementComparer,
        ComparisonContext context)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        if (left.Length != right.Length) return false;

        if (typeof(T) == typeof(byte))
        {
            if (left is byte[] lb && right is byte[] rb)
                return lb.AsSpan().SequenceEqual(rb);
        }
        for (int i = 0; i < left.Length; i++)
            if (!elementComparer(left[i], right[i], context)) return false;

        return true;
    }

    /// <summary>General (any-rank) array equality; shape precheck + elementwise compare.</summary>
    public static bool AreEqualArray<T>(
        Array? left,
        Array? right,
        Func<T, T, ComparisonContext, bool> elementComparer,
        ComparisonContext context)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;

        if (left.Rank != right.Rank) return false;

        if (left.Rank == 1)
        {
            if (left.Length != right.Length) return false;
            if (left.Length == 0) return true;

            if (left is T[] la && right is T[] ra)
            {
                int n = la.Length;
                for (int i = 0; i < n; i++)
                {
                    if (!elementComparer(la[i], ra[i], context)) return false;
                }
                return true;
            }

            int lbL = left.GetLowerBound(0);
            int lbR = right.GetLowerBound(0);
            int n1 = left.Length;

            for (int i = 0; i < n1; i++)
            {
                if (!elementComparer((T)left.GetValue(lbL + i)!, (T)right.GetValue(lbR + i)!, context))
                    return false;
            }
            return true;
        }

        for (int d = 0; d < left.Rank; d++)
        {
            if (left.GetLength(d) != right.GetLength(d)) return false;
            // Optional (stricter) lower-bound check; uncomment if you want to treat different lower bounds as unequal:
            // if (left.GetLowerBound(d) != right.GetLowerBound(d)) return false;
        }

        var ea = left.GetEnumerator();
        var eb = right.GetEnumerator();
        while (true)
        {
            bool ma = ea.MoveNext();
            bool mb = eb.MoveNext();
            if (ma != mb) return false;
            if (!ma) return true;
            if (!elementComparer((T)ea.Current!, (T)eb.Current!, context)) return false;
        }
    }

    /// <summary>Array multiset equality: flattens right once (pooled) and matches with deep comparer.</summary>
    public static bool AreEqualArrayUnordered<T>(
        Array? left,
        Array? right,
        Func<T, T, ComparisonContext, bool> elementComparer,
        ComparisonContext context)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;

        if (left.Rank != right.Rank) return false;

        static int Total(Array a)
        {
            int c = 1;
            for (int d = 0; d < a.Rank; d++) c *= a.GetLength(d);
            return c;
        }

        int lc = Total(left);
        int rc = Total(right);
        if (lc != rc) return false;

        // flatten right into pooled array
        var pool = ArrayPool<T>.Shared;
        var rArr = pool.Rent(rc);
        int n = 0;
        foreach (var item in right) rArr[n++] = (T)item!;

        var matched = ArrayPool<bool>.Shared.Rent(n);
        Array.Clear(matched, 0, n);

        try
        {
            foreach (var item in left)
            {
                var x = (T)item!;
                bool found = false;
                for (int i = 0; i < n; i++)
                {
                    if (matched[i]) continue;
                    if (elementComparer(x, rArr[i], context))
                    {
                        matched[i] = true;
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }

            for (int i = 0; i < n; i++)
                if (!matched[i]) return false;

            return true;
        }
        finally
        {
            ArrayPool<bool>.Shared.Return(matched, clearArray: true);
            pool.Return(rArr, clearArray: false);
        }
    }

    // ---------------- dictionaries ----------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool DictCore<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> a,
        IReadOnlyDictionary<TKey, TValue> b,
        Func<TValue, TValue, ComparisonContext, bool> valueComparer,
        ComparisonContext context) where TKey : notnull
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Count != b.Count) return false;

        // iterate the smaller map for better cache behavior
        var small = a.Count <= b.Count ? a : b;
        var large = ReferenceEquals(small, a) ? b : a;

        foreach (var kv in small)
        {
            if (!large.TryGetValue(kv.Key, out var rv)) return false;
            if (!valueComparer(kv.Value, rv, context)) return false;
        }
        return true;
    }

    /// <summary>Typed dictionary fast path (IDictionary).</summary>
    public static bool AreEqualDictionaries<TKey, TValue>(
        IDictionary<TKey, TValue>? left,
        IDictionary<TKey, TValue>? right,
        Func<TValue, TValue, ComparisonContext, bool> valueComparer,
        ComparisonContext context) where TKey : notnull
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;

        return DictCore(new ReadOnlyDictShim<TKey, TValue>(left),
                        new ReadOnlyDictShim<TKey, TValue>(right),
                        valueComparer, context);
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

        return DictCore(left, right, valueComparer, context);
    }

    /// <summary>
    /// Flexible entry that tries IDictionary and IReadOnlyDictionary before falling back to KVP enumerables.
    /// </summary>
    public static bool AreEqualDictionariesAny<TKey, TValue>(
        object? left,
        object? right,
        Func<TValue, TValue, ComparisonContext, bool> valueComparer,
        ComparisonContext context) where TKey : notnull
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;

        if (left is IDictionary<TKey, TValue> ld && right is IDictionary<TKey, TValue> rd)
            return DictCore(new ReadOnlyDictShim<TKey, TValue>(ld),
                            new ReadOnlyDictShim<TKey, TValue>(rd),
                            valueComparer, context);

        if (left is IReadOnlyDictionary<TKey, TValue> lrd && right is IReadOnlyDictionary<TKey, TValue> rrd)
            return DictCore(lrd, rrd, valueComparer, context);

        // Fallback: treat as enumerable KVPs
        return AreEqualDictionaryEnumerables(left as IEnumerable<KeyValuePair<TKey, TValue>>,
                                             right as IEnumerable<KeyValuePair<TKey, TValue>>,
                                             valueComparer, context);
    }

    /// <summary>
    /// Enumerable KVP fallback (builds a lookup for right once; capacity pre-sized when available).
    /// </summary>
    public static bool AreEqualDictionaryEnumerables<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>>? left,
        IEnumerable<KeyValuePair<TKey, TValue>>? right,
        Func<TValue, TValue, ComparisonContext, bool> valueComparer,
        ComparisonContext context) where TKey : notnull
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;

        int capacity = 0;
        if (right is IReadOnlyCollection<KeyValuePair<TKey, TValue>> rc)
            capacity = rc.Count;
        else if (right is ICollection<KeyValuePair<TKey, TValue>> c)
            capacity = c.Count;

        var rmap = capacity > 0
            ? new Dictionary<TKey, TValue>(capacity)
            : new Dictionary<TKey, TValue>();

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

    // ---------------- internal shims ----------------

    private readonly struct ReadOnlyDictShim<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    {
        private readonly IDictionary<TKey, TValue> _d;
        public ReadOnlyDictShim(IDictionary<TKey, TValue> d) => _d = d;
        public int Count => _d.Count;
        public IEnumerable<TKey> Keys => _d.Keys;
        public IEnumerable<TValue> Values => _d.Values;
        public TValue this[TKey key] => _d[key];
        public bool ContainsKey(TKey key) => _d.ContainsKey(key);
        public bool TryGetValue(TKey key, out TValue value) => _d.TryGetValue(key, out value!);
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _d.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _d.GetEnumerator();
    }

    // ---------------- optional tiny wrappers (kept for inlining opportunities) ----------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CompareByEquals<T>(T l, T r, ComparisonContext _) where T : struct
        => l.Equals(r);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CompareStringsWithContext(string? l, string? r, ComparisonContext _)
        => AreEqualStrings(l, r);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CompareEnumWithContext<T>(T l, T r, ComparisonContext _) where T : struct, Enum
        => l.Equals(r);
}
