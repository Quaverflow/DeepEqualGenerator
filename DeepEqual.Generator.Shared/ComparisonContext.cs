using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DeepEqual.Generator.Shared;

public sealed class ComparisonContext
{
    private readonly bool _tracking;
    private readonly HashSet<RefPair> _visited = new(RefPair.Comparer.Instance);
    private readonly Stack<RefPair> _stack = new();

    public ComparisonOptions Options { get; }

    public static ComparisonContext NoTracking { get; } = new(false, new ComparisonOptions());

    public ComparisonContext() : this(true, new ComparisonOptions()) { }

    public ComparisonContext(ComparisonOptions? options) : this(true, options ?? new ComparisonOptions()) { }

    private ComparisonContext(bool enableTracking, ComparisonOptions? options)
    {
        _tracking = enableTracking;
        Options = options ?? new ComparisonOptions();
    }

    public bool Enter(object left, object right)
    {
        if (!_tracking) return true;

        var pair = new RefPair(left, right);
        if (!_visited.Add(pair)) return false;

        _stack.Push(pair);
        return true;
    }

    public void Exit(object left, object right)
    {
        if (!_tracking) return;

        if (_stack.Count == 0) return;

        var last = _stack.Pop();
        _visited.Remove(last);
    }

    private readonly struct RefPair(object left, object right)
    {
        private readonly object _left = left;
        private readonly object _right = right;

        public sealed class Comparer : IEqualityComparer<RefPair>
        {
            public static readonly Comparer Instance = new();
            public bool Equals(RefPair x, RefPair y) => ReferenceEquals(x._left, y._left) && ReferenceEquals(x._right, y._right);
            public int GetHashCode(RefPair obj)
            {
                unchecked
                {
                    var a = RuntimeHelpers.GetHashCode(obj._left);
                    var b = RuntimeHelpers.GetHashCode(obj._right);
                    return (a * 397) ^ b;
                }
            }
        }
    }
}

    /// <summary> Marker for any diff payload. </summary>
    public interface IDiff
    {
        bool IsEmpty { get; }
    }

    /// <summary>
    /// A non-generic empty diff — for "no info"/"not supported".
    /// </summary>
    public readonly struct Diff : IDiff
    {
        public static readonly Diff Empty = new();
        public bool IsEmpty => true;
    }

    /// <summary>
    /// Strongly-typed structural diff for T.
    /// </summary>
    public readonly struct Diff<T> : IDiff
    {
        public bool HasChanges { get; }
        public bool IsReplacement { get; }
        public T? NewValue { get; }
        public IReadOnlyList<MemberChange>? MemberChanges { get; }

        private Diff(bool has, bool replace, T? newValue, IReadOnlyList<MemberChange>? changes)
        {
            HasChanges = has;
            IsReplacement = replace;
            NewValue = newValue;
            MemberChanges = changes;
        }

        public static Diff<T> Empty => new(false, false, default, null);

        public static Diff<T> Replacement(T? newValue) => new(true, true, newValue, null);

        public static Diff<T> Members(List<MemberChange> changes)
        {
            if (changes.Count == 0) return Empty;
            return new Diff<T>(true, false, default, changes);
        }

        public bool IsEmpty => !HasChanges;
    }

    /// <summary>
    /// A single member diff: either a shallow replacement or a nested diff/delta object.
    /// </summary>
    public readonly record struct MemberChange(
        int MemberIndex,
        MemberChangeKind Kind,
        object? ValueOrDiff);

    public enum MemberChangeKind
    {
        /// <summary> Replace the whole member value (shallow). </summary>
        Set = 0,

        /// <summary> Nested structural diff (ValueOrDiff is IDiff or DeltaDocument). </summary>
        Nested = 1,

        /// <summary> Sequence or dictionary operation list (ValueOrDiff is a DeltaDocument). </summary>
        CollectionOps = 2
    }

public enum CompareKind
{
    Deep,
    Shallow,
    Reference,
    Skip
}

public interface IElementComparer<in T>
{
    bool Invoke(T left, T right, ComparisonContext context);
}
// <summary>
/// Default element comparer that defers to <see cref="System.Collections.Generic.EqualityComparer{T}.Default"/>.
/// </summary>
public readonly struct DefaultElementComparer<T> : IElementComparer<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Invoke(T left, T right, ComparisonContext context)
    {
        if (left is string sa && right is string sb)
            return ComparisonHelpers.AreEqualStrings(sa, sb, context);

        if (left is double da && right is double db)
            return ComparisonHelpers.AreEqualDouble(da, db, context);

        if (left is float fa && right is float fb)
            return ComparisonHelpers.AreEqualSingle(fa, fb, context);

        if (left is decimal ma && right is decimal mb)
            return ComparisonHelpers.AreEqualDecimal(ma, mb, context);

        return EqualityComparer<T>.Default.Equals(left, right);
    }
}

/// <summary>
/// Element comparer that performs generated deep comparison when possible; falls back appropriately for polymorphic graphs.
/// </summary>
public readonly struct DeepPolymorphicElementComparer<T> : IElementComparer<T>
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public bool Invoke(T left, T right, ComparisonContext context)
        => ComparisonHelpers.DeepComparePolymorphic(left, right, context);
}

/// <summary>
/// Element comparer that delegates to a provided <see cref="System.Collections.Generic.IEqualityComparer{T}"/>.
/// </summary>
public readonly struct DelegatingElementComparer<T> : IElementComparer<T>
{
    private readonly System.Collections.Generic.IEqualityComparer<T> _inner;

    public DelegatingElementComparer(System.Collections.Generic.IEqualityComparer<T> inner)
    {
        _inner = inner ?? System.Collections.Generic.EqualityComparer<T>.Default;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public bool Invoke(T left, T right, ComparisonContext context)
        => _inner.Equals(left, right);
}

