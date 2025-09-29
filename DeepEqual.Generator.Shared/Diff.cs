namespace DeepEqual.Generator.Shared;

/// <summary>
///     A non-generic empty diff — for "no info"/"not supported".
/// </summary>
public readonly struct Diff : IDiff
{
    public static readonly Diff Empty = new();
    public bool IsEmpty => true;
}