using System.Collections.Generic;

namespace DeepEqual.Generator;

internal sealed record EqualityTypeSchema(IReadOnlyList<string> IncludeMembers, IReadOnlyList<string> IgnoreMembers);