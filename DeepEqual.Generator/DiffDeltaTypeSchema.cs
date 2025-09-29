using System.Collections.Generic;

namespace DeepEqual.Generator;

internal sealed record DiffDeltaTypeSchema(
    IReadOnlyList<string> IncludeMembers,
    IReadOnlyList<string> IgnoreMembers,
    CompareKind DefaultKind,
    bool DefaultOrderInsensitive,
    bool DefaultDeltaShallow,
    bool DefaultDeltaSkip);