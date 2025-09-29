using Microsoft.CodeAnalysis;

namespace DeepEqual.Generator;

internal readonly record struct RootRequest(
    string MetadataName,
    string QualifiedDisplayName,
    bool IncludeInternals,
    bool OrderInsensitiveCollections,
    bool EqCycleTrackingEnabled,
    bool DdCycleTrackingEnabled,
    bool IncludeBaseMembers,
    bool GenerateDiff,
    bool GenerateDelta,
    StableMemberIndexMode StableMemberIndexMode,
    bool EmitSchemaSnapshot,
    Location? AttributeLocation
);