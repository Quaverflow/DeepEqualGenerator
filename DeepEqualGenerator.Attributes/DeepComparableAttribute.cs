using System;

namespace DeepEqual.Generator.Shared;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class DeepComparableAttribute : Attribute
{
    public bool OrderInsensitiveCollections { get; set; }
    public bool CycleTracking { get; set; }
    public bool IncludeInternals { get; set; }
    public bool IncludeBaseMembers { get; set; } = true;
}