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

        var typeLeft = left.GetType();
        var typeRight = right.GetType();
        if (typeLeft != typeRight) return false;

        if (GeneratedHelperRegistry.TryCompareSameType(typeLeft, left, right, context, out var equalFromRegistry))
        {
            return equalFromRegistry;
        }

        if (left is string ls && right is string rs) return ComparisonHelpers.AreEqualStrings(ls, rs, context);

        if (left is DateTime ldt && right is DateTime rdt) return ComparisonHelpers.AreEqualDateTime(ldt, rdt);
        if (left is DateTimeOffset ldo && right is DateTimeOffset rdo) return ComparisonHelpers.AreEqualDateTimeOffset(ldo, rdo);
        if (left is TimeSpan lts && right is TimeSpan rts) return lts.Ticks == rts.Ticks;
        if (left is Guid lg && right is Guid rg) return lg.Equals(rg);
#if NET6_0_OR_GREATER
        if (left is DateOnly ldonly && right is DateOnly rdonly) return ComparisonHelpers.AreEqualDateOnly(ldonly, rdonly);
        if (left is TimeOnly ltonly && right is TimeOnly rtonly) return ComparisonHelpers.AreEqualTimeOnly(ltonly, rtonly);
#endif

        if (typeLeft.IsEnum) return left.Equals(right);

        if (left is IDictionary dictA && right is IDictionary dictB)
        {
            return EqualNonGenericDictionary(dictA, dictB, context);
        }

        if (left is IEnumerable seqA && right is IEnumerable seqB)
        {
            return EqualNonGenericSequence(seqA, seqB, context);
        }

        if (IsPrimitiveLike(left)) return left.Equals(right);

        return object.Equals(left, right);
    }

    private static bool IsPrimitiveLike(object value)
    {
        var t = value.GetType();
        if (t.IsPrimitive) return true;
        if (t == typeof(decimal)) return true;
        return false;
    }

    private static bool EqualNonGenericDictionary(IDictionary a, IDictionary b, ComparisonContext context)
    {
        if (a.Count != b.Count) return false;
        foreach (DictionaryEntry entry in a)
        {
            if (!b.Contains(entry.Key)) return false;
            var bv = b[entry.Key];
            if (!AreEqualDynamic(entry.Value, bv, context)) return false;
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
}
