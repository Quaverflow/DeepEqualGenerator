using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class CustomersKeyed
{
    [DeepCompare(OrderInsensitive = true, KeyMembers = new[] { "Id" })]
    public List<CustomerK> Customers { get; set; } = new();
}