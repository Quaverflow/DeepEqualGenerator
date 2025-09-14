using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable] public sealed class ZooRoDict { public IReadOnlyDictionary<string, IAnimal> Pets { get; init; } = new System.Collections.ObjectModel.ReadOnlyDictionary<string, IAnimal>(new Dictionary<string, IAnimal>()); }