using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class CustomComparerHolder
{
    [DeepCompare(ComparerType = typeof(CaseInsensitiveStringComparer))]
    public string? Code { get; set; }
}