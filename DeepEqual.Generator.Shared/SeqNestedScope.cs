using System.Runtime.CompilerServices;

namespace DeepEqual.Generator.Shared;

public ref struct SeqNestedScope
{
    private DeltaWriter _parent;
    private readonly int _memberIndex;
    private readonly int _index;
    private DeltaDocument? _nested;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal SeqNestedScope(DeltaWriter parent, int memberIndex, int index, DeltaDocument nested)
    {
        _parent = parent;
        _memberIndex = memberIndex;
        _index = index;
        _nested = nested;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        var doc = _nested;
        if (doc is null) return;

        if (doc.IsEmpty)
            DeltaDocument.Return(doc);
        else
            _parent.WriteSeqNestedAt(_memberIndex, _index, doc);

        _nested = null;
    }
}