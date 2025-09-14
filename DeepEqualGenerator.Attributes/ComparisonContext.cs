using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DeepEqual.Generator.Shared;

public sealed class ComparisonContext
{
    private readonly bool tracking;
    private readonly HashSet<RefPair> visited;
    private readonly Stack<RefPair> stack;

    public ComparisonOptions Options { get; }

    public static ComparisonContext NoTracking { get; } = new ComparisonContext(false, new ComparisonOptions());

    public ComparisonContext() : this(true, new ComparisonOptions()) { }

    public ComparisonContext(ComparisonOptions options) : this(true, options ?? new ComparisonOptions()) { }

    private ComparisonContext(bool enableTracking, ComparisonOptions options)
    {
        tracking = enableTracking;
        Options = options ?? new ComparisonOptions();
        if (tracking)
        {
            visited = new HashSet<RefPair>(RefPair.Comparer.Instance);
            stack = new Stack<RefPair>();
        }
        else
        {
            visited = null!;
            stack = null!;
        }
    }

    public bool Enter(object left, object right)
    {
        if (!tracking) return true;
        var pair = new RefPair(left, right);
        if (!visited.Add(pair)) return false;
        stack.Push(pair);
        return true;
    }

    public void Exit(object left, object right)
    {
        if (!tracking) return;
        if (stack.Count == 0) return;
        var last = stack.Pop();
        visited.Remove(last);
    }

    private readonly struct RefPair
    {
        public readonly object Left;
        public readonly object Right;

        public RefPair(object left, object right)
        {
            Left = left;
            Right = right;
        }

        public sealed class Comparer : IEqualityComparer<RefPair>
        {
            public static readonly Comparer Instance = new Comparer();
            public bool Equals(RefPair x, RefPair y) => ReferenceEquals(x.Left, y.Left) && ReferenceEquals(x.Right, y.Right);
            public int GetHashCode(RefPair obj)
            {
                unchecked
                {
                    int a = RuntimeHelpers.GetHashCode(obj.Left);
                    int b = RuntimeHelpers.GetHashCode(obj.Right);
                    return (a * 397) ^ b;
                }
            }
        }
    }
}
