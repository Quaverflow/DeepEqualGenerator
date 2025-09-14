using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public struct SimpleStruct
{
    public int Id { get; set; }
    public DateTime WhenUtc { get; set; }
}