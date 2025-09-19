using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class WithOrderInsensitiveMember
{
    [DeepCompare(OrderInsensitive = true)]
    public List<int> Values { get; set; } = [];
}