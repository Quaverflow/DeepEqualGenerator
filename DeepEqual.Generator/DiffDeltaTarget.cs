using Microsoft.CodeAnalysis;

namespace DeepEqual.Generator;

internal readonly record struct DiffDeltaTarget(
    INamedTypeSymbol Type,
    bool IncludeInternals,
    bool OrderInsensitiveCollections,
    bool CycleTrackingEnabled,
    bool IncludeBaseMembers,
    bool GenerateDiff,
    bool GenerateDelta,
    StableMemberIndexMode StableMode,
    bool EmitSchemaSnapshot);