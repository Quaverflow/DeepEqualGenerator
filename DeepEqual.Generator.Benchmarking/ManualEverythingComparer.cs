namespace DeepEqual.Generator.Benchmarking;

static class ManualEverythingComparer
{
    public static bool AreEqual(EverythingBagel? a, EverythingBagel? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        if (a.B != b.B || a.U8 != b.U8 || a.I8 != b.I8 || a.I16 != b.I16 || a.U16 != b.U16 ||
            a.I32 != b.I32 || a.U32 != b.U32 || a.I64 != b.I64 || a.U64 != b.U64 ||
            a.F32 != b.F32 || a.F64 != b.F64 || a.M128 != b.M128 || a.C != b.C ||
            a.S != b.S)
        {
            return false;
        }

        if (a.Ni32 != b.Ni32 || a.NEnum != b.NEnum ||
            !(a.NPoint is null ? b.NPoint is null :
                (b.NPoint is not null && a.NPoint.Value.X == b.NPoint.Value.X && a.NPoint.Value.Y == b.NPoint.Value.Y)))
        {
            return false;
        }

        if (a.E != b.E || a.P.X != b.P.X || a.P.Y != b.P.Y)
        {
            return false;
        }

        if (a.When != b.When || a.WhenOff != b.WhenOff || a.HowLong != b.HowLong)
        {
            return false;
        }
#if NET6_0_OR_GREATER
        if (a.Day != b.Day || a.Clock != b.Clock)
        {
            return false;
        }
#endif
        if (a.Id != b.Id)
        {
            return false;
        }

        if (!a.Blob.Span.SequenceEqual(b.Blob.Span))
        {
            return false;
        }

        if (!a.RBlob.Span.SequenceEqual(b.RBlob.Span))
        {
            return false;
        }

        if (!ArrayEqual(a.Numbers, b.Numbers))
        {
            return false;
        }

        if (!ArrayEqual(a.Words, b.Words))
        {
            return false;
        }

        if (!JaggedEqual(a.Jagged, b.Jagged))
        {
            return false;
        }

        if (!RectEqual(a.Rect, b.Rect))
        {
            return false;
        }

        if (!ListEqual(a.LInts, b.LInts))
        {
            return false;
        }

        if (!ListEqual(a.RListStrings, b.RListStrings))
        {
            return false;
        }

        if (!SetEqual(a.Tags, b.Tags))
        {
            return false;
        }

        if (!DictEqual(a.ByName, b.ByName))
        {
            return false;
        }

        if (!DictEqualLeaf(a.ByKey, b.ByKey))
        {
            return false;
        }

        if (!ManualValueComparer.LeafEqual(a.Left, b.Left))
        {
            return false;
        }

        if (!ManualValueComparer.LeafEqual(a.Right, b.Right))
        {
            return false;
        }

        if (a.Pair != b.Pair)
        {
            return false;
        }

        if (a.Kvp.Key != b.Kvp.Key || a.Kvp.Value != b.Kvp.Value)
        {
            return false;
        }

        if (!ManualValueComparer.AreEqual(a.Boxed, b.Boxed))
        {
            return false;
        }

        if (!DictObjEqual(a.Dyn, b.Dyn))
        {
            return false;
        }

        if (!ReferenceEquals(a.RefBlob, b.RefBlob))
        {
            return false;
        }

        return true;

        static bool ArrayEqual<T>(T[]? x, T[]? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            if (x.Length != y.Length)
            {
                return false;
            }

            for (var i = 0; i < x.Length; i++) if (!Equals(x[i], y[i]))
            {
                return false;
            }

            return true;
        }

        static bool JaggedEqual<T>(T[][]? a, T[][]? b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a is null || b is null)
            {
                return false;
            }

            if (a.Length != b.Length)
            {
                return false;
            }

            for (var i = 0; i < a.Length; i++)
                if (!ArrayEqual(a[i], b[i]))
                {
                    return false;
                }

            return true;
        }

        static bool RectEqual<T>(T[,]? a, T[,]? b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a is null || b is null)
            {
                return false;
            }

            if (a.GetLength(0) != b.GetLength(0) || a.GetLength(1) != b.GetLength(1))
            {
                return false;
            }

            for (var i = 0; i < a.GetLength(0); i++)
            for (var j = 0; j < a.GetLength(1); j++)
                if (!Equals(a[i, j], b[i, j]))
                {
                    return false;
                }

            return true;
        }

        static bool ListEqual<T>(IReadOnlyList<T>? a, IReadOnlyList<T>? b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a is null || b is null)
            {
                return false;
            }

            if (a.Count != b.Count)
            {
                return false;
            }

            for (var i = 0; i < a.Count; i++)
                if (!Equals(a[i], b[i]))
                {
                    return false;
                }

            return true;
        }

        static bool SetEqual(HashSet<string>? a, HashSet<string>? b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a is null || b is null)
            {
                return false;
            }

            return a.SetEquals(b);
        }

        static bool DictEqual(Dictionary<string, int>? a, Dictionary<string, int>? b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a is null || b is null)
            {
                return false;
            }

            if (a.Count != b.Count)
            {
                return false;
            }

            foreach (var (k, v) in a)
                if (!b.TryGetValue(k, out var bv) || v != bv)
                {
                    return false;
                }

            return true;
        }

        static bool DictEqualLeaf(IReadOnlyDictionary<string, Leaf>? a, IReadOnlyDictionary<string, Leaf>? b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a is null || b is null)
            {
                return false;
            }

            if (a.Count != b.Count)
            {
                return false;
            }

            foreach (var (k, v) in a)
                if (!b.TryGetValue(k, out var bv) || !ManualValueComparer.LeafEqual(v, bv))
                {
                    return false;
                }

            return true;
        }

        static bool DictObjEqual(IDictionary<string, object?> a, IDictionary<string, object?> b)
            => ManualValueComparer.AreEqual(a, b);
    }
}