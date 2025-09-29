using System;
using System.Runtime.CompilerServices;

namespace DeepEqual.Generator.Shared;

/// <summary>
///     Writer used by generated helpers to append operations.
///     Provides zero-alloc nested scopes for user objects and dict values.
/// </summary>
public ref struct DeltaWriter(DeltaDocument doc)
{
    public DeltaDocument Document { get; } = doc ?? throw new ArgumentNullException(nameof(doc));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteSeqNestedAt(int memberIndex, int index, DeltaDocument nested)
    {
        Document.Ops.Add(new DeltaOp(memberIndex, DeltaKind.SeqNestedAt, index, null, null, nested));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SeqNestedScope BeginSeqNestedAt(int memberIndex, int index, out DeltaWriter nestedWriter)
    {
        var nested = DeltaDocument.Rent(8);
        nestedWriter = new DeltaWriter(nested);
        return new SeqNestedScope(this, memberIndex, index, nested);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteReplaceObject(object? newValue)
    {
        Document.Ops.Add(new DeltaOp(-1, DeltaKind.ReplaceObject, -1, null, newValue, null));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteSetMember(int memberIndex, object? value)
    {
        Document.Ops.Add(new DeltaOp(memberIndex, DeltaKind.SetMember, -1, null, value, null));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteNestedMember(int memberIndex, DeltaDocument nested)
    {
        Document.Ops.Add(new DeltaOp(memberIndex, DeltaKind.NestedMember, -1, null, null, nested));
    }

    public NestedMemberScope BeginNestedMember(int memberIndex, out DeltaWriter nestedWriter)
    {
        var nested = DeltaDocument.Rent(8);
        nestedWriter = new DeltaWriter(nested);
        return new NestedMemberScope(this, memberIndex, nested);
    }

    public DictNestedScope BeginDictNested(int memberIndex, object key, out DeltaWriter nestedWriter)
    {
        var nested = DeltaDocument.Rent(8);
        nestedWriter = new DeltaWriter(nested);
        return new DictNestedScope(this, memberIndex, key, nested);
    }

    public void WriteSeqReplaceAt(int memberIndex, int index, object? value)
    {
        Document.Ops.Add(new DeltaOp(memberIndex, DeltaKind.SeqReplaceAt, index, null, value, null));
    }

    public void WriteSeqAddAt(int memberIndex, int index, object? value)
    {
        Document.Ops.Add(new DeltaOp(memberIndex, DeltaKind.SeqAddAt, index, null, value, null));
    }

    public void WriteSeqRemoveAt(int memberIndex, int index)
    {
        Document.Ops.Add(new DeltaOp(memberIndex, DeltaKind.SeqRemoveAt, index, null, null, null));
    }

    public void WriteDictSet(int memberIndex, object key, object? value)
    {
        Document.Ops.Add(new DeltaOp(memberIndex, DeltaKind.DictSet, -1, key, value, null));
    }

    public void WriteDictRemove(int memberIndex, object key)
    {
        Document.Ops.Add(new DeltaOp(memberIndex, DeltaKind.DictRemove, -1, key, null, null));
    }

    public void WriteDictNested(int memberIndex, object key, DeltaDocument nested)
    {
        Document.Ops.Add(new DeltaOp(memberIndex, DeltaKind.DictNested, -1, key, null, nested));
    }

    public ref struct NestedMemberScope
    {
        private DeltaWriter _parent;
        private readonly int _memberIndex;
        private DeltaDocument? _nested;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal NestedMemberScope(DeltaWriter parent, int memberIndex, DeltaDocument nested)
        {
            _parent = parent;
            _memberIndex = memberIndex;
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
                _parent.WriteNestedMember(_memberIndex, doc);

            _nested = null;
        }
    }

    public ref struct DictNestedScope
    {
        private DeltaWriter _parent;
        private readonly int _memberIndex;
        private readonly object _key;
        private DeltaDocument? _nested;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal DictNestedScope(DeltaWriter parent, int memberIndex, object key, DeltaDocument nested)
        {
            _parent = parent;
            _memberIndex = memberIndex;
            _key = key;
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
                _parent.WriteDictNested(_memberIndex, _key, doc);

            _nested = null;
        }
    }
}