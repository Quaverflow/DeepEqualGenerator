using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class CustomersKeyed
{
    [DeepCompare(OrderInsensitive = true, KeyMembers = ["Id"])]
    public List<CustomerK> Customers { get; set; } = [];
}