using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
        bool CycleTrackingEnabled,
        bool IncludeBaseMembers);

    private readonly record struct MemberSymbol(string Name, ITypeSymbol Type, ISymbol Symbol);

    private sealed record TypeSchema(IReadOnlyList<string> IncludeMembers, IReadOnlyList<string> IgnoreMembers);

    private enum EffectiveKind
    {
        Deep = 0,
        Shallow = 1,
        Reference = 2,
        Skip = 3
    }

    private sealed class PerCompilationCache
    {
        internal readonly Dictionary<ITypeSymbol, bool> UserObject = new(SymbolEqualityComparer.Default);
        internal readonly Dictionary<ITypeSymbol, ITypeSymbol?> EnumerableElement = new(SymbolEqualityComparer.Default);

        internal readonly Dictionary<ITypeSymbol, (ITypeSymbol Key, ITypeSymbol Val)?> DictionaryKv =
            new(SymbolEqualityComparer.Default);

        internal readonly Dictionary<ITypeSymbol, ITypeSymbol?> MemoryElement = new(SymbolEqualityComparer.Default);
        internal readonly Dictionary<INamedTypeSymbol, TypeSchema> SchemaCache = new(SymbolEqualityComparer.Default);

        internal readonly Dictionary<(INamedTypeSymbol type, bool allowInternals, bool includeBase), MemberSymbol[]>
            MemberCache = new(MemberKeyComparer.Instance);

        private sealed class
            MemberKeyComparer : IEqualityComparer<(INamedTypeSymbol type, bool allowInternals, bool includeBase)>
        {
            public static readonly MemberKeyComparer Instance = new();

            public bool Equals((INamedTypeSymbol type, bool allowInternals, bool includeBase) x,
                (INamedTypeSymbol type, bool allowInternals, bool includeBase) y)
            {
                return SymbolEqualityComparer.Default.Equals(x.type, y.type) && x.allowInternals == y.allowInternals &&
                       x.includeBase == y.includeBase;
            }

            public int GetHashCode((INamedTypeSymbol type, bool allowInternals, bool includeBase) obj)
            {
                unchecked
                {
                    var h1 = SymbolEqualityComparer.Default.GetHashCode(obj.type);
                    var h2 = obj.allowInternals ? 1 : 0;
                    var h3 = obj.includeBase ? 1 : 0;
                    return (((h1 * 397) ^ h2) * 397) ^ h3;
                }
            }
        }

        private static readonly ConditionalWeakTable<Compilation, PerCompilationCache> Table = new();

        public static PerCompilationCache Get(Compilation c)
        {
            return Table.GetValue(c, _ => new PerCompilationCache());
        }
    }

    [ThreadStatic] private static PerCompilationCache? _threadCache;
    private static PerCompilationCache Cache => _threadCache ??= new PerCompilationCache();

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var roots = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => node is TypeDeclarationSyntax { AttributeLists.Count: > 0 },
                static (ctx, _) => GetRoot(ctx)).Where(t => t is not null);
        var inputs = roots.Combine(context.CompilationProvider);
        context.RegisterSourceOutput(inputs, static (spc, pair) =>
        {
            var target = pair.Left;
            var compilation = pair.Right;
            if (target is null)
            {
                return;
            }

            _threadCache = PerCompilationCache.Get(compilation);
            EmitForRoot(spc, target.Value);
        });
    }

    private static Target? GetRoot(GeneratorSyntaxContext context)
    {
        if (context.Node is not TypeDeclarationSyntax tds)
        {
            return null;
        }

        if (context.SemanticModel.GetDeclaredSymbol(tds) is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        var attr = typeSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepComparableAttributeName);
        if (attr is null)
        {
            return null;
        }

        var includeInternals = GetNamedBool(attr, "IncludeInternals");
        var orderInsensitive = GetNamedBool(attr, "OrderInsensitiveCollections");
        var cycleTracking = true;
        var cycleArg = GetNamedBoolNullable(attr, "CycleTracking");
        if (cycleArg is { } b)
        {
            cycleTracking = b;
        }

        var includeBase = GetNamedBool(attr, "IncludeBaseMembers");

        return new Target(typeSymbol, includeInternals, orderInsensitive, cycleTracking, includeBase);
    }

    private static bool GetNamedBool(AttributeData attribute, string name)
    {
        return attribute.NamedArguments.Any(kv => kv.Key == name && kv.Value.Value is true);
    }

    private static bool? GetNamedBoolNullable(AttributeData attribute, string name)
    {
        foreach (var kv in attribute.NamedArguments)
            if (kv.Key == name && kv.Value.Value is bool b)
            {
                return b;
            }

        return null;
    }

    private static void EmitForRoot(SourceProductionContext spc, Target root)
    {
        var ns = root.Type.ContainingNamespace.IsGlobalNamespace
            ? null
            : root.Type.ContainingNamespace.ToDisplayString();
        var rootFqn = root.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var helperClass = root.Type.Name + "DeepEqual";
        var hintName = SanitizeFileName(rootFqn + "_DeepEqual.g.cs");

        var reachable = BuildReachableTypeClosure(root);
        var trackCycles = root.CycleTrackingEnabled;
        var accessibility = root.IncludeInternals || root.Type.DeclaredAccessibility != Accessibility.Public
            ? "internal"
            : "public";
        var typeParams = root.Type.Arity > 0
            ? "<" + string.Join(",", root.Type.TypeArguments.Select(a => a.Name)) + ">"
            : "";

        var w = new CodeWriter();
        w.Line("// <auto-generated/>");
        w.Line("#pragma warning disable");
        w.Line("using System;");
        w.Line("using System.Collections;");
        w.Line("using System.Collections.Generic;");
        w.Line("using DeepEqual.Generator.Shared;");
        w.Line();

        if (ns is not null)
        {
            w.Open("namespace " + ns);
        }

        w.Open(accessibility + " static class " + helperClass + typeParams);

        w.Open("static " + helperClass + "()");
        foreach (var t in reachable.Where(t => IsTypeAccessibleFromRoot(t, root))
                     .OrderBy(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal))
        {
            var fqn = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var helper = GetHelperMethodName(t);
            w.Line("GeneratedHelperRegistry.Register<" + fqn + ">((l, r, c) => " + helper + "(l, r, c));");
        }

        w.Close();
        w.Line();

        if (root.Type.IsValueType)
        {
            w.Open(accessibility + " static bool AreDeepEqual(" + rootFqn + " left, " + rootFqn + " right)");
        }
        else
        {
            w.Open(accessibility + " static bool AreDeepEqual(" + rootFqn + "? left, " + rootFqn + "? right)");
        }

        if (!root.Type.IsValueType)
        {
            w.Open("if (object.ReferenceEquals(left, right))");
            w.Line("return true;");
            w.Close();
            w.Open("if (left is null || right is null)");
            w.Line("return false;");
            w.Close();
        }

        w.Line("var context = " +
               (trackCycles
                   ? "new DeepEqual.Generator.Shared.ComparisonContext()"
                   : "DeepEqual.Generator.Shared.ComparisonContext.NoTracking") + ";");
        w.Line("return " + GetHelperMethodName(root.Type) + "(left, right, context);");
        w.Close();
        w.Line();

        if (root.Type.IsValueType)
        {
            w.Open(accessibility + " static bool AreDeepEqual(" + rootFqn + " left, " + rootFqn +
                   " right, DeepEqual.Generator.Shared.ComparisonOptions options)");
        }
        else
        {
            w.Open(accessibility + " static bool AreDeepEqual(" + rootFqn + "? left, " + rootFqn +
                   "? right, DeepEqual.Generator.Shared.ComparisonOptions options)");
        }

        if (!root.Type.IsValueType)
        {
            w.Open("if (object.ReferenceEquals(left, right))");
            w.Line("return true;");
            w.Close();
            w.Open("if (left is null || right is null)");
            w.Line("return false;");
            w.Close();
        }

        w.Line("var context = new DeepEqual.Generator.Shared.ComparisonContext(options);");
        w.Line("return " + GetHelperMethodName(root.Type) + "(left, right, context);");
        w.Close();
        w.Line();

        if (root.Type.IsValueType)
        {
            w.Open(accessibility + " static bool AreDeepEqual(" + rootFqn + " left, " + rootFqn +
                   " right, DeepEqual.Generator.Shared.ComparisonContext context)");
        }
        else
        {
            w.Open(accessibility + " static bool AreDeepEqual(" + rootFqn + "? left, " + rootFqn +
                   "? right, DeepEqual.Generator.Shared.ComparisonContext context)");
        }

        if (!root.Type.IsValueType)
        {
            w.Open("if (object.ReferenceEquals(left, right))");
            w.Line("return true;");
            w.Close();
            w.Open("if (left is null || right is null)");
            w.Line("return false;");
            w.Close();
        }

        w.Line("return " + GetHelperMethodName(root.Type) + "(left, right, context);");
        w.Close();
        w.Line();

        var emittedComparers = new HashSet<string>(StringComparer.Ordinal);
        var comparerDeclarations = new List<string[]>();

        foreach (var t in reachable.OrderBy(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                     StringComparer.Ordinal))
            EmitHelperForType(w, t, root, trackCycles, emittedComparers, comparerDeclarations);

        if (comparerDeclarations.Count > 0)
        {
            foreach (var block in comparerDeclarations)
                foreach (var line in block)
                    w.Line(line);
        }

        w.Close();
        w.Open("static class __" + SanitizeIdentifier(helperClass) + "_ModuleInit");
        w.Line("[System.Runtime.CompilerServices.ModuleInitializer]");
        w.Open("internal static void Init()");
        var generic = root.Type.Arity > 0 ? "<>" : "";
        w.Line("_ = typeof(" + helperClass + generic + ");");
        w.Close();
        w.Close();

        if (ns is not null)
        {
            w.Close();
        }

        spc.AddSource(hintName, w.ToString());
    }

    private static void EmitHelperForType(CodeWriter w, INamedTypeSymbol type, Target root, bool trackCycles,
        HashSet<string> emittedComparers, List<string[]> comparerDeclarations)
    {
        var fqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var helper = GetHelperMethodName(type);
        w.Open("private static bool " + helper + "(" + fqn + " left, " + fqn +
               " right, DeepEqual.Generator.Shared.ComparisonContext context)");

        if (!type.IsValueType)
        {
            w.Open("if (object.ReferenceEquals(left, right))");
            w.Line("return true;");
            w.Close();
            w.Open("if (left is null || right is null)");
            w.Line("return false;");
            w.Close();

            if (trackCycles)
            {
                w.Open("if (!context.Enter(left, right))");
                w.Line("return true;");
                w.Close();
                w.Open("try");
            }
        }

        var schema = GetTypeSchema(type);
        foreach (var member in OrderMembers(EnumerateMembers(type, root.IncludeInternals, root.IncludeBaseMembers, schema)))
            EmitMember(w, type, member, root, emittedComparers, comparerDeclarations);

        w.Line("return true;");
        if (!type.IsValueType && trackCycles)
        {
            w.Close();
            w.Open("finally");
            w.Line("context.Exit(left, right);");
            w.Close();
        }

        w.Close();
        w.Line();
    }

    private static void EmitMember(CodeWriter w, INamedTypeSymbol owner, MemberSymbol member, Target root,
        HashSet<string> emittedComparers, List<string[]> comparerDeclarations)
    {
        var leftExpr = "left." + member.Name;
        var rightExpr = "right." + member.Name;
        var deepAttr = GetDeepCompareAttribute(member.Symbol);
        var kind = GetEffectiveKind(member.Type, deepAttr);
        if (kind == EffectiveKind.Skip)
        {
            w.Line();
            return;
        }

        if (!member.Type.IsValueType)
        {
            w.Open("if (!object.ReferenceEquals(" + leftExpr + ", " + rightExpr + "))");
            w.Open("if (" + leftExpr + " is null || " + rightExpr + " is null)");
            w.Line("return false;");
            w.Close();
            w.Close();
        }

        if (kind == EffectiveKind.Reference)
        {
            w.Open("if (!object.ReferenceEquals(" + leftExpr + ", " + rightExpr + "))");
            w.Line("return false;");
            w.Close();
            w.Line();
            return;
        }

        var directCustomCmp = GetEffectiveComparerType(member.Type, deepAttr);
        if (directCustomCmp is not null)
        {
            var cmpFqn = directCustomCmp.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var tFqn = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var customVar = "__cmp_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
            w.Line("var " + customVar + " = (System.Collections.Generic.IEqualityComparer<" + tFqn +
                   ">)System.Activator.CreateInstance(typeof(" + cmpFqn + "))!;");
            w.Open("if (!" + customVar + ".Equals(" + leftExpr + ", " + rightExpr + "))");
            w.Line("return false;");
            w.Close();
            w.Line();
            return;
        }

        if (kind == EffectiveKind.Shallow)
        {
            w.Open("if (!object.Equals(" + leftExpr + ", " + rightExpr + "))");
            w.Line("return false;");
            w.Close();
            w.Line();
            return;
        }

        if (member.Type is INamedTypeSymbol nnt0 && nnt0.OriginalDefinition.ToDisplayString() == "System.Nullable<T>")
        {
            var valueT = nnt0.TypeArguments[0];
            var customCmpT = GetEffectiveComparerType(valueT, deepAttr);
            string? customVar = null;
            if (customCmpT is not null)
            {
                var cmpFqn = customCmpT.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var elFqn2 = valueT.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                customVar = "__cmp_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
                w.Line("var " + customVar + " = (System.Collections.Generic.IEqualityComparer<" + elFqn2 +
                       ">)System.Activator.CreateInstance(typeof(" + cmpFqn + "))!;");
            }

            w.Open("if (" + leftExpr + ".HasValue != " + rightExpr + ".HasValue)");
            w.Line("return false;");
            w.Close();
            w.Open("if (" + leftExpr + ".HasValue)");
            if (customVar is not null)
            {
                w.Open("if (!" + customVar + ".Equals(" + leftExpr + ".Value, " + rightExpr + ".Value))");
                w.Line("return false;");
                w.Close();
            }
            else
            {
                EmitNullableValueCompare_NoCustom(w, leftExpr, rightExpr, valueT);
            }

            w.Close();
            w.Line();
            return;
        }

        if (TryEmitWellKnownStructCompare(w, leftExpr, rightExpr, member.Type))
        {
            w.Line();
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_String)
        {
            w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualStrings(" + leftExpr + ", " + rightExpr +
                   ", context))");
            w.Line("return false;");
            w.Close();
            w.Line();
            return;
        }

        if (member.Type.TypeKind == TypeKind.Enum)
        {
            var enumFqn = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualEnum<" + enumFqn + ">(" + leftExpr +
                   ", " + rightExpr + "))");
            w.Line("return false;");
            w.Close();
            w.Line();
            return;
        }

        if (IsNumericWithTolerance(member.Type))
        {
            var call = GetNumericCall(member.Type, leftExpr, rightExpr, "context");
            w.Open("if (!" + call + ")");
            w.Line("return false;");
            w.Close();
            w.Line();
            return;
        }

        if (TryGetReadOnlyMemory(member.Type, out var romEl))
        {
            var elFqn = romEl!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var elKind = GetEffectiveKind(romEl, null);
            var elemCustomCmpT = GetEffectiveComparerType(romEl, deepAttr);
            string? elemCustomVar = null;
            if (elemCustomCmpT is not null)
            {
                var cmpFqn = elemCustomCmpT.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                elemCustomVar = "__cmpE_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
                w.Line("var " + elemCustomVar + " = (System.Collections.Generic.IEqualityComparer<" + elFqn +
                       ">)System.Activator.CreateInstance(typeof(" + cmpFqn + "))!;");
            }

            var cmpName = EnsureComparerStruct(emittedComparers, comparerDeclarations, romEl, elKind,
                "M_" + SanitizeIdentifier(owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)) + "_" +
                member.Name, elemCustomVar);
            w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualReadOnlyMemory<" + elFqn + ", " +
                   cmpName + ">(" + leftExpr + ", " + rightExpr + ", new " + cmpName + "(" + (elemCustomVar ?? "") +
                   "), context))");
            w.Line("return false;");
            w.Close();
            w.Line();
            return;
        }

        if (TryGetMemory(member.Type, out var memEl))
        {
            var elFqn = memEl!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var elKind = GetEffectiveKind(memEl, null);
            var elemCustomCmpT = GetEffectiveComparerType(memEl, deepAttr);
            string? elemCustomVar = null;
            if (elemCustomCmpT is not null)
            {
                var cmpFqn = elemCustomCmpT.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                elemCustomVar = "__cmpE_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
                w.Line("var " + elemCustomVar + " = (System.Collections.Generic.IEqualityComparer<" + elFqn +
                       ">)System.Activator.CreateInstance(typeof(" + cmpFqn + "))!;");
            }

            var cmpName = EnsureComparerStruct(emittedComparers, comparerDeclarations, memEl, elKind,
                "M_" + SanitizeIdentifier(owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)) + "_" +
                member.Name, elemCustomVar);
            w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualMemory<" + elFqn + ", " + cmpName + ">(" +
                   leftExpr + ", " + rightExpr + ", new " + cmpName + "(" + (elemCustomVar ?? "") + "), context))");
            w.Line("return false;");
            w.Close();
            w.Line();
            return;
        }

        if (member.Type.IsValueType && member.Type.SpecialType != SpecialType.None)
        {
            w.Open("if (!" + leftExpr + ".Equals(" + rightExpr + "))");
            w.Line("return false;");
            w.Close();
            w.Line();
            return;
        }

        if (member.Type is IArrayTypeSymbol arr)
        {
            var el = arr.ElementType;
            var elFqn = el.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var unordered = arr.Rank == 1 && ResolveOrderInsensitive(root, deepAttr, el, owner);
            var elKind = GetEffectiveKind(el, null);

            var elemCustomCmpT = GetEffectiveComparerType(el, deepAttr);
            string? elemCustomVar = null;
            if (elemCustomCmpT is not null)
            {
                var cmpFqn = elemCustomCmpT.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                elemCustomVar = "__cmpE_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
                w.Line("var " + elemCustomVar + " = (System.Collections.Generic.IEqualityComparer<" + elFqn +
                       ">)System.Activator.CreateInstance(typeof(" + cmpFqn + "))!;");
            }

            var cmpName = EnsureComparerStruct(emittedComparers, comparerDeclarations, el, elKind,
                "M_" + SanitizeIdentifier(owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)) + "_" +
                member.Name, elemCustomVar);

            if (unordered && TryGetKeySpec(el, deepAttr, root, out var keyTypeFqn,
                    out var keyExprFmt))
            {
                var listA = "__listA_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
                var listB = "__listB_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
                var dictA = "__ka_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
                var dictB = "__kb_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
                var tmpA = "__eA_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
                var tmpB = "__eB_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);

                w.Line("var " + listA + " = " + leftExpr + " as System.Collections.Generic.IReadOnlyList<" + elFqn +
                       ">;");
                w.Line("var " + listB + " = " + rightExpr + " as System.Collections.Generic.IReadOnlyList<" + elFqn +
                       ">;");

                w.Open("if (!object.ReferenceEquals(" + listA + ", " + listB + "))");
                w.Open("if (" + listA + " is null || " + listB + " is null)");
                w.Line("return false;");
                w.Close();

                w.Line("var " + dictA + " = new System.Collections.Generic.Dictionary<" + keyTypeFqn +
                       ", System.Collections.Generic.List<" + elFqn + ">>();");
                w.Line("var " + dictB + " = new System.Collections.Generic.Dictionary<" + keyTypeFqn +
                       ", System.Collections.Generic.List<" + elFqn + ">>();");

                w.Open("for (int __i = 0; __i < " + listA + ".Count; __i++)");
                w.Line("var " + tmpA + " = " + listA + "[__i];");
                w.Line("var __k = " + string.Format(keyExprFmt, tmpA) + ";");
                w.Open("if (!" + dictA + ".TryGetValue(__k, out var __lst))");
                w.Line("__lst = " + dictA + "[__k] = new System.Collections.Generic.List<" + elFqn + ">();");
                w.Close();
                w.Line("__lst.Add(" + tmpA + ");");
                w.Close();
                w.Open("for (int __j = 0; __j < " + listB + ".Count; __j++)");
                w.Line("var " + tmpB + " = " + listB + "[__j];");
                w.Line("var __k = " + string.Format(keyExprFmt, tmpB) + ";");
                w.Open("if (!" + dictB + ".TryGetValue(__k, out var __lst))");
                w.Line("__lst = " + dictB + "[__k] = new System.Collections.Generic.List<" + elFqn + ">();");
                w.Close();
                w.Line("__lst.Add(" + tmpB + ");");
                w.Close();
                w.Line("if (" + dictA + ".Count != " + dictB + ".Count) return false;");
                w.Open("foreach (var __kv in " + dictA + ")");
                w.Open("if (!" + dictB + ".TryGetValue(__kv.Key, out var __lstB)) return false;");
                w.Line("if (__kv.Value.Count != __lstB.Count) return false;");
                w.Line("var __m = new bool[__lstB.Count];");
                w.Line("var __cmp = new " + cmpName + "(" + (elemCustomVar ?? "") + ");");
                w.Open("for (int __x = 0; __x < __kv.Value.Count; __x++)");
                w.Line("bool __f = false;");
                w.Open("for (int __y = 0; __y < __lstB.Count; __y++)");
                w.Open("if (!__m[__y])");
                w.Open("if (__cmp.Invoke(__kv.Value[__x], __lstB[__y], context))");
                w.Line("__m[__y] = (__f = true);");
                w.Close();
                w.Close();
                w.Close();
                w.Open("if (!__f)");
                w.Line("return false;");
                w.Close();
                w.Close();
                w.Close();
                w.Close();
            }
            else if (unordered && IsHashFriendly(el))
            {
                var eqExpr = GetEqualityComparerExprForHash(el, "context", elemCustomVar);
                var la = leftExpr;
                var lb = rightExpr;

                w.Open("if (!object.ReferenceEquals(" + la + ", " + lb + "))");
                w.Open("if (" + la + " is null || " + lb + " is null)");
                w.Line("return false;");
                w.Close();
                w.Open("if (" + la + ".Length != " + lb + ".Length)");
                w.Line("return false;");
                w.Close();
                w.Line("var __ra = new System.Collections.Generic.List<" + elFqn + ">(" + la + ");");
                w.Line("var __rb = new System.Collections.Generic.List<" + elFqn + ">(" + lb + ");");
                w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualSequencesUnorderedHash(__ra, __rb, " +
                       eqExpr + "))");
                w.Line("return false;");
                w.Close();
                w.Close();
            }
            else
            {
                if (unordered)
                {
                    w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualArrayUnordered<" + elFqn + ", " +
                           cmpName + ">((Array?)" + leftExpr + ", (Array?)" + rightExpr + ", new " + cmpName + "(" +
                           (elemCustomVar ?? "") + "), context))");
                    w.Line("return false;");
                    w.Close();
                }
                else
                {
                    if (arr.Rank == 1)
                    {
                        w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualArrayRank1<" + elFqn + ", " +
                               cmpName + ">(" + leftExpr + " as " + elFqn + "[], " + rightExpr + " as " + elFqn +
                               "[], new " + cmpName + "(" + (elemCustomVar ?? "") + "), context))");
                        w.Line("return false;");
                        w.Close();
                    }
                    else
                    {
                        w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualArray<" + elFqn + ", " +
                               cmpName + ">((Array?)" + leftExpr + ", (Array?)" + rightExpr + ", new " + cmpName + "(" +
                               (elemCustomVar ?? "") + "), context))");
                        w.Line("return false;");
                        w.Close();
                    }
                }
            }

            w.Line();
            return;
        }

        if (TryGetDictionaryInterface(member.Type, out var keyT, out var valT))
        {
            var kFqn = keyT!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var vFqn = valT!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var vKind = GetEffectiveKind(valT, null);

            var valCustomCmpT = GetEffectiveComparerType(valT, deepAttr);
            string? valCustomVar = null;
            if (valCustomCmpT is not null)
            {
                var cmpFqn = valCustomCmpT.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                valCustomVar = "__cmpV_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
                w.Line("var " + valCustomVar + " = (System.Collections.Generic.IEqualityComparer<" + vFqn +
                       ">)System.Activator.CreateInstance(typeof(" + cmpFqn + "))!");
            }

            var lro = "__roMapA_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
            var rro = "__roMapB_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
            w.Line("var " + lro + " = " + leftExpr + " as global::System.Collections.Generic.IDictionary<" +
                   kFqn + ", " + vFqn + ">;");
            w.Line("var " + rro + " = " + rightExpr + " as global::System.Collections.Generic.IDictionary<" +
                   kFqn + ", " + vFqn + ">;");
            w.Open("if (" + lro + " is not null && " + rro + " is not null)");
            w.Open("if (" + lro + ".Count != " + rro + ".Count)");
            w.Line("return false;");
            w.Close();

            w.Open("foreach (var __kv in " + lro + ")");
            w.Open("if (!" + rro + ".TryGetValue(__kv.Key, out var __rv))");
            w.Line("return false;");
            w.Close();

            if (TryGetReadOnlyMemory(valT, out var romVal))
            {
                var elFqn = romVal!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var elKind = GetEffectiveKind(romVal, null);
                var cmpName = EnsureComparerStruct(emittedComparers, comparerDeclarations, romVal, elKind,
                    "M_" + SanitizeIdentifier(owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)) + "_" +
                    member.Name + "_Val", valCustomVar);
                w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualReadOnlyMemory<" + elFqn + ", " +
                       cmpName + ">(__kv.Value, __rv, new " + cmpName + "(" + (valCustomVar ?? "") + "), context))");
                w.Line("return false;");
                w.Close();
            }
            else if (TryGetMemory(valT, out var mVal))
            {
                var elFqn = mVal!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var elKind = GetEffectiveKind(mVal, null);
                var cmpName = EnsureComparerStruct(emittedComparers, comparerDeclarations, mVal, elKind,
                    "M_" + SanitizeIdentifier(owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)) + "_" +
                    member.Name + "_Val", valCustomVar);
                w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualMemory<" + elFqn + ", " + cmpName +
                       ">(__kv.Value, __rv, new " + cmpName + "(" + (valCustomVar ?? "") + "), context))");
                w.Line("return false;");
                w.Close();
            }
            else
            {
                var vExpr = BuildInlineCompareExpr("__kv.Value", "__rv", valT, vKind, "context", valCustomVar);
                w.Open("if (!(" + vExpr + "))");
                w.Line("return false;");
                w.Close();
            }

            w.Close();
            w.Line("return true;");
            w.Close();
            var lrw = "__rwMapA_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
            var rrw = "__rwMapB_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
            w.Line("var " + lrw + " = " + leftExpr + " as global::System.Collections.Generic.IDictionary<" + kFqn +
                   ", " + vFqn + ">;");
            w.Line("var " + rrw + " = " + rightExpr + " as global::System.Collections.Generic.IDictionary<" + kFqn +
                   ", " + vFqn + ">;");
            w.Open("if (" + lrw + " is not null && " + rrw + " is not null)");
            w.Open("if (" + lrw + ".Count != " + rrw + ".Count)");
            w.Line("return false;");
            w.Close();

            w.Open("foreach (var __kv in " + lrw + ")");
            w.Open("if (!" + rrw + ".TryGetValue(__kv.Key, out var __rv))");
            w.Line("return false;");
            w.Close();

            if (TryGetReadOnlyMemory(valT, out var romVal2))
            {
                var elFqn = romVal2!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var elKind = GetEffectiveKind(romVal2, null);
                var cmpName = EnsureComparerStruct(emittedComparers, comparerDeclarations, romVal2, elKind,
                    "M_" + SanitizeIdentifier(owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)) + "_" +
                    member.Name + "_Val", valCustomVar);
                w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualReadOnlyMemory<" + elFqn + ", " +
                       cmpName + ">(__kv.Value, __rv, new " + cmpName + "(" + (valCustomVar ?? "") + "), context))");
                w.Line("return false;");
                w.Close();
            }
            else if (TryGetMemory(valT, out var mVal2))
            {
                var elFqn = mVal2!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var elKind = GetEffectiveKind(mVal2, null);
                var cmpName = EnsureComparerStruct(emittedComparers, comparerDeclarations, mVal2, elKind,
                    "M_" + SanitizeIdentifier(owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)) + "_" +
                    member.Name + "_Val", valCustomVar);
                w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualMemory<" + elFqn + ", " + cmpName +
                       ">(__kv.Value, __rv, new " + cmpName + "(" + (valCustomVar ?? "") + "), context))");
                w.Line("return false;");
                w.Close();
            }
            else
            {
                var vExpr2 = BuildInlineCompareExpr("__kv.Value", "__rv", valT, vKind, "context", valCustomVar);
                w.Open("if (!(" + vExpr2 + "))");
                w.Line("return false;");
                w.Close();
            }

            w.Close();
            w.Line("return true;");
            w.Close();
            var cmpAny = EnsureComparerStruct(emittedComparers, comparerDeclarations, valT, vKind,
                "M_" + SanitizeIdentifier(owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)) + "_" +
                member.Name + "_Val", valCustomVar);
            w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDictionariesAny<" + kFqn + ", " + vFqn +
                   ", " + cmpAny + ">(" + leftExpr + ", " + rightExpr + ", new " + cmpAny + "(" + (valCustomVar ?? "") +
                   "), context))");
            w.Line("return false;");
            w.Close();

            w.Line();
            return;
        }

        if (TryGetEnumerableInterface(member.Type, out var elT))
        {
            var elFqn = elT!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var elKind = GetEffectiveKind(elT, null);
            var unordered = ResolveOrderInsensitive(root, deepAttr, elT, owner);

            var elemCustomCmpT = GetEffectiveComparerType(elT, deepAttr);
            string? elemCustomVar = null;
            if (elemCustomCmpT is not null)
            {
                var cmpFqn = elemCustomCmpT.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                elemCustomVar = "__cmpE_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
                w.Line("var " + elemCustomVar + " = (System.Collections.Generic.IEqualityComparer<" + elFqn +
                       ">)System.Activator.CreateInstance(typeof(" + cmpFqn + "))!;");
            }

            if (unordered &&
                TryGetKeySpec(elT, deepAttr, root, out var keyTypeFqn2, out var keyExprFmt2) &&
                IsUserObjectType(elT))
            {
                var la = "__seqA_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
                var lb = "__seqB_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
                var da = "__dictA_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
                var db = "__dictB_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
                var tmpA = "__eA_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
                var tmpB = "__eB_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
                var cmpName = EnsureComparerStruct(
                    emittedComparers, comparerDeclarations, elT, elKind,
                    "M_" + SanitizeIdentifier(owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)) + "_" +
                    member.Name,
                    elemCustomVar);

                w.Line("var " + la + " = " + leftExpr + " as System.Collections.Generic.IEnumerable<" + elFqn + ">;");
                w.Line("var " + lb + " = " + rightExpr + " as System.Collections.Generic.IEnumerable<" + elFqn + ">;");

                w.Open("if (!object.ReferenceEquals(" + la + ", " + lb + "))");
                w.Open("if (" + la + " is null || " + lb + " is null)");
                w.Line("return false;");
                w.Close();

                w.Line("var " + da + " = new System.Collections.Generic.Dictionary<" + keyTypeFqn2 +
                       ", System.Collections.Generic.List<" + elFqn + ">>();");
                w.Line("var " + db + " = new System.Collections.Generic.Dictionary<" + keyTypeFqn2 +
                       ", System.Collections.Generic.List<" + elFqn + ">>();");

                w.Open("foreach (var " + tmpA + " in " + la + ")");
                w.Line("var __k = " + string.Format(keyExprFmt2, tmpA) + ";");
                w.Open("if (!" + da + ".TryGetValue(__k, out var __lst))");
                w.Line("__lst = " + da + "[__k] = new System.Collections.Generic.List<" + elFqn + ">();");
                w.Close();
                w.Line("__lst.Add(" + tmpA + ");");
                w.Close();
                w.Open("foreach (var " + tmpB + " in " + lb + ")");
                w.Line("var __k = " + string.Format(keyExprFmt2, tmpB) + ";");
                w.Open("if (!" + db + ".TryGetValue(__k, out var __lst))");
                w.Line("__lst = " + db + "[__k] = new System.Collections.Generic.List<" + elFqn + ">();");
                w.Close();
                w.Line("__lst.Add(" + tmpB + ");");
                w.Close();
                w.Line("if (" + da + ".Count != " + db + ".Count) return false;");
                w.Open("foreach (var __kv in " + da + ")");
                w.Open("if (!" + db + ".TryGetValue(__kv.Key, out var __lstB))");
                w.Line("return false;");
                w.Close();
                w.Line("if (__kv.Value.Count != __lstB.Count) return false;");
                w.Line("var __m = new bool[__lstB.Count];");
                w.Line("var __cmp = new " + cmpName + "(" + (elemCustomVar ?? "") + ");");

                w.Open("for (int __x = 0; __x < __kv.Value.Count; __x++)");
                w.Line("bool __f = false;");
                w.Open("for (int __y = 0; __y < __lstB.Count; __y++)");
                w.Open("if (!__m[__y])");
                w.Open("if (__cmp.Invoke(__kv.Value[__x], __lstB[__y], context))");
                w.Line("__m[__y] = (__f = true);");
                w.Close();
                w.Close();
                w.Close();
                w.Open("if (!__f)");
                w.Line("return false;");
                w.Close();
                w.Close();
                w.Close();
                w.Close();
                w.Line();
                return;
            }

            if (unordered && IsHashFriendly(elT))
            {
                var la = "__seqA_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
                var lb = "__seqB_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
                var eqExpr = GetEqualityComparerExprForHash(elT, "context", elemCustomVar);

                w.Line("var " + la + " = " + leftExpr + " as System.Collections.Generic.IEnumerable<" + elFqn + ">;");
                w.Line("var " + lb + " = " + rightExpr + " as System.Collections.Generic.IEnumerable<" + elFqn + ">;");

                w.Open("if (!object.ReferenceEquals(" + la + ", " + lb + "))");
                w.Open("if (" + la + " is null || " + lb + " is null)");
                w.Line("return false;");
                w.Close();

                w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualSequencesUnorderedHash<" + elFqn +
                       ">(" + la + ", " + lb + ", " + eqExpr + "))");
                w.Line("return false;");
                w.Close();

                w.Close();
            }
            else
            {
                var cmpName = EnsureComparerStruct(emittedComparers, comparerDeclarations, elT, elKind,
                    "M_" + SanitizeIdentifier(owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)) + "_" +
                    member.Name, elemCustomVar);
                var api = unordered ? "AreEqualSequencesUnordered" : "AreEqualSequencesOrdered";
                w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers." + api + "<" + elFqn + ", " + cmpName +
                       ">(" + leftExpr + " as IEnumerable<" + elFqn + ">, " + rightExpr + " as IEnumerable<" + elFqn +
                       ">, new " + cmpName + "(" + (elemCustomVar ?? "") + "), context))");
                w.Line("return false;");
                w.Close();
            }

            w.Line();
            return;
        }

        if ((member.Type.TypeKind == TypeKind.Interface || member.Type is INamedTypeSymbol { IsAbstract: true })
            && !(TryGetDictionaryInterface(member.Type, out _, out _) || TryGetEnumerableInterface(member.Type, out _)))
        {
            var declFqn = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.DeepComparePolymorphic<" + declFqn + ">(" +
                   leftExpr + ", " + rightExpr + ", context))");
            w.Line("return false;");
            w.Close();
            w.Line();
            return;
        }


        if (member.Type.SpecialType == SpecialType.System_Object)
        {
            w.Open("if (!DeepEqual.Generator.Shared.DynamicDeepComparer.AreEqualDynamic(" + leftExpr + ", " +
                   rightExpr + ", context))");
            w.Line("return false;");
            w.Close();
            w.Line();
            return;
        }

        if (member.Type is INamedTypeSymbol nts && IsUserObjectType(nts))
        {
            var helperExpr = "DeepEqual.Generator.Shared.ComparisonHelpers.DeepComparePolymorphic<" +
                             nts.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ">(" + leftExpr + ", " +
                             rightExpr + ", context)";
            w.Open("if (!(" + helperExpr + "))");
            w.Line("return false;");
            w.Close();
            w.Line();
            return;
        }

        w.Open("if (!object.Equals(" + leftExpr + ", " + rightExpr + "))");
        w.Line("return false;");
        w.Close();
        w.Line();
    }

    private static void EmitNullableValueCompare_NoCustom(CodeWriter w, string leftExpr, string rightExpr, ITypeSymbol valueType)
    {
        if (TryEmitWellKnownStructCompare(w, leftExpr + ".Value", rightExpr + ".Value", valueType))
        {
            return;
        }

        if (valueType.TypeKind == TypeKind.Enum)
        {
            var enumFqn = valueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualEnum<" + enumFqn + ">(" + leftExpr +
                   ".Value, " + rightExpr + ".Value))");
            w.Line("return false;");
            w.Close();
            return;
        }

        if (valueType.SpecialType == SpecialType.System_String)
        {
            w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualStrings(" + leftExpr + ".Value, " +
                   rightExpr + ".Value, context))");
            w.Line("return false;");
            w.Close();
            return;
        }

        if (IsNumericWithTolerance(valueType))
        {
            var call = GetNumericCall(valueType, leftExpr + ".Value", rightExpr + ".Value", "context");
            w.Open("if (!" + call + ")");
            w.Line("return false;");
            w.Close();
            return;
        }

        if (valueType.IsValueType && valueType.SpecialType != SpecialType.None)
        {
            w.Open("if (!" + leftExpr + ".Value.Equals(" + rightExpr + ".Value))");
            w.Line("return false;");
            w.Close();
            return;
        }

        if (valueType is INamedTypeSymbol namedTypeSymbol && IsUserObjectType(namedTypeSymbol))
        {
            var helperExpr = "DeepEqual.Generator.Shared.ComparisonHelpers.DeepComparePolymorphic<" +
                             namedTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ">(" + leftExpr +
                             ".Value, " + rightExpr + ".Value, context)";
            w.Open("if (!(" + helperExpr + "))");
            w.Line("return false;");
            w.Close();
            return;
        }

        w.Open("if (!object.Equals(" + leftExpr + ".Value, " + rightExpr + ".Value))");
        w.Line("return false;");
        w.Close();
    }

    private static bool TryEmitWellKnownStructCompare(CodeWriter w, string leftExpr, string rightExpr, ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_DateTime)
        {
            w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDateTime(" + leftExpr + ", " + rightExpr +
                   "))");
            w.Line("return false;");
            w.Close();
            return true;
        }

        var fqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (fqn == "global::System.DateTimeOffset")
        {
            w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDateTimeOffset(" + leftExpr + ", " +
                   rightExpr + "))");
            w.Line("return false;");
            w.Close();
            return true;
        }

        if (fqn == "global::System.TimeSpan")
        {
            w.Open("if (" + leftExpr + ".Ticks != " + rightExpr + ".Ticks)");
            w.Line("return false;");
            w.Close();
            return true;
        }

        if (fqn == "global::System.Guid")
        {
            w.Open("if (!" + leftExpr + ".Equals(" + rightExpr + "))");
            w.Line("return false;");
            w.Close();
            return true;
        }

        if (fqn == "global::System.DateOnly")
        {
            w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDateOnly(" + leftExpr + ", " + rightExpr +
                   "))");
            w.Line("return false;");
            w.Close();
            return true;
        }

        if (fqn == "global::System.TimeOnly")
        {
            w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualTimeOnly(" + leftExpr + ", " + rightExpr +
                   "))");
            w.Line("return false;");
            w.Close();
            return true;
        }

        return false;
    }

    private static string EnsureComparerStruct(HashSet<string> emitted, List<string[]> declarations, ITypeSymbol elementType,
        EffectiveKind elementKind, string hint, string? customComparerVar = null)
    {
        var elFqn = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var cmpName = "__Cmp__" + SanitizeIdentifier(elFqn) + "__" + hint;
        if (!emitted.Add(cmpName))
        {
            return cmpName;
        }

        if (customComparerVar is not null)
        {
            var linesX = new List<string>
            {
                "private readonly struct " + cmpName + " : DeepEqual.Generator.Shared.IElementComparer<" + elFqn + ">",
                "{",
                "    private readonly System.Collections.Generic.IEqualityComparer<" + elFqn + "> __c;",
                "    public " + cmpName + "(System.Collections.Generic.IEqualityComparer<" + elFqn +
                "> c) { __c = c; }",
                "    public bool Invoke(" + elFqn + " l, " + elFqn +
                " r, DeepEqual.Generator.Shared.ComparisonContext c) { return __c.Equals(l, r); }",
                "}",
                ""
            };
            declarations.Add(linesX.ToArray());
            return cmpName;
        }

        var expr = BuildInlineCompareExpr("l", "r", elementType, elementKind);
        var lines = new List<string>
        {
            "private readonly struct " + cmpName + " : DeepEqual.Generator.Shared.IElementComparer<" + elFqn + ">",
            "{",
            "    public " + cmpName + "(" + "System.Collections.Generic.IEqualityComparer<" + elFqn + "> _ = null) {}",
            "    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]",
            "    public bool Invoke(" + elFqn + " l, " + elFqn + " r, DeepEqual.Generator.Shared.ComparisonContext c)",
            "    {",
            "        return " + expr + ";",
            "    }",
            "}",
            ""
        };
        declarations.Add(lines.ToArray());
        return cmpName;
    }

    private static string BuildInlineCompareExpr(string l, string r, ITypeSymbol type, EffectiveKind kind,
        string ctxVar = "c", string? customEqVar = null)
    {
        if (customEqVar is not null)
        {
            return customEqVar + ".Equals(" + l + ", " + r + ")";
        }

        if (kind == EffectiveKind.Reference)
        {
            return "object.ReferenceEquals(" + l + ", " + r + ")";
        }

        if (kind == EffectiveKind.Shallow)
        {
            return "object.Equals(" + l + ", " + r + ")";
        }

        if (type is INamedTypeSymbol nt && nt.OriginalDefinition.ToDisplayString() == "System.Nullable<T>")
        {
            var tArg = nt.TypeArguments[0];
            var inner = BuildInlineCompareExpr(l + ".Value", r + ".Value", tArg, GetEffectiveKind(tArg, null), ctxVar, customEqVar);
            return "(" + l + ".HasValue == " + r + ".HasValue) && (!" + l + ".HasValue || (" + inner + "))";
        }

        if (type.SpecialType == SpecialType.System_String)
        {
            return "DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualStrings(" + l + ", " + r + ", " + ctxVar + ")";
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            var enumFqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return "DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualEnum<" + enumFqn + ">(" + l + ", " + r + ")";
        }

        if (type.SpecialType == SpecialType.System_DateTime)
        {
            return "DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDateTime(" + l + ", " + r + ")";
        }

        var fqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (fqn == "global::System.DateTimeOffset")
        {
            return "DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDateTimeOffset(" + l + ", " + r + ")";
        }

        if (fqn == "global::System.TimeSpan")
        {
            return l + ".Ticks == " + r + ".Ticks";
        }

        if (fqn == "global::System.Guid")
        {
            return l + ".Equals(" + r + ")";
        }

        if (fqn == "global::System.DateOnly")
        {
            return "DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDateOnly(" + l + ", " + r + ")";
        }

        if (fqn == "global::System.TimeOnly")
        {
            return "DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualTimeOnly(" + l + ", " + r + ")";
        }


        if (type.SpecialType == SpecialType.System_Double)
        {
            return "DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDouble(" + l + ", " + r + ", " + ctxVar + ")";
        }

        if (type.SpecialType == SpecialType.System_Single)
        {
            return "DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualSingle(" + l + ", " + r + ", " + ctxVar + ")";
        }

        if (type.SpecialType == SpecialType.System_Decimal)
        {
            return "DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDecimal(" + l + ", " + r + ", " + ctxVar + ")";
        }

        if (type.SpecialType == SpecialType.System_Object)
        {
            return "DeepEqual.Generator.Shared.DynamicDeepComparer.AreEqualDynamic(" + l + ", " + r + ", " + ctxVar +
                   ")";
        }

        if (type.IsValueType && type.SpecialType != SpecialType.None)
        {
            return l + ".Equals(" + r + ")";
        }

        if (type.TypeKind == TypeKind.Interface || type is INamedTypeSymbol { IsAbstract: true })
        {
            var ts = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return "DeepEqual.Generator.Shared.ComparisonHelpers.DeepComparePolymorphic<" + ts + ">(" + l + ", " + r +
                   ", " + ctxVar + ")";
        }

        if (type is INamedTypeSymbol nts && IsUserObjectType(nts))
        {
            var ts = nts.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return "DeepEqual.Generator.Shared.ComparisonHelpers.DeepComparePolymorphic<" + ts + ">(" + l + ", " + r +
                   ", " + ctxVar + ")";
        }

        return "object.Equals(" + l + ", " + r + ")";
    }

    private static bool TryGetReadOnlyMemory(ITypeSymbol type, out ITypeSymbol? elementType)
    {
        if (Cache.MemoryElement.TryGetValue(type, out var cached) && cached is not null)
        {
            elementType = cached;
            return true;
        }

        elementType = null;
        if (type is INamedTypeSymbol named && named.OriginalDefinition.ToDisplayString() == "System.ReadOnlyMemory<T>")
        {
            elementType = named.TypeArguments[0];
            Cache.MemoryElement[type] = elementType;
            return true;
        }

        Cache.MemoryElement[type] = null;
        return false;
    }

    private static bool TryGetMemory(ITypeSymbol type, out ITypeSymbol? elementType)
    {
        if (Cache.MemoryElement.TryGetValue(type, out var cached) && cached is not null)
        {
            elementType = cached;
            return true;
        }

        elementType = null;
        if (type is INamedTypeSymbol named && named.OriginalDefinition.ToDisplayString() == "System.Memory<T>")
        {
            elementType = named.TypeArguments[0];
            Cache.MemoryElement[type] = elementType;
            return true;
        }

        Cache.MemoryElement[type] = null;
        return false;
    }

    private static bool IsHashFriendly(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String)
        {
            return true;
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        if (type.SpecialType is SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal)
        {
            return false;
        }

        if (type.IsValueType && type.SpecialType != SpecialType.None)
        {
            return true;
        }

        var fqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (fqn is "global::System.DateTime" or "global::System.DateTimeOffset")
        {
            return true;
        }

        return false;
    }

    private static string GetNumericCall(ITypeSymbol type, string l, string r, string ctxVar)
    {
        return type.SpecialType switch
        {
            SpecialType.System_Single => "DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualSingle(" + l + ", " + r +
                                         ", " + ctxVar + ")",
            SpecialType.System_Double => "DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDouble(" + l + ", " + r +
                                         ", " + ctxVar + ")",
            SpecialType.System_Decimal => "DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDecimal(" + l + ", " +
                                          r + ", " + ctxVar + ")",
            _ => l + ".Equals(" + r + ")"
        };
    }

    private static bool IsNumericWithTolerance(ITypeSymbol type)
    {
        return type.SpecialType is SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal;
    }

    private static TypeSchema GetTypeSchema(INamedTypeSymbol type)
    {
        if (Cache.SchemaCache.TryGetValue(type, out var cached))
        {
            return cached;
        }

        var attr = type.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepCompareAttributeName);
        if (attr is null)
        {
            var empty = new TypeSchema(Array.Empty<string>(), Array.Empty<string>());
            Cache.SchemaCache[type] = empty;
            return empty;
        }

        static string[] ReadStringArray(TypedConstant arg)
        {
            if (arg is { Kind: TypedConstantKind.Array, IsNull: false })
            {
                return arg.Values.Select(v => v.Value?.ToString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            }

            return [];
        }

        var include = Array.Empty<string>();
        var ignore = Array.Empty<string>();

        foreach (var kv in attr.NamedArguments)
            if (kv.Key == "Members")
            {
                include = ReadStringArray(kv.Value);
            }
            else if (kv.Key == "IgnoreMembers")
            {
                ignore = ReadStringArray(kv.Value);
            }

        var schema = new TypeSchema(include, ignore);
        Cache.SchemaCache[type] = schema;
        return schema;
    }

    private static IEnumerable<MemberSymbol> EnumerateMembers(INamedTypeSymbol type, bool allowInternals,
        bool includeBase, TypeSchema schema)
    {
        if (schema.IncludeMembers.Count == 0 && schema.IgnoreMembers.Count == 0)
        {
            var key = (type, allowInternals, includeBase);
            if (!Cache.MemberCache.TryGetValue(key, out var cached))
            {
                cached = EnumerateMembersUncached(type, allowInternals, includeBase, schema).ToArray();
                Cache.MemberCache[key] = cached;
            }

            return cached;
        }

        return EnumerateMembersUncached(type, allowInternals, includeBase, schema);
    }

    private static IEnumerable<MemberSymbol> EnumerateMembersUncached(INamedTypeSymbol ownerType, bool includeInternals,
        bool includeBase, TypeSchema schema)
    {
        static bool IsAccessible(ISymbol s, bool inclInternals, INamedTypeSymbol owner)
        {
            return s.DeclaredAccessibility switch
            {
                Accessibility.Public => true,
                Accessibility.Internal or Accessibility.ProtectedAndInternal => inclInternals &&
                    SymbolEqualityComparer.Default.Equals(s.ContainingAssembly, owner.ContainingAssembly),
                _ => false
            };
        }

        var hasInclude = schema.IncludeMembers.Count > 0;
        var includeSet = hasInclude ? new HashSet<string>(schema.IncludeMembers, StringComparer.Ordinal) : null;
        var ignoreSet = schema.IgnoreMembers.Count > 0
            ? new HashSet<string>(schema.IgnoreMembers, StringComparer.Ordinal)
            : null;

        var yielded = new HashSet<string>(StringComparer.Ordinal);
        for (var t = ownerType;
             t is not null && t.SpecialType != SpecialType.System_Object;
             t = includeBase ? t.BaseType : null)
        {
            foreach (var p in t.GetMembers().OfType<IPropertySymbol>())
            {
                if (p.IsStatic)
                {
                    continue;
                }

                if (p.Parameters.Length != 0)
                {
                    continue;
                }

                if (p.GetMethod is null)
                {
                    continue;
                }

                if (!IsAccessible(p, includeInternals, ownerType))
                {
                    continue;
                }

                if (yielded.Contains(p.Name))
                {
                    continue;
                }

                if (hasInclude && !includeSet!.Contains(p.Name))
                {
                    continue;
                }

                if (ignoreSet is not null && ignoreSet.Contains(p.Name))
                {
                    continue;
                }

                if (ownerType.IsValueType && SymbolEqualityComparer.Default.Equals(p.Type, ownerType) &&
                    !hasInclude)
                {
                    continue;
                }

                yielded.Add(p.Name);
                yield return new MemberSymbol(p.Name, p.Type, p);
            }

            foreach (var f in t.GetMembers().OfType<IFieldSymbol>())
            {
                if (f.IsStatic || f.IsConst || f.IsImplicitlyDeclared)
                {
                    continue;
                }

                if (f.AssociatedSymbol is not null)
                {
                    continue;
                }

                if (f.Name.StartsWith("<", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!IsAccessible(f, includeInternals, ownerType))
                {
                    continue;
                }

                if (yielded.Contains(f.Name))
                {
                    continue;
                }

                if (hasInclude && !includeSet!.Contains(f.Name))
                {
                    continue;
                }

                if (ignoreSet is not null && ignoreSet.Contains(f.Name))
                {
                    continue;
                }

                if (ownerType.IsValueType && SymbolEqualityComparer.Default.Equals(f.Type, ownerType) &&
                    !hasInclude)
                {
                    continue;
                }

                yielded.Add(f.Name);
                yield return new MemberSymbol(f.Name, f.Type, f);
            }

            if (!includeBase)
            {
                break;
            }
        }
    }

    private static IEnumerable<MemberSymbol> OrderMembers(IEnumerable<MemberSymbol> members)
    {
        return members.Select(m => (m, key: MemberCost(m))).OrderBy(t => t.key)
            .ThenBy(t => t.m.Name, StringComparer.Ordinal).Select(t => t.m);
    }

    private static int MemberCost(MemberSymbol member)
    {
        var attr = GetDeepCompareAttribute(member.Symbol);
        var kind = GetEffectiveKind(member.Type, attr);
        if (kind == EffectiveKind.Skip)
        {
            return 99;
        }

        if (kind is EffectiveKind.Reference or EffectiveKind.Shallow)
        {
            return 0;
        }

        var t = member.Type;
        if (t is INamedTypeSymbol nnt && nnt.OriginalDefinition.ToDisplayString() == "System.Nullable<T>")
        {
            var inner = nnt.TypeArguments[0];
            if (inner.TypeKind == TypeKind.Enum)
            {
                return 2;
            }

            if (IsWellKnownStruct(inner) || (inner.IsValueType && inner.SpecialType != SpecialType.None))
            {
                return 3;
            }

            t = inner;
        }

        if (t.SpecialType == SpecialType.System_String)
        {
            return 1;
        }

        if (t.TypeKind == TypeKind.Enum)
        {
            return 2;
        }

        if (IsWellKnownStruct(t) || (t.IsValueType && t.SpecialType != SpecialType.None))
        {
            return 3;
        }

        if (t.SpecialType == SpecialType.System_Object)
        {
            return 6;
        }

        if (t is IArrayTypeSymbol)
        {
            return 9;
        }

        if (TryGetDictionaryInterface(t, out _, out _))
        {
            return 8;
        }

        if (TryGetEnumerableInterface(t, out _))
        {
            return 9;
        }

        if (TryGetReadOnlyMemory(t, out _))
        {
            return 3;
        }

        if (TryGetMemory(t, out _))
        {
            return 3;
        }

        if (t is INamedTypeSymbol nts && IsUserObjectType(nts))
        {
            return 7;
        }

        return 7;
    }

    private static bool IsWellKnownStruct(ITypeSymbol t)
    {
        if (t.SpecialType == SpecialType.System_DateTime)
        {
            return true;
        }

        var fqn = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return fqn is "global::System.DateTimeOffset" or "global::System.TimeSpan" or "global::System.Guid" or "global::System.DateOnly" or "global::System.TimeOnly";
    }

    private static AttributeData? GetDeepCompareAttribute(ISymbol symbol)
    {
        return symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepCompareAttributeName);
    }

    private static EffectiveKind GetEffectiveKind(ITypeSymbol type, AttributeData? memberAttribute)
    {
        if (memberAttribute is not null)
        {
            var val = memberAttribute.NamedArguments.FirstOrDefault(p => p.Key == "Kind").Value.Value;
            if (val is int mk)
            {
                return (EffectiveKind)mk;
            }
        }

        var typeAttr = type.OriginalDefinition.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepCompareAttributeName);
        if (typeAttr is not null)
        {
            var val = typeAttr.NamedArguments.FirstOrDefault(p => p.Key == "Kind").Value.Value;
            if (val is int tk)
            {
                return (EffectiveKind)tk;
            }
        }

        return EffectiveKind.Deep;
    }

    private static INamedTypeSymbol? GetEffectiveComparerType(ITypeSymbol comparedType, AttributeData? memberAttribute)
    {
        INamedTypeSymbol? fromMember = null;
        if (memberAttribute is not null)
        {
            foreach (var kv in memberAttribute.NamedArguments)
                if (kv is { Key: "ComparerType", Value.Value: INamedTypeSymbol ts } &&
                    ImplementsIEqualityComparerFor(ts, comparedType))
                {
                    fromMember = ts;
                    break;
                }
        }

        if (fromMember is not null)
        {
            return fromMember;
        }

        var typeAttr = comparedType.OriginalDefinition.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepCompareAttributeName);
        if (typeAttr is not null)
        {
            foreach (var kv in typeAttr.NamedArguments)
                if (kv is { Key: "ComparerType", Value.Value: INamedTypeSymbol ts2 } &&
                    ImplementsIEqualityComparerFor(ts2, comparedType))
                {
                    return ts2;
                }
        }

        return null;
    }

    private static bool ImplementsIEqualityComparerFor(INamedTypeSymbol comparerType, ITypeSymbol argument)
    {
        foreach (var i in comparerType.AllInterfaces)
            if (i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEqualityComparer<T>")
            {
                if (SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], argument))
                {
                    return true;
                }
            }

        return false;
    }

    private static bool TryGetKeySpec(ITypeSymbol elementType, AttributeData? memberAttribute, Target root,
         out string keyTypeFqn, out string keyExprFormat)
    {
        var keys = new List<MemberSymbol>();
        keyTypeFqn = "";
        keyExprFormat = "{0}";
        var names = Array.Empty<string>();

        if (memberAttribute is not null)
        {
            foreach (var kv in memberAttribute.NamedArguments)
                if (kv is { Key: "KeyMembers", Value.Values: { Length: > 0 } arr })
                {
                    names = arr.Select(v => v.Value?.ToString() ?? "").Where(s => s.Length > 0).ToArray();
                    break;
                }
        }

        if (names.Length == 0)
        {
            var typeAttr = elementType.OriginalDefinition.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepCompareAttributeName);
            if (typeAttr is not null)
            {
                foreach (var kv in typeAttr.NamedArguments)
                    if (kv is { Key: "KeyMembers", Value.Values: { Length: > 0 } arr2 })
                    {
                        names = arr2.Select(v => v.Value?.ToString() ?? "").Where(s => s.Length > 0).ToArray();
                        break;
                    }
            }
        }

        if (names.Length == 0)
        {
            return false;
        }

        foreach (var n in names)
        {
            var m = FindMemberOn(elementType, n, root.IncludeInternals, root.IncludeBaseMembers);
            if (m is not null)
            {
                keys.Add(m.Value);
            }
        }

        if (keys.Count == 0)
        {
            return false;
        }

        if (keys.Count == 1)
        {
            keyTypeFqn = keys[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            keyExprFormat = "{0}." + keys[0].Name;
            return true;
        }

        if (keys.Count > 7)
        {
            return false;
        }

        keyTypeFqn = "global::System.ValueTuple<" + string.Join(",",
            keys.Select(k => k.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))) + ">";
        keyExprFormat = "(" + string.Join(",", keys.Select(k => "{0}." + k.Name)) + ")";
        return true;
    }

    private static MemberSymbol? FindMemberOn(ITypeSymbol type, string name, bool includeInternals, bool includeBase)
    {
        for (var t = type;
             t is not null && t.SpecialType != SpecialType.System_Object;
             t = includeBase ? (t as INamedTypeSymbol)?.BaseType : null)
        {
            foreach (var p in t.GetMembers().OfType<IPropertySymbol>())
            {
                if (p.Name != name)
                {
                    continue;
                }

                if (p.IsStatic || p.Parameters.Length != 0 || p.GetMethod is null)
                {
                    continue;
                }

                if (!IsAccessibleForMember(p, includeInternals, type))
                {
                    continue;
                }

                return new MemberSymbol(p.Name, p.Type, p);
            }

            foreach (var f in t.GetMembers().OfType<IFieldSymbol>())
            {
                if (f.Name != name)
                {
                    continue;
                }

                if (f.IsStatic || f.IsConst || f.IsImplicitlyDeclared)
                {
                    continue;
                }

                if (!IsAccessibleForMember(f, includeInternals, type))
                {
                    continue;
                }

                return new MemberSymbol(f.Name, f.Type, f);
            }

            if (!includeBase)
            {
                break;
            }
        }

        return null;
    }

    private static bool IsAccessibleForMember(ISymbol s, bool inclInternals, ITypeSymbol owner)
    {
        return s.DeclaredAccessibility switch
        {
            Accessibility.Public => true,
            Accessibility.Internal or Accessibility.ProtectedAndInternal => inclInternals &&
                                                                            SymbolEqualityComparer.Default.Equals(
                                                                                s.ContainingAssembly,
                                                                                owner.ContainingAssembly),
            _ => false
        };
    }

    private static string GetEqualityComparerExprForHash(ITypeSymbol elType, string ctxVar, string? customVar)
    {
        if (customVar is not null)
        {
            return customVar;
        }

        if (elType.SpecialType == SpecialType.System_String)
        {
            return "DeepEqual.Generator.Shared.ComparisonHelpers.GetStringComparer(" + ctxVar + ")";
        }

        var fqn = elType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (fqn == "global::System.DateTime")
        {
            return "DeepEqual.Generator.Shared.ComparisonHelpers.StrictDateTimeComparer.Instance";
        }

        if (fqn == "global::System.DateTimeOffset")
        {
            return "DeepEqual.Generator.Shared.ComparisonHelpers.StrictDateTimeOffsetComparer.Instance";
        }

        return "System.Collections.Generic.EqualityComparer<" +
               elType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ">.Default";
    }

    private static bool ResolveOrderInsensitive(Target root, AttributeData? memberAttribute, ITypeSymbol elementType,
        INamedTypeSymbol? containingType)
    {
        if (memberAttribute is not null)
        {
            var opt = memberAttribute.NamedArguments.FirstOrDefault(a => a.Key == "OrderInsensitive").Value;
            if (opt.Value is bool b)
            {
                return b;
            }
        }

        var typeAttr = elementType.OriginalDefinition.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepComparableAttributeName);
        if (typeAttr is not null)
        {
            var opt = typeAttr.NamedArguments.FirstOrDefault(a => a.Key == "OrderInsensitiveCollections").Value;
            if (opt.Value is bool b)
            {
                return b;
            }
        }

        if (containingType is not null)
        {
            var containerAttr = containingType.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepComparableAttributeName);
            if (containerAttr is not null)
            {
                var opt = containerAttr.NamedArguments.FirstOrDefault(a => a.Key == "OrderInsensitiveCollections")
                    .Value;
                if (opt.Value is bool b)
                {
                    return b;
                }
            }
        }

        return root.OrderInsensitiveCollections;
    }

    private static bool TryGetEnumerableInterface(ITypeSymbol type, out ITypeSymbol? elementType)
    {
        if (Cache.EnumerableElement.TryGetValue(type, out var cached))
        {
            elementType = cached;
            return cached is not null;
        }

        if (type is INamedTypeSymbol named &&
            named.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>")
        {
            elementType = named.TypeArguments[0];
            Cache.EnumerableElement[type] = elementType;
            return true;
        }

        foreach (var i in type.AllInterfaces)
            if (i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>")
            {
                elementType = i.TypeArguments[0];
                Cache.EnumerableElement[type] = elementType;
                return true;
            }

        elementType = null;
        Cache.EnumerableElement[type] = null;
        return false;
    }

    private static bool TryGetDictionaryInterface(ITypeSymbol type, out ITypeSymbol? keyType,
        out ITypeSymbol? valueType)
    {
        if (Cache.DictionaryKv.TryGetValue(type, out var cached))
        {
            if (cached is not null)
            {
                keyType = cached.Value.Key;
                valueType = cached.Value.Val;
                return true;
            }

            keyType = null;
            valueType = null;
            return false;
        }

        if (type is INamedTypeSymbol named)
        {
            var defSelf = named.OriginalDefinition.ToDisplayString();
            if (defSelf is "System.Collections.Generic.IDictionary<TKey, TValue>" or "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>")
            {
                keyType = named.TypeArguments[0];
                valueType = named.TypeArguments[1];
                Cache.DictionaryKv[type] = (keyType, valueType);
                return true;
            }
        }

        foreach (var i in type.AllInterfaces)
        {
            var def = i.OriginalDefinition.ToDisplayString();
            if (def is "System.Collections.Generic.IDictionary<TKey, TValue>" or "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>")
            {
                keyType = i.TypeArguments[0];
                valueType = i.TypeArguments[1];
                Cache.DictionaryKv[type] = (keyType, valueType);
                return true;
            }
        }

        keyType = null;
        valueType = null;
        Cache.DictionaryKv[type] = null;
        return false;
    }

    private static bool IsUserObjectType(ITypeSymbol type)
    {
        if (Cache.UserObject.TryGetValue(type, out var cached))
        {
            return cached;
        }

        if (type is not INamedTypeSymbol n)
        {
            Cache.UserObject[type] = false;
            return false;
        }

        if (n.SpecialType != SpecialType.None)
        {
            Cache.UserObject[type] = false;
            return false;
        }

        var ns = n.ContainingNamespace?.ToDisplayString() ?? "";
        if (ns.StartsWith("System", StringComparison.Ordinal))
        {
            Cache.UserObject[type] = false;
            return false;
        }

        var asm = n.ContainingAssembly?.Name ?? "";
        if (asm is "mscorlib" or "System.Private.CoreLib" or "System.Runtime")
        {
            Cache.UserObject[type] = false;
            return false;
        }

        var ok = n.TypeKind is TypeKind.Class or TypeKind.Struct;
        Cache.UserObject[type] = ok;
        return ok;
    }

    private static bool IsTypeAccessibleFromRoot(INamedTypeSymbol t, Target root)
    {
        if (t.DeclaredAccessibility == Accessibility.Public)
        {
            var cur = t.ContainingType;
            while (cur is not null)
            {
                if (cur.DeclaredAccessibility != Accessibility.Public)
                {
                    return false;
                }

                cur = cur.ContainingType;
            }

            return true;
        }

        if (!root.IncludeInternals)
        {
            return false;
        }

        if (!SymbolEqualityComparer.Default.Equals(t.ContainingAssembly, root.Type.ContainingAssembly))
        {
            return false;
        }

        var c = t;
        while (c is not null)
        {
            if (c.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal
                or Accessibility.ProtectedAndInternal)
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

            foreach (var member in EnumerateMembers(current, root.IncludeInternals, root.IncludeBaseMembers, schema))
            {
                var kind = GetEffectiveKind(member.Type, GetDeepCompareAttribute(member.Symbol));
                if (kind is EffectiveKind.Skip or EffectiveKind.Shallow or EffectiveKind.Reference)
                {
                    continue;
                }

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

            if (t is IArrayTypeSymbol at)
            {
                Accumulate(at.ElementType);
                return;
            }

            if (TryGetDictionaryInterface(t, out _, out var valT))
            {
                Accumulate(valT!);
                return;
            }

            if (TryGetEnumerableInterface(t, out var elT))
            {
                Accumulate(elT!);
                return;
            }

            if (TryGetReadOnlyMemory(t, out var rmT))
            {
                Accumulate(rmT!);
                return;
            }

            if (TryGetMemory(t, out var mT))
            {
                Accumulate(mT!);
                return;
            }

            if (t is INamedTypeSymbol n && IsUserObjectType(n) && IsTypeAccessibleFromRoot(n, root) && set.Add(n))
            {
                queue.Enqueue(n);
            }
        }
    }

    private static string GetHelperMethodName(INamedTypeSymbol type)
    {
        return "AreDeepEqual__" + SanitizeIdentifier(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
    }

    private static string SanitizeIdentifier(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s) sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        return sb.ToString();
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var arr = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(arr);
    }

    private sealed class CodeWriter
    {
        private readonly StringBuilder _buffer = new();
        private int _indent;

        public void Line(string text = "")
        {
            if (text.Length == 0)
            {
                _buffer.AppendLine();
                return;
            }

            _buffer.Append(' ', _indent * 4);
            _buffer.AppendLine(text);
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

        public override string ToString()
        {
            return _buffer.ToString();
        }
    }
}