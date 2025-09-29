using System.Runtime.CompilerServices;

namespace DeepEqual.Generator.Shared;

/// <summary>
///     Element comparer that performs generated deep comparison when possible; falls back appropriately for polymorphic
///     graphs.
/// </summary>
public readonly struct DeepPolymorphicElementComparer<T> : IElementComparer<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Invoke(T left, T right, ComparisonContext context)
    {
        return ComparisonHelpers.DeepComparePolymorphic(left, right, context);
    }
}