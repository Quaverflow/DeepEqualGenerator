using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class MemberKindContainer
{
    public Item ValDeep { get; set; } = new();

    [DeepCompare(Kind = CompareKind.Shallow)]
    public Item ValShallow { get; set; } = new();

    [DeepCompare(Kind = CompareKind.Reference)]
    public Item ValReference { get; set; } = new();

    [DeepCompare(Kind = CompareKind.Skip)] public Item ValSkipped { get; set; } = new();
}