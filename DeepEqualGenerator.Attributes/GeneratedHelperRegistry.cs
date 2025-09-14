using System;
using System.Collections.Concurrent;

namespace DeepEqual.Generator.Shared;

public static class GeneratedHelperRegistry
{
    private static readonly ConcurrentDictionary<Type, Func<object, object, ComparisonContext, bool>> comparerMap = new();
    // Negative cache: remember types we looked up and found no comparer for
    private static readonly ConcurrentDictionary<Type, bool> negativeCache = new();

    /// <summary>Register a generated comparer for T (called by module initializers in generated files).</summary>
    public static void Register<T>(Func<T, T, ComparisonContext, bool> comparer)
    {
        comparerMap[typeof(T)] = (l, r, c) => comparer((T)l, (T)r, c);
        negativeCache.TryRemove(typeof(T), out _);
    }

    /// <summary>
    /// If a comparer is registered for the runtime type of <paramref name="left"/> (which must equal right's),
    /// invokes it and returns true with the result in <paramref name="equal"/>. Otherwise returns false.
    /// Uses a negative cache to avoid repeated misses.
    /// </summary>
    public static bool TryCompare(object? left, object? right, ComparisonContext context, out bool equal)
    {
        // Reference / null fast paths
        if (ReferenceEquals(left, right)) { equal = true; return true; }
        if (left is null || right is null) { equal = false; return true; }

        var runtimeType = left.GetType();
        if (runtimeType != right.GetType())
        {
            equal = false;
            return true; // types differ -> definitely not equal (treat as handled)
        }

        // If we've already seen that nothing is registered for this type, skip lookup
        if (negativeCache.TryGetValue(runtimeType, out var neg) && neg)
        {
            equal = false;
            return false;
        }

        if (comparerMap.TryGetValue(runtimeType, out var comparer))
        {
            equal = comparer(left, right, context);
            return true;
        }

        // Miss: remember the absence
        negativeCache[runtimeType] = true;
        equal = false;
        return false;
    }
    public static bool TryCompareSameType(Type runtimeType, object left, object right, ComparisonContext context, out bool equal)
    {
        if (negativeCache.ContainsKey(runtimeType))
        {
            equal = false;
            return false;
        }
        if (comparerMap.TryGetValue(runtimeType, out var comparer))
        {
            equal = comparer(left, right, context);
            return true;
        }
        negativeCache[runtimeType] = true;
        equal = false;
        return false;
    }
    public static bool HasComparer(Type runtimeType) => comparerMap.ContainsKey(runtimeType);
}
