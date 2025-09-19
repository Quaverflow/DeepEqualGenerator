using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace DeepEqual.Generator;

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
     
    public static void DiagnosticPass(SourceProductionContext spc, INamedTypeSymbol type)
    {
        foreach (var a in type.GetAttributes())
        {
            var an = a.AttributeClass?.ToDisplayString();
            if (an == "DeepEqual.Generator.Shared.DeepComparableExternalAttribute" ||
                an == "DeepEqual.Generator.Shared.DeepCompareExternalAttribute")
            {
                var loc = a.ApplicationSyntaxReference?.GetSyntax()?.GetLocation();

                string? path = null;
                foreach (var kv in a.NamedArguments)
                    if (kv is { Key: "path", Value.Value: string s }) { path = s; break; }
                if (path is null && a.ConstructorArguments.Length > 0 && a.ConstructorArguments[0].Value is string s0)
                {
                    path = s0;
                }

                if (string.IsNullOrWhiteSpace(path))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(EX001, loc, "<empty>"));
                    continue;
                }

                var tokens = path.Split(['.'], StringSplitOptions.RemoveEmptyEntries);
                for (var i = 0; i < tokens.Length; i++)
                {
                    var t = tokens[i];
                    var isDictSegment = t.EndsWith("Items", StringComparison.Ordinal) || t.EndsWith("Dictionary", StringComparison.Ordinal);
                    if (isDictSegment)
                    {
                        var next = (i + 1) < tokens.Length ? tokens[i + 1] : "";
                        var ok = next.Contains("<key>") || next.Contains("<value>");
                        if (!ok)
                        {
                            spc.ReportDiagnostic(Diagnostic.Create(EX002, loc, path));
                            break;
                        }
                    }
                }

                for (var i = 0; i < tokens.Length; i++)
                {
                    var t = tokens[i];
                    var looksEnumerable = t.EndsWith("[]", StringComparison.Ordinal) || t.EndsWith("List", StringComparison.Ordinal) || t.EndsWith("Enumerable", StringComparison.Ordinal);
                    var hasNext = (i + 1) < tokens.Length;
                    if (looksEnumerable && !hasNext)
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(EX003, loc, path));
                        break;
                    }
                }
            }
        }
    }

}
internal static class ExternalPathResolver
{
    public enum PathDiag
    {
        Unresolvable,                   DictionarySideInvalid,          AmbiguousEnumerable         }

    private enum DictSide { None, Key, Value }

    private sealed class Segment
    {
        public string Name = "";
        public DictSide Side = DictSide.None;
    }

    private static readonly SymbolDisplayFormat Fqn = SymbolDisplayFormat.FullyQualifiedFormat;

    public static (INamedTypeSymbol owner, ISymbol member, ITypeSymbol memberType) ResolveMemberPath(
        Compilation compilation,
        INamedTypeSymbol root,
        string path,
        bool includeInternals,
        bool includeBase,
        Action<Location?, string, PathDiag>? report,
        Location? attrLocation)
    {
        var segments = Parse(path, report, attrLocation);
        if (segments.Count == 0)
        {
            report?.Invoke(attrLocation, "Empty path.", PathDiag.Unresolvable);
            throw new InvalidOperationException();
        }

        ITypeSymbol cur = root;
        INamedTypeSymbol? owner = null;
        ISymbol? found = null;

        for (var i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];

            if (cur is not INamedTypeSymbol named)
            {
                report?.Invoke(attrLocation, $"Type '{Describe(cur)}' has no members.", PathDiag.Unresolvable);
                throw new InvalidOperationException();
            }

            var next = FindMember(named, seg.Name, includeInternals, includeBase);

            if (next is null
                && seg.Side == DictSide.None
                && !TryGetDictionaryTypes(cur, out _, out _)                && TryGetEnumerableElementType(cur, out var elem)
                && elem is INamedTypeSymbol elemNamed)
            {
                next = FindMember(elemNamed, seg.Name, includeInternals, includeBase);
                if (next is not null)
                {
                    owner = next.Value.Owner;
                    found = next.Value.Symbol;
                    var memberType = next.Value.Type;

                    cur = memberType;

                    if (i + 1 < segments.Count
                        && !TryGetDictionaryTypes(cur, out _, out _)
                        && TryGetEnumerableElementType(cur, out var elem2))
                    {
                        cur = elem2;
                    }

                    continue;                }
            }

            if (next is null)
            {
                report?.Invoke(attrLocation, $"Member '{seg.Name}' not found on '{Describe(named)}'.", PathDiag.Unresolvable);
                throw new InvalidOperationException();
            }

            owner = next.Value.Owner;
            found = next.Value.Symbol;
            var memberType2 = next.Value.Type;

            if (seg.Side != DictSide.None)
            {
                if (!TryGetDictionaryTypes(memberType2, out var k, out var v))
                {
                    report?.Invoke(attrLocation,
                        $"Path step '{seg.Name}' uses '<{seg.Side}>' but '{Describe(memberType2)}' is not a dictionary type.",
                        PathDiag.DictionarySideInvalid);
                    throw new InvalidOperationException();
                }
                cur = seg.Side == DictSide.Key ? k : v;
            }
            else
            {
                cur = memberType2;

                if (i + 1 < segments.Count
                    && !TryGetDictionaryTypes(cur, out _, out _)
                    && TryGetEnumerableElementType(cur, out var elem3))
                {
                    cur = elem3;
                }
            }
        }


        return (owner!, found!, cur);
    }
    private static bool TryGetEnumerableElementType(ITypeSymbol t, out ITypeSymbol elem)
    {
        elem = null!;
        if (t.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        if (t is IArrayTypeSymbol ats)
        {
            elem = ats.ElementType;
            return true;
        }

        if (t is INamedTypeSymbol nt)
        {
            foreach (var i in nt.AllInterfaces.Prepend(nt))
            {
                var def = i.OriginalDefinition.ToDisplayString(Fqn);
                if (def == "global::System.Collections.Generic.IEnumerable<T>")
                {
                    elem = i.TypeArguments[0];
                    return true;
                }
            }
        }
        return false;
    }
    private static (INamedTypeSymbol Owner, ISymbol Symbol, ITypeSymbol Type)? FindMember(
        INamedTypeSymbol start,
        string name,
        bool includeInternals,
        bool includeBase)
    {
        static bool IsAccessible(ISymbol s, bool incl, INamedTypeSymbol rootOwner) =>
            s.DeclaredAccessibility switch
            {
                Accessibility.Public => true,
                Accessibility.Internal or Accessibility.ProtectedAndInternal =>
                    incl && SymbolEqualityComparer.Default.Equals(s.ContainingAssembly, rootOwner.ContainingAssembly),
                _ => false
            };

        for (var t = start; t is not null; t = includeBase ? t.BaseType : null)
        {
            foreach (var p in t.GetMembers().OfType<IPropertySymbol>())
            {
                if (p.Name == name && p.GetMethod is not null && p.Parameters.Length == 0 && IsAccessible(p, includeInternals, start))
                {
                    return (t, p, p.Type);
                }
            }
            foreach (var f in t.GetMembers().OfType<IFieldSymbol>())
            {
                if (f.Name == name && f is { IsStatic: false, IsConst: false, IsImplicitlyDeclared: false } && IsAccessible(f, includeInternals, start))
                {
                    return (t, f, f.Type);
                }
            }
            if (!includeBase)
            {
                break;
            }
        }
        return null;
    }

    private static bool TryGetDictionaryTypes(ITypeSymbol t, out ITypeSymbol key, out ITypeSymbol value)
    {
        key = value = null!;
        IEnumerable<INamedTypeSymbol> shapes = t is INamedTypeSymbol nt
            ? nt.AllInterfaces.Prepend(nt)
            : Array.Empty<INamedTypeSymbol>();

        foreach (var i in shapes)
        {
            var def = i.OriginalDefinition.ToDisplayString(Fqn);
            if (def is "global::System.Collections.Generic.IDictionary<TKey, TValue>" or
                     "global::System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>")
            {
                key = i.TypeArguments[0];
                value = i.TypeArguments[1];
                return true;
            }
        }
        return false;
    }

    private static List<Segment> Parse(string path, Action<Location?, string, PathDiag>? report, Location? loc)
    {
        var parts = path.Split('.');
        var list = new List<Segment>(parts.Length);

        foreach (var p in parts)
        {
            var seg = new Segment();

            if (p.EndsWith("<Key>", StringComparison.Ordinal))
            {
                seg.Name = p[..^5];
                seg.Side = DictSide.Key;
            }
            else if (p.EndsWith("<Value>", StringComparison.Ordinal))
            {
                seg.Name = p[..^7];
                seg.Side = DictSide.Value;
            }
            else
            {
                seg.Name = p;
            }

            if (seg.Name.Length == 0)
            {
                report?.Invoke(loc, $"Empty segment in path '{path}'.", PathDiag.Unresolvable);
                continue;
            }

            list.Add(seg);
        }

        return list;
    }

    private static string Describe(ITypeSymbol t) => t.ToDisplayString(Fqn);
}
