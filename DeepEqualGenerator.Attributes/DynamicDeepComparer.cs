using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DeepEqual.Generator.Shared;

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

        // NEW: generic IDictionary<TKey,TValue> with same K/V runtime types
        if (TryAsGenericMap(left, out var gA) && TryAsGenericMap(right, out var gB) &&
            gA.KeyType == gB.KeyType && gA.ValueType == gB.ValueType)
        {
            if (gA.Count != gB.Count) return false;

            foreach (var kv in gA.Enumerate())
            {
                var key = gA.GetKey(kv);
                var val = gA.GetValue(kv);
                var (found, rv) = gB.Lookup(right, key);
                if (!found) return false;
                if (!AreEqualDynamic(val, rv, ctx)) return false;
            }
            return true;
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

    // ---- generic IDictionary<TKey,TValue> abstraction ----------------------

    private readonly struct GenericMapView
    {
        public readonly int Count;
        public readonly Func<IEnumerable> Enumerate; // yields KeyValuePair<K,V> objects
        public readonly Func<object, object> GetKey; // object kv -> K (boxed)
        public readonly Func<object, object?> GetValue; // object kv -> V (boxed)
        public readonly Func<object, object, (bool found, object? value)> Lookup; // (map, key) -> found/value (boxed)
        public readonly Type KeyType;
        public readonly Type ValueType;

        public GenericMapView(int count,
            Func<IEnumerable> enumerate,
            Func<object, object> getKey,
            Func<object, object?> getValue,
            Func<object, object, (bool found, object? value)> lookup,
            Type keyType,
            Type valueType)
        {
            Count = count;
            Enumerate = enumerate;
            GetKey = getKey;
            GetValue = getValue;
            Lookup = lookup;
            KeyType = keyType;
            ValueType = valueType;
        }
    }

    private static readonly ConcurrentDictionary<Type, (Func<object, GenericMapView> open, bool ok)> _genMapCache = new();

    private static bool TryAsGenericMap(object obj, out GenericMapView view)
    {
        var (open, ok) = _genMapCache.GetOrAdd(obj.GetType(), BuildGenericMapFactory);
        if (ok)
        {
            view = open(obj);
            return true;
        }
        view = default;
        return false;
    }

    private static (Func<object, GenericMapView> open, bool ok) BuildGenericMapFactory(Type t)
    {
        foreach (var i in t.GetInterfaces())
        {
            if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            {
                var args = i.GetGenericArguments(); // K, V
                var keyType = args[0];
                var valueType = args[1];

                // KVP accessors
                var kvType = typeof(KeyValuePair<,>).MakeGenericType(keyType, valueType);
                var keyProp = kvType.GetProperty("Key")!;
                var valProp = kvType.GetProperty("Value")!;

                // TryGetValue(map, K, out V)
                var tryGet = i.GetMethod("TryGetValue")!;

                Func<object, GenericMapView> f = o =>
                {
                    var enumerable = (IEnumerable)o; // IEnumerable<KeyValuePair<K,V>>
                    int count = TryGetGenericCount(o, kvType, out var c) ? c : CountEnumer(enumerable);

                    (bool found, object? value) Lookup(object map, object key)
                    {
                        var argsLoc = new object?[] { key, null };
                        var ok = (bool)tryGet.Invoke(map, argsLoc)!;
                        return (ok, argsLoc[1]);
                    }

                    return new GenericMapView(
                        count,
                        () => enumerable,
                        kv => keyProp.GetValue(kv)!,
                        kv => valProp.GetValue(kv),
                        Lookup,
                        keyType,
                        valueType
                    );
                };

                return (f, true);
            }
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
    }
}
