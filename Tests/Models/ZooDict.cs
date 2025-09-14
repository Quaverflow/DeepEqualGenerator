using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable] public sealed class ZooDict { public Dictionary<string, IAnimal> Pets { get; init; } = new(); }