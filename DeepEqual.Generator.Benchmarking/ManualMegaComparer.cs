namespace DeepEqual.Generator.Benchmarking;

static class ManualMegaComparer
{
    public static bool AreEqual(MegaRoot? a, MegaRoot? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (!string.Equals(a.Title, b.Title, StringComparison.Ordinal)) return false;
        if (!ManualEverythingComparer.AreEqual(a.Bagel, b.Bagel)) return false;
        if (!ManualBigGraphComparer.AreEqual(a.Graph, b.Graph)) return false;

        if (a.Bagels.Count != b.Bagels.Count) return false;
        var matched = new bool[b.Bagels.Count];
        for (int i = 0; i < a.Bagels.Count; i++)
        {
            bool ok = false;
            for (int j = 0; j < b.Bagels.Count; j++)
            {
                if (matched[j]) continue;
                if (ManualEverythingComparer.AreEqual(a.Bagels[i], b.Bagels[j])) { matched[j] = true; ok = true; break; }
            }
            if (!ok) return false;
        }

        if (a.BagelIndex.Count != b.BagelIndex.Count) return false;
        foreach (var (k, v) in a.BagelIndex)
            if (!b.BagelIndex.TryGetValue(k, out var bv) || !ManualEverythingComparer.AreEqual(v, bv)) return false;

        if (a.Jaggy.Length != b.Jaggy.Length) return false;
        for (int i = 0; i < a.Jaggy.Length; i++)
        {
            var ax = a.Jaggy[i];
            var bx = b.Jaggy[i];
            if (ax.Length != bx.Length) return false;
            for (int j = 0; j < ax.Length; j++)
                if (ax[j] != bx[j]) return false;
        }

        if (a.Data.Length != b.Data.Length) return false;
        if (!a.Data.Span.SequenceEqual(b.Data.Span)) return false;
        if (a.RData.Length != b.RData.Length) return false;
        if (!a.RData.Span.SequenceEqual(b.RData.Span)) return false;

        if (a.Mixed.Count != b.Mixed.Count) return false;
        for (int i = 0; i < a.Mixed.Count; i++)
            if (!ManualValueComparer.AreEqual(a.Mixed[i], b.Mixed[i])) return false;

        if (!DictObjEqual(a.Meta, b.Meta)) return false;
        if (!ManualValueComparer.AreEqual(a.Expando, b.Expando)) return false;

        if (!ManualValueComparer.AreEqual(a.Polymorph, b.Polymorph)) return false;

        if (a.ForcedOrdered.Count != b.ForcedOrdered.Count) return false;
        for (int i = 0; i < a.ForcedOrdered.Count; i++)
            if (a.ForcedOrdered[i] != b.ForcedOrdered[i]) return false;

        return true;

        static bool DictObjEqual(Dictionary<string, object?> a, Dictionary<string, object?> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var (k, v) in a)
                if (!b.TryGetValue(k, out var bv) || !ManualValueComparer.AreEqual(v, bv)) return false;
            return true;
        }
    }
}