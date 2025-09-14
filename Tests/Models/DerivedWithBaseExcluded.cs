using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable(IncludeBaseMembers = false)]
public sealed class DerivedWithBaseExcluded : BaseEntity
{
    public string? Name { get; set; }
}