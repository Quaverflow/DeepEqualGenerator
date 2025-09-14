using System;
using System.Collections;
using System.Collections.Generic;

namespace DeepEqual.Generator.Shared;

public static class DynamicDeepComparer
{
    public static bool AreEqualDynamic(object? left, object? right, ComparisonContext context)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;

        // Fast paths that respect options
        if (left is string sa && right is string sb)
            return ComparisonHelpers.AreEqualStrings(sa, sb, context);

        if (left is double da && right is double db)
            return ComparisonHelpers.AreEqualDouble(da, db, context);

        if (left is float fa && right is float fb)
            return ComparisonHelpers.AreEqualDouble(fa, fb, context);

        if (left is decimal m1 && right is decimal m2)
            return ComparisonHelpers.AreEqualDecimal(m1, m2, context);

        // Runtime-type match required for structural compare
        var typeLeft = left.GetType();
        var typeRight = right.GetType();
        if (typeLeft != typeRight) return false;

        // Registry-first: if there is a generated helper for this runtime type, use it.
        if (GeneratedHelperRegistry.TryCompare(left, right, context, out var eqFromRegistry))
            return eqFromRegistry;

        // Dictionaries (string/object gets the common Expando/Json case)
        if (left is IDictionary<string, object?> sdictA && right is IDictionary<string, object?> sdictB)
            return EqualStringObjectDictionary(sdictA, sdictB, context);

        if (left is IDictionary dictA && right is IDictionary dictB)
            return EqualNonGenericDictionary(dictA, dictB, context);

        // Arrays: structural, element-by-element
        if (left is Array arrA && right is Array arrB)
        {
            if (arrA.Length != arrB.Length) return false;
            for (int i = 0; i < arrA.Length; i++)
            {
                if (!AreEqualDynamic(arrA.GetValue(i), arrB.GetValue(i), context)) return false;
            }
            return true;
        }

        // Other enumerables (exclude string: already handled)
        if (left is IEnumerable seqA && right is IEnumerable seqB)
            return EqualNonGenericSequence(seqA, seqB, context);

        // Primitive-like and everything else
        if (IsPrimitiveLike(left)) return left.Equals(right);

        // Fallback to .Equals for unknown reference types
        return left.Equals(right);
    }

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

    private static bool EqualNonGenericSequence(IEnumerable a, IEnumerable b, ComparisonContext context)
    {
        var ea = a.GetEnumerator();
        var eb = b.GetEnumerator();
        while (true)
        {
            bool ma = ea.MoveNext();
            bool mb = eb.MoveNext();
            if (ma != mb) return false;
            if (!ma) return true;
            if (!AreEqualDynamic(ea.Current, eb.Current, context)) return false;
        }
    }

    private static bool IsPrimitiveLike(object v)
        => v is bool
        || v is byte || v is sbyte
        || v is short || v is ushort
        || v is int || v is uint
        || v is long || v is ulong
        || v is char
        || v is Guid
        || v is DateTime || v is DateTimeOffset || v is TimeSpan
        || v.GetType().IsEnum;
}
