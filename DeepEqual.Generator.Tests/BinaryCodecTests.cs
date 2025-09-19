#nullable enable
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DeepEqual.Generator.Shared;
using Xunit;

public sealed class BinaryDeltaCodecTests
{
    private static DeltaDocument RoundTrip(DeltaDocument doc, BinaryDeltaOptions? opts = null)
    {
        var buf = new ArrayBufferWriter<byte>();
        BinaryDeltaCodec.Write(doc, buf, opts);
        return BinaryDeltaCodec.Read(buf.WrittenSpan, opts);
    }

    private static void AssertDocsEqual(DeltaDocument a, DeltaDocument b)
    {
        Assert.Equal(a.Operations.Count, b.Operations.Count);
        for (int i = 0; i < a.Operations.Count; i++)
        {
            var x = a.Operations[i];
            var y = b.Operations[i];
            Assert.Equal(x.Kind, y.Kind);
            Assert.Equal(x.MemberIndex, y.MemberIndex);
            Assert.Equal(x.Index, y.Index);
            AssertValueEqual(x.Key, y.Key);
            AssertValueEqual(x.Value, y.Value);

            if ((x.Nested is null) != (y.Nested is null))
                Assert.True(false, $"Nested presence mismatch at op {i}");

            if (x.Nested is not null)
                AssertDocsEqual(x.Nested, y.Nested);
        }
    }

    private static void AssertValueEqual(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return;
        if (a is null || b is null) Assert.Equal(a, b);

        if (a is byte[] ba && b is byte[] bb)
        {
            Assert.Equal(ba, bb);
            return;
        }

        if (a is Array arrA && b is Array arrB)
        {
            Assert.Equal(arrA.Length, arrB.Length);
            for (int i = 0; i < arrA.Length; i++)
                AssertValueEqual(arrA.GetValue(i), arrB.GetValue(i));
            return;
        }

        if (a is IList la && b is IList lb)
        {
            Assert.Equal(la.Count, lb.Count);
            for (int i = 0; i < la.Count; i++)
                AssertValueEqual(la[i], lb[i]);
            return;
        }

        if (a is IDictionary da && b is IDictionary db)
        {
            Assert.Equal(da.Count, db.Count);
            foreach (DictionaryEntry e in da)
            {
                Assert.True(db.Contains(e.Key), $"Missing key {e.Key}");
                AssertValueEqual(e.Value, db[e.Key]);
            }
            return;
        }

               if (a.GetType().IsEnum && b.GetType().IsEnum)
        {
            Assert.Equal(a.GetType(), b.GetType());
            Assert.Equal(EnumBits(a), EnumBits(b));
            return;

            static ulong EnumBits(object e)
            {
                var ut = Enum.GetUnderlyingType(e.GetType());
                switch (Type.GetTypeCode(ut))
                {
                    case TypeCode.SByte:
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                    case TypeCode.Int64:
                        return unchecked((ulong)Convert.ToInt64(e));
                    case TypeCode.Byte:
                    case TypeCode.UInt16:
                    case TypeCode.UInt32:
                    case TypeCode.UInt64:
                        return Convert.ToUInt64(e);
                    default:
                        throw new NotSupportedException($"Unexpected enum underlying type: {ut}");
                }
            }
        }

               Assert.Equal(a, b);
    }

       private enum E8 : byte { Zero = 0, One = 1, Big = 200 }
    private enum E32 : int { Minus = -3, Zero = 0, Plus = 42 }

    private static DeltaDocument Doc(params DeltaOp[] ops)
    {
        var d = new DeltaDocument();
        var w = new DeltaWriter(d);

        foreach (var op in ops)
        {
            switch (op.Kind)
            {
                case DeltaKind.ReplaceObject:
                    w.WriteReplaceObject(op.Value);
                    break;

                case DeltaKind.SetMember:
                    w.WriteSetMember(op.MemberIndex, op.Value);
                    break;

                case DeltaKind.NestedMember:
                    if (op.Nested is null) throw new InvalidOperationException("NestedMember requires Nested");
                    w.WriteNestedMember(op.MemberIndex, op.Nested);
                    break;

                case DeltaKind.SeqAddAt:
                    w.WriteSeqAddAt(op.MemberIndex, op.Index, op.Value);
                    break;

                case DeltaKind.SeqReplaceAt:
                    w.WriteSeqReplaceAt(op.MemberIndex, op.Index, op.Value);
                    break;

                case DeltaKind.SeqRemoveAt:
                    w.WriteSeqRemoveAt(op.MemberIndex, op.Index);
                    break;

                case DeltaKind.DictSet:
                    w.WriteDictSet(op.MemberIndex, op.Key!, op.Value);
                    break;

                case DeltaKind.DictRemove:
                    w.WriteDictRemove(op.MemberIndex, op.Key!);
                    break;

                case DeltaKind.DictNested:
                    if (op.Nested is null) throw new InvalidOperationException("DictNested requires Nested");
                    w.WriteDictNested(op.MemberIndex, op.Key!, op.Nested);
                    break;

                default:
                    throw new NotSupportedException($"Unhandled kind: {op.Kind}");
            }
        }

        return d;
    }

    private static void AppendOp(ref DeltaWriter w, in DeltaOp op)
    {
        switch (op.Kind)
        {
            case DeltaKind.ReplaceObject:
                w.WriteReplaceObject(op.Value);
                break;

            case DeltaKind.SetMember:
                w.WriteSetMember(op.MemberIndex, op.Value);
                break;

            case DeltaKind.NestedMember:
                w.WriteNestedMember(op.MemberIndex, op.Nested!);
                break;

            case DeltaKind.SeqReplaceAt:
                w.WriteSeqReplaceAt(op.MemberIndex, op.Index, op.Value);
                break;

            case DeltaKind.SeqAddAt:
                w.WriteSeqAddAt(op.MemberIndex, op.Index, op.Value);
                break;

            case DeltaKind.SeqRemoveAt:
                w.WriteSeqRemoveAt(op.MemberIndex, op.Index);
                break;

            case DeltaKind.DictSet:
                w.WriteDictSet(op.MemberIndex, op.Key!, op.Value);
                break;

            case DeltaKind.DictRemove:
                w.WriteDictRemove(op.MemberIndex, op.Key!);
                break;

            case DeltaKind.DictNested:
                w.WriteDictNested(op.MemberIndex, op.Key!, op.Nested!);
                break;

            default:
                throw new System.InvalidOperationException($"Unsupported kind {op.Kind}");
        }
    }
    private static DeltaDocument NestedDemo()
    {
        var inner = Doc(
            new DeltaOp(MemberIndex: 2, Kind: DeltaKind.SetMember, Index: -1, Key: null, Value: "inside", Nested: null),
            new DeltaOp(MemberIndex: 3, Kind: DeltaKind.SeqAddAt, Index: 1, Key: null, Value: 999, Nested: null)
        );

        return Doc(
            new DeltaOp(MemberIndex: 1, Kind: DeltaKind.SetMember, Index: -1, Key: null, Value: 123, Nested: null),
            new DeltaOp(MemberIndex: 5, Kind: DeltaKind.NestedMember, Index: -1, Key: null, Value: null, Nested: inner)
        );
    }

    [Fact]
    public void RoundTrip_Headerless_AllScalarTypes()
    {
        var doc = Doc(
            new DeltaOp(1, DeltaKind.SetMember, -1, null, (sbyte)-7, null),
            new DeltaOp(2, DeltaKind.SetMember, -1, null, (byte)200, null),
            new DeltaOp(3, DeltaKind.SetMember, -1, null, (short)-12345, null),
            new DeltaOp(4, DeltaKind.SetMember, -1, null, (ushort)54321, null),
            new DeltaOp(5, DeltaKind.SetMember, -1, null, -123456789, null),
            new DeltaOp(6, DeltaKind.SetMember, -1, null, (uint)4123456789, null),
            new DeltaOp(7, DeltaKind.SetMember, -1, null, (long)-9_000_000_000, null),
            new DeltaOp(8, DeltaKind.SetMember, -1, null, (ulong)9_000_000_000, null),
            new DeltaOp(9, DeltaKind.SetMember, -1, null, true, null),
            new DeltaOp(10, DeltaKind.SetMember, -1, null, false, null),
            new DeltaOp(11, DeltaKind.SetMember, -1, null, 'ß', null),
            new DeltaOp(12, DeltaKind.SetMember, -1, null, 123.25f, null),
            new DeltaOp(13, DeltaKind.SetMember, -1, null, 123.25d, null),
            new DeltaOp(14, DeltaKind.SetMember, -1, null, 79228162514264337593543950335m, null),            new DeltaOp(15, DeltaKind.SetMember, -1, null, "hello 🌍", null),
            new DeltaOp(16, DeltaKind.SetMember, -1, null, Guid.NewGuid(), null),
            new DeltaOp(17, DeltaKind.SetMember, -1, null, new DateTime(2020, 2, 29, 12, 34, 56, DateTimeKind.Utc), null),
            new DeltaOp(18, DeltaKind.SetMember, -1, null, TimeSpan.FromMilliseconds(123456789), null),
            new DeltaOp(19, DeltaKind.SetMember, -1, null, new DateTimeOffset(new DateTime(2021, 1, 2, 3, 4, 5, DateTimeKind.Unspecified), TimeSpan.FromMinutes(150)), null)
        );

        var round = RoundTrip(doc, new BinaryDeltaOptions { IncludeHeader = false });
        AssertDocsEqual(doc, round);
    }

    [Fact]
    public void RoundTrip_Headerful_StringAndTypeTables_EnumsLossless()
    {
        var doc = Doc(
            new DeltaOp(1, DeltaKind.SetMember, -1, null, E8.Big, null),
            new DeltaOp(2, DeltaKind.SetMember, -1, null, E32.Minus, null),
            new DeltaOp(3, DeltaKind.DictSet, -1, Key: "who", Value: "me", Nested: null),
            new DeltaOp(3, DeltaKind.DictSet, -1, Key: "who", Value: "you", Nested: null),            new DeltaOp(4, DeltaKind.ReplaceObject, -1, null, "replace-me", null)
        );

        var opts = new BinaryDeltaOptions
        {
            IncludeHeader = true,
            UseStringTable = true,
            UseTypeTable = true,
            StableTypeFingerprint = 0xAABBCCDDEEFF0011UL
        };

        var round = RoundTrip(doc, opts);
        AssertDocsEqual(doc, round);
    }

    [Fact]
    public void RoundTrip_Collections_Array_List_Dictionary()
    {
        var arr = new object?[] { 1, "x", (byte)5, null, true, 12.5 };
        var list = new List<int> { 1, 2, 3, 5, 8, 13 };
        var dict = new Dictionary<string, object?> { ["id"] = 42, ["name"] = "neo", ["ok"] = true };

        var doc = Doc(
            new DeltaOp(1, DeltaKind.SetMember, -1, null, arr, null),
            new DeltaOp(2, DeltaKind.SetMember, -1, null, list, null),
            new DeltaOp(3, DeltaKind.SetMember, -1, null, dict, null),
            new DeltaOp(4, DeltaKind.SetMember, -1, null, new byte[] { 1, 2, 3, 4, 5 }, null)
        );

        var round = RoundTrip(doc, new BinaryDeltaOptions { IncludeHeader = true });
        AssertDocsEqual(doc, round);
    }

    [Fact]
    public void RoundTrip_SequenceAndDictionaryOps_AllKinds()
    {
        var doc = Doc(
            new DeltaOp(1, DeltaKind.SeqAddAt, Index: 0, Key: null, Value: "a", Nested: null),
            new DeltaOp(1, DeltaKind.SeqAddAt, Index: 1, Key: null, Value: "b", Nested: null),
            new DeltaOp(1, DeltaKind.SeqReplaceAt, Index: 1, Key: null, Value: "B", Nested: null),
            new DeltaOp(1, DeltaKind.SeqRemoveAt, Index: 0, Key: null, Value: null, Nested: null),

            new DeltaOp(2, DeltaKind.DictSet, -1, Key: "who", Value: "me", Nested: null),
            new DeltaOp(2, DeltaKind.DictNested, -1, Key: "nested",
                        Value: null,
                        Nested: Doc(new DeltaOp(5, DeltaKind.SetMember, -1, null, 777, null))),
            new DeltaOp(2, DeltaKind.DictRemove, -1, Key: "who", Value: null, Nested: null)
        );

        var round = RoundTrip(doc, new BinaryDeltaOptions { IncludeHeader = false });
        AssertDocsEqual(doc, round);
    }

    [Fact]
    public void RoundTrip_NestedDocuments_MultiLevel()
    {
        var doc = NestedDemo();
        var round1 = RoundTrip(doc, new BinaryDeltaOptions { IncludeHeader = false });
        var round2 = RoundTrip(doc, new BinaryDeltaOptions { IncludeHeader = true, UseStringTable = true, UseTypeTable = true });
        AssertDocsEqual(doc, round1);
        AssertDocsEqual(doc, round2);
    }

    [Fact]
    public void Headerless_Enums_AreStillLossless_WithInlineIdentity()
    {
        var doc = Doc(
            new DeltaOp(1, DeltaKind.SetMember, -1, null, E8.One, null),
            new DeltaOp(2, DeltaKind.SetMember, -1, null, E32.Plus, null)
        );

        var round = RoundTrip(doc, new BinaryDeltaOptions { IncludeHeader = false });
        AssertDocsEqual(doc, round);
    }

    [Fact]
    public void Safety_MaxOps_Triggers()
    {
               var big = new DeltaDocument();
        var w = new DeltaWriter(big);
        for (int i = 0; i < 10_001; i++)
            w.WriteSetMember(1, i);

        var opts = new BinaryDeltaOptions { IncludeHeader = false };
        opts.Safety.MaxOps = 10_000;

        var buf = new ArrayBufferWriter<byte>();
        BinaryDeltaCodec.Write(big, buf, opts);

        Assert.Throws<InvalidOperationException>(() =>
            BinaryDeltaCodec.Read(buf.WrittenSpan, opts));
    }

    [Fact]
    public void Safety_MaxNesting_Triggers()
    {
               DeltaDocument current = new();
        for (int i = 0; i < 64; i++)
            current = Doc(new DeltaOp(i, DeltaKind.NestedMember, -1, null, null, current));

               var writeOpts = new BinaryDeltaOptions { IncludeHeader = false };
        writeOpts.Safety.MaxNesting = 1_000_000;

        var buf = new ArrayBufferWriter<byte>();
        BinaryDeltaCodec.Write(current, buf, writeOpts);

               var readOpts = new BinaryDeltaOptions { IncludeHeader = false };
        readOpts.Safety.MaxNesting = 32;

        Assert.Throws<InvalidOperationException>(() =>
            BinaryDeltaCodec.Read(buf.WrittenSpan, readOpts));
    }

    [Fact]
    public void ByteArray_RoundTrip()
    {
        var data = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        var doc = Doc(new DeltaOp(7, DeltaKind.SetMember, -1, null, data, null));

        var round = RoundTrip(doc, new BinaryDeltaOptions { IncludeHeader = true });
        AssertDocsEqual(doc, round);
    }

    [Fact]
    public void LargeNumbersAndNegative_StayExact()
    {
        var doc = Doc(
            new DeltaOp(1, DeltaKind.SetMember, -1, null, long.MinValue, null),
            new DeltaOp(2, DeltaKind.SetMember, -1, null, long.MaxValue, null),
            new DeltaOp(3, DeltaKind.SetMember, -1, null, int.MinValue, null),
            new DeltaOp(4, DeltaKind.SetMember, -1, null, int.MaxValue, null),
            new DeltaOp(5, DeltaKind.SetMember, -1, null, (ulong)ulong.MaxValue, null)
        );

        var round = RoundTrip(doc, new BinaryDeltaOptions { IncludeHeader = false });
        AssertDocsEqual(doc, round);
    }

    [Fact]
    public void Size_Usually_Smaller_Than_TrivialTextualRepresentation()
    {
               var doc = Doc(
            new DeltaOp(1, DeltaKind.SetMember, -1, null, "who", null),
            new DeltaOp(1, DeltaKind.SetMember, -1, null, "you", null),
            new DeltaOp(2, DeltaKind.DictSet, -1, "message", "hello hello hello hello hello", null)
        );

        var buf = new ArrayBufferWriter<byte>();
        BinaryDeltaCodec.Write(doc, buf, new BinaryDeltaOptions { IncludeHeader = true, UseStringTable = true });

        var textual = new StringBuilder();
        foreach (var op in doc.Operations)
        {
            textual.Append(op.Kind).Append('|')
                   .Append(op.MemberIndex).Append('|')
                   .Append(op.Index).Append('|')
                   .Append(op.Key?.ToString()).Append('|')
                   .Append(op.Value?.ToString()).AppendLine();
        }

               var binarySize = buf.WrittenCount;
        var textSize = Encoding.UTF8.GetByteCount(textual.ToString());
        Assert.True(binarySize < textSize, $"Binary {binarySize} vs textual {textSize}");
    }
}
