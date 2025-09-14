using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace DeepEqual.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class DeepEqualGenerator : IIncrementalGenerator
{
    private const string DeepComparableAttributeName = "DeepEqual.Generator.Shared.DeepComparableAttribute";
    private const string DeepCompareAttributeName = "DeepEqual.Generator.Shared.DeepCompareAttribute";
    private readonly record struct Target(
        INamedTypeSymbol Type,
        bool IncludeInternals,
        bool OrderInsensitiveCollections,
        bool CycleTrackingEnabled);

    private readonly record struct MemberSymbol(string Name, ITypeSymbol Type, ISymbol Symbol);

    private sealed record TypeSchema(IReadOnlyList<string> IncludeMembers, IReadOnlyList<string> IgnoreMembers);

    private enum EffectiveKind { Deep = 0, Shallow = 1, Reference = 2, Skip = 3 }

    private sealed class PerCompilationCache
    {
        internal readonly Dictionary<ITypeSymbol, bool> IsUserObject = new(SymbolEqualityComparer.Default);
        internal readonly Dictionary<ITypeSymbol, ITypeSymbol?> IEnumerableElement = new(SymbolEqualityComparer.Default);
        internal readonly Dictionary<ITypeSymbol, (ITypeSymbol Key, ITypeSymbol Val)?> IDictionaryTypes = new(SymbolEqualityComparer.Default);
        internal readonly Dictionary<INamedTypeSymbol, TypeSchema> TypeSchemaCache = new(SymbolEqualityComparer.Default);

        private sealed class MemberKeyComparer : IEqualityComparer<(INamedTypeSymbol type, bool allowInternals)>
        {
            public static readonly MemberKeyComparer Instance = new();
            public bool Equals((INamedTypeSymbol type, bool allowInternals) x, (INamedTypeSymbol type, bool allowInternals) y)
                => SymbolEqualityComparer.Default.Equals(x.type, y.type) && x.allowInternals == y.allowInternals;
            public int GetHashCode((INamedTypeSymbol type, bool allowInternals) obj)
            {
                unchecked
                {
                    int h1 = SymbolEqualityComparer.Default.GetHashCode(obj.type);
                    int h2 = obj.allowInternals ? 1 : 0;
                    return (h1 * 397) ^ h2;
                }
            }
        }

        internal readonly Dictionary<(INamedTypeSymbol type, bool allowInternals), MemberSymbol[]> MembersNoSchema
            = new(MemberKeyComparer.Instance);

        private static readonly ConditionalWeakTable<Compilation, PerCompilationCache> Table = new();

        public static PerCompilationCache Get(Compilation c) => Table.GetValue(c, _ => new PerCompilationCache());
    }

    [ThreadStatic]
    private static PerCompilationCache? t_cache;

    private static PerCompilationCache Cache => t_cache ??= new PerCompilationCache();

    
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var roots = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is TypeDeclarationSyntax { AttributeLists.Count: > 0 },
            static (ctx, _) => GetRootTarget(ctx)
        ).Where(t => t is not null);

                var inputs = roots.Combine(context.CompilationProvider);

        context.RegisterSourceOutput(inputs, static (spc, pair) =>
        {
            var target = pair.Left;
            var compilation = pair.Right;
            if (target is null) return;

                        t_cache = PerCompilationCache.Get(compilation);
            EmitForRoot(spc, target.Value);
        });
    }

    private static Target? GetRootTarget(GeneratorSyntaxContext context)
    {
        if (context.Node is not TypeDeclarationSyntax tds) return null;
        if (context.SemanticModel.GetDeclaredSymbol(tds) is not INamedTypeSymbol typeSymbol) return null;

        var deepComparable = typeSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepComparableAttributeName);
        if (deepComparable is null) return null;

                bool includeInternals = GetNamedBool(deepComparable, "IncludeInternals") || GetNamedBool(deepComparable, "IncludePrivateMembers");
        bool orderInsensitive = GetNamedBool(deepComparable, "OrderInsensitiveCollections");

                                                bool cycleTrackingEnabled = true;
        var cycleArg = GetNamedBoolNullable(deepComparable, "CycleTracking");
        if (cycleArg is bool ct) cycleTrackingEnabled = ct;
        else
        {
            var disableArg = GetNamedBoolNullable(deepComparable, "DisableCycleTracking");
            if (disableArg is true) cycleTrackingEnabled = false;
        }

        return new Target(typeSymbol, includeInternals, orderInsensitive, cycleTrackingEnabled);
    }

    private static bool GetNamedBool(AttributeData attribute, string name)
        => attribute.NamedArguments.Any(kv => kv.Key == name && kv.Value.Value is true);

        private static bool? GetNamedBoolNullable(AttributeData attribute, string name)
    {
        foreach (var kv in attribute.NamedArguments)
        {
            if (kv.Key == name && kv.Value.Value is bool b) return b;
        }
        return null;
    }

    private static void EmitForRoot(SourceProductionContext spc, Target root)
    {
        var ns = root.Type.ContainingNamespace.IsGlobalNamespace ? null : root.Type.ContainingNamespace.ToDisplayString();
        var rootFull = root.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var rootName = root.Type.Name;
        var helperClass = $"{rootName}DeepEqual";
        var hintName = SanitizeFileName($"{rootFull}_DeepEqual.g.cs");

        var reachable = BuildReachableTypeClosure(root);

                bool trackCycles = root.CycleTrackingEnabled;

        var accessibility = root.IncludeInternals || root.Type.DeclaredAccessibility != Accessibility.Public ? "internal" : "public";
        var typeParams = root.Type.Arity > 0 ? $"<{string.Join(",", root.Type.TypeArguments.Select(a => a.Name))}>" : "";

        var cw = new CodeWriter();

        cw.Line("// <auto-generated /> DO NOT EDIT");
        cw.Line("using System;");
        cw.Line("using System.Collections;");
        cw.Line("using System.Collections.Generic;");
        cw.Line("using DeepEqual.Generator.Shared;");
        cw.Line();

        if (ns is not null) cw.Open($"namespace {ns}");

        cw.Open($"{accessibility} static class {helperClass}{typeParams}");
        {
                        cw.Open($"static {helperClass}()");
            foreach (var t in reachable
                     .Where(t => IsTypeAccessibleFromRoot(t, root))
                     .OrderBy(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal))
            {
                var fqn = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var helper = GetHelperMethodName(t);
                cw.Line($"GeneratedHelperRegistry.Register<{fqn}>((l, r, ctx) => {helper}(l, r, ctx));");
            }
            cw.Close();             cw.Line();

                        var emittedComparers = new HashSet<string>(StringComparer.Ordinal);
            var comparerDecls = new List<string[]>(); 
                        if (root.Type.IsValueType)
                cw.Open($"{accessibility} static bool AreDeepEqual({rootFull} left, {rootFull} right)");
            else
                cw.Open($"{accessibility} static bool AreDeepEqual({rootFull}? left, {rootFull}? right)");

            if (!root.Type.IsValueType)
            {
                cw.Open("if (object.ReferenceEquals(left, right))");
                cw.Line("return true;");
                cw.Close();

                cw.Open("if (left is null || right is null)");
                cw.Line("return false;");
                cw.Close();
            }

                        cw.Line($"var context = {(trackCycles ? "new DeepEqual.Generator.Shared.ComparisonContext()" : "DeepEqual.Generator.Shared.ComparisonContext.NoTracking")};");
            cw.Line($"return {GetHelperMethodName(root.Type)}(left, right, context);");
            cw.Close();
            cw.Line();

                        if (root.Type.IsValueType)
                cw.Open($"{accessibility} static TraceResult DeepEqualWithTrace({rootFull} left, {rootFull} right)");
            else
                cw.Open($"{accessibility} static TraceResult DeepEqualWithTrace({rootFull}? left, {rootFull}? right)");

            cw.Line("var diffs = new List<string>(capacity: 16);");
            cw.Line($"var context = {(trackCycles ? "new DeepEqual.Generator.Shared.ComparisonContext()" : "DeepEqual.Generator.Shared.ComparisonContext.NoTracking")};");

            if (!root.Type.IsValueType)
            {
                cw.Open("if (object.ReferenceEquals(left, right))");
                cw.Line("return new TraceResult(true, diffs);");
                cw.Close();

                cw.Open("if (left is null || right is null)");
                cw.Line("diffs.Add(\"(root): one is null and the other is not\");");
                cw.Line("return new TraceResult(false, diffs);");
                cw.Close();
            }
            cw.Line($"CollectDifferences__{SanitizeIdentifier(root.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}(left, right, string.Empty, context, diffs);");
            cw.Line("return new TraceResult(diffs.Count == 0, diffs);");
            cw.Close();
            cw.Line();

                        cw.Open($"{accessibility} readonly struct TraceResult");
            cw.Line($"{accessibility} bool AreEqual {{ get; }}");
            cw.Line($"{accessibility} IReadOnlyList<string> Differences {{ get; }}");
            cw.Open($"{accessibility} TraceResult(bool areEqual, IReadOnlyList<string> differences)");
            cw.Line("AreEqual = areEqual;");
            cw.Line("Differences = differences;");
            cw.Close();
            cw.Close();
            cw.Line();

            cw.Open("private static string AppendPath(string path, string segment)");
            cw.Line("return string.IsNullOrEmpty(path) ? segment : path + \".\" + segment;");
            cw.Close();
            cw.Line();

                                    foreach (var t in reachable)
            {
                var schema = GetTypeSchema(t);
                foreach (var m in EnumerateComparableMembers(t, root.IncludeInternals, schema))
                {
                    var deepAttr = GetDeepCompareAttribute(m.Symbol);
                    var kind = GetEffectiveKind(m.Type, deepAttr);
                    if (kind is EffectiveKind.Skip or EffectiveKind.Shallow or EffectiveKind.Reference) continue;

                    if (m.Type is IArrayTypeSymbol arr)
                    {
                        var el = arr.ElementType;
                        var elKind = GetEffectiveKind(el, null);
                        EnsureComparerStruct(emittedComparers, comparerDecls, el, elKind, root, $"M_{SanitizeIdentifier(t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}_{m.Name}");
                    }
                    else if (TryGetDictionaryInterface(m.Type, out _, out var valT))
                    {
                        var v = valT!;
                        var vKind = GetEffectiveKind(v, null);
                        EnsureComparerStruct(emittedComparers, comparerDecls, v, vKind, root, $"M_{SanitizeIdentifier(t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}_{m.Name}_Val");
                    }
                    else if (TryGetEnumerableInterface(m.Type, out var elT))
                    {
                                                if (m.Type.SpecialType == SpecialType.System_String) continue;

                        var el = elT!;
                        var elKind = GetEffectiveKind(el, null);
                        EnsureComparerStruct(emittedComparers, comparerDecls, el, elKind, root, $"M_{SanitizeIdentifier(t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}_{m.Name}");
                    }
                }
            }

            if (comparerDecls.Count > 0)
            {
                cw.Line("// ---- element comparer structs (auto-generated) ----");
                foreach (var block in comparerDecls)
                    foreach (var line in block) cw.Line(line);
                cw.Line();
            }

                        foreach (var t in reachable.OrderBy(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal))
                EmitHelperForType_Fast(cw, t, root, trackCycles);

                        foreach (var t in reachable.OrderBy(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal))
                EmitHelperForType_Trace(cw, t, root, trackCycles);
        }
        cw.Close(); 
                cw.Open($"static class __{SanitizeIdentifier(helperClass)}_ModuleInit");
        cw.Line("[System.Runtime.CompilerServices.ModuleInitializer]");
        cw.Open("internal static void Init()");
        var generic = root.Type.Arity > 0 ? "<>" : "";
        cw.Line($"_ = typeof({helperClass}{generic});");
        cw.Close();
        cw.Close();

        if (ns is not null) cw.Close(); 
        spc.AddSource(hintName, cw.ToString());
    }

    
    private static void EmitHelperForType_Fast(CodeWriter cw, INamedTypeSymbol type, Target root, bool trackCycles)
    {
        var fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var helperName = GetHelperMethodName(type);

        cw.Open($"private static bool {helperName}({fullName} left, {fullName} right, DeepEqual.Generator.Shared.ComparisonContext context)");

        if (!type.IsValueType)
        {
            cw.Open("if (object.ReferenceEquals(left, right))");
            cw.Line("return true;");
            cw.Close();

            cw.Open("if (left is null || right is null)");
            cw.Line("return false;");
            cw.Close();

            if (trackCycles)
            {
                cw.Open("if (!context.Enter(left, right))");
                cw.Line("// already visited pair; avoid cycles");
                cw.Line("return true;");
                cw.Close();

                cw.Open("try");
            }
        }

        var schema = GetTypeSchema(type);
        foreach (var member in OrderMembersByCost(type, EnumerateComparableMembers(type, root.IncludeInternals, schema), root))
            EmitMember_Fast(cw, type, member, root);

        if (!type.IsValueType)
        {
            cw.Line("return true;");

            if (trackCycles)
            {
                cw.Close();                 cw.Open("finally");
                cw.Line("context.Exit(left, right);");
                cw.Close();
            }
        }
        else
        {
            cw.Line("return true;");
        }

        cw.Close();
        cw.Line();
    }

    private static void EmitMember_Fast(CodeWriter cw, INamedTypeSymbol owner, MemberSymbol member, Target root)
    {
        var l = $"left.{member.Name}";
        var r = $"right.{member.Name}";
        var deepAttr = GetDeepCompareAttribute(member.Symbol);
        var kind = GetEffectiveKind(member.Type, deepAttr);

        cw.Line($"// -- Member: {member.Name} ({member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})");

        if (kind == EffectiveKind.Skip) { cw.Line(); return; }

                if (!member.Type.IsValueType)
        {
            cw.Open($"if (!object.ReferenceEquals({l}, {r}))");
            cw.Open($"if ({l} is null || {r} is null)");
            cw.Line("return false;");
            cw.Close();
            cw.Close();
        }

                if (kind == EffectiveKind.Reference)
        {
            cw.Open($"if (!object.ReferenceEquals({l}, {r}))");
            cw.Line("return false;");
            cw.Close();
            cw.Line();
            return;
        }

                if (kind == EffectiveKind.Shallow)
        {
            cw.Open($"if (!object.Equals({l}, {r}))");
            cw.Line("return false;");
            cw.Close();
            cw.Line();
            return;
        }

                if (member.Type is INamedTypeSymbol nnt && nnt.OriginalDefinition.ToDisplayString() == "System.Nullable<T>")
        {
            var valueT = nnt.TypeArguments[0];
            cw.Open($"if ({l}.HasValue != {r}.HasValue)");
            cw.Line("return false;");
            cw.Close();

            cw.Open($"if ({l}.HasValue)");
            EmitNullableValueCompare_Fast(cw, l, r, valueT, root);
            cw.Close();
            cw.Line();
            return;
        }

                if (TryEmitWellKnownStructCompare(cw, l, r, member.Type)) { cw.Line(); return; }

        if (member.Type.SpecialType == SpecialType.System_String)
        {
            cw.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualStrings({l}, {r}))");
            cw.Line("return false;");
            cw.Close();
            cw.Line();
            return;
        }

        if (member.Type.TypeKind == TypeKind.Enum)
        {
            var enumFqn = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            cw.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualEnum<{enumFqn}>({l}, {r}))");
            cw.Line("return false;");
            cw.Close();
            cw.Line();
            return;
        }

        if (member.Type.IsValueType && member.Type.SpecialType != SpecialType.None)
        {
            cw.Open($"if (!{l}.Equals({r}))");
            cw.Line("return false;");
            cw.Close();
            cw.Line();
            return;
        }

                if (member.Type is IArrayTypeSymbol arr)
        {
            var el = arr.ElementType;
            var elFqn = el.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var unordered = ResolveOrderInsensitive(root, deepAttr, el, owner);
            var elKind = GetEffectiveKind(el, null);
            var cmpName = GetComparerStructName(el, $"M_{SanitizeIdentifier(owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}_{member.Name}");

            if (unordered)
            {
                cw.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualArrayUnordered<{elFqn}, {cmpName}>((Array?){l}, (Array?){r}, new {cmpName}(), context))");
                cw.Line("return false;");
                cw.Close();
            }
            else
            {
                if (arr.Rank == 1)
                {
                    cw.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualArrayRank1<{elFqn}, {cmpName}>({l} as {elFqn}[], {r} as {elFqn}[], new {cmpName}(), context))");
                    cw.Line("return false;");
                    cw.Close();
                }
                else
                {
                    cw.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualArray<{elFqn}, {cmpName}>((Array?){l}, (Array?){r}, new {cmpName}(), context))");
                    cw.Line("return false;");
                    cw.Close();
                }
            }
            cw.Line();
            return;
        }

                if (TryGetDictionaryInterface(member.Type, out var keyT, out var valT))
        {
            var kFqn = keyT!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var vFqn = valT!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var vKind = GetEffectiveKind(valT!, null);

            var lro = $"__roDictA_{SanitizeIdentifier(owner.Name)}_{SanitizeIdentifier(member.Name)}";
            var rro = $"__roDictB_{SanitizeIdentifier(owner.Name)}_{SanitizeIdentifier(member.Name)}";
            cw.Line($"var {lro} = {l} as global::System.Collections.Generic.IReadOnlyDictionary<{kFqn}, {vFqn}>;");
            cw.Line($"var {rro} = {r} as global::System.Collections.Generic.IReadOnlyDictionary<{kFqn}, {vFqn}>;");

            cw.Open($"if (!object.ReferenceEquals({lro}, {rro}))");
            {
                cw.Open($"if ({lro} is not null && {rro} is not null)");
                {
                    cw.Open($"if ({lro}.Count != {rro}.Count)");
                    cw.Line("return false;");
                    cw.Close();

                    cw.Open($"foreach (var __kv in {lro})");
                    cw.Line($"if (!{rro}.TryGetValue(__kv.Key, out var __rv)) return false;");
                    var vExprRO = BuildInlineCompareExpr($"__kv.Value", $"__rv", valT!, vKind, root, "context");
                    cw.Open($"if (!({vExprRO}))");
                    cw.Line("return false;");
                    cw.Close();
                    cw.Close();                 }
                cw.Close(); 
                cw.Open("else");
                {
                    var lrw = $"__rwDictA_{SanitizeIdentifier(owner.Name)}_{SanitizeIdentifier(member.Name)}";
                    var rrw = $"__rwDictB_{SanitizeIdentifier(owner.Name)}_{SanitizeIdentifier(member.Name)}";
                    cw.Line($"var {lrw} = {l} as global::System.Collections.Generic.IDictionary<{kFqn}, {vFqn}>;");
                    cw.Line($"var {rrw} = {r} as global::System.Collections.Generic.IDictionary<{kFqn}, {vFqn}>;");

                    cw.Open($"if ({lrw} is not null && {rrw} is not null)");
                    {
                        cw.Open($"if ({lrw}.Count != {rrw}.Count)");
                        cw.Line("return false;");
                        cw.Close();

                        cw.Open($"foreach (var __kv in {lrw})");
                        cw.Line($"if (!{rrw}.TryGetValue(__kv.Key, out var __rv)) return false;");
                        var vExprRW = BuildInlineCompareExpr($"__kv.Value", $"__rv", valT!, vKind, root, "context");
                        cw.Open($"if (!({vExprRW}))");
                        cw.Line("return false;");
                        cw.Close();
                        cw.Close();                     }
                    cw.Close(); 
                                        cw.Open("else");
                    {
                        var cmpName = GetComparerStructName(valT!, $"M_{SanitizeIdentifier(owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}_{member.Name}_Val");
                        cw.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDictionariesAny<{kFqn}, {vFqn}, {cmpName}>({l}, {r}, new {cmpName}(), context))");
                        cw.Line("return false;");
                        cw.Close();
                    }
                    cw.Close();                 }
                cw.Close();             }
            cw.Close(); 
            cw.Line();
            return;
        }

                if (TryGetEnumerableInterface(member.Type, out var elT))
        {
            var elFqn = elT!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var elKind = GetEffectiveKind(elT!, null);
            var unordered = ResolveOrderInsensitive(root, deepAttr, elT!, owner);
            var cmpName = GetComparerStructName(elT!, $"M_{SanitizeIdentifier(owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}_{member.Name}");
            var api = unordered ? "AreEqualSequencesUnordered" : "AreEqualSequencesOrdered";

            cw.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.{api}<{elFqn}, {cmpName}>({l} as IEnumerable<{elFqn}>, {r} as IEnumerable<{elFqn}>, new {cmpName}(), context))");
            cw.Line("return false;");
            cw.Close();
            cw.Line();
            return;
        }

                if (member.Type.SpecialType == SpecialType.System_Object)
        {
            cw.Open($"if (!DynamicDeepComparer.AreEqualDynamic({l}, {r}, context))");
            cw.Line("return false;");
            cw.Close();
            cw.Line();
            return;
        }

                if (member.Type is INamedTypeSymbol nts && IsUserObjectType(nts))
        {
            if (IsTypeAccessibleFromRoot(nts, root))
            {
                var helper = GetHelperMethodName(nts);
                cw.Open($"if (!{helper}({l}, {r}, context))");
                cw.Line("return false;");
                cw.Close();
            }
            else
            {
                cw.Open($"if (!object.Equals({l}, {r}))");
                cw.Line("return false;");
                cw.Close();
            }
            cw.Line();
            return;
        }

                cw.Open($"if (!object.Equals({l}, {r}))");
        cw.Line("return false;");
        cw.Close();
        cw.Line();
    }

    private static void EmitNullableValueCompare_Fast(CodeWriter cw, string l, string r, ITypeSymbol valueType, Target root)
    {
        if (TryEmitWellKnownStructCompare(cw, $"{l}.Value", $"{r}.Value", valueType)) return;

        if (valueType.TypeKind == TypeKind.Enum)
        {
            var enumFqn = valueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            cw.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualEnum<{enumFqn}>({l}.Value, {r}.Value))");
            cw.Line("return false;");
            cw.Close();
            return;
        }

        if (valueType.SpecialType == SpecialType.System_String)
        {
            cw.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualStrings({l}.Value, {r}.Value))");
            cw.Line("return false;");
            cw.Close();
            return;
        }

        if (valueType.IsValueType && valueType.SpecialType != SpecialType.None)
        {
            cw.Open($"if (!{l}.Value.Equals({r}.Value))");
            cw.Line("return false;");
            cw.Close();
            return;
        }

        if (valueType is INamedTypeSymbol vnts && IsUserObjectType(vnts))
        {
            if (IsTypeAccessibleFromRoot(vnts, root))
            {
                var helper = GetHelperMethodName(vnts);
                cw.Open($"if (!{helper}({l}.Value, {r}.Value, context))");
                cw.Line("return false;");
                cw.Close();
            }
            else
            {
                cw.Open($"if (!object.Equals({l}.Value, {r}.Value))");
                cw.Line("return false;");
                cw.Close();
            }
            return;
        }

        cw.Open($"if (!object.Equals({l}.Value, {r}.Value))");
        cw.Line("return false;");
        cw.Close();
    }

    
    private static void EmitHelperForType_Trace(CodeWriter cw, INamedTypeSymbol type, Target root, bool trackCycles)
    {
        var fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var name = $"CollectDifferences__{SanitizeIdentifier(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}";

        cw.Open($"private static void {name}({fullName} left, {fullName} right, string path, DeepEqual.Generator.Shared.ComparisonContext context, List<string> diffs)");

        if (!type.IsValueType)
        {
            cw.Open("if (object.ReferenceEquals(left, right))");
            cw.Line("return;");
            cw.Close();

            cw.Open("if (left is null || right is null)");
            cw.Line("diffs.Add($\"{path}: one is null and the other is not\");");
            cw.Line("return;");
            cw.Close();

            if (trackCycles)
            {
                cw.Open("if (!context.Enter(left, right))");
                cw.Line("// already visited this pair → avoid infinite cycle expansion");
                cw.Line("return;");
                cw.Close();

                cw.Open("try");
            }
        }

        var schema = GetTypeSchema(type);
        foreach (var member in OrderMembersByCost(type, EnumerateComparableMembers(type, root.IncludeInternals, schema), root))
            EmitMember_Trace(cw, type, member, root);

        if (!type.IsValueType)
        {
            if (trackCycles)
            {
                cw.Close();                 cw.Open("finally");
                cw.Line("context.Exit(left, right);");
                cw.Close();
            }
        }

        cw.Close();
        cw.Line();
    }

    private static void EmitMember_Trace(CodeWriter cw, INamedTypeSymbol owner, MemberSymbol member, Target root)
    {
        var l = $"left.{member.Name}";
        var r = $"right.{member.Name}";
        var deepAttr = GetDeepCompareAttribute(member.Symbol);
        var kind = GetEffectiveKind(member.Type, deepAttr);

        cw.Line($"// -- Trace Member: {member.Name} ({member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})");
        cw.Line($"var __path_{member.Name} = AppendPath(path, \"{member.Name}\");");

        if (kind == EffectiveKind.Skip)
        {
            cw.Line($"__after_{member.Name}: ;");
            cw.Line();
            return;
        }

        if (!member.Type.IsValueType)
        {
            cw.Open($"if (!object.ReferenceEquals({l}, {r}))");
            cw.Open($"if ({l} is null || {r} is null)");
            cw.Line($"diffs.Add($\"{{__path_{member.Name}}}: one is null and the other is not\");");
            cw.Line("goto __after_" + member.Name + ";");
            cw.Close();
            cw.Close();
        }

        if (kind == EffectiveKind.Reference)
        {
            cw.Open($"if (!object.ReferenceEquals({l}, {r}))");
            cw.Line($"diffs.Add($\"{{__path_{member.Name}}}: reference mismatch\");");
            cw.Close();
            cw.Line($"__after_{member.Name}: ;");
            cw.Line();
            return;
        }

        if (kind == EffectiveKind.Shallow)
        {
            cw.Open($"if (!object.Equals({l}, {r}))");
            cw.Line($"diffs.Add($\"{{__path_{member.Name}}}: values differ\");");
            cw.Close();
            cw.Line($"__after_{member.Name}: ;");
            cw.Line();
            return;
        }

                if (member.Type is INamedTypeSymbol nnt && nnt.OriginalDefinition.ToDisplayString() == "System.Nullable<T>")
        {
            var valueT = nnt.TypeArguments[0];
            cw.Open($"if ({l}.HasValue != {r}.HasValue)");
            cw.Line($"diffs.Add($\"{{__path_{member.Name}}}: one is null and the other is not\");");
            cw.Line("goto __after_" + member.Name + ";");
            cw.Close();

            cw.Open($"if ({l}.HasValue)");
            EmitNullableValueCompare_Trace(cw, l, r, valueT, $"__path_{member.Name}", root);
            cw.Close();

            cw.Line($"__after_{member.Name}: ;");
            cw.Line();
            return;
        }

                if (member.Type.SpecialType == SpecialType.System_String)
        {
            cw.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualStrings({l}, {r}))");
            cw.Line($"diffs.Add($\"{{__path_{member.Name}}}: string values differ\");");
            cw.Close();
            cw.Line($"__after_{member.Name}: ;");
            cw.Line();
            return;
        }

                if (member.Type is IArrayTypeSymbol arr)
        {
            var el = arr.ElementType;
            var elFqn = el.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var unordered = ResolveOrderInsensitive(root, deepAttr, el, owner);
            var elKind = GetEffectiveKind(el, null);
            var elCmp = BuildElementComparerSymbol(el, elKind, root); 
            if (unordered)
            {
                cw.Open($"if (!ComparisonHelpers.AreEqualArrayUnordered<{elFqn}>((Array?){l}, (Array?){r}, {elCmp}, context))");
                cw.Line($"diffs.Add($\"{{__path_{member.Name}}}: unordered array differs\");");
                cw.Close();
            }
            else
            {
                if (arr.Rank == 1)
                    cw.Open($"if (!ComparisonHelpers.AreEqualArrayRank1<{elFqn}>({l} as {elFqn}[], {r} as {elFqn}[], {elCmp}, context))");
                else
                    cw.Open($"if (!ComparisonHelpers.AreEqualArray<{elFqn}>((Array?){l}, (Array?){r}, {elCmp}, context))");

                cw.Line("// Walk ordered elements to report index-level differences");
                cw.Open($"if ((Array?){l} is null || (Array?){r} is null)");
                cw.Line($"diffs.Add($\"{{__path_{member.Name}}}: one is null and the other is not\");");
                cw.Line("goto __after_" + member.Name + ";");
                cw.Close();

                cw.Line("var __ea = ((IEnumerable)" + l + ").GetEnumerator();");
                cw.Line("var __eb = ((IEnumerable)" + r + ").GetEnumerator();");
                cw.Line("var __i = 0;");
                cw.Open("while (true)");
                cw.Line("bool __ma = __ea.MoveNext();");
                cw.Line("bool __mb = __eb.MoveNext();");
                cw.Line($"if (__ma != __mb) {{ diffs.Add($\"{{__path_{member.Name}}}: lengths differ\"); break; }}");
                cw.Line("if (!__ma) break;");

                if (el is INamedTypeSymbol elN && IsUserObjectType(elN) && IsTypeAccessibleFromRoot(elN, root))
                {
                    var helper = $"CollectDifferences__{SanitizeIdentifier(elN.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}";
                    cw.Line($"var __elPath = AppendPath(__path_{member.Name}, \"[\" + __i.ToString() + \"]\");");
                    cw.Line($"{helper}(({elFqn})__ea.Current, ({elFqn})__eb.Current, __elPath, context, diffs);");
                }
                else
                {
                    cw.Open($"if (!DynamicDeepComparer.AreEqualDynamic(({elFqn})__ea.Current, ({elFqn})__eb.Current, context))");
                    cw.Line($"diffs.Add($\"{{AppendPath(__path_{member.Name}, \"[\" + __i.ToString() + \"]\")}}: elements differ\");");
                    cw.Close();
                }
                cw.Line("__i++;");
                cw.Close();                 cw.Close();             }

            cw.Line($"__after_{member.Name}: ;");
            cw.Line();
            return;
        }

                if (TryGetDictionaryInterface(member.Type, out var keyT, out var valT))
        {
            var kFqn = keyT!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var vFqn = valT!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var vKind = GetEffectiveKind(valT!, null);
            var vCmp = BuildElementComparerSymbol(valT!, vKind, root);

            cw.Open($"if (!ComparisonHelpers.AreEqualDictionariesAny<{kFqn}, {vFqn}>({l}, {r}, {vCmp}, context))");
            cw.Line($"diffs.Add($\"{{__path_{member.Name}}}: dictionaries differ\");");
            cw.Close();

            cw.Line($"__after_{member.Name}: ;");
            cw.Line();
            return;
        }

                if (TryGetEnumerableInterface(member.Type, out var elT))
        {
            var elFqn = elT!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var elKind = GetEffectiveKind(elT!, null);
            var unordered = ResolveOrderInsensitive(root, deepAttr, elT!, owner);
            var elCmp = BuildElementComparerSymbol(elT!, elKind, root);
            var api = unordered ? "AreEqualSequencesUnordered" : "AreEqualSequencesOrdered";

            cw.Open($"if (!ComparisonHelpers.{api}<{elFqn}>({l} as IEnumerable<{elFqn}>, {r} as IEnumerable<{elFqn}>, {elCmp}, context))");
            cw.Line($"diffs.Add($\"{{__path_{member.Name}}}: {(unordered ? "unordered" : "ordered")} sequence differs\");");
            cw.Close();

            cw.Line($"__after_{member.Name}: ;");
            cw.Line();
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Object)
        {
            cw.Open($"if (!DynamicDeepComparer.AreEqualDynamic({l}, {r}, context))");
            cw.Line($"diffs.Add($\"{{__path_{member.Name}}}: dynamic values differ\");");
            cw.Close();

            cw.Line($"__after_{member.Name}: ;");
            cw.Line();
            return;
        }

        if (member.Type is INamedTypeSymbol nts && IsUserObjectType(nts))
        {
            if (IsTypeAccessibleFromRoot(nts, root))
            {
                var helper = $"CollectDifferences__{SanitizeIdentifier(nts.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}";
                cw.Line($"{helper}({l}, {r}, __path_{member.Name}, context, diffs);");
            }
            else
            {
                cw.Open($"if (!object.Equals({l}, {r}))");
                cw.Line($"diffs.Add($\"{{__path_{member.Name}}}: values differ\");");
                cw.Close();
            }

            cw.Line($"__after_{member.Name}: ;");
            cw.Line();
            return;
        }

        cw.Open($"if (!object.Equals({l}, {r}))");
        cw.Line($"diffs.Add($\"{{__path_{member.Name}}}: values differ\");");
        cw.Close();

        cw.Line($"__after_{member.Name}: ;");
        cw.Line();
    }

    private static void EmitNullableValueCompare_Trace(
        CodeWriter cw, string l, string r, ITypeSymbol valueType, string pathExpr, Target root)
    {
        if (TryEmitWellKnownStructCompare_Trace(cw, $"{l}.Value", $"{r}.Value", pathExpr, valueType)) return;

        if (valueType.TypeKind == TypeKind.Enum)
        {
            var enumFqn = valueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            cw.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualEnum<{enumFqn}>({l}.Value, {r}.Value))");
            cw.Line($"diffs.Add($\"{{{pathExpr}}}: enum values differ\");");
            cw.Close();
            return;
        }

        if (valueType.SpecialType == SpecialType.System_String)
        {
            cw.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualStrings({l}.Value, {r}.Value))");
            cw.Line($"diffs.Add($\"{{{pathExpr}}}: string values differ\");");
            cw.Close();
            return;
        }

        if (valueType.IsValueType && valueType.SpecialType != SpecialType.None)
        {
            cw.Open($"if (!{l}.Value.Equals({r}.Value))");
            cw.Line($"diffs.Add($\"{{{pathExpr}}}: values differ\");");
            cw.Close();
            return;
        }

        if (valueType is INamedTypeSymbol vnts && IsUserObjectType(vnts))
        {
            if (IsTypeAccessibleFromRoot(vnts, root))
            {
                var helper = $"CollectDifferences__{SanitizeIdentifier(vnts.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}";
                cw.Line($"{helper}({l}.Value, {r}.Value, {pathExpr}, context, diffs);");
            }
            else
            {
                cw.Open($"if (!object.Equals({l}.Value, {r}.Value))");
                cw.Line($"diffs.Add($\"{{{pathExpr}}}: values differ\");");
                cw.Close();
            }
            return;
        }

        cw.Open($"if (!object.Equals({l}.Value, {r}.Value))");
        cw.Line($"diffs.Add($\"{{{pathExpr}}}: values differ\");");
        cw.Close();
    }

    
    private static bool TryEmitWellKnownStructCompare(CodeWriter cw, string l, string r, ITypeSymbol t)
    {
        if (t.SpecialType == SpecialType.System_DateTime)
        {
            cw.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDateTime({l}, {r}))");
            cw.Line("return false;");
            cw.Close();
            return true;
        }

        var fqn = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (fqn == "global::System.DateTimeOffset")
        {
            cw.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDateTimeOffset({l}, {r}))");
            cw.Line("return false;");
            cw.Close();
            return true;
        }

        if (fqn == "global::System.TimeSpan")
        {
            cw.Open($"if ({l}.Ticks != {r}.Ticks)");
            cw.Line("return false;");
            cw.Close();
            return true;
        }

        if (fqn == "global::System.Guid")
        {
            cw.Open($"if (!{l}.Equals({r}))");
            cw.Line("return false;");
            cw.Close();
            return true;
        }

        if (fqn == "global::System.DateOnly")
        {
            cw.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDateOnly({l}, {r}))");
            cw.Line("return false;");
            cw.Close();
            return true;
        }

        if (fqn == "global::System.TimeOnly")
        {
            cw.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualTimeOnly({l}, {r}))");
            cw.Line("return false;");
            cw.Close();
            return true;
        }

        return false;
    }

    private static bool TryEmitWellKnownStructCompare_Trace(
        CodeWriter cw, string l, string r, string pathExpr, ITypeSymbol t)
    {
        if (t.SpecialType == SpecialType.System_DateTime)
        {
            cw.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDateTime({l}, {r}))");
            cw.Line($"diffs.Add($\"{{{pathExpr}}}: DateTime values differ\");");
            cw.Close();
            return true;
        }

        var fqn = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (fqn == "global::System.DateTimeOffset")
        {
            cw.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDateTimeOffset({l}, {r}))");
            cw.Line($"diffs.Add($\"{{{pathExpr}}}: DateTimeOffset values differ\");");
            cw.Close();
            return true;
        }

        if (fqn == "global::System.TimeSpan")
        {
            cw.Open($"if ({l}.Ticks != {r}.Ticks)");
            cw.Line($"diffs.Add($\"{{{pathExpr}}}: TimeSpan ticks differ\");");
            cw.Close();
            return true;
        }

        if (fqn == "global::System.Guid")
        {
            cw.Open($"if (!{l}.Equals({r}))");
            cw.Line($"diffs.Add($\"{{{pathExpr}}}: Guid values differ\");");
            cw.Close();
            return true;
        }

        if (fqn == "global::System.DateOnly")
        {
            cw.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDateOnly({l}, {r}))");
            cw.Line($"diffs.Add($\"{{{pathExpr}}}: DateOnly values differ\");");
            cw.Close();
            return true;
        }

        if (fqn == "global::System.TimeOnly")
        {
            cw.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualTimeOnly({l}, {r}))");
            cw.Line($"diffs.Add($\"{{{pathExpr}}}: TimeOnly values differ\");");
            cw.Close();
            return true;
        }

        return false;
    }

    
    private static string GetComparerStructName(ITypeSymbol elementType, string hint)
    {
        var elFqn = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return $"__Cmp__{SanitizeIdentifier(elFqn)}__{hint}";
    }

    private static string EnsureComparerStruct(
        HashSet<string> emitted,
        List<string[]> decls,
        ITypeSymbol elementType,
        EffectiveKind elementKind,
        Target root,
        string hint)
    {
        var elFqn = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var cmpName = $"__Cmp__{SanitizeIdentifier(elFqn)}__{hint}";
        if (!emitted.Add(cmpName)) return cmpName;

        var expr = BuildInlineCompareExpr("l", "r", elementType, elementKind, root); 
        var lines = new List<string>(12)
        {
            $"private readonly struct {cmpName} : DeepEqual.Generator.Shared.IElementComparer<{elFqn}>",
            "{",
            "    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]",
            $"    public bool Invoke({elFqn} l, {elFqn} r, DeepEqual.Generator.Shared.ComparisonContext c)",
            "    {",
            $"        return " + expr + ";",
            "    }",
            "}",
            ""
        };
        decls.Add(lines.ToArray());
        return cmpName;
    }

    private static string BuildElementComparerSymbol(ITypeSymbol elementType, EffectiveKind elementKind, Target root)
    {
                if (elementType is INamedTypeSymbol kvp &&
            kvp.ConstructedFrom?.ToDisplayString() == "System.Collections.Generic.KeyValuePair<TKey, TValue>")
        {
            var k = kvp.TypeArguments[0];
            var v = kvp.TypeArguments[1];

            var keyExpr = BuildInlineCompareExpr("l.Key", "r.Key", k, GetEffectiveKind(k, null), root);
            var valExpr = BuildInlineCompareExpr("l.Value", "r.Value", v, GetEffectiveKind(v, null), root);

            return $"(l, r, c) => ({keyExpr}) && ({valExpr})";
        }

        if (elementType is INamedTypeSymbol nt && nt.OriginalDefinition.ToDisplayString() == "System.Nullable<T>")
        {
            var tArg = nt.TypeArguments[0];
            var inner = BuildInlineCompareExpr("l.Value", "r.Value", tArg, GetEffectiveKind(tArg, null), root);
            return $"(l, r, c) => (l.HasValue == r.HasValue) && (!l.HasValue || ({inner}))";
        }

        if (elementType.SpecialType == SpecialType.System_Object)
            return "DynamicDeepComparer.AreEqualDynamic";

        if (elementKind == EffectiveKind.Shallow)
            return "(l, r, c) => object.Equals(l, r)";

        if (elementKind == EffectiveKind.Reference)
            return "(l, r, c) => object.ReferenceEquals(l, r)";

        if (elementType.SpecialType == SpecialType.System_String)
            return "(l, r, c) => object.ReferenceEquals(l, r) ? true : (l is null || r is null ? false : ComparisonHelpers.AreEqualStrings(l, r))";

        if (elementType.TypeKind == TypeKind.Enum)
        {
            var elFqn = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return $"(l, r, c) => ComparisonHelpers.AreEqualEnum<{elFqn}>(l, r)";
        }

        if (elementType.SpecialType == SpecialType.System_DateTime) return "(l, r, c) => ComparisonHelpers.AreEqualDateTime(l, r)";
        {
            var fqn = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (fqn == "global::System.DateTimeOffset") return "(l, r, c) => ComparisonHelpers.AreEqualDateTimeOffset(l, r)";
            if (fqn == "global::System.Guid") return "(l, r, c) => l.Equals(r)";
            if (fqn == "global::System.TimeSpan") return "(l, r, c) => l.Ticks == r.Ticks";
            if (fqn == "global::System.DateOnly") return "(l, r, c) => ComparisonHelpers.AreEqualDateOnly(l, r)";
            if (fqn == "global::System.TimeOnly") return "(l, r, c) => ComparisonHelpers.AreEqualTimeOnly(l, r)";
        }

        if (elementType.IsValueType && elementType.SpecialType != SpecialType.None)
            return "(l, r, c) => l.Equals(r)";

        if (elementType is INamedTypeSymbol uts && IsUserObjectType(uts))
        {
            if (IsTypeAccessibleFromRoot(uts, root))
            {
                var helper = GetHelperMethodName(uts);

                if (uts.IsValueType)
                {
                    return $"(l, r, c) => {helper}(l, r, c)";
                }
                else
                {
                    return $"(l, r, c) => object.ReferenceEquals(l, r) ? true : (l is null || r is null ? false : {helper}(l, r, c))";
                }
            }
            else
            {
                return "(l, r, c) => object.Equals(l, r)";
            }
        }

        return "(l, r, c) => object.Equals(l, r)";
    }

    private static string BuildInlineCompareExpr(string l, string r, ITypeSymbol type, EffectiveKind kind, Target root, string ctxVar = "c")
    {
        if (kind == EffectiveKind.Reference) return $"object.ReferenceEquals({l}, {r})";
        if (kind == EffectiveKind.Shallow) return $"object.Equals({l}, {r})";

        if (type is INamedTypeSymbol nt && nt.OriginalDefinition.ToDisplayString() == "System.Nullable<T>")
        {
            var tArg = nt.TypeArguments[0];
            var inner = BuildInlineCompareExpr($"{l}.Value", $"{r}.Value", tArg, GetEffectiveKind(tArg, null), root, ctxVar);
            return $"({l}.HasValue == {r}.HasValue) && (!{l}.HasValue || ({inner}))";
        }

        if (type.SpecialType == SpecialType.System_String) return $"DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualStrings({l}, {r})";
        if (type.TypeKind == TypeKind.Enum)
        {
            var enumFqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return $"DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualEnum<{enumFqn}>({l}, {r})";
        }
        if (type.SpecialType == SpecialType.System_DateTime) return $"DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDateTime({l}, {r})";

        var fqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (fqn == "global::System.DateTimeOffset") return $"DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDateTimeOffset({l}, {r})";
        if (fqn == "global::System.TimeSpan") return $"{l}.Ticks == {r}.Ticks";
        if (fqn == "global::System.Guid") return $"{l}.Equals({r})";
        if (fqn == "global::System.DateOnly") return $"DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDateOnly({l}, {r})";
        if (fqn == "global::System.TimeOnly") return $"DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualTimeOnly({l}, {r})";

        if (type.SpecialType == SpecialType.System_Object)
            return $"DynamicDeepComparer.AreEqualDynamic({l}, {r}, {ctxVar})";

        if (type.IsValueType && type.SpecialType != SpecialType.None)
            return $"{l}.Equals({r})";

        if (type is INamedTypeSymbol nts && IsUserObjectType(nts))
        {
            if (IsTypeAccessibleFromRoot(nts, root))
            {
                var helper = GetHelperMethodName(nts);
                if (nts.IsValueType) return $"{helper}({l}, {r}, {ctxVar})";
                return $"(object.ReferenceEquals({l}, {r}) ? true : ({l} is null || {r} is null ? false : {helper}({l}, {r}, {ctxVar})))";
            }
            return $"object.Equals({l}, {r})";
        }
        return $"object.Equals({l}, {r})";
    }

    
    private static TypeSchema GetTypeSchema(INamedTypeSymbol type)
    {
        if (Cache.TypeSchemaCache.TryGetValue(type, out var cached)) return cached;

        var attr = type.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepCompareAttributeName);
        if (attr is null)
        {
            var empty = new TypeSchema(Array.Empty<string>(), Array.Empty<string>());
            Cache.TypeSchemaCache[type] = empty;
            return empty;
        }

        static string[] ReadStringArray(TypedConstant arg)
        {
            if (arg is { Kind: TypedConstantKind.Array, IsNull: false })
                return arg.Values.Select(v => v.Value?.ToString() ?? string.Empty)
                                 .Where(s => !string.IsNullOrWhiteSpace(s))
                                 .ToArray();
            return Array.Empty<string>();
        }

        string[] include = Array.Empty<string>();
        string[] ignore = Array.Empty<string>();

        foreach (var kv in attr.NamedArguments)
        {
            if (kv.Key == "Members") include = ReadStringArray(kv.Value);
            else if (kv.Key == "IgnoreMembers") ignore = ReadStringArray(kv.Value);
        }

        var schema = new TypeSchema(include, ignore);
        Cache.TypeSchemaCache[type] = schema;
        return schema;
    }

    private static IEnumerable<MemberSymbol> EnumerateComparableMembers(INamedTypeSymbol type, bool allowInternals, TypeSchema schema)
    {
                if (schema.IncludeMembers.Count == 0 && schema.IgnoreMembers.Count == 0)
        {
            var key = (type, allowInternals);
            if (!Cache.MembersNoSchema.TryGetValue(key, out var cached))
            {
                cached = EnumerateComparableMembersUncached(type, allowInternals, schema).ToArray();
                Cache.MembersNoSchema[key] = cached;
            }
            return cached;
        }

        return EnumerateComparableMembersUncached(type, allowInternals, schema);

        static IEnumerable<MemberSymbol> EnumerateComparableMembersUncached(INamedTypeSymbol ownerType, bool inclInternals, TypeSchema schm)
        {
            static bool IsAccessible(ISymbol s, bool inclInternals, INamedTypeSymbol owner)
            {
                return s.DeclaredAccessibility switch
                {
                    Accessibility.Public => true,
                    Accessibility.Internal or Accessibility.ProtectedAndInternal
                        => inclInternals && SymbolEqualityComparer.Default.Equals(s.ContainingAssembly, owner.ContainingAssembly),
                    _ => false
                };
            }

            bool hasInclude = schm.IncludeMembers.Count > 0;
            var includeSet = hasInclude ? new HashSet<string>(schm.IncludeMembers, StringComparer.Ordinal) : null;
            var ignoreSet = schm.IgnoreMembers.Count > 0 ? new HashSet<string>(schm.IgnoreMembers, StringComparer.Ordinal) : null;

            foreach (var p in ownerType.GetMembers().OfType<IPropertySymbol>())
            {
                if (p.IsStatic) continue;
                if (p.Parameters.Length != 0) continue;
                if (p.GetMethod is null) continue;
                if (!IsAccessible(p, inclInternals, ownerType)) continue;
                if (hasInclude && !includeSet!.Contains(p.Name)) continue;
                if (ignoreSet is not null && ignoreSet.Contains(p.Name)) continue;

                if (ownerType.IsValueType && SymbolEqualityComparer.Default.Equals(p.Type, ownerType) && !hasInclude) continue;

                yield return new MemberSymbol(p.Name, p.Type, p);
            }

            foreach (var f in ownerType.GetMembers().OfType<IFieldSymbol>())
            {
                if (f.IsStatic || f.IsConst || f.IsImplicitlyDeclared) continue;
                if (f.AssociatedSymbol is not null) continue;
                if (f.Name.StartsWith("<", StringComparison.Ordinal)) continue;
                if (!IsAccessible(f, inclInternals, ownerType)) continue;
                if (hasInclude && !includeSet!.Contains(f.Name)) continue;
                if (ignoreSet is not null && ignoreSet.Contains(f.Name)) continue;

                if (ownerType.IsValueType && SymbolEqualityComparer.Default.Equals(f.Type, ownerType) && !hasInclude) continue;

                yield return new MemberSymbol(f.Name, f.Type, f);
            }
        }
    }

        private static IEnumerable<MemberSymbol> OrderMembersByCost(INamedTypeSymbol owner, IEnumerable<MemberSymbol> members, Target root)
        => members
            .Select(m => (m, key: GetMemberCostGroup(owner, m, root)))
            .OrderBy(t => t.key)
            .ThenBy(t => t.m.Name, StringComparer.Ordinal)
            .Select(t => t.m);

    private static int GetMemberCostGroup(INamedTypeSymbol owner, MemberSymbol member, Target root)
    {
        var attr = GetDeepCompareAttribute(member.Symbol);
        var kind = GetEffectiveKind(member.Type, attr);

        if (kind == EffectiveKind.Skip) return 99;
        if (kind == EffectiveKind.Reference || kind == EffectiveKind.Shallow) return 0;

        var t = member.Type;

                if (t is INamedTypeSymbol nnt && nnt.OriginalDefinition.ToDisplayString() == "System.Nullable<T>")
        {
            var inner = nnt.TypeArguments[0];
            if (inner.TypeKind == TypeKind.Enum) return 2;
            if (IsWellKnownStruct(inner) || (inner.IsValueType && inner.SpecialType != SpecialType.None)) return 3;
            t = inner;
        }

        if (t.SpecialType == SpecialType.System_String) return 1;
        if (t.TypeKind == TypeKind.Enum) return 2;
        if (IsWellKnownStruct(t) || (t.IsValueType && t.SpecialType != SpecialType.None)) return 3;
        if (t.SpecialType == SpecialType.System_Object) return 6;

        if (t is IArrayTypeSymbol) return 9;
        if (TryGetDictionaryInterface(t, out _, out _)) return 8;
        if (TryGetEnumerableInterface(t, out _)) return 9;

        if (t is INamedTypeSymbol nts && IsUserObjectType(nts)) return 7;

        return 7;
    }

    private static bool IsWellKnownStruct(ITypeSymbol t)
    {
        if (t.SpecialType == SpecialType.System_DateTime) return true;
        var fqn = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return fqn is "global::System.DateTimeOffset"
                 or "global::System.TimeSpan"
                 or "global::System.Guid"
                 or "global::System.DateOnly"
                 or "global::System.TimeOnly";
    }

    private static AttributeData? GetDeepCompareAttribute(ISymbol symbol)
        => symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepCompareAttributeName);

    private static EffectiveKind GetEffectiveKind(ITypeSymbol type, AttributeData? memberAttribute)
    {
        if (memberAttribute is not null)
        {
            var val = memberAttribute.NamedArguments.FirstOrDefault(p => p.Key == "Kind").Value.Value;
            if (val is int mk) return (EffectiveKind)mk;
        }

        var typeAttr = type.OriginalDefinition.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepCompareAttributeName);
        if (typeAttr is not null)
        {
            var val = typeAttr.NamedArguments.FirstOrDefault(p => p.Key == "Kind").Value.Value;
            if (val is int tk) return (EffectiveKind)tk;
        }
        return EffectiveKind.Deep;
    }

    private static bool ResolveOrderInsensitive(Target root, AttributeData? memberAttribute, ITypeSymbol elementType, INamedTypeSymbol? containingType)
    {
                if (memberAttribute is not null)
        {
            var opt = memberAttribute.NamedArguments.FirstOrDefault(a => a.Key == "OrderInsensitive").Value;
            if (opt.Value is bool b) return b;
        }

                var typeAttr = elementType.OriginalDefinition.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepCompareAttributeName);
        if (typeAttr is not null)
        {
            var opt = typeAttr.NamedArguments.FirstOrDefault(a => a.Key == "OrderInsensitive").Value;
            if (opt.Value is bool b) return b;
        }

                if (containingType is not null)
        {
            var containerAttr = containingType.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepCompareAttributeName);
            if (containerAttr is not null)
            {
                var opt = containerAttr.NamedArguments.FirstOrDefault(a => a.Key == "OrderInsensitive").Value;
                if (opt.Value is bool b) return b;
            }
        }

                return root.OrderInsensitiveCollections;
    }

    private static bool TryGetEnumerableInterface(ITypeSymbol type, out ITypeSymbol? elementType)
    {
        if (Cache.IEnumerableElement.TryGetValue(type, out var cached))
        {
            elementType = cached;
            return cached is not null;
        }

                if (type is INamedTypeSymbol named &&
            named.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>")
        {
            elementType = named.TypeArguments[0];
            Cache.IEnumerableElement[type] = elementType;
            return true;
        }

        foreach (var i in type.AllInterfaces)
        {
            if (i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>")
            {
                elementType = i.TypeArguments[0];
                Cache.IEnumerableElement[type] = elementType;
                return true;
            }
        }
        elementType = null;
        Cache.IEnumerableElement[type] = null;
        return false;
    }

    private static bool TryGetReadOnlyListInterface(ITypeSymbol type, out ITypeSymbol? elementType)
    {
        if (type is INamedTypeSymbol named)
        {
            var defSelf = named.OriginalDefinition.ToDisplayString();
            if (defSelf == "System.Collections.Generic.List<T>"
                || defSelf == "System.Collections.Generic.IReadOnlyList<T>"
                || defSelf == "System.Collections.Generic.IList<T>")
            {
                elementType = named.TypeArguments[0];
                return true;
            }
        }

        foreach (var i in type.AllInterfaces)
        {
            var def = i.OriginalDefinition.ToDisplayString();
            if (def == "System.Collections.Generic.IReadOnlyList<T>" || def == "System.Collections.Generic.IList<T>")
            {
                elementType = i.TypeArguments[0];
                return true;
            }
        }

        elementType = null; return false;
    }

    private static bool TryGetDictionaryInterface(ITypeSymbol type, out ITypeSymbol? keyType, out ITypeSymbol? valueType)
    {
        if (Cache.IDictionaryTypes.TryGetValue(type, out var cached))
        {
            if (cached is not null)
            {
                keyType = cached.Value.Key;
                valueType = cached.Value.Val;
                return true;
            }
            keyType = null; valueType = null; return false;
        }

                if (type is INamedTypeSymbol named)
        {
            var defSelf = named.OriginalDefinition.ToDisplayString();
            if (defSelf == "System.Collections.Generic.IDictionary<TKey, TValue>" ||
                defSelf == "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>")
            {
                keyType = named.TypeArguments[0];
                valueType = named.TypeArguments[1];
                Cache.IDictionaryTypes[type] = (keyType, valueType);
                return true;
            }
        }

        foreach (var i in type.AllInterfaces)
        {
            var def = i.OriginalDefinition.ToDisplayString();
            if (def == "System.Collections.Generic.IDictionary<TKey, TValue>" ||
                def == "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>")
            {
                keyType = i.TypeArguments[0];
                valueType = i.TypeArguments[1];
                Cache.IDictionaryTypes[type] = (keyType, valueType);
                return true;
            }
        }
        keyType = null; valueType = null;
        Cache.IDictionaryTypes[type] = null;
        return false;
    }

    private static bool IsUserObjectType(ITypeSymbol type)
    {
        if (Cache.IsUserObject.TryGetValue(type, out var cached)) return cached;

        if (type is not INamedTypeSymbol n) { Cache.IsUserObject[type] = false; return false; }
        if (n.SpecialType != SpecialType.None) { Cache.IsUserObject[type] = false; return false; } 
        var ns = n.ContainingNamespace?.ToDisplayString() ?? "";
        if (ns.StartsWith("System", StringComparison.Ordinal)) { Cache.IsUserObject[type] = false; return false; }

        var asm = n.ContainingAssembly?.Name ?? "";
        if (asm is "mscorlib" or "System.Private.CoreLib" or "System.Runtime") { Cache.IsUserObject[type] = false; return false; }

        var ok = n.TypeKind is TypeKind.Class or TypeKind.Struct && n is not IArrayTypeSymbol;
        Cache.IsUserObject[type] = ok;
        return ok;
    }

    private static bool IsTypeAccessibleFromRoot(INamedTypeSymbol t, Target root)
    {
                if (t.DeclaredAccessibility == Accessibility.Public)
        {
            var cur = t.ContainingType;
            while (cur is not null)
            {
                if (cur.DeclaredAccessibility != Accessibility.Public) return false;
                cur = cur.ContainingType;
            }
            return true;
        }

                if (!root.IncludeInternals) return false;
        if (!SymbolEqualityComparer.Default.Equals(t.ContainingAssembly, root.Type.ContainingAssembly)) return false;

        var c = t;
        while (c is not null)
        {
            if (c.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedAndInternal)
            {
                c = c.ContainingType;
                continue;
            }
            return false;
        }
        return true;
    }

    private static HashSet<INamedTypeSymbol> BuildReachableTypeClosure(Target root)
    {
        var set = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var queue = new Queue<INamedTypeSymbol>();

        set.Add(root.Type);
        queue.Enqueue(root.Type);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var schema = GetTypeSchema(current);

            foreach (var member in EnumerateComparableMembers(current, root.IncludeInternals, schema))
            {
                var kind = GetEffectiveKind(member.Type, GetDeepCompareAttribute(member.Symbol));
                if (kind is EffectiveKind.Skip or EffectiveKind.Shallow or EffectiveKind.Reference) continue;
                Accumulate(member.Type);
            }
        }
        return set;

        void Accumulate(ITypeSymbol t)
        {
                        if (t is INamedTypeSymbol nnt && nnt.OriginalDefinition.ToDisplayString() == "System.Nullable<T>")
            {
                t = nnt.TypeArguments[0];
            }

            if (t is IArrayTypeSymbol at) { Accumulate(at.ElementType); return; }
            if (TryGetDictionaryInterface(t, out _, out var valT)) { Accumulate(valT!); return; }
            if (TryGetEnumerableInterface(t, out var elT)) { Accumulate(elT!); return; }

            if (t is INamedTypeSymbol n && IsUserObjectType(n) && IsTypeAccessibleFromRoot(n, root) && set.Add(n))
            {
                queue.Enqueue(n);
            }
        }
    }

    private static string GetHelperMethodName(INamedTypeSymbol type)
        => $"AreDeepEqual__{SanitizeIdentifier(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}";

    private static string SanitizeIdentifier(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s) sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        return sb.ToString();
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var arr = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(arr);
    }

    
    private sealed class CodeWriter
    {
        private readonly StringBuilder _sb = new();
        private int _indent;

        public void Line(string text = "")
        {
            if (text.Length == 0) { _sb.AppendLine(); return; }
            _sb.Append(' ', _indent * 4);
            _sb.AppendLine(text);
        }

        public void Open(string header)
        {
            Line(header);
            Line("{");
            _indent++;
        }

        public void Close()
        {
            _indent = Math.Max(0, _indent - 1);
            Line("}");
        }

        public override string ToString() => _sb.ToString();
    }
}
