using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DeepEqual.Generator.Shared;

/// <summary>
///     Per-call comparison context:
///     - Holds options
///     - (Optionally) tracks visited object pairs to break cycles
///     - Provides thread-local, reset-per-call "no-tracking" instance to be safe under parallel usage
/// </summary>
public sealed class ComparisonContext
{
    /// <summary>
    ///     Thread-local, reset-per-call context with cycle tracking disabled and default options.
    ///     Safe from parallel races (no shared mutable state across threads or calls).
    /// </summary>
    [ThreadStatic] private static ComparisonContext? _cachedNoTracking;

    private readonly bool _tracking;
    private Stack<RefPair>? _stack;
    private HashSet<RefPair>? _visited;

    /// <summary>Creates a context with cycle tracking enabled and default options.</summary>
    public ComparisonContext() : this(true, new ComparisonOptions())
    {
    }

    /// <summary>Creates a context with cycle tracking enabled and explicit options.</summary>
    public ComparisonContext(ComparisonOptions? options) : this(true, options ?? new ComparisonOptions())
    {
    }

    internal ComparisonContext(bool trackCycles, ComparisonOptions? options)
    {
        _tracking = trackCycles;
        Options = options ?? new ComparisonOptions();
    }

    public ComparisonOptions Options { get; private set; }

    /// <summary>
    ///     Reusable thread-local context with cycle tracking disabled and default options.
    ///     Safe because when tracking==false the context keeps no per-call mutable state.
    /// </summary>
    public static ComparisonContext NoTracking => _cachedNoTracking ??= new ComparisonContext(false, null);


    /// <summary>
    ///     Enter a (left,right) object pair. Returns false if we've already visited the pair (cycle).
    ///     When cycle tracking is disabled, returns true and performs no bookkeeping.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Enter(object left, object right)
    {
        if (!_tracking) return true;

        _visited ??= new HashSet<RefPair>(RefPair.Comparer.Instance);
        _stack ??= new Stack<RefPair>();

        var pair = new RefPair(left, right);
        if (!_visited.Add(pair)) return false;

        _stack.Push(pair);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Exit(object left, object right)
    {
        if (!_tracking || _stack is null || _visited is null) return;
        if (_stack.Count == 0) return;

        var last = _stack.Pop();
        _visited.Remove(last);
    }


    private readonly struct RefPair
    {
        private readonly object _left;
        private readonly object _right;

        public RefPair(object left, object right)
        {
            _left = left;
            _right = right;
        }

        public sealed class Comparer : IEqualityComparer<RefPair>
        {
            public static readonly Comparer Instance = new();

            public bool Equals(RefPair x, RefPair y)
            {
                return ReferenceEquals(x._left, y._left) && ReferenceEquals(x._right, y._right);
            }

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

/// <summary>
///     Strongly-typed structural diff for T.
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

    public static Diff<T> Replacement(T? newValue)
    {
        return new Diff<T>(true, true, newValue, null);
    }

    public static Diff<T> Members(List<MemberChange> changes)
    {
        if (changes.Count == 0) return Empty;

        return new Diff<T>(true, false, default, changes);
    }

    public bool IsEmpty => !HasChanges;
}