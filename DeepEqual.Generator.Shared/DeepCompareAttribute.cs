using System;

namespace DeepEqual.Generator.Shared;

/// <summary>
///     Overrides how a member (property/field) or a whole type is compared.
/// </summary>
/// <remarks>
///     Apply this to:
///     <list type="bullet">
///         <item>
///             <description>a property or field — to control how that single member is compared;</description>
///         </item>
///         <item>
///             <description>a class or struct — to set the default comparison rules wherever that type appears.</description>
///         </item>
///     </list>
///     Member-level settings take priority over type-level settings.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct)]
public class DeepCompareAttribute : Attribute
{
    /// <summary>
    ///     How to compare the target.
    /// </summary>
    /// <value>
    ///     Default is <see cref="CompareKind.Deep" /> (walks into nested members).
    ///     Use <see cref="CompareKind.Shallow" /> to call <c>.Equals</c> only,
    ///     <see cref="CompareKind.Reference" /> to require the same object instance,
    ///     or <see cref="CompareKind.Skip" /> to ignore the member.
    /// </value>
    public CompareKind Kind { get; set; } = CompareKind.Deep;

    /// <summary>
    ///     When the target is a sequence (array or collection), compare as unordered.
    /// </summary>
    public bool OrderInsensitive { get; set; } = false;

    /// <summary>
    ///     Only compare these member names of the annotated type.
    ///     If set, any member not listed here is ignored.
    /// </summary>
    public string[] Members { get; set; } = [];

    /// <summary>
    ///     Ignore these member names during comparison.
    /// </summary>
    public string[] IgnoreMembers { get; set; } = [];

    /// <summary>
    ///     Custom equality comparer type to use for this member or type.
    ///     The type must implement <c>IEqualityComparer&lt;T&gt;</c>.
    /// </summary>
    public Type? ComparerType { get; set; } = null;

    /// <summary>
    ///     Property/field names that act as keys when matching items in an unordered collection of objects.
    /// </summary>
    public string[] KeyMembers { get; set; } = [];

    /// <summary>
    ///     When generating deltas for this member or type, record only a shallow replacement (new value)
    ///     rather than recursing to capture nested diffs.
    /// </summary>
    public bool DeltaShallow { get; set; } = false;

    /// <summary>
    ///     Never emit a delta entry for this member (equivalent to treating it as immutable for patching).
    /// </summary>
    public bool DeltaSkip { get; set; } = false;
}