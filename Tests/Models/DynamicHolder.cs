using System.Dynamic;
using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class DynamicHolder
{
    public IDictionary<string, object?> Data { get; set; } = new ExpandoObject();
}