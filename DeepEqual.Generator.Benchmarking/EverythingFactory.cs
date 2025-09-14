using System.Dynamic;

namespace DeepEqual.Generator.Benchmarking;

public static class EverythingFactory
{
    public static EverythingBagel Create(int seed, bool mutateShallow = false, bool mutateDeep = false)
    {
        var e = new EverythingBagel
        {
            B = (seed & 1) == 0,
            U8 = (byte)(seed % 256),
            I8 = (sbyte)((seed % 200) - 100),
            I16 = (short)(seed % 1000),
            U16 = (ushort)(seed % 1000),
            I32 = seed * 31,
            U32 = (uint)(seed * 31),
            I64 = (long)seed * 1_000_003L,
            U64 = (ulong)(seed * 1_000_003L),
            F32 = (float)(seed * 0.123),
            F64 = seed * 0.123456789,
            M128 = (decimal)(seed * 0.987654321m),
            C = (char)('A' + (seed % 26)),
            S = $"S-{seed:000000}",
            NI32 = (seed % 3 == 0) ? null : seed * 17,
            NEnum = (seed % 4 == 0) ? null : TinyEnum.B,
            NPoint = (seed % 5 == 0) ? null : new MiniPoint { X = seed, Y = seed * 2 },
            E = (TinyEnum)(seed % 4),
            P = new MiniPoint { X = seed % 100, Y = (seed % 100) * 2 },
            When = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddMinutes(seed % 500),
            WhenOff = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero).AddMinutes(seed % 500),
            HowLong = TimeSpan.FromSeconds(seed % 10_000),
#if NET6_0_OR_GREATER
            Day = new DateOnly(2025, 1, 1).AddDays(seed % 365),
            Clock = new TimeOnly((seed % 24), (seed % 60), (seed % 60)),
#endif
            Id = DeterministicGuid($"E-{seed}"),
            Blob = new Memory<byte>(MakeBytes(seed, 64)),
            RBlob = new ReadOnlyMemory<byte>(MakeBytes(seed + 1, 64)),
            Numbers = Enumerable.Range(0, 32).Select(i => (i + seed) % 1000).ToArray(),
            Words = new[] { "alpha", "beta", $"w-{seed}" },
            Jagged = new[]
            {
                new[] { 1, 2, 3 },
                new[] { 5 + seed%5, 6 + seed%7 }
            },
            Rect = new int[,]
            {
                { 1, 2, 3 },
                { 4, 5, (6 + seed % 3) }
            },
            LInts = Enumerable.Range(0, 64).Select(i => i * 3 + seed).ToList(),
            RListStrings = Enumerable.Range(0, 16).Select(i => $"s{i + seed}").ToList(),
            Tags = new HashSet<string>(new[] { "x", "y", $"t-{seed}" }, StringComparer.Ordinal),
            ByName = Enumerable.Range(0, 16).ToDictionary(i => $"k{i}", i => i + seed, StringComparer.Ordinal),
            ByKey = Enumerable.Range(0, 8).ToDictionary(i => $"id{i}", i => new Leaf { Name = $"L{i}", Score = i + seed }),
            Left = new Leaf { Name = "left", Score = 10 + (seed % 5) },
            Right = new Leaf { Name = "right", Score = 20 + (seed % 5) },
            Pair = (seed % 100, $"pair-{seed}"),
            Kvp = new KeyValuePair<string, int>($"kvp-{seed}", seed % 123),
            Boxed = (seed % 2 == 0) ? (object)($"box-{seed}") : (object)(seed % 999),
            Dyn = MakeExpando(seed),
            RefBlob = (seed % 2 == 0) ? new byte[] { 1, 2, 3 } : new byte[] { 1, 2, 3 }
        };

        if (mutateShallow) e.S = $"DIFF-{seed}";
        if (mutateDeep)
        {
            e.Right!.Score += 1;
            e.ByName!["k7"] += 1;
        }
        return e;

        static byte[] MakeBytes(int s, int n)
        {
            var rng = new Random(s);
            var arr = new byte[n];
            rng.NextBytes(arr);
            return arr;
        }

        static Guid DeterministicGuid(string s)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(s);
            Span<byte> g = stackalloc byte[16];
            for (var i = 0; i < 16; i++) g[i] = (byte)(bytes[i % bytes.Length] + i * 31);
            return new Guid(g);
        }

        static IDictionary<string, object?> MakeExpando(int seed)
        {
            dynamic eo = new ExpandoObject();
            eo.id = seed;
            eo.name = $"dyn-{seed}";
            eo.arr = new[] { 1, 2, 3 + (seed % 3) };
            eo.map = new Dictionary<string, object?>
            {
                ["a"] = 1,
                ["b"] = new[] { "p", "q" },
                ["c"] = new Dictionary<string, object?> { ["z"] = seed % 10 }
            };
            return eo;
        }
    }
}