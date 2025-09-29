namespace DeepEqual.Generator.Shared;

public enum DeltaKind
{
    ReplaceObject = 0,

    SetMember = 1,
    NestedMember = 2,

    SeqReplaceAt = 10,
    SeqAddAt = 11,
    SeqRemoveAt = 12,
    SeqNestedAt = 13,
    DictSet = 20,
    DictRemove = 21,
    DictNested = 22
}