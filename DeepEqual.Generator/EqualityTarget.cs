using Microsoft.CodeAnalysis;

namespace DeepEqual.Generator;

internal readonly record struct EqualityTarget(
    INamedTypeSymbol Type,
    bool IncludeInternals,
    bool OrderInsensitiveCollections,
    bool CycleTrackingEnabled,
    bool IncludeBaseMembers);