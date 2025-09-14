using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace DeepEqual.Generator.Shared;

public static class GeneratedHelperRegistry
{
    private static readonly ConcurrentDictionary<Type, Func<object, object, ComparisonContext, bool>> ComparerMap = new();
    private static readonly ConcurrentDictionary<Type, bool> NegativeCache = new();

    public static void Register<T>(Func<T, T, ComparisonContext, bool> comparer)
    {
        var t = typeof(T);
        ComparerMap[t] = (l, r, c) => comparer((T)l, (T)r, c);
        NegativeCache.TryRemove(t, out _);     }

    public static bool TryCompare(object left, object right, ComparisonContext context, out bool equal)
    {
        if (ReferenceEquals(left, right)) { equal = true; return true; }
        if (left is null || right is null) { equal = false; return true; }
        var t = left.GetType();
        if (t != right.GetType()) { equal = false; return true; }
        return TryCompareSameType(t, left, right, context, out equal);
    }

    public static bool TryCompareSameType(Type runtimeType, object left, object right, ComparisonContext context, out bool equal)
    {
        if (ComparerMap.TryGetValue(runtimeType, out var comparer))
        {
            equal = comparer(left, right, context);
            return true;
        }

        if (NegativeCache.TryGetValue(runtimeType, out var neg) && neg)
        {
            equal = false;
            return false;
        }

        NegativeCache[runtimeType] = true;
        equal = false;
        return false;
    }

    public static void WarmUp(Type runtimeType)
    {
        var asm = runtimeType.Assembly;
        var ns = runtimeType.Namespace;
        var name = runtimeType.Name; 
        var backtick = name.IndexOf('`');
        if (backtick >= 0)
        {
            name = name[..backtick];
        }

        var helperFullName = (string.IsNullOrEmpty(ns) ? "" : ns + ".") + name + "DeepEqual";
        var helper = asm.GetType(helperFullName, throwOnError: false);
        if (helper != null)
        {
            RuntimeHelpers.RunClassConstructor(helper.TypeHandle);
            NegativeCache.TryRemove(runtimeType, out _);
        }
    }

    public static bool HasComparer(Type runtimeType) => ComparerMap.ContainsKey(runtimeType);
}
