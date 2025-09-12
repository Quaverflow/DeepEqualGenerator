using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DeepEqual.Generator.Shared;

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
public static partial class DynamicDeepComparer
{
    public static bool AreEqualDynamic(object? left, object? right, ComparisonContext ctx)
    {
        if (ReferenceEquals(left, right)) { return true; }
        if (left is null || right is null) { return false; }

        // strings / primitives fast-paths (unchanged)
        if (left is string ls && right is string rs) { return ComparisonHelpers.AreEqualStrings(ls, rs); }
        if (IsPrimitiveLike(left) && IsPrimitiveLike(right)) { return left.Equals(right); }
        if (left is DateTime ldt && right is DateTime rdt) { return ComparisonHelpers.AreEqualDateTime(ldt, rdt); }
        if (left is DateTimeOffset ldo && right is DateTimeOffset rdo) { return ComparisonHelpers.AreEqualDateTimeOffset(ldo, rdo); }
        if (left is TimeSpan lts && right is TimeSpan rts) { return lts.Ticks == rts.Ticks; }
        if (left is Guid lg && right is Guid rg) { return lg.Equals(rg); }

        // Expando / any IDictionary<string, T> / non-generic IDictionary with string keys
        if (TryAsStringKeyedMap(left, out var mapA) && TryAsStringKeyedMap(right, out var mapB))
        {
            return EqualStringKeyedMap(mapA, mapB, ctx);
        }

        // IEnumerable (not string): ordered dynamic element comparison
        if (left is IEnumerable ea && right is IEnumerable eb && left is not string && right is not string)
        {
            return EqualDynamicSequence(ea, eb, ctx);
        }

        // Try strongly-typed helper when runtime types match
        if (left.GetType() == right.GetType() &&
            GeneratedHelperRegistry.TryCompareSameType(left.GetType(), left, right, ctx, out var eq))
        {
            return eq;
        }

        return left.Equals(right);
    }

    private static bool IsPrimitiveLike(object o) =>
        o is bool or byte or sbyte or short or ushort or int or uint or long or ulong or char or float or double or decimal || o.GetType().IsEnum;

    // ---- string-keyed map abstraction -------------------------------------

    private readonly struct MapView
    {
        public readonly int Count;
        public readonly Func<IEnumerable> Enumerate; // yields (key,value) objects
        public readonly Func<object, string> GetKey;
        public readonly Func<object, object?> GetValue;
        public MapView(int count, Func<IEnumerable> enumerate, Func<object, string> getKey, Func<object, object?> getValue)
        { Count = count; Enumerate = enumerate; GetKey = getKey; GetValue = getValue; }
    }

    private static readonly ConcurrentDictionary<Type, (Func<object, MapView> open, bool ok)> _mapCache = new();

    private static bool TryAsStringKeyedMap(object obj, out MapView view)
    {
        // ExpandoObject
        if (obj is System.Dynamic.ExpandoObject exp)
        {
            var d = (IDictionary<string, object?>)exp;
            view = new MapView(d.Count, () => d, kv => ((KeyValuePair<string, object?>)kv).Key, kv => ((KeyValuePair<string, object?>)kv).Value);
            return true;
        }

        // IDictionary<string, object?>
        if (obj is IDictionary<string, object?> dictObj)
        {
            view = new MapView(dictObj.Count, () => dictObj, kv => ((KeyValuePair<string, object?>)kv).Key, kv => ((KeyValuePair<string, object?>)kv).Value);
            return true;
        }

        var t = obj.GetType();
        var (open, ok) = _mapCache.GetOrAdd(t, BuildMapViewFactory);
        if (ok)
        {
            view = open(obj);
            return true;
        }

        view = default;
        return false;
    }

    // Supports:
    // - IDictionary<TKey,TValue> where TKey == string (any TValue)
    // - Non-generic IDictionary where entries are DictionaryEntry/string keys
    private static (Func<object, MapView> open, bool ok) BuildMapViewFactory(Type t)
    {
        // 1) Prefer any IDictionary<string, TValue> (fast/strongly-typed)
        foreach (var i in t.GetInterfaces())
        {
            if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            {
                var args = i.GetGenericArguments();
                if (args[0] == typeof(string))
                {
                    var kvType = typeof(KeyValuePair<,>).MakeGenericType(typeof(string), args[1]);
                    var keyProp = kvType.GetProperty("Key")!;
                    var valProp = kvType.GetProperty("Value")!;

                    Func<object, MapView> f = o =>
                    {
                        var enumerable = (IEnumerable)o; // IEnumerable<KeyValuePair<string,T>>
                        int count = TryGetGenericCount(o, kvType, out var c) ? c : CountEnumer(enumerable);
                        return new MapView(
                            count,
                            () => enumerable,
                            kv => (string)keyProp.GetValue(kv)!,
                            kv => valProp.GetValue(kv)
                        );
                    };
                    return (f, true);
                }
            }
        }

        // 2) Fallback: non-generic IDictionary (handle DictionaryEntry OR KeyValuePair at runtime)
        if (typeof(IDictionary).IsAssignableFrom(t))
        {
            Func<object, MapView> f = o =>
            {
                var d = (IDictionary)o;
                return new MapView(
                    d.Count,
                    () => d, // IEnumerable
                    kv =>
                    {
                        if (kv is DictionaryEntry de) return de.Key is string s ? s : de.Key?.ToString() ?? string.Empty;
                        if (TryGetKVPairKey(kv, out var sKey)) return sKey;
                        return kv?.ToString() ?? string.Empty;
                    },
                    kv =>
                    {
                        if (kv is DictionaryEntry de) return de.Value;
                        if (TryGetKVPairValue(kv, out var val)) return val;
                        return null;
                    }
                );
            };
            return (f, true);
        }

        return (static _ => default, false);

        static bool TryGetGenericCount(object o, Type kvType, out int count)
        {
            var collIface = typeof(ICollection<>).MakeGenericType(kvType);
            if (collIface.IsInstanceOfType(o))
            {
                var pi = collIface.GetProperty("Count")!;
                count = (int)pi.GetValue(o)!;
                return true;
            }
            count = 0; return false;
        }

        static int CountEnumer(IEnumerable e)
        {
            int n = 0; var en = e.GetEnumerator(); while (en.MoveNext()) n++; return n;
        }

        // handle KeyValuePair<string, T> objects in non-generic path
        static bool TryGetKVPairKey(object kv, out string key)
        {
            var t = kv.GetType();
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(KeyValuePair<,>) &&
                t.GetGenericArguments()[0] == typeof(string))
            {
                key = (string)t.GetProperty("Key")!.GetValue(kv)!;
                return true;
            }
            key = "";
            return false;
        }
        static bool TryGetKVPairValue(object kv, out object? value)
        {
            var t = kv.GetType();
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                value = t.GetProperty("Value")!.GetValue(kv);
                return true;
            }
            value = null;
            return false;
        }
    }

    private static bool EqualStringKeyedMap(MapView a, MapView b, ComparisonContext ctx)
    {
        if (a.Count != b.Count) { return false; }

        // Build right dictionary for O(1) lookups
        var right = new Dictionary<string, object?>(a.Count, StringComparer.Ordinal);
        foreach (var kv in b.Enumerate())
        {
            right[b.GetKey(kv)] = b.GetValue(kv);
        }

        foreach (var kv in a.Enumerate())
        {
            var key = a.GetKey(kv);
            if (!right.TryGetValue(key, out var rv)) { return false; }
            if (!AreEqualDynamic(a.GetValue(kv), rv, ctx)) { return false; }
        }
        return true;
    }

    private static bool EqualDynamicSequence(IEnumerable a, IEnumerable b, ComparisonContext ctx)
    {
        var ea = a.GetEnumerator();
        var eb = b.GetEnumerator();
        while (true)
        {
            bool ma = ea.MoveNext(), mb = eb.MoveNext();
            if (ma != mb) return false;
            if (!ma) return true;
            if (!AreEqualDynamic(ea.Current, eb.Current, ctx)) return false;
        }
    }
}
public static class GeneratedHelperRegistry
{
    private static readonly Dictionary<Type, Func<object, object, ComparisonContext, bool>> _map = new();


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