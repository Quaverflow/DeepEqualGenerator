using System;
using System.Buffers;

namespace DeepEqual.Generator.Shared;

/// <summary>Ergonomic bridge for tests and adapters.</summary>
public static class DeltaDocumentBinaryExtensions
{
    /// <summary>Encode to binary.</summary>
    public static void ToBinary(this DeltaDocument doc, IBufferWriter<byte> output, BinaryDeltaOptions? options = null)
    {
        BinaryDeltaCodec.Write(doc, output, options);
    }

    /// <summary>Decode from binary.</summary>
    public static DeltaDocument FromBinary(ReadOnlySpan<byte> data, BinaryDeltaOptions? options = null)
    {
        return BinaryDeltaCodec.Read(data, options);
    }
}