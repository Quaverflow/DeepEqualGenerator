using System;

namespace DeepEqual.Generator.Shared;

/// <summary>
///     Specifies whether generated members emit access tracking diagnostics in addition to dirty bits.
/// </summary>
public enum AccessMode
{
    None = 0,
    Write = 1,
}

/// <summary>
///     Configures the level of detail captured for access tracking diagnostics.
/// </summary>
public enum AccessGranularity
{
    Bits = 0,
    Counts = 1,
    CountsAndLast = 2,
}

/// <summary>
///     Opts a user type into generator-emitted dirty-bit tracking for member changes.
/// </summary>
/// <remarks>
///     When applied, the source generator emits per-member bit offsets and internal methods
///     on the annotated type so property setters can mark members dirty efficiently.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class DeltaTrackAttribute : Attribute
{
    /// <summary>
    ///     Enables interlocked operations for dirty-bit updates. When <c>false</c>, updates are not
    ///     thread-safe but are faster. Default is <c>false</c>.
    /// </summary>
    public bool ThreadSafe { get; set; } = false;

    /// <summary>
    ///     Controls ambient access tracking for members unless overridden per property.
    /// </summary>
    public AccessMode AccessTrack { get; set; } = AccessMode.None;

    /// <summary>
    ///     Defines the default level of diagnostics collected for the annotated type.
    /// </summary>
    public AccessGranularity AccessGranularity { get; set; } = AccessGranularity.Bits;

    /// <summary>
    ///     Sets the default per-instance event log capacity. <c>0</c> disables logging unless overridden.
    /// </summary>
    public int AccessLogCapacity { get; set; } = 0;
}
