using System;

namespace DeepEqual.Generator.Shared;

public sealed class ComparisonOptions
{
    public StringComparison StringComparison { get; set; } = StringComparison.Ordinal;
    public bool TreatNaNEqual { get; set; } = true;
    public double DoubleEpsilon { get; set; } = 0.0;
    public float FloatEpsilon { get; set; } = 0f;
    public decimal DecimalEpsilon { get; set; } = 0m;
}