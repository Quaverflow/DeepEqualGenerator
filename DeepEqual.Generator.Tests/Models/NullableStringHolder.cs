using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class NullableStringHolder
{
    public string? Value { get; set; }
}