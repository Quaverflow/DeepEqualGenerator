using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class GuidHolder
{
    public Guid Id { get; set; }
}