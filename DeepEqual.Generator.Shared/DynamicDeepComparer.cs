using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DeepEqual.Generator.Shared;

public static class DynamicDeepComparer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool AreEqualDynamic(object? left, object? right, ComparisonContext context)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;

        // Fast value-like cases first
        if (left is string sa && right is string sb) return ComparisonHelpers.AreEqualStrings(sa, sb, context);
        if (left is double da && right is double db) return ComparisonHelpers.AreEqualDouble(da, db, context);
        if (left is float fa && right is float fb) return ComparisonHelpers.AreEqualSingle(fa, fb, context);
        if (left is decimal m1 && right is decimal m2) return ComparisonHelpers.AreEqualDecimal(m1, m2, context);
        if (IsNumeric(left) && IsNumeric(right)) return NumericEqual(left, right, context);

        var typeLeft = left.GetType();
        var typeRight = right.GetType();
        if (!ReferenceEquals(typeLeft, typeRight)) return false;

        // Prefer generated same-type comparers for user types (including those that implement IEnumerable)
        if (GeneratedHelperRegistry.TryCompareSameType(typeLeft, left, right, context, out var eqFromRegistry))
            return eqFromRegistry;

        // Known container shapes
        if (left is IDictionary<string, object?> sdictA && right is IDictionary<string, object?> sdictB)
            return EqualStringObjectDictionary(sdictA, sdictB, context);

        if (left is IDictionary dictA && right is IDictionary dictB)
            return EqualNonGenericDictionary(dictA, dictB, context);

        if (left is Array arrA && right is Array arrB)
        {
            // Rank must match
            if (arrA.Rank != arrB.Rank) return false;

            return ComparisonHelpers.AreEqualArray<object?, DeepPolymorphicElementComparer<object?>>(
                arrA,
                arrB,
                new DeepPolymorphicElementComparer<object?>(),
                context
            );
        }

        if (left is IList listA && right is IList listB)
            return EqualNonGenericList(listA, listB, context);

        if (left is IEnumerable seqA && right is IEnumerable seqB)
            return EqualNonGenericSequence(seqA, seqB, context);

        // Primitive-like fallback
        if (IsPrimitiveLike(left)) return left.Equals(right);

        return left.Equals(right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool EqualNonGenericList(IList a, IList b, ComparisonContext context)
    {
        var n = a.Count;
        if (n != b.Count) return false;
        for (var i = 0; i < n; i++)
            if (!AreEqualDynamic(a[i], b[i], context))
                return false;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool EqualStringObjectDictionary(
        IDictionary<string, object?> a,
        IDictionary<string, object?> b,
        ComparisonContext context)
    {
        if (a.Count != b.Count) return false;

        foreach (var kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out var rb)) return false;
            if (!AreEqualDynamic(kvp.Value, rb, context)) return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool EqualNonGenericDictionary(IDictionary a, IDictionary b, ComparisonContext context)
    {
        if (a.Count != b.Count) return false;

        foreach (DictionaryEntry de in a)
        {
            if (!b.Contains(de.Key)) return false;
            var rv = b[de.Key];
            if (!AreEqualDynamic(de.Value, rv, context)) return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool EqualNonGenericSequence(IEnumerable a, IEnumerable b, ComparisonContext context)
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
                if (!AreEqualDynamic(ea.Current, eb.Current, context)) return false;
            }
        }
        finally
        {
            (ea as IDisposable)?.Dispose();
            (eb as IDisposable)?.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsPrimitiveLike(object v)
    {
        return v is bool
               || v is byte or sbyte
               || v is short or ushort
               || v is int or uint
               || v is long or ulong
               || v is char
               || v is Guid
               || v is DateTime or DateTimeOffset or TimeSpan
               || v.GetType().IsEnum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNumeric(object o)
    {
        return o is byte or sbyte or short or ushort or int or uint or long or ulong
            or float or double or decimal;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool NumericEqual(object a, object b, ComparisonContext context)
    {
        if (a is float or double or decimal || b is float or double or decimal)
        {
            var da = a is decimal mad ? (double)mad : Convert.ToDouble(a);
            var db = b is decimal mbd ? (double)mbd : Convert.ToDouble(b);
            return ComparisonHelpers.AreEqualDouble(da, db, context);
        }

        var va = Convert.ToDecimal(a);
        var vb = Convert.ToDecimal(b);
        return va == vb;
    }
}
