using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DeepEqual.Generator.Shared;

/// <summary>
///     Default element comparer that defers to <see cref="EqualityComparer{T}.Default" />.
/// </summary>
public readonly struct DefaultElementComparer<T> : IElementComparer<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Invoke(T left, T right, ComparisonContext context)
    {
        if (left is string sa && right is string sb) return ComparisonHelpers.AreEqualStrings(sa, sb, context);

        if (left is double da && right is double db) return ComparisonHelpers.AreEqualDouble(da, db, context);

        if (left is float fa && right is float fb) return ComparisonHelpers.AreEqualSingle(fa, fb, context);

        if (left is decimal ma && right is decimal mb) return ComparisonHelpers.AreEqualDecimal(ma, mb, context);

        return EqualityComparer<T>.Default.Equals(left, right);
    }
}