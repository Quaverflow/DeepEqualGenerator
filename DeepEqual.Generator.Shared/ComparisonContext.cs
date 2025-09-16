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
