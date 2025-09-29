using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DeepEqual.Generator.Shared;

/// <summary>
///     Reader for a delta document.
/// </summary>
public struct DeltaReader(DeltaDocument? doc)
{
    private readonly DeltaDocument _doc = doc ?? DeltaDocument.Empty;
    private int _pos = 0;

    public ReadOnlySpan<DeltaOp> AsSpan()
    {
        return CollectionsMarshal.AsSpan(_doc.Ops);
    }

    public bool TryRead(out DeltaOp op)
    {
        if (_pos < _doc.Ops.Count)
        {
            op = _doc.Ops[_pos++];
            return true;
        }

        op = default;
        return false;
    }

    public void ForEachMember(int memberIndex, Action<DeltaOp> action)
    {
        foreach (var op in _doc.Operations)
            if (op.MemberIndex == memberIndex)
                action(op);
    }

    public IEnumerable<DeltaOp> EnumerateAll()
    {
        return _doc.Operations;
    }

    public IEnumerable<DeltaOp> EnumerateMember(int memberIndex)
    {
        foreach (var op in _doc.Operations)
            if (op.MemberIndex == memberIndex)
                yield return op;
    }

    public void Reset()
    {
        _pos = 0;
    }
}