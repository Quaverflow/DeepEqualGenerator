using System;
using System.Collections.Concurrent;

namespace DeepEqual.Generator.Shared;

public static class GeneratedHelperRegistry
{
        private static readonly ConcurrentDictionary<Type, Func<object, object, ComparisonContext, bool>> _map = new();

    public static void Register<T>(Func<T, T, ComparisonContext, bool> comparer)
    {
        _map[typeof(T)] = (l, r, c) => comparer((T)l, (T)r, c);
    }

    public static bool TryCompareSameType(Type t, object l, object r, ComparisonContext ctx, out bool equal)
    {
        if (_map.TryGetValue(t, out var f))
        {
            equal = f(l, r, ctx); return true;
        }
        equal = false; return false;
    }
}