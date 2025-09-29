namespace DeepEqual.Generator.Shared;

public enum MemberChangeKind
{
    /// <summary> Replace the whole member value (shallow). </summary>
    Set = 0,

    /// <summary> Nested structural diff (ValueOrDiff is IDiff or DeltaDocument). </summary>
    Nested = 1,

    /// <summary> Sequence or dictionary operation list (ValueOrDiff is a DeltaDocument). </summary>
    CollectionOps = 2
}