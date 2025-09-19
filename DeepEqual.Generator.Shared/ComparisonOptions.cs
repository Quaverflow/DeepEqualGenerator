using System;

namespace DeepEqual.Generator.Shared;

/// <summary>
///     Options that influence how deep comparisons and deltas are computed.
/// </summary>
public sealed class ComparisonOptions
{
    /// <summary>
    ///     Controls string equality semantics for value-like comparisons.
    /// </summary>
    public StringComparison StringComparison { get; set; } = StringComparison.Ordinal;

    /// <summary>
    ///     Treats floating-point NaN values as equal when <c>true</c>.
    /// </summary>
    public bool TreatNaNEqual { get; set; } = true;

    /// <summary>
    ///     Absolute tolerance used for double comparisons when not using exact comparison.
    /// </summary>
    public double DoubleEpsilon { get; set; } = 0.0;

    /// <summary>
    ///     Absolute tolerance used for float comparisons when not using exact comparison.
    /// </summary>
    public float FloatEpsilon { get; set; } = 0f;

    /// <summary>
    ///     Absolute tolerance used for decimal comparisons when not using exact comparison.
    /// </summary>
    public decimal DecimalEpsilon { get; set; } = 0m;

    /// <summary>
    ///     When <c>true</c>, dirty-bit based delta emission re-checks equality for flagged members
    ///     to suppress no-op writes. Default is <c>false</c> (fastest).
    /// </summary>
    public bool ValidateDirtyOnEmit { get; set; } = false;
}