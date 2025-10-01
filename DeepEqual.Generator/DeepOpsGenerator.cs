using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DeepEqual.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class DeepOpsGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var ownedRequests =
            context.SyntaxProvider.ForAttributeWithMetadataName(
                    GenCommon.DeepComparableAttributeMetadataName,
                    static (node, _) => node is TypeDeclarationSyntax,
                    static (gasc, ct) =>
                    {
                        if (gasc.TargetSymbol is not INamedTypeSymbol typeSymbol) return null;

                        var attr = gasc.Attributes.FirstOrDefault(a =>
                            a.AttributeClass?.ToDisplayString() == GenCommon.DeepComparableAttributeMetadataName);
                        if (attr is null) return (RootRequest?)null;

                        static bool HasNamedTrue(AttributeData a, string name)
                        {
                            return a.NamedArguments.Any(kv => kv.Key == name && kv.Value.Value is true);
                        }

                        static bool? GetNamedBool(AttributeData a, string name)
                        {
                            var has = a.NamedArguments.FirstOrDefault(kv => kv.Key == name);
                            return has.Value.Value is bool b ? b : null;
                        }

                        static int GetEnumValue(AttributeData a, string name)
                        {
                            var arg = a.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value;
                            return arg is { Kind: TypedConstantKind.Enum, Value: int i } ? i : 0;
                        }

                        var includeInternals = HasNamedTrue(attr, "IncludeInternals");
                        var orderInsensitive = HasNamedTrue(attr, "OrderInsensitiveCollections");
                        var includeBase = HasNamedTrue(attr, "IncludeBaseMembers");
                        var genDiff = HasNamedTrue(attr, "GenerateDiff");
                        var genDelta = HasNamedTrue(attr, "GenerateDelta");

                        var cycleSpecified = GetNamedBool(attr, "CycleTracking");
                        var eqCycle = cycleSpecified ?? false;
                        var ddCycle = cycleSpecified ?? false;

                        var stableMode = (StableMemberIndexMode)GetEnumValue(attr, "StableMemberIndex");
                        var attrLoc = attr.ApplicationSyntaxReference?.GetSyntax(ct).GetLocation();
                        var emitSnapshot = HasNamedTrue(attr, "EmitSchemaSnapshot");

                        var metadataName = GenCommon.BuildMetadataName(typeSymbol);
                        var fqn = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                        return new RootRequest(
                            metadataName, fqn,
                            includeInternals, orderInsensitive,
                            eqCycle, ddCycle, includeBase,
                            genDiff, genDelta, stableMode, emitSnapshot, attrLoc);
                    })
                .Where(static r => r is not null)
                .Select(static (r, _) => r!.Value);

        var external = context.CompilationProvider.Select((comp, _) =>
        {
            var asm = comp.Assembly;
            var extRoots = new List<(INamedTypeSymbol Root, AttributeData Attr)>();
            var extMember = new List<(INamedTypeSymbol Root, string Path, AttributeData Attr)>();
            foreach (var a in asm.GetAttributes())
            {
                var name = a.AttributeClass?.ToDisplayString();
                if (name == GenCommon.ExternalDeepComparableMetadataName && a.ConstructorArguments.Length == 1)
                {
                    if (a.ConstructorArguments[0].Value is INamedTypeSymbol rootTs) extRoots.Add((rootTs, a));
                }
                else if (name == GenCommon.ExternalDeepCompareMetadataName && a.ConstructorArguments.Length == 2)
                {
                    if (a.ConstructorArguments[0].Value is INamedTypeSymbol rootTs &&
                        a.ConstructorArguments[1].Value is string path)
                        extMember.Add((rootTs, path, a));
                }
            }

            return (comp, extRoots, extMember);
        });

        var inputs = ownedRequests.Collect().Combine(external);
        context.RegisterSourceOutput(inputs, (spc, all) =>
        {
            var (ownedList, (compilation, extRoots, extMembers)) = all;

            foreach (var (rootType, path, attr) in extMembers)
                try
                {
                    var (_, member, _) = ExternalPathResolver.ResolveMemberPath(
                        compilation,
                        rootType,
                        path,
                        false,
                        true,
                        (loc, msg, kind) =>
                        {
                            var diag = kind switch
                            {
                                ExternalPathResolver.PathDiag.DictionarySideInvalid => Diagnostics.EX002,
                                ExternalPathResolver.PathDiag.AmbiguousEnumerable => Diagnostics.EX003,
                                _ => Diagnostics.EX001
                            };
                            spc.ReportDiagnostic(Diagnostic.Create(diag, loc, msg));
                        },
                        attr.ApplicationSyntaxReference?.GetSyntax().GetLocation());
                    _ = member;
                }
                catch
                {
                }

            var roots =
                new Dictionary<INamedTypeSymbol, (bool incInt, bool ordIns, bool eqCycle, bool ddCycle, bool incBase,
                    bool genDiff, bool genDelta, StableMemberIndexMode stableMode, bool emitSnapshot, Location? loc)>(
                    SymbolEqualityComparer.Default);

            foreach (var req in ownedList)
            {
                var t = compilation.GetTypeByMetadataName(req.MetadataName);
                if (t is null) continue;

                if (!roots.ContainsKey(t))
                    roots[t] = (req.IncludeInternals, req.OrderInsensitiveCollections, req.EqCycleTrackingEnabled,
                        req.DdCycleTrackingEnabled, req.IncludeBaseMembers, req.GenerateDiff, req.GenerateDelta,
                        req.StableMemberIndexMode, req.EmitSchemaSnapshot, req.AttributeLocation);
            }

            static bool HasNamedTrue(AttributeData a, string name)
            {
                return a.NamedArguments.Any(kv => kv.Key == name && kv.Value.Value is true);
            }

            static (bool? val, bool present) GetNamedBool(AttributeData a, string name)
            {
                var entry = a.NamedArguments.FirstOrDefault(kv => kv.Key == name);
                return (entry.Value.Value is bool b ? b : null, entry.Value.Value is bool);
            }

            static int GetEnumValue(AttributeData a, string name)
            {
                var arg = a.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value;
                return arg is { Kind: TypedConstantKind.Enum, Value: int i } ? i : 0;
            }

            foreach (var (extType, attr) in extRoots)
            {
                if (roots.ContainsKey(extType)) continue;

                var incInt = HasNamedTrue(attr, "IncludeInternals");
                var ordIns = HasNamedTrue(attr, "OrderInsensitiveCollections");
                var incBase = HasNamedTrue(attr, "IncludeBaseMembers");
                var genDiff = HasNamedTrue(attr, "GenerateDiff");
                var genDelta = HasNamedTrue(attr, "GenerateDelta");
                var (cycleVal, present) = GetNamedBool(attr, "CycleTracking");
                var eqCycle = present && (cycleVal ?? false);
                var ddCycle = present && (cycleVal ?? false);
                var stableMode = (StableMemberIndexMode)GetEnumValue(attr, "StableMemberIndex");
                var emitSnapshot = HasNamedTrue(attr, "EmitSchemaSnapshot");
                var loc = attr.ApplicationSyntaxReference?.GetSyntax().GetLocation();

                roots[extType] = (incInt, ordIns, eqCycle, ddCycle, incBase, genDiff, genDelta, stableMode,
                    emitSnapshot, loc);
            }

            var eqEmitter = new EqualityEmitter();
            var ddEmitter = new DiffDeltaEmitter();
            var seenHints = new HashSet<string>(StringComparer.Ordinal);

            foreach (var kvp in roots)
            {
                var type = kvp.Key;
                var (incInt, ordIns, eqCycle, ddCycle, incBase, genDiff, genDelta, stableMode, emitSnapshot, loc) =
                    kvp.Value;

                Diagnostics.DiagnosticPass(spc, type);

                {
                    var hint = GenCommon.SanitizeFileName(
                        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "_DeepEqual.g.cs");
                    if (seenHints.Add(hint))
                        eqEmitter.EmitForRoot(
                            spc,
                            new EqualityTarget(type, incInt, ordIns, eqCycle, incBase),
                            hint);
                }

                if (genDiff || genDelta)
                {
                    if (genDelta && stableMode == StableMemberIndexMode.Off && loc is not null)
                        spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.DL001, loc));

                    var hint = GenCommon.SanitizeFileName(
                        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "_DeepOps.g.cs");
                    if (seenHints.Add(hint))
                        ddEmitter.EmitForRoot(
                            spc,
                            new DiffDeltaTarget(type, incInt, ordIns, ddCycle, incBase, genDiff, genDelta, stableMode,
                                emitSnapshot),
                            hint);
                }
            }
        });
    }
}