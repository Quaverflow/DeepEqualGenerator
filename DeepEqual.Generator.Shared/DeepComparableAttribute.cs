using System;

namespace DeepEqual.Generator.Shared;

/// <summary>
/// Controls whether member indices used in deltas are stable across builds.
/// </summary>
public enum StableMemberIndexMode
{
    Auto = 0,
    On = 1,
    Off = 2
}

/// <summary>
/// Marks a class or struct as a root for generated deep comparison helpers and sets defaults for that type.
/// </summary>
/// <remarks>
/// <para>
/// Apply this to any model you want to compare deeply. A static helper named
/// <c>{TypeName}DeepEqual</c> is generated with <c>AreDeepEqual(left, right)</c>.
/// </para>
/// <para>
/// Nested types and referenced user types are included automatically when they appear under the root.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class DeepComparableAttribute : Attribute
{
    /// <summary> Treat all collections under this type as unordered by default. </summary>
    public bool OrderInsensitiveCollections { get; set; }

    /// <summary> Enable cycle tracking for this type's object graphs. </summary>
    public bool CycleTracking { get; set; }

    /// <summary> Include <c>internal</c> members and compare internal types from the same assembly. </summary>
    public bool IncludeInternals { get; set; }

    /// <summary> Include members from base classes by default. </summary>
    public bool IncludeBaseMembers { get; set; } = true;

    /// <summary>
    /// Opt-in: also generate "diff" APIs for this root and all reachable user types.
    /// </summary>
    /// <remarks>
    /// When enabled, a sibling static helper named <c>{TypeName}DeepOps</c> is emitted with
    /// <c>TryGetDiff</c>/<c>GetDiff</c> (structural diffs) and the registry is populated for runtime dispatch.
    /// </remarks>
    public bool GenerateDiff { get; set; } = false;

    /// <summary>
    /// Opt-in: also generate "delta" (patch) APIs for this root and all reachable user types.
    /// </summary>
    /// <remarks>
    /// When enabled, <c>{TypeName}DeepOps</c> also exposes <c>ComputeDelta</c>/<c>ApplyDelta</c>.
    /// </remarks>
    public bool GenerateDelta { get; set; } = false;
    /// <summary>
    /// Controls whether generated deltas use stable per-member indices. Auto enables stability when delta is generated; Off uses ephemeral ordinals.
    /// </summary>
    public StableMemberIndexMode StableMemberIndex { get; set; } = StableMemberIndexMode.Auto;

}

/// <summary>
/// Assembly-scoped: acts like [DeepComparable] but targets a 3rd-party root type.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ExternalDeepComparableAttribute : DeepComparableAttribute
{
    public ExternalDeepComparableAttribute(Type root)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
    }
    /// <summary>The external root type you're "adopting". Required.</summary>
    public Type Root { get; }
}

/// <summary>
/// Assembly-scoped: acts like [DeepCompare] but applies to a member reached via Root + Path.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ExternalDeepCompareAttribute : DeepCompareAttribute
{
    public ExternalDeepCompareAttribute(Type root, string path)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        Path = !string.IsNullOrWhiteSpace(path) ? path : throw new ArgumentNullException(nameof(path));
    }
    /// <summary>The external root type where the path starts. Required.</summary>
    public Type Root { get; }
    /// <summary>
    /// Member path, e.g. "Nested.MoreNested.Prop", or for dictionaries:
    /// "SomeDictionary&lt;Key&gt;.Id" / "SomeDictionary&lt;Value&gt;.Name".
    /// </summary>
    public string Path { get; }
}