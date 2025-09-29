using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DeepEqual.Generator.Shared;

/// <summary>
///     Element comparer that delegates to a provided <see cref="System.Collections.Generic.IEqualityComparer{T}" />.
/// </summary>
public readonly struct DelegatingElementComparer<T>(IEqualityComparer<T>? inner) : IElementComparer<T>
{
    private readonly IEqualityComparer<T> _inner = inner ?? EqualityComparer<T>.Default;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Invoke(T left, T right, ComparisonContext context)
    {
        return _inner.Equals(left, right);
    }
}