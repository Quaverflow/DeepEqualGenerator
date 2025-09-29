namespace DeepEqual.Generator.Shared;

/// <summary>
///     A single member diff: either a shallow replacement or a nested diff/delta object.
/// </summary>
public readonly record struct MemberChange(
    int MemberIndex,
    MemberChangeKind Kind,
    object? ValueOrDiff);