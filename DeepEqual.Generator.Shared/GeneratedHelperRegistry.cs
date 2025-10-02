using System;
using System.Collections.Concurrent;

namespace DeepEqual.Generator.Shared;

/// <summary>
///     Central registry for generated deep-equality, diff, and delta helpers.
/// </summary>
/// <remarks>
///     This registry avoids runtime reflection in hot paths and is AOT/trimming friendly.
/// </remarks>
public static class GeneratedHelperRegistry
{
    /// <summary>
    ///     Delegate used for runtime delta apply dispatch (object-typed).
    /// </summary>
    public delegate void ApplyDeltaObjFunc(ref object? target, ref DeltaReader reader);

    /// <summary>
    ///     Delegate used to register strongly-typed delta apply providers.
    /// </summary>
    public delegate void ApplyDeltaRef<T>(ref T? target, ref DeltaReader reader);

    /// <summary>
    ///     Delegate used for runtime delta compute dispatch (object-typed).
    /// </summary>
    public delegate void ComputeDeltaObjFunc(object? left, object? right, ComparisonContext context,
        ref DeltaWriter writer);

    /// <summary>
    ///     Delegate used to register strongly-typed delta compute providers.
    /// </summary>
    public delegate void ComputeDeltaRef<T>(T? left, T? right, ComparisonContext context, ref DeltaWriter writer);

    /// <summary>
    ///     Delegate used for runtime diff dispatch.
    /// </summary>
    public delegate bool DiffFunc(object left, object right, ComparisonContext context, out IDiff diff);

    private static readonly ConcurrentDictionary<Type, bool> _eqMiss = new();
    private static readonly ConcurrentDictionary<Type, Func<object, object, ComparisonContext, bool>> _eqMap = new();
    private static readonly ConcurrentDictionary<Type, DiffFunc> _diffMap = new();

    private static readonly ConcurrentDictionary<Type, ComputeDeltaObjFunc> _deltaComputeMap = new();
    private static readonly ConcurrentDictionary<Type, ApplyDeltaObjFunc> _deltaApplyObjMap = new();

    /// <summary>
    ///     Registers a strongly-typed deep equality comparer for runtime dispatch.
    /// </summary>
    public static void RegisterComparer<T>(Func<T, T, ComparisonContext, bool> comparer)
    {
        var t = typeof(T);
        _eqMap[t] = (l, r, c) => comparer((T)l, (T)r, c);
    }

    /// <summary>
    ///     Attempts to compare two objects of the same runtime type using a generated comparer.
    /// </summary>
    public static bool TryCompareSameType(Type runtimeType, object left, object right, ComparisonContext context,
        out bool equal)
    {
        if (_eqMiss.TryGetValue(runtimeType, out _))
        {
            equal = false;
            return false;
        }

        if (_eqMap.TryGetValue(runtimeType, out var fn))
        {
            equal = fn(left, right, context);
            return true;
        }

        for (var bt = runtimeType.BaseType; bt is not null; bt = bt.BaseType)
            if (_eqMap.TryGetValue(bt, out var cmp))
            {
                _eqMap.TryAdd(runtimeType, cmp);
                equal = cmp(left, right, context);
                return true;
            }

        foreach (var i in runtimeType.GetInterfaces())
            if (_eqMap.TryGetValue(i, out var cmp))
            {
                _eqMap.TryAdd(runtimeType, cmp);
                equal = cmp(left, right, context);
                return true;
            }

        _eqMiss.TryAdd(runtimeType, true);
        equal = false;
        return false;
    }


    /// <summary>
    ///     Attempts to retrieve the registered deep equality comparer for the given runtime type.
    /// </summary>
    public static bool TryGetComparerSameType(Type runtimeType, out Func<object, object, ComparisonContext, bool>? comparer)
    {
        if (_eqMap.TryGetValue(runtimeType, out var fn))
        {
            comparer = fn;
            return true;
        }

        if (_eqMiss.TryGetValue(runtimeType, out _))
        {
            comparer = null;
            return false;
        }

        for (var bt = runtimeType.BaseType; bt is not null; bt = bt.BaseType)
            if (_eqMap.TryGetValue(bt, out var baseFn))
            {
                _eqMap.TryAdd(runtimeType, baseFn);
                comparer = baseFn;
                return true;
            }

        foreach (var i in runtimeType.GetInterfaces())
            if (_eqMap.TryGetValue(i, out var ifaceFn))
            {
                _eqMap.TryAdd(runtimeType, ifaceFn);
                comparer = ifaceFn;
                return true;
            }

        _eqMiss.TryAdd(runtimeType, true);
        comparer = null;
        return false;
    }

    /// <summary>
    ///     Attempts to compare two objects; succeeds when both are null, reference-equal, or share the same runtime type with
    ///     a registered comparer.
    /// </summary>
    public static bool TryCompare(object? left, object? right, ComparisonContext context, out bool equal)
    {
        if (ReferenceEquals(left, right))
        {
            equal = true;
            return true;
        }

        if (left is null || right is null)
        {
            equal = false;
            return true;
        }

        var t = left.GetType();
        if (!ReferenceEquals(t, right.GetType()))
        {
            equal = false;
            return true;
        }

        return TryCompareSameType(t, left, right, context, out equal);
    }

    /// <summary>
    ///     Registers a strongly-typed diff provider for runtime dispatch.
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
    ///     Attempts to compute a diff for two objects of the same runtime type using a registered provider.
    /// </summary>
    public static bool TryGetDiffSameType(Type runtimeType, object left, object right, ComparisonContext ctx,
        out IDiff diff)
    {
        if (_diffMap.TryGetValue(runtimeType, out var fn))
        {
            return fn(left, right, ctx, out diff);
        }

        diff = Diff.Empty;
        return false;
    }

    /// <summary>
    ///     Registers strongly-typed delta compute/apply providers for runtime dispatch.
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
            var local = (T?)target;
            apply(ref local, ref r);
            target = local;
        };
    }

    /// <summary>
    ///     Computes a delta between two objects of the same runtime type using a registered provider.
    /// </summary>
    public static void ComputeDeltaSameType(Type runtimeType, object? left, object? right, ComparisonContext context,
        ref DeltaWriter writer)
    {
        if (_deltaComputeMap.TryGetValue(runtimeType, out var fn))
        {
            fn(left, right, context, ref writer);
            return;
        }

        for (var bt = runtimeType.BaseType; bt is not null; bt = bt.BaseType)
            if (_deltaComputeMap.TryGetValue(bt, out var baseFn))
            {
                _deltaComputeMap.TryAdd(runtimeType, baseFn);
                baseFn(left, right, context, ref writer);
                return;
            }

        foreach (var i in runtimeType.GetInterfaces())
            if (_deltaComputeMap.TryGetValue(i, out var ifaceFn))
            {
                _deltaComputeMap.TryAdd(runtimeType, ifaceFn);
                ifaceFn(left, right, context, ref writer);
                return;
            }
    }

    public static bool TryComputeDeltaSameType(Type runtimeType, object? left, object? right, ComparisonContext context,
        ref DeltaWriter writer)
    {
        if (_deltaComputeMap.TryGetValue(runtimeType, out var fn))
        {
            fn(left, right, context, ref writer);
            return true;
        }

        for (var bt = runtimeType.BaseType; bt is not null; bt = bt.BaseType)
            if (_deltaComputeMap.TryGetValue(bt, out var baseFn))
            {
                _deltaComputeMap.TryAdd(runtimeType, baseFn);
                baseFn(left, right, context, ref writer);
                return true;
            }

        foreach (var i in runtimeType.GetInterfaces())
            if (_deltaComputeMap.TryGetValue(i, out var ifaceFn))
            {
                _deltaComputeMap.TryAdd(runtimeType, ifaceFn);
                ifaceFn(left, right, context, ref writer);
                return true;
            }

        return false;
    }

    /// <summary>
    ///     Attempts to apply a delta to an object using a registered provider based on the runtime type.
    /// </summary>
    public static bool TryApplyDeltaSameType(Type runtimeType, ref object? target, ref DeltaReader reader)
    {
        if (_deltaApplyObjMap.TryGetValue(runtimeType, out var fn))
        {
            fn(ref target, ref reader);
            return true;
        }

        for (var bt = runtimeType.BaseType; bt is not null; bt = bt.BaseType)
            if (_deltaApplyObjMap.TryGetValue(bt, out var baseFn))
            {
                _deltaApplyObjMap.TryAdd(runtimeType, baseFn);
                baseFn(ref target, ref reader);
                return true;
            }

        foreach (var i in runtimeType.GetInterfaces())
            if (_deltaApplyObjMap.TryGetValue(i, out var ifaceFn))
            {
                _deltaApplyObjMap.TryAdd(runtimeType, ifaceFn);
                ifaceFn(ref target, ref reader);
                return true;
            }

        return false;
    }
}