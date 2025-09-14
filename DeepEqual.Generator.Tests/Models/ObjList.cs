using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable] public sealed class ObjList { public List<object?> Items { get; init; } = new(); }