using System;

namespace DeepEqual.Generator.Shared;

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
}