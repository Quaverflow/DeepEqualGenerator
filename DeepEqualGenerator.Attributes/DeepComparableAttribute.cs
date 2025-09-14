using System;

namespace DeepEqual.Generator.Shared
{
    /// <summary>
    /// Marks a class or struct as a root for generated deep comparison helpers and sets defaults for that type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Apply this to any model you want to compare deeply. A static helper named
    /// <c>{TypeName}DeepEqual</c> will be generated with <c>AreDeepEqual(left, right)</c>.
    /// </para>
    /// <para>
    /// Nested types and referenced user types are included automatically when they appear under the root.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [DeepComparable]
    /// public sealed class Order
    /// {
    ///     public string Id { get; set; } = "";
    ///     public List&lt;OrderLine&gt; Lines { get; set; } = new();
    /// }
    ///
    /// // Usage:
    /// var equal = OrderDeepEqual.AreDeepEqual(orderA, orderB);
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class DeepComparableAttribute : Attribute
    {
        /// <summary>
        /// Treat all collections under this type as unordered by default.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When <see langword="true"/>, list and array order does not matter unless a member says otherwise.
        /// Duplicates still count (multiset behavior).
        /// </para>
        /// <para>
        /// Member-level settings with <see cref="DeepCompareAttribute.OrderInsensitive"/> take priority.
        /// </para>
        /// </remarks>
        public bool OrderInsensitiveCollections { get; set; }

        /// <summary>
        /// Enable cycle tracking for this type's object graphs.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When <see langword="true"/>, the comparer remembers pairs it has already visited so it can safely
        /// handle graphs with loops (e.g., parent &lt;-&gt; child). This prevents infinite recursion.
        /// </para>
        /// <para>
        /// Leave this off only if you are sure your graphs have no cycles and you want the absolute minimum overhead.
        /// </para>
        /// </remarks>
        public bool CycleTracking { get; set; }

        /// <summary>
        /// Include <c>internal</c> members and compare internal types from the same assembly.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This does not include <c>private</c> or <c>protected</c> members.
        /// </para>
        /// </remarks>
        public bool IncludeInternals { get; set; }

        /// <summary>
        /// Include members from base classes by default.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If <see langword="true"/>, members declared on base classes are part of the comparison.
        /// You can turn this off on a specific type by setting it to <see langword="false"/>.
        /// </para>
        /// </remarks>
        public bool IncludeBaseMembers { get; set; } = true;
    }
}
