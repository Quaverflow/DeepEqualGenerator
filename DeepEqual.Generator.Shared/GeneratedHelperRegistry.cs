using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace DeepEqual.Generator.Shared;

public static class GeneratedHelperRegistry
{
    private static readonly ConcurrentDictionary<Type, Func<object, object, ComparisonContext, bool>> _eqMap = new();
    private static readonly ConcurrentDictionary<Type, bool> _negativeEqCache = new();

    private static readonly ConcurrentDictionary<Type, DiffFunc> _diffMap = new();
    private static readonly ConcurrentDictionary<Type, ComputeDeltaObjFunc> _deltaComputeMap = new();
    private static readonly ConcurrentDictionary<Type, ApplyDeltaObjFunc> _deltaApplyObjMap = new();

    public delegate bool DiffFunc(object left, object right, ComparisonContext context, out IDiff diff);

    public delegate void ComputeDeltaObjFunc(object? left, object? right, ref DeltaWriter writer);
    public delegate void ApplyDeltaObjFunc(ref object? target, ref DeltaReader reader);

    public delegate void ComputeDeltaRef<T>(T? left, T? right, ref DeltaWriter writer);
    public delegate void ApplyDeltaRef<T>(ref T? target, ref DeltaReader reader);

    public static void RegisterComparer<T>(Func<T, T, ComparisonContext, bool> comparer)
    {
        var t = typeof(T);
        _eqMap[t] = (l, r, c) => comparer((T)l, (T)r, c);
        _negativeEqCache.TryRemove(t, out _);
    }

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
        if (_eqMap.TryGetValue(runtimeType, out var cmp))
        {
            equal = cmp(left, right, context);
            return true;
        }

        if (TryCompareAssignable(runtimeType, left, right, context, out equal))
        {
            return true;
        }

        if (_negativeEqCache.TryGetValue(runtimeType, out var neg) && neg)
        {
            equal = false;
            return false;
        }

        _negativeEqCache[runtimeType] = true;
        equal = false;
        return false;
    }

    private static bool TryCompareAssignable(Type runtimeType, object left, object right, ComparisonContext context, out bool equal)
    {
        for (var t = runtimeType.BaseType; t != null; t = t.BaseType)
        {
            if (_eqMap.TryGetValue(t, out var cmp))
            {
                equal = cmp(left, right, context);
                return true;
            }
        }

        foreach (var i in runtimeType.GetInterfaces())
        {
            if (_eqMap.TryGetValue(i, out var cmp))
            {
                equal = cmp(left, right, context);
                return true;
            }
        }

        equal = false;
        return false;
    }

    public static void WarmUp(Type runtimeType)
    {
        var asm = runtimeType.Assembly;
        var ns = runtimeType.Namespace;
        var name = runtimeType.Name;
        var backtick = name.IndexOf('`');
        if (backtick >= 0) name = name[..backtick];

        var helperEquality = (string.IsNullOrEmpty(ns) ? "" : ns + ".") + name + "DeepEqual";
        var helperDiffDelta = (string.IsNullOrEmpty(ns) ? "" : ns + ".") + name + "DeepOps";

        var typeEq = asm.GetType(helperEquality, throwOnError: false);
        var typeOps = asm.GetType(helperDiffDelta, throwOnError: false);

        if (typeEq != null) RuntimeHelpers.RunClassConstructor(typeEq.TypeHandle);
        if (typeOps != null) RuntimeHelpers.RunClassConstructor(typeOps.TypeHandle);

        _negativeEqCache.TryRemove(runtimeType, out _);
    }

    public static void RegisterDiff<T>(Func<T?, T?, ComparisonContext, (bool hasDiff, Diff<T> diff)> difffer)
    {
        var t = typeof(T);
        _diffMap[t] = (object a, object b, ComparisonContext c, out IDiff outDiff) =>
        {
            var (has, d) = difffer((T?)a, (T?)b, c);
            outDiff = d;
            return has;
        };
    }

    public static bool TryGetDiffSameType(Type runtimeType, object left, object right, ComparisonContext ctx, out IDiff diff)
    {
        if (_diffMap.TryGetValue(runtimeType, out var fn))
        {
            return fn(left, right, ctx, out diff);
        }
        diff = Diff.Empty;
        return false;
    }

    public static void RegisterDelta<T>(
        ComputeDeltaRef<T> compute,
        ApplyDeltaRef<T> apply)
    {
        var t = typeof(T);

        _deltaComputeMap[t] = (object? left, object? right, ref DeltaWriter w) =>
        {
            compute((T?)left, (T?)right, ref w);
        };

        _deltaApplyObjMap[t] = (ref object? target, ref DeltaReader r) =>
        {
            T? local = (T?)target;
            apply(ref local, ref r);
            target = local;
        };
    }

    public static void ComputeDeltaSameType(Type runtimeType, object? left, object? right, ref DeltaWriter writer)
    {
        if (_deltaComputeMap.TryGetValue(runtimeType, out var fn))
        {
            fn(left, right, ref writer);
        }
    }

    public static bool TryApplyDeltaSameType(Type runtimeType, ref object? target, ref DeltaReader reader)
    {
        if (_deltaApplyObjMap.TryGetValue(runtimeType, out var fn))
        {
            fn(ref target, ref reader);
            return true;
        }
        return false;
    }
}
