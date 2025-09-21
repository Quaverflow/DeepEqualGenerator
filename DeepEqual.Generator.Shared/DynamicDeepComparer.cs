using System;
using System.Collections;
using System.Collections.Generic;

namespace DeepEqual.Generator.Shared;

public static class DynamicDeepComparer
{
    // FILE: DeepEqual.Generator.Shared/DynamicDeepComparer.cs

    public static bool AreEqualDynamic(object? left, object? right, ComparisonContext context)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;

        // Value-like fast paths with options-aware comparison
        if (left is string sa && right is string sb)
            return ComparisonHelpers.AreEqualStrings(sa, sb, context);

        if (left is double da && right is double db)
            return ComparisonHelpers.AreEqualDouble(da, db, context);

        if (left is float fa && right is float fb)
            return ComparisonHelpers.AreEqualSingle(fa, fb, context); // <-- bugfix: use Single overload

        if (left is decimal m1 && right is decimal m2)
            return ComparisonHelpers.AreEqualDecimal(m1, m2, context);

        if (IsNumeric(left) && IsNumeric(right))
            return NumericEqual(left, right, context);

        var typeLeft = left.GetType();
        var typeRight = right.GetType();
        if (!ReferenceEquals(typeLeft, typeRight)) return false;

        // Collections first (avoid interface scanning/alloc in registry lookup)
        if (left is IDictionary<string, object?> sdictA && right is IDictionary<string, object?> sdictB)
            return EqualStringObjectDictionary(sdictA, sdictB, context);

        if (left is IDictionary dictA && right is IDictionary dictB)
            return EqualNonGenericDictionary(dictA, dictB, context);

        if (left is Array arrA && right is Array arrB)
        {
            if (arrA.Length != arrB.Length) return false;
            for (var i = 0; i < arrA.Length; i++)
                if (!AreEqualDynamic(arrA.GetValue(i), arrB.GetValue(i), context)) return false;
            return true;
        }

        // Non-generic IList<T> and List<T> still implement non-generic IList
        if (left is IList listA && right is IList listB)
            return EqualNonGenericList(listA, listB, context);

        if (left is IEnumerable seqA && right is IEnumerable seqB)
            return EqualNonGenericSequence(seqA, seqB, context);

        if (GeneratedHelperRegistry.TryCompareSameType(typeLeft, left, right, context, out var eqFromRegistry))
            return eqFromRegistry;

        // Primitives & enums & other value-like stuff
        if (IsPrimitiveLike(left)) return left.Equals(right);

        // Fallback
        return left.Equals(right);
    }
    private static bool EqualNonGenericList(IList a, IList b, ComparisonContext context)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (!AreEqualDynamic(a[i], b[i], context)) return false;
        return true;
    }

    private static bool EqualStringObjectDictionary(
        IDictionary<string, object?> a,
        IDictionary<string, object?> b,
        ComparisonContext context)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out var rb))
            {
                return false;
            }

            if (!AreEqualDynamic(kvp.Value, rb, context))
            {
                return false;
            }
        }

        return true;
    }

    private static bool EqualNonGenericDictionary(IDictionary a, IDictionary b, ComparisonContext context)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (DictionaryEntry de in a)
        {
            if (!b.Contains(de.Key))
            {
                return false;
            }

            var rv = b[de.Key];
            if (!AreEqualDynamic(de.Value, rv, context))
            {
                return false;
            }
        }

        return true;
    }

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
                if (ma != mb)
                {
                    return false;
                }

                if (!ma)
                {
                    return true;
                }

                if (!AreEqualDynamic(ea.Current, eb.Current, context))
                {
                    return false;
                }
            }
        }
        finally
        {
            (ea as IDisposable)?.Dispose();
            (eb as IDisposable)?.Dispose();
        }
    }

    private static bool IsPrimitiveLike(object v)
    {
        return v is bool
               || v is byte || v is sbyte
               || v is short || v is ushort
               || v is int || v is uint
               || v is long || v is ulong
               || v is char
               || v is Guid
               || v is DateTime || v is DateTimeOffset || v is TimeSpan
               || v.GetType().IsEnum;
    }

    private static bool IsNumeric(object o)
    {
        return o is byte or sbyte or short or ushort or int or uint or long or ulong
            or float or double or decimal;
    }

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