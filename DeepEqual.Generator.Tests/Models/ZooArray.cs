using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable] public sealed class ZooArray { public IAnimal[] Animals { get; init; } = Array.Empty<IAnimal>(); }