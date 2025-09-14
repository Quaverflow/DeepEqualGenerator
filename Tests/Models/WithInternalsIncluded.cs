using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable(IncludeInternals = true)]
public sealed class WithInternalsIncluded
{
    public int Shown { get; set; }
    internal int Hidden { get; set; }
}