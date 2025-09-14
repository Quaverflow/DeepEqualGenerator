using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class WithInternalsExcluded
{
    public int Shown { get; set; }
    internal int Hidden { get; set; }
}