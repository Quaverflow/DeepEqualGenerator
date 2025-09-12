using System;

namespace DeepEqual.Generator.Attributes;

/// <summary>
/// Put this on any class/struct you want a separate DeepEqual class generated for (root entry points).
/// Nested types are included by default; you do NOT have to annotate them.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class DeepComparableAttribute : Attribute
{
    /// <summary>Compare IEnumerable&lt;T&gt; as unordered (multiset) by default at the type level.</summary>
    public bool OrderInsensitiveCollections { get; set; }

}