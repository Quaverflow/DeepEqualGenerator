using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace DeepEqual.Generator.Shared;

/// <summary>
/// Central registry for generated deep-equality, diff, and delta helpers.
/// </summary>
/// <remarks>
/// This registry avoids runtime reflection in hot paths and is AOT/trimming friendly.
/// </remarks>
public static class GeneratedHelperRegistry
{
    private static readonly ConcurrentDictionary<Type, Func<object, object, ComparisonContext, bool>> _eqMap = new();
    private static readonly ConcurrentDictionary<Type, bool> _negativeEqCache = new();

    private static readonly ConcurrentDictionary<Type, DiffFunc> _diffMap = new();

    private static readonly ConcurrentDictionary<Type, ComputeDeltaObjFunc> _deltaComputeMap = new();
    private static readonly ConcurrentDictionary<Type, ApplyDeltaObjFunc> _deltaApplyObjMap = new();

    /// <summary>
    /// Delegate used for runtime diff dispatch.
    /// </summary>
    public delegate bool DiffFunc(object left, object right, ComparisonContext context, out IDiff diff);

    /// <summary>
    /// Delegate used for runtime delta compute dispatch (object-typed).
    /// </summary>
    public delegate void ComputeDeltaObjFunc(object? left, object? right, ComparisonContext context, ref DeltaWriter writer);

    /// <summary>
    /// Delegate used for runtime delta apply dispatch (object-typed).
    /// </summary>
    public delegate void ApplyDeltaObjFunc(ref object? target, ref DeltaReader reader);

    /// <summary>
    /// Registers a strongly-typed deep equality comparer for runtime dispatch.
    /// </summary>
    public static void RegisterComparer<T>(Func<T, T, ComparisonContext, bool> comparer)
    {
        var t = typeof(T);
        _eqMap[t] = (l, r, c) => comparer((T)l, (T)r, c);
        _negativeEqCache.TryRemove(t, out _);
    }

    /// <summary>
    /// Attempts to compare two objects of the same runtime type using a generated comparer.
    /// </summary>
    public static bool TryCompareSameType(Type runtimeType, object left, object right, ComparisonContext context, out bool equal)
    {
        if (_eqMap.TryGetValue(runtimeType, out var fn))
        {
            equal = fn(left, right, context);
            return true;
        }

        WarmUp(runtimeType);

        if (_eqMap.TryGetValue(runtimeType, out var fn2))
        {
            equal = fn2(left, right, context);
            return true;
        }

        for (var bt = runtimeType.BaseType; bt != null; bt = bt.BaseType)
        {
            if (_eqMap.TryGetValue(bt, out var cmp))
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

    /// <summary>
    /// Attempts to compare two objects; succeeds when both are null, reference-equal, or share the same runtime type with a registered comparer.
    /// </summary>
    public static bool TryCompare(object? left, object? right, ComparisonContext context, out bool equal)
    {
        if (ReferenceEquals(left, right)) { equal = true; return true; }
        if (left is null || right is null) { equal = false; return true; }
        var t = left.GetType();
        if (!ReferenceEquals(t, right.GetType())) { equal = false; return true; }
        return TryCompareSameType(t, left, right, context, out equal);
    }

    /// <summary>
    /// Registers a strongly-typed diff provider for runtime dispatch.
    /// </summary>
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

    /// <summary>
    /// Attempts to compute a diff for two objects of the same runtime type using a registered provider.
    /// </summary>
    public static bool TryGetDiffSameType(Type runtimeType, object left, object right, ComparisonContext ctx, out IDiff diff)
    {
        if (_diffMap.TryGetValue(runtimeType, out var fn))
        {
            return fn(left, right, ctx, out diff);
        }

        WarmUp(runtimeType);

        if (_diffMap.TryGetValue(runtimeType, out var fn2))
        {
            return fn2(left, right, ctx, out diff);
        }

        diff = Diff.Empty;
        return false;
    }

    /// <summary>
    /// Delegate used to register strongly-typed delta compute providers.
    /// </summary>
    public delegate void ComputeDeltaRef<T>(T? left, T? right, ComparisonContext context, ref DeltaWriter writer);

    /// <summary>
    /// Delegate used to register strongly-typed delta apply providers.
    /// </summary>
    public delegate void ApplyDeltaRef<T>(ref T? target, ref DeltaReader reader);

    /// <summary>
    /// Registers strongly-typed delta compute/apply providers for runtime dispatch.
    /// </summary>
    public static void RegisterDelta<T>(ComputeDeltaRef<T> compute, ApplyDeltaRef<T> apply)
    {
        var t = typeof(T);

        _deltaComputeMap[t] = (object? left, object? right, ComparisonContext ctx, ref DeltaWriter w) =>
        {
            compute((T?)left, (T?)right, ctx, ref w);
        };

        _deltaApplyObjMap[t] = (ref object? target, ref DeltaReader r) =>
        {
            T? local = (T?)target;
            apply(ref local, ref r);
            target = local;
        };
    }

    /// <summary>
    /// Computes a delta between two objects of the same runtime type using a registered provider.
    /// </summary>
    public static void ComputeDeltaSameType(Type runtimeType, object? left, object? right, ComparisonContext context, ref DeltaWriter writer)
    {
        if (_deltaComputeMap.TryGetValue(runtimeType, out var fn))
        {
            fn(left, right, context, ref writer);
            return;
        }

        WarmUp(runtimeType);

        if (_deltaComputeMap.TryGetValue(runtimeType, out var fn2))
        {
            fn2(left, right, context, ref writer);
        }
    }

    /// <summary>
    /// Attempts to apply a delta to an object using a registered provider based on the runtime type.
    /// </summary>
    public static bool TryApplyDeltaSameType(Type runtimeType, ref object? target, ref DeltaReader reader)
    {
        if (_deltaApplyObjMap.TryGetValue(runtimeType, out var fn))
        {
            fn(ref target, ref reader);
            return true;
        }

        WarmUp(runtimeType);

        if (_deltaApplyObjMap.TryGetValue(runtimeType, out var fn2))
        {
            fn2(ref target, ref reader);
            return true;
        }

        return false;
    }
    /// <summary>
    /// Attempts to compute a delta for two objects of the same runtime type using a registered provider.
    /// Returns <c>true</c> if a provider was found and invoked; otherwise <c>false</c>.
    /// </summary>
    public static bool TryComputeDeltaSameType(Type runtimeType, object? left, object? right, ComparisonContext context, ref DeltaWriter writer)
    {
        if (_deltaComputeMap.TryGetValue(runtimeType, out var fn))
        {
            fn(left, right, context, ref writer);
            return true;
        }

        WarmUp(runtimeType);

        if (_deltaComputeMap.TryGetValue(runtimeType, out var fn2))
        {
            fn2(left, right, context, ref writer);
            return true;
        }

        return false;
    }
    /// <summary>
    /// Ensures generated helper types for the given runtime type are initialized.
    /// </summary>
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
}
