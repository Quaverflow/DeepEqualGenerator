using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace DeepEqual.Generator.Shared;

/// <summary>
/// Central registry for generated deep-equality, diff, and delta helpers.
/// </summary>
/// <remarks>
/// This registry avoids runtime reflection, is AOT/trimming friendly, and provides
/// runtime dispatch for polymorphic graphs without allocations.
/// </remarks>
public static class GeneratedHelperRegistry
{
    private static readonly ConcurrentDictionary<Type, Func<object?, object?, ComparisonContext, bool>> _eqMap = new();
    private static readonly ConcurrentDictionary<Type, bool> _negativeEqCache = new();

    private static readonly ConcurrentDictionary<Type, DiffFunc> _diffMap = new();

    private static readonly ConcurrentDictionary<Type, ComputeDeltaObjFunc> _deltaComputeMap = new();
    private static readonly ConcurrentDictionary<Type, ApplyDeltaObjFunc> _deltaApplyMap = new();

    /// <summary>
    /// Delegate used for runtime diff dispatch.
    /// </summary>
    public delegate bool DiffFunc(object? left, object? right, ComparisonContext context, out IDiff diff);

    /// <summary>
    /// Delegate used for runtime delta compute dispatch with object-typed arguments.
    /// </summary>
    public delegate void ComputeDeltaObjFunc(object? left, object? right, ComparisonContext context, ref DeltaWriter writer);

    /// <summary>
    /// Delegate used for runtime delta apply dispatch with object-typed arguments.
    /// </summary>
    public delegate void ApplyDeltaObjFunc(ref object? target, ref DeltaReader reader);

    /// <summary>
    /// Delegate used to register strongly-typed deep equality comparers.
    /// </summary>
    public delegate bool ComparerRef<T>(T? left, T? right, ComparisonContext context);

    /// <summary>
    /// Delegate used to register strongly-typed diff providers.
    /// </summary>
    public delegate (bool hasDiff, Diff<T> diff) DiffRef<T>(T? left, T? right, ComparisonContext context);

    /// <summary>
    /// Delegate used to register strongly-typed delta compute providers.
    /// </summary>
    public delegate void ComputeDeltaRef<T>(T? left, T? right, ComparisonContext context, ref DeltaWriter writer);

    /// <summary>
    /// Delegate used to register strongly-typed delta apply providers.
    /// </summary>
    public delegate void ApplyDeltaRef<T>(ref T? target, ref DeltaReader reader);

    /// <summary>
    /// Registers a strongly-typed deep equality comparer for runtime dispatch.
    /// </summary>
    public static void RegisterComparer<T>(ComparerRef<T> comparer)
    {
        _eqMap[typeof(T)] = (object? l, object? r, ComparisonContext c) => comparer((T?)l, (T?)r, c);
    }

    /// <summary>
    /// Attempts to compare two objects of the same runtime type using a generated comparer.
    /// </summary>
    public static bool TryCompareSameType(Type runtimeType, object? left, object? right, ComparisonContext context, out bool equal)
    {
        if (_eqMap.TryGetValue(runtimeType, out var fn))
        {
            equal = fn(left, right, context);
            return true;
        }

        if (_negativeEqCache.ContainsKey(runtimeType))
        {
            equal = false;
            return false;
        }

        equal = false;
        _negativeEqCache.TryAdd(runtimeType, true);
        return false;
    }

    /// <summary>
    /// Attempts to compare two objects; succeeds when both are null, reference-equal,
    /// or share the same runtime type with a registered comparer.
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
    public static void RegisterDiff<T>(DiffRef<T> tryGet)
    {
        var t = typeof(T);
        _diffMap[t] = (object? l, object? r, ComparisonContext c, out IDiff idiff) =>
        {
            var (has, diff) = tryGet((T?)l, (T?)r, c);
            idiff = diff;
            return has;
        };
    }

    /// <summary>
    /// Attempts to compute a diff for two objects of the same runtime type using a registered provider.
    /// </summary>
    public static bool TryGetDiffSameType(Type runtimeType, object? left, object? right, ComparisonContext context, out IDiff diff)
    {
        if (_diffMap.TryGetValue(runtimeType, out var fn))
        {
            return fn(left, right, context, out diff);
        }
        diff = default!;
        return false;
    }

    /// <summary>
    /// Registers strongly-typed delta compute and apply providers for runtime dispatch.
    /// </summary>
    public static void RegisterDelta<T>(ComputeDeltaRef<T> compute, ApplyDeltaRef<T> apply)
    {
        var t = typeof(T);

        _deltaComputeMap[t] = (object? left, object? right, ComparisonContext ctx, ref DeltaWriter w) =>
        {
            compute((T?)left, (T?)right, ctx, ref w);
        };

        _deltaApplyMap[t] = (ref object? target, ref DeltaReader r) =>
        {
            T? typed = (T?)target;
            apply(ref typed, ref r);
            target = typed;
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
        }
    }

    /// <summary>
    /// Attempts to apply a delta to an object using a registered provider based on the runtime type.
    /// </summary>
    public static bool TryApplyDeltaSameType(Type runtimeType, ref object? target, ref DeltaReader reader)
    {
        if (_deltaApplyMap.TryGetValue(runtimeType, out var fn))
        {
            fn(ref target, ref reader);
            return true;
        }
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

}
