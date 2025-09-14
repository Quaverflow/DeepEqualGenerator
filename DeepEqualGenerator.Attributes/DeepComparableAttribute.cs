using System;

namespace DeepEqual.Generator.Shared;

/// <summary>
/// Marks a root type (class or struct) for which the DeepEqual source generator
/// will produce a dedicated deep-comparison helper.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute to any public (or internal) class/struct to make it an
/// entry point for generated deep equality. Nested types referenced from the root
/// are automatically included; you do <b>not</b> need to annotate them.
/// </para>
/// <para>
/// This attribute only declares intent; actual comparison behavior may be further
/// influenced by per-member attributes (e.g., <c>DeepCompareAttribute</c>) and
/// generator options in your project.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [DeepComparable]
/// public sealed class Order
/// {
///     public int Id { get; init; }
///     public Customer Customer { get; init; } = default!;
///     public List&lt;OrderLine&gt; Lines { get; init; } = new();
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class DeepComparableAttribute : Attribute
{
    /// <summary>
    /// When <see langword="true"/>, sequences (e.g., <see cref="System.Collections.Generic.IEnumerable{T}"/>)
    /// are compared as <em>order-insensitive</em> multisets by default at the type level.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This affects collections encountered under the annotated type unless overridden at a more specific
    /// scope (e.g., via a member-level attribute). It treats duplicates as significant but ignores element order.
    /// </para>
    /// <para>
    /// Example: <c>[1,2,2]</c> equals <c>[2,1,2]</c>, but not <c>[1,2]</c>.
    /// </para>
    /// </remarks>
    public bool OrderInsensitiveCollections { get; set; }

    /// <summary>
    /// Enables cycle detection while traversing object graphs to prevent infinite recursion
    /// and to ensure consistent equality semantics for cyclic structures.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <see langword="true"/>, the generated comparer tracks visited object pairs
    /// (by reference) to safely compare graphs with back-references (e.g., parent/child links).
    /// </para>
    /// <para>
    /// Disable only if you are certain your graphs are acyclic and you need the absolute minimum overhead.
    /// </para>
    /// </remarks>
    public bool CycleTracking { get; set; }

    /// <summary>
    /// Includes internal members and generates comparison helpers for <c>internal</c> types
    /// within the same assembly as the annotated type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This does <b>not</b> include <c>private</c> or <c>protected</c> members. It primarily expands visibility
    /// to compare <c>internal</c> fields/properties and navigate through <c>internal</c> types when required.
    /// </para>
    /// <para>
    /// If your assembly uses <c>InternalsVisibleTo</c>, consider whether enabling this is necessary,
    /// since friend assemblies can already access internals.
    /// </para>
    /// </remarks>
    public bool IncludeInternals { get; set; }
}
