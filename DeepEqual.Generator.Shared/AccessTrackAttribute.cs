using System;

namespace DeepEqual.Generator.Shared;

/// <summary>
///     Overrides access tracking configuration for an individual member.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class AccessTrackAttribute : Attribute
{
    public AccessMode Mode { get; set; } = AccessMode.Write;
    public AccessGranularity Granularity { get; set; } = AccessGranularity.Bits;
    public int LogCapacity { get; set; } = 0;
}
