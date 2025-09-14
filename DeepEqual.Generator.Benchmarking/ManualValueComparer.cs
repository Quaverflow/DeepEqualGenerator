namespace DeepEqual.Generator.Benchmarking;

static class ManualValueComparer
{
    public static bool AreEqual(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.GetType() != b.GetType()) return false;

        switch (a)
        {
            case string sa: return sa == (string)b!;
            case bool va: return va == (bool)b!;
            case char ca: return ca == (char)b!;
            case sbyte sb: return sb == (sbyte)b!;
            case byte by: return by == (byte)b!;
            case short sh: return sh == (short)b!;
            case ushort ush: return ush == (ushort)b!;
            case int ia: return ia == (int)b!;
            case uint uia: return uia == (uint)b!;
            case long la: return la == (long)b!;
            case ulong ula: return ula == (ulong)b!;
            case float fa: return fa == (float)b!;
            case double da: return da == (double)b!;
            case decimal de: return de == (decimal)b!;
            case Guid ga: return ga == (Guid)b!;
            case DateTime dt: return dt == (DateTime)b!;
            case DateTimeOffset dto: return dto == (DateTimeOffset)b!;
            case TimeSpan tsp: return tsp == (TimeSpan)b!;
#if NET6_0_OR_GREATER
            case DateOnly don: return don == (DateOnly)b!;
            case TimeOnly ton: return ton == (TimeOnly)b!;
#endif
            case TinyEnum ee: return ee == (TinyEnum)b!;
            case MiniPoint mp: return ((MiniPoint)b!).X == mp.X && ((MiniPoint)b!).Y == mp.Y;
            case byte[] arrA: return ReferenceEquals(arrA, b);
            case Array aa: return ArrayEqual(aa, (Array)b!);
            case IDictionary<string, object?> da1: return DictObjectEqual(da1, (IDictionary<string, object?>)b!);
            case Leaf la1: return LeafEqual(la1, (Leaf)b!);
            default: return a.Equals(b);
        }
    }

    private static bool ArrayEqual(Array a, Array b)
    {
        if (a.Rank != b.Rank) return false;
        for (var d = 0; d < a.Rank; d++)
            if (a.GetLength(d) != b.GetLength(d)) return false;

        if (a is int[] ai && b is int[] bi) return SequenceEqual(ai, bi);
        if (a is string[] as1 && b is string[] bs1) return SequenceEqual(as1, bs1);
        if (a is int[][] aj && b is int[][] bj)
        {
            if (aj.Length != bj.Length) return false;
            for (var i = 0; i < aj.Length; i++)
                if (!SequenceEqual(aj[i], bj[i])) return false;
            return true;
        }

        var idx = new int[a.Rank];
        return WalkRect(a, b, 0, idx);

        static bool WalkRect(Array a, Array b, int dim, int[] idx)
        {
            if (dim == a.Rank)
            {
                var va = a.GetValue(idx);
                var vb = b.GetValue(idx);
                return Equals(va, vb);
            }
            for (var i = 0; i < a.GetLength(dim); i++)
            {
                idx[dim] = i;
                if (!WalkRect(a, b, dim + 1, idx)) return false;
            }
            return true;
        }

        static bool SequenceEqual<T>(T[] x, T[] y)
        {
            if (x.Length != y.Length) return false;
            for (var i = 0; i < x.Length; i++)
                if (!Equals(x[i], y[i])) return false;
            return true;
        }
    }

    private static bool DictObjectEqual(IDictionary<string, object?> a, IDictionary<string, object?> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var kv in a)
        {
            if (!b.TryGetValue(kv.Key, out var bv)) return false;
            if (!AreEqual(kv.Value, bv)) return false;
        }
        return true;
    }

    public static bool LeafEqual(Leaf? a, Leaf? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        return a.Name == b.Name && a.Score == b.Score;
    }
}