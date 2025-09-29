using System;

namespace DeepEqual.Generator.Shared;

/// <summary>
///     Assembly-scoped: acts like [DeepCompare] but applies to a member reached via Root + Path.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ExternalDeepCompareAttribute(Type root, string path) : DeepCompareAttribute
{
    /// <summary>The external root type where the path starts. Required.</summary>
    public Type Root { get; } = root ?? throw new ArgumentNullException(nameof(root));

    /// <summary>
    ///     Member path, e.g. "Nested.MoreNested.Prop", or for dictionaries:
    ///     "SomeDictionary&lt;Key&gt;.Id" / "SomeDictionary&lt;Value&gt;.Name".
    /// </summary>
    public string Path { get; } =
        !string.IsNullOrWhiteSpace(path) ? path : throw new ArgumentNullException(nameof(path));
}