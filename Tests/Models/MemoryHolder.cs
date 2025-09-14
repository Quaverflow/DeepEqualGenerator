using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class MemoryHolder
{
    public Memory<byte> Buf { get; set; }
    public ReadOnlyMemory<byte> RBuf { get; set; }
}