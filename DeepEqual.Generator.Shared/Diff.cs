using System.Collections.Generic;

namespace DeepEqual.Generator.Shared
{
    /// <summary> Marker for any diff payload. </summary>
    public interface IDiff
    {
        bool IsEmpty { get; }
    }

    /// <summary>
    /// A non-generic empty diff — for "no info"/"not supported".
    /// </summary>
    public readonly struct Diff : IDiff
    {
        public static readonly Diff Empty = new();
        public bool IsEmpty => true;
    }

    /// <summary>
    /// Strongly-typed structural diff for T.
    /// </summary>
    public readonly struct Diff<T> : IDiff
    {
        public bool HasChanges { get; }
        public bool IsReplacement { get; }
        public T? NewValue { get; }
        public IReadOnlyList<MemberChange>? MemberChanges { get; }

        private Diff(bool has, bool replace, T? newValue, IReadOnlyList<MemberChange>? changes)
        {
            HasChanges = has;
            IsReplacement = replace;
            NewValue = newValue;
            MemberChanges = changes;
        }

        public static Diff<T> Empty => new(false, false, default, null);

        public static Diff<T> Replacement(T? newValue) => new(true, true, newValue, null);

        public static Diff<T> Members(List<MemberChange> changes)
        {
            if (changes.Count == 0) return Empty;
            return new Diff<T>(true, false, default, changes);
        }

        public bool IsEmpty => !HasChanges;
    }

    /// <summary>
    /// A single member diff: either a shallow replacement or a nested diff/delta object.
    /// </summary>
    public readonly record struct MemberChange(
        int MemberIndex,
        MemberChangeKind Kind,
        object? ValueOrDiff);

    public enum MemberChangeKind
    {
        /// <summary> Replace the whole member value (shallow). </summary>
        Set = 0,

        /// <summary> Nested structural diff (ValueOrDiff is IDiff or DeltaDocument). </summary>
        Nested = 1,

        /// <summary> Sequence or dictionary operation list (ValueOrDiff is a DeltaDocument). </summary>
        CollectionOps = 2
    }
}
