
#nullable enable
using Microsoft.CodeAnalysis;

namespace DeepEqual.Generator
{
    internal static class Diagnostics
    {
        private const string Category = "DeepEqual.Generator";

        internal static readonly DiagnosticDescriptor DL001 =
            new DiagnosticDescriptor(
                id: "DL001",
                title: "Stable member indices are disabled while delta generation is enabled",
                messageFormat: "GenerateDelta=true but StableMemberIndex=Off; enable StableMemberIndex or set it to Auto/On",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor EX001 =
            new DiagnosticDescriptor(
                id: "EX001",
                title: "Unresolvable external path",
                messageFormat: "Unresolvable external path: {0}",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor EX002 =
            new DiagnosticDescriptor(
                id: "EX002",
                title: "Dictionary side missing or invalid",
                messageFormat: "Dictionary side missing or invalid in path: {0}; use <key> or <value>",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor EX003 =
            new DiagnosticDescriptor(
                id: "EX003",
                title: "Ambiguous enumerable element type",
                messageFormat: "Ambiguous enumerable element type for path: {0}",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor EQ001 =
            new DiagnosticDescriptor(
                id: "EQ001",
                title: "Conflicting comparison rules",
                messageFormat: "Conflicting comparison rules for member: {0}",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor EQ002 =
            new DiagnosticDescriptor(
                id: "EQ002",
                title: "Deep compare requested without available helper or registry",
                messageFormat: "Deep compare requested for type: {0}, but no helper/registry is available",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor DF001 =
            new DiagnosticDescriptor(
                id: "DF001",
                title: "Member excluded from diff",
                messageFormat: "Diff requested but member excluded: {0}",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor DF002 =
            new DiagnosticDescriptor(
                id: "DF002",
                title: "Unordered collection without KeyMembers",
                messageFormat: "Unordered collection without KeyMembers on member: {0}",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor DF003 =
            new DiagnosticDescriptor(
                id: "DF003",
                title: "Deep diff on element type lacking diff support",
                messageFormat: "Deep diff on element type lacking diff support; falling back to replace on member: {0}",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor RG001 =
            new DiagnosticDescriptor(
                id: "RG001",
                title: "Referenced type not generated or registered",
                messageFormat: "Type referenced for deep/diff/delta is not generated or registered: {0}",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);
    }
}
