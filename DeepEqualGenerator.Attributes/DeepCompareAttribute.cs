using System;

namespace DeepEqual.Generator.Attributes;

/// <summary>
/// Per-member or per-type override. Apply to:
///  - a property/field (to control that member), or
///  - a class/struct (to set the default for that type wherever it appears).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class DeepCompareAttribute : Attribute
{
    public CompareKind Kind { get; set; } = CompareKind.Deep;

    /// <summary>
    /// When the target is a sequence (array or IEnumerable&lt;T&gt;), choose unordered matching (multiset).
    /// If not set, the generator falls back to the parent type's default or ordered.
    /// </summary>
    public bool OrderInsensitive { get; set; } = true;

    public string[] Members { get; set; } = [];
    public string[] IgnoreMembers { get; set; } = [];
}