using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable(IncludeBaseMembers = true)]
public sealed class DerivedWithBaseIncluded : BaseEntity
{
    public string? Name { get; set; }
}