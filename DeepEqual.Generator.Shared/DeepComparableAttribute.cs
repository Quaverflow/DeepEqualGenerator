using System;

namespace DeepEqual.Generator.Shared;

/// <summary>
///     Marks a class or struct as a root for generated deep comparison helpers and sets defaults for that type.
/// </summary>
/// <remarks>
///     <para>
///         Apply this to any model you want to compare deeply. A static helper named
///         <c>{TypeName}DeepEqual</c> is generated with <c>AreDeepEqual(left, right)</c>.
///     </para>
///     <para>
///         Nested types and referenced user types are included automatically when they appear under the root.
///     </para>
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
    ///     Opt-in: also generate "diff" APIs for this root and all reachable user types.
    /// </summary>
    /// <remarks>
    ///     When enabled, a sibling static helper named <c>{TypeName}DeepOps</c> is emitted with
    ///     <c>TryGetDiff</c>/<c>GetDiff</c> (structural diffs) and the registry is populated for runtime dispatch.
    /// </remarks>
    public bool GenerateDiff { get; set; } = false;

    /// <summary>
    ///     Opt-in: also generate "delta" (patch) APIs for this root and all reachable user types.
    /// </summary>
    /// <remarks>
    ///     When enabled, <c>{TypeName}DeepOps</c> also exposes <c>ComputeDelta</c>/<c>ApplyDelta</c>.
    /// </remarks>
    public bool GenerateDelta { get; set; } = false;

    /// <summary>
    ///     Controls whether generated deltas use stable per-member indices. Auto enables stability when delta is generated;
    ///     Off uses ephemeral ordinals.
    /// </summary>
    public StableMemberIndexMode StableMemberIndex { get; set; } = StableMemberIndexMode.Auto;

    public bool EmitSchemaSnapshot { get; set; } = false;
}