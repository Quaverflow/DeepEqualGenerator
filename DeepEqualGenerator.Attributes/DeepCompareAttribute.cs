using System;

namespace DeepEqual.Generator.Shared;

/// <summary>
/// Overrides deep comparison behavior for a specific member or type.
/// </summary>
/// <remarks>
/// <para>
/// Apply to:
/// <list type="bullet">
///   <item><description>
/// A property or field — to control how that member is compared.
/// </description></item>
///   <item><description>
/// A class or struct — to define default comparison rules whenever that type appears.
/// </description></item>
/// </list>
/// </para>
/// <para>
/// Member-level attributes take precedence over type-level ones. If neither is present,
/// the generator falls back to the defaults defined by <see cref="DeepComparableAttribute"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class Customer
/// {
///     [DeepCompare(Kind = CompareKind.Shallow)]
///     public string Name { get; init; } = string.Empty;
///
///     [DeepCompare(OrderInsensitive = true)]
///     public List&lt;string&gt; Tags { get; init; } = new();
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class DeepCompareAttribute : Attribute
{
    /// <summary>
    /// Defines the comparison strategy to use for the target member or type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Default is <see cref="CompareKind.Deep"/>, which recursively compares submembers.
    /// You can switch to <see cref="CompareKind.Shallow"/> (reference equality) or other
    /// available modes depending on your use case.
    /// </para>
    /// </remarks>
    public CompareKind Kind { get; set; } = CompareKind.Deep;

    /// <summary>
    /// When applied to a sequence (array or <see cref="System.Collections.Generic.IEnumerable{T}"/>),
    /// specifies that it should be treated as an <em>unordered multiset</em>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// By default (<see langword="false"/>), sequences are compared in order for predictability
    /// and performance. Setting this to <see langword="true"/> ignores ordering and compares
    /// only element counts and values.
    /// </para>
    /// <para>
    /// Example: <c>[1,2,2]</c> equals <c>[2,1,2]</c>, but not <c>[1,2]</c>.
    /// </para>
    /// </remarks>
    public bool OrderInsensitive { get; set; } = false;

    /// <summary>
    /// Restricts deep comparison to a specific set of member names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If specified, only the listed members of the target type will be considered for equality.
    /// Useful for partial comparisons (e.g., ignoring audit fields or large navigation properties).
    /// </para>
    /// <para>
    /// Applies only when the attribute is placed on a type. Ignored at the property/field level.
    /// </para>
    /// </remarks>
    public string[] Members { get; set; } = [];

    /// <summary>
    /// Excludes the specified member names from comparison.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Commonly used to skip fields such as <c>Timestamp</c>, <c>RowVersion</c>, or transient caches.
    /// </para>
    /// <para>
    /// Applies at both the type and member level. When both <see cref="Members"/> and
    /// <see cref="IgnoreMembers"/> are provided, <see cref="IgnoreMembers"/> takes precedence.
    /// </para>
    /// </remarks>
    public string[] IgnoreMembers { get; set; } = [];
}
