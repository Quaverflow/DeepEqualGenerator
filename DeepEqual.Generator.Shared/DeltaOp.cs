using System.Runtime.CompilerServices;

namespace DeepEqual.Generator.Shared;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly struct DeltaOp(
    int memberIndex,
    DeltaKind kind,
    int index,
    object? key,
    object? value,
    DeltaDocument? nested)
{
    public readonly int MemberIndex = memberIndex;
    public readonly DeltaKind Kind = kind;
    public readonly int Index = index;
    public readonly object? Key = key;
    public readonly object? Value = value;
    public readonly DeltaDocument? Nested = nested;
}