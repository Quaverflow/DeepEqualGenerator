using System;
using System.Collections.Concurrent;

namespace DeepEqual.Generator.Shared;

public static class GeneratedHelperRegistry
{
    private static readonly ConcurrentDictionary<Type, Func<object, object, ComparisonContext, bool>> comparerMap = new();
    private static readonly ConcurrentDictionary<Type, bool> negativeCache = new();

    public static void Register<T>(Func<T, T, ComparisonContext, bool> comparer)
    {
        comparerMap[typeof(T)] = (l, r, c) => comparer((T)l, (T)r, c);
        negativeCache.TryRemove(typeof(T), out _);
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