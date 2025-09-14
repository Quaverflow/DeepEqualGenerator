using System;

namespace DeepEqual.Generator.Shared;

/// <summary>
/// Overrides how a member (property/field) or a whole type is compared.
/// </summary>
/// <remarks>
/// Apply this to:
/// <list type="bullet">
///   <item><description>a property or field — to control how that single member is compared;</description></item>
///   <item><description>a class or struct — to set the default comparison rules wherever that type appears.</description></item>
/// </list>
/// Member-level settings take priority over type-level settings.
/// </remarks>
/// <example>
/// <code>
/// public sealed class Order
/// {
///     // Only check reference (same object) for this member
///     [DeepCompare(Kind = CompareKind.Reference)]
///     public byte[]? RawPayload { get; set; }
///
///     // Ignore order for this list (treat like a bag)
///     [DeepCompare(OrderInsensitive = true)]
///     public List&lt;string&gt; Tags { get; set; } = new();
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class DeepCompareAttribute : Attribute
{
    /// <summary>
    /// How to compare the target.
    /// </summary>
    /// <value>
    /// Default is <see cref="CompareKind.Deep"/> (walks into nested members).
    /// Use <see cref="CompareKind.Shallow"/> to call <c>.Equals</c> only,
    /// <see cref="CompareKind.Reference"/> to require the same object instance,
    /// or <see cref="CompareKind.Skip"/> to ignore the member.
    /// </value>
    public CompareKind Kind { get; set; } = CompareKind.Deep;

    /// <summary>
    /// When the target is a sequence (array or collection), compare as unordered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If <see langword="true"/>, element order does not matter (duplicates still must match).
    /// If <see langword="false"/>, elements are compared in order (index by index).
    /// </para>
    /// <para>
    /// This setting affects only the annotated member or type. It does not change others.
    /// </para>
    /// </remarks>
    public bool OrderInsensitive { get; set; } = false;

    /// <summary>
    /// Only compare these member names of the annotated type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use on a type to narrow which members participate in equality.
    /// If set, any member not listed here is ignored.
    /// </para>
    /// <para>
    /// Member names must match exactly (including case).
    /// </para>
    /// </remarks>
    public string[] Members { get; set; } = [];

    /// <summary>
    /// Ignore these member names during comparison.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Useful for skipping audit fields (e.g., <c>UpdatedAt</c>) or transient caches.
    /// </para>
    /// <para>
    /// If a name appears in both <see cref="Members"/> and <see cref="IgnoreMembers"/>,
    /// it is ignored.
    /// </para>
    /// </remarks>
    public string[] IgnoreMembers { get; set; } = [];

    /// <summary>
    /// Custom equality comparer type to use for this member or type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The type must implement <c>IEqualityComparer&lt;T&gt;</c> where <c>T</c> matches the
    /// member's (or type's) element type. The generator will create and use an instance of it.
    /// </para>
    /// <para>
    /// Example: a case-insensitive string comparer for a <c>string</c> property.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public sealed class CaseInsensitive : IEqualityComparer&lt;string&gt;
    /// {
    ///     public bool Equals(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    ///     public int GetHashCode(string s) => StringComparer.OrdinalIgnoreCase.GetHashCode(s);
    /// }
    ///
    /// public sealed class Product
    /// {
    ///     [DeepCompare(ComparerType = typeof(CaseInsensitive))]
    ///     public string? Sku { get; set; }
    /// }
    /// </code>
    /// </example>
    public Type? ComparerType { get; set; } = null;

    /// <summary>
    /// Property/field names that act as keys when matching items in an unordered collection of objects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this when comparing two lists of objects where order does not matter, but each item has an identity.
    /// Items with the same key are paired and then compared deeply.
    /// </para>
    /// <para>
    /// For a single key, specify one name (e.g., <c>"Id"</c>).
    /// For a composite key, list multiple names (e.g., <c>{"Id","Region"}</c>).
    /// </para>
    /// <para>
    /// Keys must exist on the element type and be comparable (strings, numbers, etc.).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public sealed class Customer { public string Id { get; set; } = ""; public string Name { get; set; } = ""; }
    ///
    /// public sealed class Batch
    /// {
    ///     [DeepCompare(OrderInsensitive = true, KeyMembers = new[] { "Id" })]
    ///     public List&lt;Customer&gt; Customers { get; set; } = new();
    /// }
    /// </code>
    /// </example>
    public string[] KeyMembers { get; set; } = [];
}