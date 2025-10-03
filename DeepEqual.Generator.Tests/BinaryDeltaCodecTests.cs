#nullable enable
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DeepEqual.Generator.Shared;
using DeepEqual.RewrittenTests.Domain;
using Xunit;
using Xunit.Sdk;

namespace DeepEqual.RewrittenTests;

public class BinaryDeltaCodecTests
{
    private const string SharedString = "shared-key-886";
    private static readonly string[] RandomStrings =
    {
        SharedString,
        "alpha-key-001",
        "beta-key-002",
        "gamma-value-lorem"
    };

    private static readonly Guid SampleGuid = new("7b69aee1-74e1-4cc4-92ef-521b83b0d541");
    private static readonly DateTime SampleDateTime = new(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    private static readonly DateTimeOffset SampleDateTimeOffset = new(2024, 1, 2, 3, 4, 5, TimeSpan.FromMinutes(150));
    private static readonly TimeSpan SampleTimeSpan = TimeSpan.FromMinutes(123);
    private static readonly byte[] SampleBytes = { 1, 2, 3, 4, 5 };

    private enum ByteStatus : byte
    {
        Zero = 0,
        One = 1,
        Two = 2
    }

    private enum OtherStatus
    {
        None = 0,
        Ready = 1
    }

    private readonly record struct DocCase(string Name, Func<DeltaDocument> Factory);
    private readonly record struct OptionCase(string Name, Func<BinaryDeltaOptions> Factory);

    public static IEnumerable<object[]> RoundTripCases()
    {
        foreach (var doc in DocumentCases())
        {
            foreach (var opt in OptionCases())
            {
                yield return new object[] { doc.Name, doc.Factory, opt.Name, opt.Factory };
            }
        }
    }

    public static IEnumerable<object[]> DeterminismCases()
    {
        yield return new object[] { (Func<DeltaDocument>)CreateNestedDocument, (Func<BinaryDeltaOptions>)(() => new BinaryDeltaOptions { IncludeHeader = false, UseStringTable = false, UseTypeTable = false }) };
        yield return new object[] { (Func<DeltaDocument>)CreateNestedDocument, (Func<BinaryDeltaOptions>)(() => new BinaryDeltaOptions { IncludeHeader = true, UseStringTable = true, UseTypeTable = true }) };
        yield return new object[] { (Func<DeltaDocument>)CreateNestedDocument, (Func<BinaryDeltaOptions>)(() => new BinaryDeltaOptions { IncludeHeader = true, UseStringTable = true, UseTypeTable = true, IncludeEnumTypeIdentity = false }) };
    }
    /// <summary>Round-trips representative documents across supported codec option matrices.</summary>
    [Theory]
    [MemberData(nameof(RoundTripCases))]
    public void RoundTrip_ReturnsStructurallyIdenticalDocument(string docLabel, Func<DeltaDocument> docFactory, string optionLabel, Func<BinaryDeltaOptions> optionsFactory)
    {
        var original = docFactory();
        var options = optionsFactory();
        var encoded = Encode(original, options);
        var decoded = BinaryDeltaCodec.Read(encoded, options);

        AssertDocsEqual(original, decoded, options, $"{docLabel}/{optionLabel}");
    }

    /// <summary>Confirms encoding emits identical bytes for identical input runs.</summary>
    [Theory]
    [MemberData(nameof(DeterminismCases))]
    public void Encoding_IsDeterministic(Func<DeltaDocument> docFactory, Func<BinaryDeltaOptions> optionsFactory)
    {
        var options = optionsFactory();
        var bytes1 = Encode(docFactory(), options);
        var bytes2 = Encode(docFactory(), options);
        var bytes3 = Encode(docFactory(), options);

        Assert.Equal(bytes1, bytes2);
        Assert.Equal(bytes1, bytes3);
    }

    /// <summary>Validates that toggling header mode changes the payload.</summary>
    [Fact]
    public void Encoding_DiffersWhenHeaderFlagChanges()
    {
        var doc = CreateNestedDocument();
        var headerless = Encode(doc, new BinaryDeltaOptions { IncludeHeader = false, UseStringTable = false, UseTypeTable = false });
        var headerful = Encode(doc, new BinaryDeltaOptions { IncludeHeader = true, UseStringTable = true, UseTypeTable = true });

        Assert.NotEqual(headerless, headerful);
    }

    /// <summary>Ensures the string table reduces payload size when strings repeat.</summary>
    [Fact]
    public void StringTable_ReducesPayloadForRepeatedStrings()
    {
        var doc = CreateStringHeavyDocument(32);
        var withTable = Encode(doc, new BinaryDeltaOptions { IncludeHeader = true, UseStringTable = true, UseTypeTable = false });
        var withoutTable = Encode(doc, new BinaryDeltaOptions { IncludeHeader = true, UseStringTable = false, UseTypeTable = false });

        Assert.True(withTable.Length < withoutTable.Length,
            $"Expected string table payload to shrink size ({withTable.Length} vs {withoutTable.Length}).");
    }

    /// <summary>Ensures the type table deduplicates enum metadata when enabled.</summary>
    [Fact]
    public void TypeTable_ReducesEnumMetadataOverhead()
    {
        var doc = CreateEnumRichDocument();
        var withTypeTable = Encode(doc, new BinaryDeltaOptions { IncludeHeader = true, UseStringTable = false, UseTypeTable = true });
        var withoutTypeTable = Encode(doc, new BinaryDeltaOptions { IncludeHeader = true, UseStringTable = false, UseTypeTable = false });

        Assert.True(withTypeTable.Length < withoutTypeTable.Length,
            $"Expected type table payload to shrink size ({withTypeTable.Length} vs {withoutTypeTable.Length}).");
    }

    /// <summary>Verifies stable type fingerprints affect the emitted header bytes.</summary>
    [Fact]
    public void StableTypeFingerprintChangesHeader()
    {
        var doc = CreateSimpleDocument();
        var baseline = Encode(doc, new BinaryDeltaOptions { IncludeHeader = true, UseStringTable = false, UseTypeTable = false, StableTypeFingerprint = 0 });
        var different = Encode(doc, new BinaryDeltaOptions { IncludeHeader = true, UseStringTable = false, UseTypeTable = false, StableTypeFingerprint = 0x0F0F0F0F0F0F0F0FUL });

        Assert.NotEqual(baseline, different);
    }

    /// <summary>Rejects headerless payloads when header mode is requested.</summary>
    [Fact]
    public void ReadFailsOnHeaderMismatch()
    {
        var doc = CreateSimpleDocument();
        var headerlessPayload = Encode(doc, new BinaryDeltaOptions { IncludeHeader = false });
        var readerOptions = new BinaryDeltaOptions { IncludeHeader = true };

        var ex = Assert.Throws<InvalidOperationException>(() => BinaryDeltaCodec.Read(headerlessPayload, readerOptions));
        Assert.Contains("Invalid BinaryDelta header magic", ex.Message);
    }

    /// <summary>Rejects headerful payloads when headerless mode is requested.</summary>
    [Fact]
    public void ReadFailsOnHeaderMismatch_Inverse()
    {
        var doc = CreateSimpleDocument();
        var headerfulPayload = Encode(doc, new BinaryDeltaOptions { IncludeHeader = true, UseStringTable = true, UseTypeTable = true });
        var readerOptions = new BinaryDeltaOptions { IncludeHeader = false };

        Assert.ThrowsAny<Exception>(() => BinaryDeltaCodec.Read(headerfulPayload, readerOptions));
    }

    /// <summary>Throws when encountering a newer, unsupported version marker.</summary>
    [Fact]
    public void ReadFailsForUnsupportedVersion()
    {
        var doc = CreateSimpleDocument();
        var options = new BinaryDeltaOptions { IncludeHeader = true };
        var payload = Encode(doc, options);
        payload[4] = 0x02;

        var ex = Assert.Throws<NotSupportedException>(() => BinaryDeltaCodec.Read(payload, options));
        Assert.Contains("Unsupported BinaryDelta version", ex.Message);
    }

    /// <summary>Throws when the payload terminates unexpectedly.</summary>
    [Fact]
    public void ReadFailsOnTruncatedPayload()
    {
        var doc = CreateNestedDocument();
        var options = new BinaryDeltaOptions { IncludeHeader = true, UseStringTable = true, UseTypeTable = true };
        var payload = Encode(doc, options);

        Assert.True(payload.Length > 1, "Encoded payload unexpectedly empty.");
        var truncated = payload.Take(payload.Length - 1).ToArray();

        Assert.Throws<InvalidOperationException>(() => BinaryDeltaCodec.Read(truncated, options));
    }

    /// <summary>Detects corruption in the value stream.</summary>
    [Fact]
    public void ReadFailsOnCorruptedValueTag()
    {
        var doc = CreateSimpleDocument();
        var options = new BinaryDeltaOptions { IncludeHeader = false };
        var payload = Encode(doc, options);

        var tagIndex = Array.IndexOf(payload, (byte)15);
        Assert.True(tagIndex >= 0, "Test payload did not contain an inline string tag.");
        payload[tagIndex] = 0xF0;

        var ex = Assert.Throws<InvalidOperationException>(() => BinaryDeltaCodec.Read(payload, options));
        Assert.Contains("Unknown value tag", ex.Message);
    }

    /// <summary>Enforces the MaxOps safety cap during decoding.</summary>
    [Fact]
    public void MaxOpsSafetyIsEnforced()
    {
        var doc = CreateStringHeavyDocument(8);
        var encoded = Encode(doc, new BinaryDeltaOptions { IncludeHeader = false });
        var readerOptions = new BinaryDeltaOptions { IncludeHeader = false };
        readerOptions.Safety.MaxOps = 2;

        var ex = Assert.Throws<InvalidOperationException>(() => BinaryDeltaCodec.Read(encoded, readerOptions));
        Assert.Contains("Bound exceeded", ex.Message);
    }

    /// <summary>Enforces the MaxNesting safety cap during decoding.</summary>
    [Fact]
    public void MaxNestingSafetyIsEnforced()
    {
        var doc = CreateNestedDocument();
        var encoded = Encode(doc, new BinaryDeltaOptions { IncludeHeader = false });
        var readerOptions = new BinaryDeltaOptions { IncludeHeader = false };
        readerOptions.Safety.MaxNesting = 0;

        var ex = Assert.Throws<InvalidOperationException>(() => BinaryDeltaCodec.Read(encoded, readerOptions));
        Assert.Contains("Max nesting exceeded", ex.Message);
    }

    /// <summary>Enforces the MaxStringBytes safety cap during decoding.</summary>
    [Fact]
    public void MaxStringBytesSafetyIsEnforced()
    {
        var doc = new DeltaDocument();
        doc.Ops.Add(new DeltaOp(1, DeltaKind.SetMember, -1, null, new string('a', 64), null));
        var encoded = Encode(doc, new BinaryDeltaOptions { IncludeHeader = false });
        var readerOptions = new BinaryDeltaOptions { IncludeHeader = false };
        readerOptions.Safety.MaxStringBytes = 8;

        var ex = Assert.Throws<InvalidOperationException>(() => BinaryDeltaCodec.Read(encoded, readerOptions));
        Assert.Contains("Bound exceeded", ex.Message);
    }

    /// <summary>Round-trip preserves enum type identity regardless of option set.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void EnumValuesPreserveTypeIdentity(bool includeHeader, bool useTypeTable)
    {
        var doc = CreateEnumRichDocument();
        var options = new BinaryDeltaOptions { IncludeHeader = includeHeader, UseTypeTable = useTypeTable, UseStringTable = true };
        var encoded = Encode(doc, options);
        var decoded = BinaryDeltaCodec.Read(encoded, options);

        foreach (var op in decoded.Ops)
        {
            if (op.Value is Enum value)
            {
                Assert.Equal(typeof(OrderStatus), value.GetType());
            }
        }
    }

    /// <summary>Option toggles enum identity on the wire.</summary>
    [Fact]
    public void EnumTypeIdentityOptionTogglesValueShape()
    {
        var doc = new DeltaDocument();
        doc.Ops.Add(new DeltaOp(800, DeltaKind.SetMember, -1, null, OrderStatus.Submitted, null));
        doc.Ops.Add(new DeltaOp(801, DeltaKind.SetMember, -1, null, ByteStatus.Two, null));
        doc.Ops.Add(new DeltaOp(802, DeltaKind.SetMember, -1, null, OtherStatus.Ready, null));

        var identityOn = new BinaryDeltaOptions
        {
            IncludeHeader = true,
            UseStringTable = true,
            UseTypeTable = true,
            IncludeEnumTypeIdentity = true
        };

        var encodedOn = Encode(doc, identityOn);
        var decodedOn = BinaryDeltaCodec.Read(encodedOn, identityOn);
        Assert.Equal(3, decodedOn.Ops.Count);
        Assert.Equal(OrderStatus.Submitted, Assert.IsType<OrderStatus>(decodedOn.Ops[0].Value));
        Assert.Equal(ByteStatus.Two, Assert.IsType<ByteStatus>(decodedOn.Ops[1].Value));
        Assert.Equal(OtherStatus.Ready, Assert.IsType<OtherStatus>(decodedOn.Ops[2].Value));

        var identityOff = new BinaryDeltaOptions
        {
            IncludeHeader = true,
            UseStringTable = true,
            UseTypeTable = true,
            IncludeEnumTypeIdentity = false
        };

        var encodedOff = Encode(doc, identityOff);
        Assert.DoesNotContain((byte)21, encodedOff);
        var decodedOff = BinaryDeltaCodec.Read(encodedOff, identityOff);
        Assert.Equal(3, decodedOff.Ops.Count);

        var firstPrimitive = Assert.IsType<int>(decodedOff.Ops[0].Value);
        Assert.Equal((int)OrderStatus.Submitted, firstPrimitive);
        var secondPrimitive = Assert.IsType<byte>(decodedOff.Ops[1].Value);
        Assert.Equal((byte)ByteStatus.Two, secondPrimitive);
        var thirdPrimitive = Assert.IsType<int>(decodedOff.Ops[2].Value);
        Assert.Equal((int)OtherStatus.Ready, thirdPrimitive);
    }

    /// <summary>Shared codec entry points remain safe when accessed concurrently.</summary>
    [Fact]
    public void EncodeDecode_IsThreadSafeAcrossParallelCalls()
    {
        var doc = CreateNestedDocument();
        var options = new BinaryDeltaOptions { IncludeHeader = true, UseStringTable = true, UseTypeTable = true };

        Parallel.For(0, Environment.ProcessorCount, _ =>
        {
            var encoded = Encode(doc, options);
            var decoded = BinaryDeltaCodec.Read(encoded, options);
            AssertDocsEqual(doc, decoded, options, "parallel");
        });
    }

    /// <summary>Randomized round-trips cover a wide swath of operation/value combinations.</summary>
    [Fact]
    public void RandomizedRoundTripsStayStable()
    {
        var rng = new Random(0x0C0DEC0D);

        for (var i = 0; i < 25; i++)
        {
            var doc = GenerateRandomDocument(rng, depth: 0, maxDepth: 3, maxOps: 12);
            foreach (var options in new[]
                     {
                         new BinaryDeltaOptions { IncludeHeader = false },
                         new BinaryDeltaOptions { IncludeHeader = true, UseStringTable = true, UseTypeTable = true },
                         new BinaryDeltaOptions { IncludeHeader = true, UseStringTable = false, UseTypeTable = true },
                         new BinaryDeltaOptions { IncludeHeader = true, UseStringTable = true, UseTypeTable = true, IncludeEnumTypeIdentity = false }
                     })
            {
                var encoded = Encode(doc, options);
                var decoded = BinaryDeltaCodec.Read(encoded, options);
                AssertDocsEqual(doc, decoded, options, $"fuzz[{i}-{(options.IncludeHeader ? "header" : "bare")},{(options.UseStringTable ? "strings" : "noStrings")}]" );
            }
        }
    }

    /// <summary>Stress round-trips a very large document (opt-in via RUN_LONG_CODEC_TESTS).</summary>
    [Fact]
    [Trait("Category", "Long")]
    public void LargeDocumentRoundTrip()
    {
        var doc = CreateLargeDocument(50_000);
        var options = new BinaryDeltaOptions { IncludeHeader = false };
        var encoded = Encode(doc, options);
        var decoded = BinaryDeltaCodec.Read(encoded, options);

        AssertDocsEqual(doc, decoded, options, "long-doc");
    }

    private static IEnumerable<DocCase> DocumentCases()
    {
        yield return new DocCase("empty", CreateEmptyDocument);
        yield return new DocCase("simple", CreateSimpleDocument);
        yield return new DocCase("multi-value", CreateMultiValueDocument);
        yield return new DocCase("nested", CreateNestedDocument);
        yield return new DocCase("string-heavy", () => CreateStringHeavyDocument(24));
    }

    private static IEnumerable<OptionCase> OptionCases()
    {
        yield return new OptionCase("headerless", () => new BinaryDeltaOptions { IncludeHeader = false, UseStringTable = false, UseTypeTable = false });
        yield return new OptionCase("header+tabled", () => new BinaryDeltaOptions { IncludeHeader = true, UseStringTable = true, UseTypeTable = true });
        yield return new OptionCase("header+no-string", () => new BinaryDeltaOptions { IncludeHeader = true, UseStringTable = false, UseTypeTable = true });
        yield return new OptionCase("header+no-type", () => new BinaryDeltaOptions { IncludeHeader = true, UseStringTable = true, UseTypeTable = false });
        yield return new OptionCase("header+minimal", () => new BinaryDeltaOptions { IncludeHeader = true, UseStringTable = false, UseTypeTable = false });
        yield return new OptionCase("header+tables-no-enum-id", () => new BinaryDeltaOptions { IncludeHeader = true, UseStringTable = true, UseTypeTable = true, IncludeEnumTypeIdentity = false });
    }

    private static DeltaDocument CreateEmptyDocument() => new();

    private static DeltaDocument CreateSimpleDocument()
    {
        var doc = new DeltaDocument();
        doc.Ops.Add(new DeltaOp(1, DeltaKind.SetMember, -1, null, SharedString, null));
        doc.Ops.Add(new DeltaOp(2, DeltaKind.SetMember, -1, null, 42, null));
        doc.Ops.Add(new DeltaOp(3, DeltaKind.ReplaceObject, -1, null, OrderStatus.Completed, null));
        return doc;
    }

    private static DeltaDocument CreateMultiValueDocument()
    {
        var doc = new DeltaDocument();
        doc.Ops.Add(new DeltaOp(10, DeltaKind.SetMember, -1, null, (sbyte)-5, null));
        doc.Ops.Add(new DeltaOp(11, DeltaKind.SetMember, -1, null, (byte)250, null));
        doc.Ops.Add(new DeltaOp(12, DeltaKind.SetMember, -1, null, (short)-1234, null));
        doc.Ops.Add(new DeltaOp(13, DeltaKind.SetMember, -1, null, (ushort)65500, null));
        doc.Ops.Add(new DeltaOp(14, DeltaKind.SetMember, -1, null, -123456789, null));
        doc.Ops.Add(new DeltaOp(15, DeltaKind.SetMember, -1, null, (uint)123456789, null));
        doc.Ops.Add(new DeltaOp(16, DeltaKind.SetMember, -1, null, (long)-9876543210, null));
        doc.Ops.Add(new DeltaOp(17, DeltaKind.SetMember, -1, null, (ulong)9876543210, null));
        doc.Ops.Add(new DeltaOp(18, DeltaKind.SetMember, -1, null, '?', null));
        doc.Ops.Add(new DeltaOp(19, DeltaKind.SetMember, -1, null, 3.14f, null));
        doc.Ops.Add(new DeltaOp(20, DeltaKind.SetMember, -1, null, 2.718281828459045, null));
        doc.Ops.Add(new DeltaOp(21, DeltaKind.SetMember, -1, null, 12345.6789m, null));
        doc.Ops.Add(new DeltaOp(22, DeltaKind.SetMember, -1, null, SampleGuid, null));
        doc.Ops.Add(new DeltaOp(23, DeltaKind.SetMember, -1, null, SampleDateTime, null));
        doc.Ops.Add(new DeltaOp(24, DeltaKind.SetMember, -1, null, SampleTimeSpan, null));
        doc.Ops.Add(new DeltaOp(25, DeltaKind.SetMember, -1, null, SampleDateTimeOffset, null));
        doc.Ops.Add(new DeltaOp(26, DeltaKind.SetMember, -1, null, new byte[] { 10, 20, 30, 40 }, null));
        doc.Ops.Add(new DeltaOp(27, DeltaKind.SetMember, -1, null, new[] { 1, 2, 3, 4 }, null));
        doc.Ops.Add(new DeltaOp(28, DeltaKind.SetMember, -1, null, new List<string> { "alpha", SharedString, "beta" }, null));
        doc.Ops.Add(new DeltaOp(29, DeltaKind.SetMember, -1, null, new Dictionary<string, object?> { ["first"] = 1, ["second"] = SharedString }, null));
        doc.Ops.Add(new DeltaOp(30, DeltaKind.SetMember, -1, null, new[] { SharedString, "local-note" }, null));
        doc.Ops.Add(new DeltaOp(31, DeltaKind.SetMember, -1, null, OrderStatus.Submitted, null));
        return doc;
    }

    private static DeltaDocument CreateNestedDocument()
    {
        var doc = new DeltaDocument();

        var memberNested = new DeltaDocument();
        memberNested.Ops.Add(new DeltaOp(301, DeltaKind.SetMember, -1, null, new List<int> { 1, 2, 3 }, null));
        memberNested.Ops.Add(new DeltaOp(302, DeltaKind.DictSet, -1, "inner-key", 99, null));

        var dictNested = new DeltaDocument();
        dictNested.Ops.Add(new DeltaOp(401, DeltaKind.SetMember, -1, null, SharedString, null));
        dictNested.Ops.Add(new DeltaOp(402, DeltaKind.SeqAddAt, 0, null, OrderStatus.Draft, null));

        var seqNested = new DeltaDocument();
        seqNested.Ops.Add(new DeltaOp(501, DeltaKind.SetMember, -1, null, SampleBytes, null));

        doc.Ops.Add(new DeltaOp(101, DeltaKind.SeqAddAt, 0, null, SharedString, null));
        doc.Ops.Add(new DeltaOp(102, DeltaKind.SeqReplaceAt, 1, null, 5.5m, null));
        doc.Ops.Add(new DeltaOp(103, DeltaKind.SeqRemoveAt, 2, null, SharedString, null));
        doc.Ops.Add(new DeltaOp(104, DeltaKind.DictSet, -1, "primary", new List<string> { SharedString, "delta" }, null));
        doc.Ops.Add(new DeltaOp(105, DeltaKind.DictNested, -1, "nested", null, dictNested));
        doc.Ops.Add(new DeltaOp(106, DeltaKind.NestedMember, -1, null, null, memberNested));
        doc.Ops.Add(new DeltaOp(107, DeltaKind.SeqNestedAt, 0, null, null, seqNested));

        return doc;
    }

    private static DeltaDocument CreateStringHeavyDocument(int repeat)
    {
        var doc = new DeltaDocument();
        for (var i = 0; i < repeat; i++)
            doc.Ops.Add(new DeltaOp(200 + i, DeltaKind.SetMember, -1, null, SharedString, null));

        return doc;
    }

    private static DeltaDocument CreateEnumRichDocument()
    {
        var doc = new DeltaDocument();
        var values = Enum.GetValues<OrderStatus>();
        for (var i = 0; i < 16; i++)
            doc.Ops.Add(new DeltaOp(500 + i, DeltaKind.SetMember, -1, null, values[i % values.Length], null));

        return doc;
    }

    private static DeltaDocument CreateLargeDocument(int opCount)
    {
        var doc = new DeltaDocument();
        for (var i = 0; i < opCount; i++)
            doc.Ops.Add(new DeltaOp(700 + (i % 32), DeltaKind.SeqAddAt, i, null, i, null));

        return doc;
    }

    private static DeltaDocument GenerateRandomDocument(Random rng, int depth, int maxDepth, int maxOps)
    {
        var doc = new DeltaDocument();
        var ops = rng.Next(1, maxOps + 1);
        for (var i = 0; i < ops; i++)
            doc.Ops.Add(CreateRandomOp(rng, depth, maxDepth));

        return doc;
    }

    private static DeltaOp CreateRandomOp(Random rng, int depth, int maxDepth)
    {
        var memberIndex = rng.Next(0, 512);
        var kind = (DeltaKind)rng.Next(Enum.GetValues<DeltaKind>().Length);
        var index = -1;
        object? key = null;
        object? value = null;
        DeltaDocument? nested = null;

        switch (kind)
        {
            case DeltaKind.ReplaceObject:
            case DeltaKind.SetMember:
                value = CreateRandomValue(rng, depth);
                break;

            case DeltaKind.NestedMember:
                nested = depth < maxDepth
                    ? GenerateRandomDocument(rng, depth + 1, maxDepth, rng.Next(0, 6))
                    : new DeltaDocument();
                break;

            case DeltaKind.SeqAddAt:
            case DeltaKind.SeqReplaceAt:
                index = rng.Next(0, 16);
                value = CreateRandomValue(rng, depth);
                break;

            case DeltaKind.SeqRemoveAt:
                index = rng.Next(0, 16);
                value = CreateRandomValue(rng, depth);
                break;

            case DeltaKind.SeqNestedAt:
                index = rng.Next(0, 16);
                nested = depth < maxDepth
                    ? GenerateRandomDocument(rng, depth + 1, maxDepth, rng.Next(0, 6))
                    : new DeltaDocument();
                break;

            case DeltaKind.DictSet:
                key = CreateRandomKey(rng);
                value = CreateRandomValue(rng, depth);
                break;

            case DeltaKind.DictRemove:
                key = CreateRandomKey(rng);
                break;

            case DeltaKind.DictNested:
                key = CreateRandomKey(rng);
                nested = depth < maxDepth
                    ? GenerateRandomDocument(rng, depth + 1, maxDepth, rng.Next(0, 6))
                    : new DeltaDocument();
                break;
        }

        return new DeltaOp(memberIndex, kind, index, key, value, nested);
    }

    private static object CreateRandomKey(Random rng)
        => RandomStrings[rng.Next(RandomStrings.Length)];

    private static object? CreateRandomValue(Random rng, int depth)
    {
        var choice = rng.Next(0, 18);
        return choice switch
        {
            0 => null,
            1 => rng.Next(-5000, 5000),
            2 => (long)rng.NextInt64(-5_000_000, 5_000_000),
            3 => rng.NextDouble() * 1234.567,
            4 => rng.Next(0, 2) == 0,
            5 => new decimal(rng.NextDouble() * 987.65),
            6 => RandomStrings[rng.Next(RandomStrings.Length)],
            7 => SampleGuid,
            8 => SampleDateTime.AddMinutes(rng.Next(-60, 60)),
            9 => SampleTimeSpan + TimeSpan.FromSeconds(rng.Next(-900, 900)),
            10 => SampleDateTimeOffset.AddMinutes(rng.Next(-60, 60)),
            11 => new byte[] { (byte)rng.Next(0, 255), (byte)rng.Next(0, 255), (byte)rng.Next(0, 255) },
            12 => new[] { rng.Next(-10, 10), rng.Next(-10, 10), rng.Next(-10, 10) },
            13 => new List<string> { RandomStrings[rng.Next(RandomStrings.Length)], RandomStrings[rng.Next(RandomStrings.Length)] },
            14 => new Dictionary<string, object?> { ["a"] = rng.Next(0, 10), ["b"] = RandomStrings[rng.Next(RandomStrings.Length)] },
            15 => (OrderStatus)rng.Next(0, Enum.GetValues<OrderStatus>().Length),
            16 => depth < 2
                ? new object?[] { rng.Next(-10, 10), RandomStrings[rng.Next(RandomStrings.Length)], (OrderStatus)rng.Next(0, 3) }
                : new object?[] { rng.Next(-10, 10), null },
            _ => SharedString + "-extra"
        };
    }

    private static byte[] Encode(DeltaDocument doc, BinaryDeltaOptions options)
    {
        var writer = new ArrayBufferWriter<byte>();
        BinaryDeltaCodec.Write(doc, writer, options);
        return writer.WrittenSpan.ToArray();
    }

    private static void AssertDocsEqual(DeltaDocument expected, DeltaDocument actual, BinaryDeltaOptions options, string context)
    {
        AssertEqual(expected.Ops.Count, actual.Ops.Count, $"{context}.opCount");

        for (var i = 0; i < expected.Ops.Count; i++)
        {
            var left = expected.Ops[i];
            var right = actual.Ops[i];
            var opContext = $"{context}.op[{i}]";

            AssertEqual(left.Kind, right.Kind, $"{opContext}.kind");
            AssertEqual(left.MemberIndex, right.MemberIndex, $"{opContext}.memberIndex");
            AssertEqual(left.Index, right.Index, $"{opContext}.index");
            AssertValueEqual(left.Key, right.Key, $"{opContext}.key", options);
            AssertValueEqual(left.Value, right.Value, $"{opContext}.value", options);

            if (left.Nested is null)
            {
                Assert.True(right.Nested is null, $"{opContext}.nested expected null but found document");
            }
            else
            {
                Assert.True(right.Nested is not null, $"{opContext}.nested expected document but was null");
                AssertDocsEqual(left.Nested, right.Nested!, options, $"{opContext}.nested");
            }
        }
    }

    private static void AssertValueEqual(object? expected, object? actual, string context, BinaryDeltaOptions options)
    {
        if (!options.IncludeEnumTypeIdentity)
        {
            expected = NormalizeEnumValue(expected);
            actual = NormalizeEnumValue(actual);
        }

        if (expected is null || actual is null)
        {
            Assert.True(Equals(expected, actual), $"{context} expected {FormatValue(expected)} but found {FormatValue(actual)}");
            return;
        }

        if (expected is Enum expectedEnum && actual is Enum actualEnum)
        {
            Assert.Equal(expectedEnum.GetType(), actualEnum.GetType());
            Assert.Equal(Convert.ToInt64(expectedEnum), Convert.ToInt64(actualEnum));
            return;
        }

        if (expected is byte[] expectedBytes && actual is byte[] actualBytes)
        {
            AssertEqual(expectedBytes.Length, actualBytes.Length, $"{context}.length");
            for (var i = 0; i < expectedBytes.Length; i++)
                AssertEqual(expectedBytes[i], actualBytes[i], $"{context}[{i}]");
            return;
        }

        if (expected is Array expectedArray && actual is Array actualArray)
        {
            AssertEqual(expectedArray.Length, actualArray.Length, $"{context}.length");
            for (var i = 0; i < expectedArray.Length; i++)
                AssertValueEqual(expectedArray.GetValue(i), actualArray.GetValue(i), $"{context}[{i}]", options);
            return;
        }

        if (expected is IList expectedList && actual is IList actualList)
        {
            AssertEqual(expectedList.Count, actualList.Count, $"{context}.count");
            for (var i = 0; i < expectedList.Count; i++)
                AssertValueEqual(expectedList[i], actualList[i], $"{context}[{i}]", options);
            return;
        }

        if (expected is IDictionary expectedDict && actual is IDictionary actualDict)
        {
            AssertEqual(expectedDict.Count, actualDict.Count, $"{context}.count");
            foreach (DictionaryEntry entry in expectedDict)
            {
                var normalizedKey = options.IncludeEnumTypeIdentity ? entry.Key : NormalizeEnumValue(entry.Key);
                Assert.True(actualDict.Contains(normalizedKey!), $"{context} missing key {FormatValue(normalizedKey)}");
                AssertValueEqual(entry.Value, actualDict[normalizedKey!], $"{context}[{FormatValue(normalizedKey)}]", options);
            }

            return;
        }

        if (expected is DeltaDocument expectedDoc && actual is DeltaDocument actualDoc)
        {
            AssertDocsEqual(expectedDoc, actualDoc, options, $"{context}.doc");
            return;
        }

        Assert.True(Equals(expected, actual), $"{context} expected {FormatValue(expected)} but found {FormatValue(actual)}");
    }

    private static object? NormalizeEnumValue(object? value)
    {
        if (value is null) return null;

        if (value is Enum enumValue)
        {
            var underlying = Enum.GetUnderlyingType(enumValue.GetType());
            return Convert.ChangeType(enumValue, underlying);
        }

        return value;
    }

    private static void AssertEqual<T>(T expected, T actual, string context)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new XunitException($"Mismatch at {context}: expected {FormatValue(expected)} but found {FormatValue(actual)}");
    }

    private static string FormatValue(object? value)
        => value switch
        {
            null => "null",
            string s => $"\"{s}\"",
            Enum e => $"{e.GetType().Name}.{e}",
            Array array => "[" + string.Join(",", array.Cast<object?>().Select(FormatValue)) + "]",
            _ => value?.ToString() ?? "<null>"
        };
}
