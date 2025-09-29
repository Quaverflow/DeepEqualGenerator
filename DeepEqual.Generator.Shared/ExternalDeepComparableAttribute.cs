using System;

namespace DeepEqual.Generator.Shared;

/// <summary>
///     Assembly-scoped: acts like [DeepComparable] but targets a 3rd-party root type.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ExternalDeepComparableAttribute(Type root) : DeepComparableAttribute
{
    /// <summary>The external root type you're "adopting". Required.</summary>
    public Type Root { get; } = root ?? throw new ArgumentNullException(nameof(root));
}