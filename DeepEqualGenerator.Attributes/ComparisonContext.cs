using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DeepEqual.Generator.Attributes;

/// <summary>Tracks visited object pairs to break cycles in reference graphs.</summary>
public sealed class ComparisonContext
{
    private readonly HashSet<ObjectPair> _visited = new(ObjectPair.ReferenceComparer.Instance);

    public bool Enter(object left, object right) => _visited.Add(new ObjectPair(left, right));
    public void Exit(object left, object right) => _visited.Remove(new ObjectPair(left, right));

    private readonly struct ObjectPair(object left, object right)
    {
        private object Left { get; } = left;
        private object Right { get; } = right;

        public sealed class ReferenceComparer : IEqualityComparer<ObjectPair>
        {
            public static readonly ReferenceComparer Instance = new();
            public bool Equals(ObjectPair x, ObjectPair y)
                => ReferenceEquals(x.Left, y.Left) && ReferenceEquals(x.Right, y.Right);
            public int GetHashCode(ObjectPair p)
            {
                unchecked
                {
                    var a = RuntimeHelpers.GetHashCode(p.Left);
                    var b = RuntimeHelpers.GetHashCode(p.Right);
                    return (a * 397) ^ b;
                }
            }
        }
    }
}