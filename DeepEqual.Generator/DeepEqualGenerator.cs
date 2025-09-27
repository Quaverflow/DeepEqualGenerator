using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DeepEqual.Generator;

internal static class GenCommon
{
    internal const string DeepComparableAttributeMetadataName = "DeepEqual.Generator.Shared.DeepComparableAttribute";
    internal const string DeepCompareAttributeMetadataName = "DeepEqual.Generator.Shared.DeepCompareAttribute";

    internal const string ExternalDeepComparableMetadataName =
        "DeepEqual.Generator.Shared.ExternalDeepComparableAttribute";

    internal const string ExternalDeepCompareMetadataName = "DeepEqual.Generator.Shared.ExternalDeepCompareAttribute";

    internal static HashSet<INamedTypeSymbol> BuildReachableTypeClosure(EqualityTarget root)
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
                var kind = GenCommon.GetEffectiveKind(member.Type, GetDeepCompareAttribute(member.Symbol));
                if (kind is EffectiveKind.Skip or EffectiveKind.Shallow or EffectiveKind.Reference) continue;

                Accumulate(member.Type);
            }
        }

        return set;

        void Accumulate(ITypeSymbol t)
        {
            if (t is INamedTypeSymbol nnt && nnt.OriginalDefinition.ToDisplayString() == "System.Nullable<T>")
                t = nnt.TypeArguments[0];

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

            if (t is INamedTypeSymbol n && GenCommon.IsUserObjectType(n) && IsTypeAccessibleFromRoot(n, root) && set.Add(n))
                queue.Enqueue(n);
        }
    }

    internal static bool TryEmitWellKnownStructCompare(CodeWriter w, string leftExpr, string rightExpr, ITypeSymbol type)
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


    internal static bool TryGetReadOnlyMemory(ITypeSymbol type, out ITypeSymbol? elementType)
    {
        elementType = null;
        if (type is INamedTypeSymbol named && named.OriginalDefinition.ToDisplayString() == "System.ReadOnlyMemory<T>")
        {
            elementType = named.TypeArguments[0];
            return true;
        }

        return false;
    }

    internal static bool TryGetMemory(ITypeSymbol type, out ITypeSymbol? elementType)
    {
        elementType = null;
        if (type is INamedTypeSymbol named && named.OriginalDefinition.ToDisplayString() == "System.Memory<T>")
        {
            elementType = named.TypeArguments[0];
            return true;
        }

        return false;
    }

    internal static bool IsHashFriendly(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String) return true;

        if (type.TypeKind == TypeKind.Enum) return true;

        if (type.SpecialType is SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal)
            return false;

        if (type.IsValueType && type.SpecialType != SpecialType.None) return true;

        var fqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (fqn is "global::System.DateTime" or "global::System.DateTimeOffset") return true;

        return false;
    }

    internal static string GetNumericCall(ITypeSymbol type, string l, string r, string ctxVar)
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

    internal static bool IsNumericWithTolerance(ITypeSymbol type)
    {
        return type.SpecialType is SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal;
    }

    internal static EqualityTypeSchema GetTypeSchema(INamedTypeSymbol type)
    {
        var attr = type.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepCompareAttributeMetadataName);
        if (attr is null)
        {
            var empty = new EqualityTypeSchema(Array.Empty<string>(), Array.Empty<string>());
            return empty;
        }

        static string[] ReadStringArray(TypedConstant arg)
        {
            if (arg is { Kind: TypedConstantKind.Array, IsNull: false })
                return arg.Values.Select(v => v.Value?.ToString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

            return [];
        }

        var include = Array.Empty<string>();
        var ignore = Array.Empty<string>();

        foreach (var kv in attr.NamedArguments)
            if (kv.Key == "Members")
                include = ReadStringArray(kv.Value);
            else if (kv.Key == "IgnoreMembers") ignore = ReadStringArray(kv.Value);

        var schema = new EqualityTypeSchema(include, ignore);
        return schema;
    }

    internal static IEnumerable<EqualityMemberSymbol> EnumerateMembers(INamedTypeSymbol type, bool allowInternals,
        bool includeBase, EqualityTypeSchema schema)
    {
        return EnumerateMembersUncached(type, allowInternals, includeBase, schema);
    }

    internal static IEnumerable<EqualityMemberSymbol> EnumerateMembersUncached(INamedTypeSymbol ownerType,
        bool includeInternals,
        bool includeBase, EqualityTypeSchema schema)
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
                if (p.IsStatic) continue;

                if (p.Parameters.Length != 0) continue;

                if (p.GetMethod is null) continue;

                if (!IsAccessible(p, includeInternals, ownerType)) continue;

                if (yielded.Contains(p.Name)) continue;

                if (hasInclude && !includeSet!.Contains(p.Name)) continue;

                if (ignoreSet is not null && ignoreSet.Contains(p.Name)) continue;

                if (ownerType.IsValueType && SymbolEqualityComparer.Default.Equals(p.Type, ownerType) &&
                    !hasInclude)
                    continue;

                yielded.Add(p.Name);
                yield return new EqualityMemberSymbol(p.Name, p.Type, p);
            }

            foreach (var f in t.GetMembers().OfType<IFieldSymbol>())
            {
                if (f.IsStatic || f.IsConst || f.IsImplicitlyDeclared) continue;

                if (f.AssociatedSymbol is not null) continue;

                if (f.Name.StartsWith("<", StringComparison.Ordinal)) continue;

                if (!IsAccessible(f, includeInternals, ownerType)) continue;

                if (yielded.Contains(f.Name)) continue;

                if (hasInclude && !includeSet!.Contains(f.Name)) continue;

                if (ignoreSet is not null && ignoreSet.Contains(f.Name)) continue;

                if (ownerType.IsValueType && SymbolEqualityComparer.Default.Equals(f.Type, ownerType) &&
                    !hasInclude)
                    continue;

                yielded.Add(f.Name);
                yield return new EqualityMemberSymbol(f.Name, f.Type, f);
            }

            if (!includeBase) break;
        }
    }

    internal static IEnumerable<EqualityMemberSymbol> OrderMembers(IEnumerable<EqualityMemberSymbol> members)
    {
        return members.Select(m => (m, key: MemberCost(m))).OrderBy(t => t.key)
            .ThenBy(t => t.m.Name, StringComparer.Ordinal).Select(t => t.m);
    }

    internal static int MemberCost(EqualityMemberSymbol equalityMember)
    {
        var attr = GetDeepCompareAttribute(equalityMember.Symbol);
        var kind = GenCommon.GetEffectiveKind(equalityMember.Type, attr);
        if (kind == EffectiveKind.Skip) return 99;

        if (kind is EffectiveKind.Reference or EffectiveKind.Shallow) return 0;

        var t = equalityMember.Type;
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

        if (TryGetReadOnlyMemory(t, out _)) return 3;

        if (TryGetMemory(t, out _)) return 3;

        if (t is INamedTypeSymbol nts && GenCommon.IsUserObjectType(nts)) return 7;

        return 7;
    }

    internal static bool IsWellKnownStruct(ITypeSymbol t)
    {
        if (t.SpecialType == SpecialType.System_DateTime) return true;

        var fqn = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return fqn is "global::System.DateTimeOffset" or "global::System.TimeSpan" or "global::System.Guid"
            or "global::System.DateOnly" or "global::System.TimeOnly";
    }

    internal static AttributeData? GetDeepCompareAttribute(ISymbol symbol)
    {
        return symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepCompareAttributeMetadataName);
    }


    internal static INamedTypeSymbol? GetEffectiveComparerType(ITypeSymbol comparedType, AttributeData? memberAttribute)
    {
        INamedTypeSymbol? fromMember = null;
        if (memberAttribute is not null)
            foreach (var kv in memberAttribute.NamedArguments)
                if (kv is { Key: "ComparerType", Value.Value: INamedTypeSymbol ts } &&
                    ImplementsIEqualityComparerFor(ts, comparedType))
                {
                    fromMember = ts;
                    break;
                }

        if (fromMember is not null) return fromMember;

        var typeAttr = comparedType.OriginalDefinition.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepCompareAttributeMetadataName);
        if (typeAttr is not null)
            foreach (var kv in typeAttr.NamedArguments)
                if (kv is { Key: "ComparerType", Value.Value: INamedTypeSymbol ts2 } &&
                    ImplementsIEqualityComparerFor(ts2, comparedType))
                    return ts2;

        return null;
    }

    internal static bool ImplementsIEqualityComparerFor(INamedTypeSymbol comparerType, ITypeSymbol argument)
    {
        foreach (var i in comparerType.AllInterfaces)
            if (i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEqualityComparer<T>")
                if (SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], argument))
                    return true;

        return false;
    }

    internal static bool TryGetKeySpec(ITypeSymbol elementType, AttributeData? memberAttribute, EqualityTarget root,
        out string keyTypeFqn, out string keyExprFormat)
    {
        var keys = new List<EqualityMemberSymbol>();
        keyTypeFqn = "";
        keyExprFormat = "{0}";
        var names = Array.Empty<string>();

        if (memberAttribute is not null)
            foreach (var kv in memberAttribute.NamedArguments)
                if (kv is { Key: "KeyMembers", Value.Values: { Length: > 0 } arr })
                {
                    names = arr.Select(v => v.Value?.ToString() ?? "").Where(s => s.Length > 0).ToArray();
                    break;
                }

        if (names.Length == 0)
        {
            var typeAttr = elementType.OriginalDefinition.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepCompareAttributeMetadataName);
            if (typeAttr is not null)
                foreach (var kv in typeAttr.NamedArguments)
                    if (kv is { Key: "KeyMembers", Value.Values: { Length: > 0 } arr2 })
                    {
                        names = arr2.Select(v => v.Value?.ToString() ?? "").Where(s => s.Length > 0).ToArray();
                        break;
                    }
        }

        if (names.Length == 0) return false;

        foreach (var n in names)
        {
            var m = FindMemberOn(elementType, n, root.IncludeInternals, root.IncludeBaseMembers);
            if (m is not null) keys.Add(m.Value);
        }

        if (keys.Count == 0) return false;

        if (keys.Count == 1)
        {
            keyTypeFqn = keys[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            keyExprFormat = "{0}." + keys[0].Name;
            return true;
        }

        if (keys.Count > 7) return false;

        keyTypeFqn = "global::System.ValueTuple<" + string.Join(",",
            keys.Select(k => k.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))) + ">";
        keyExprFormat = "(" + string.Join(",", keys.Select(k => "{0}." + k.Name)) + ")";
        return true;
    }

    internal static EqualityMemberSymbol? FindMemberOn(ITypeSymbol type, string name, bool includeInternals, bool includeBase)
    {
        for (var t = type;
             t is not null && t.SpecialType != SpecialType.System_Object;
             t = includeBase ? (t as INamedTypeSymbol)?.BaseType : null)
        {
            foreach (var p in t.GetMembers().OfType<IPropertySymbol>())
            {
                if (p.Name != name) continue;

                if (p.IsStatic || p.Parameters.Length != 0 || p.GetMethod is null) continue;

                if (!IsAccessibleForMember(p, includeInternals, type)) continue;

                return new EqualityMemberSymbol(p.Name, p.Type, p);
            }

            foreach (var f in t.GetMembers().OfType<IFieldSymbol>())
            {
                if (f.Name != name) continue;

                if (f.IsStatic || f.IsConst || f.IsImplicitlyDeclared) continue;

                if (!IsAccessibleForMember(f, includeInternals, type)) continue;

                return new EqualityMemberSymbol(f.Name, f.Type, f);
            }

            if (!includeBase) break;
        }

        return null;
    }

    internal static bool IsAccessibleForMember(ISymbol s, bool inclInternals, ITypeSymbol owner)
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

    internal static string GetEqualityComparerExprForHash(ITypeSymbol elType, string ctxVar, string? customVar)
    {
        if (customVar is not null) return customVar;

        if (elType.SpecialType == SpecialType.System_String)
            return "DeepEqual.Generator.Shared.ComparisonHelpers.GetStringComparer(" + ctxVar + ")";

        var fqn = elType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (fqn == "global::System.DateTime")
            return "DeepEqual.Generator.Shared.ComparisonHelpers.StrictDateTimeComparer.Instance";

        if (fqn == "global::System.DateTimeOffset")
            return "DeepEqual.Generator.Shared.ComparisonHelpers.StrictDateTimeOffsetComparer.Instance";

        return "System.Collections.Generic.EqualityComparer<" +
               elType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ">.Default";
    }

    internal static bool TryGetEnumerableInterface(ITypeSymbol type, out ITypeSymbol? elementType)
    {
        if (type is INamedTypeSymbol named &&
            named.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>")
        {
            elementType = named.TypeArguments[0];
            return true;
        }

        foreach (var i in type.AllInterfaces)
            if (i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>")
            {
                elementType = i.TypeArguments[0];
                return true;
            }

        elementType = null;
        return false;
    }

    internal static bool TryGetDictionaryInterface(ITypeSymbol type, out ITypeSymbol? keyType,
        out ITypeSymbol? valueType)
    {
        if (type is INamedTypeSymbol named)
        {
            var defSelf = named.OriginalDefinition.ToDisplayString();
            if (defSelf is "System.Collections.Generic.IDictionary<TKey, TValue>"
                or "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>")
            {
                keyType = named.TypeArguments[0];
                valueType = named.TypeArguments[1];
                return true;
            }
        }

        foreach (var i in type.AllInterfaces)
        {
            var def = i.OriginalDefinition.ToDisplayString();
            if (def is "System.Collections.Generic.IDictionary<TKey, TValue>"
                or "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>")
            {
                keyType = i.TypeArguments[0];
                valueType = i.TypeArguments[1];
                return true;
            }
        }

        keyType = null;
        valueType = null;
        return false;
    }

    internal static bool IsTypeAccessibleFromRoot(INamedTypeSymbol t, EqualityTarget root)
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

    internal static string BuildMetadataName(INamedTypeSymbol symbol)
    {
        var sb = new StringBuilder();
        var containing = symbol.ContainingType;
        if (containing is not null)
        {
            sb.Append(BuildMetadataName(containing));
            sb.Append('+');
            sb.Append(symbol.MetadataName);
        }
        else
        {
            var ns = symbol.ContainingNamespace?.ToDisplayString() ?? "";
            if (!string.IsNullOrEmpty(ns))
            {
                sb.Append(ns);
                sb.Append('.');
            }

            sb.Append(symbol.MetadataName);
        }

        return sb.ToString();
    }

    internal static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        if (value.StartsWith("global::", StringComparison.Ordinal))
            value = value["global::".Length..];

        var arr = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(arr);
    }

    internal static string SanitizeIdentifier(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s) sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');

        return sb.ToString();
    }

    internal static EffectiveKind GetEffectiveKind(ITypeSymbol type, AttributeData? memberAttribute)
    {
        if (memberAttribute is not null)
        {
            var val = memberAttribute.NamedArguments.FirstOrDefault(p => p.Key == "Kind").Value.Value;
            if (val is int mk) return (EffectiveKind)mk;
        }

        var typeAttr = type.OriginalDefinition.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepComparableAttributeMetadataName);
        if (typeAttr is not null)
        {
            var val = typeAttr.NamedArguments.FirstOrDefault(p => p.Key == "Kind").Value.Value;
            if (val is int tk) return (EffectiveKind)tk;
        }

        return EffectiveKind.Deep;
    }
    internal static string GetHelperMethodName(INamedTypeSymbol type)
    {
        return "AreDeepEqual__" +
               GenCommon.SanitizeIdentifier(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
    }

    internal static string BuildInlineCompareExpr(string l, string r, ITypeSymbol type, EffectiveKind kind,
        string ctxVar = "c", string? customEqVar = null)
    {
        if (customEqVar is not null)
            return customEqVar + ".Equals(" + l + ", " + r + ")";

        if (kind == EffectiveKind.Reference)
            return "object.ReferenceEquals(" + l + ", " + r + ")";

        if (kind == EffectiveKind.Shallow)
            return "object.Equals(" + l + ", " + r + ")";

        if (type is INamedTypeSymbol nt && nt.OriginalDefinition.ToDisplayString() == "System.Nullable<T>")
        {
            var tArg = nt.TypeArguments[0];
            var inner = BuildInlineCompareExpr(l + ".Value", r + ".Value", tArg, GenCommon.GetEffectiveKind(tArg, null), ctxVar,
                customEqVar);
            return "(" + l + ".HasValue == " + r + ".HasValue) && (!" + l + ".HasValue || (" + inner + "))";
        }

        if (type.SpecialType == SpecialType.System_String)
            return "DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualStrings(" + l + ", " + r + ", " + ctxVar + ")";
        if (type.TypeKind == TypeKind.Enum)
        {
            var enumFqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return "DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualEnum<" + enumFqn + ">(" + l + ", " + r + ")";
        }

        if (type.SpecialType == SpecialType.System_DateTime)
            return "DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDateTime(" + l + ", " + r + ")";

        var fqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (fqn == "global::System.DateTimeOffset")
            return "DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDateTimeOffset(" + l + ", " + r + ")";
        if (fqn == "global::System.TimeSpan")
            return l + ".Ticks == " + r + ".Ticks";
        if (fqn == "global::System.Guid")
            return l + ".Equals(" + r + ")";
        if (fqn == "global::System.DateOnly")
            return "DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDateOnly(" + l + ", " + r + ")";
        if (fqn == "global::System.TimeOnly")
            return "DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualTimeOnly(" + l + ", " + r + ")";

        if (type.SpecialType == SpecialType.System_Double)
            return "DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDouble(" + l + ", " + r + ", " + ctxVar + ")";
        if (type.SpecialType == SpecialType.System_Single)
            return "DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualSingle(" + l + ", " + r + ", " + ctxVar + ")";
        if (type.SpecialType == SpecialType.System_Decimal)
            return "DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDecimal(" + l + ", " + r + ", " + ctxVar + ")";

        if (type.SpecialType == SpecialType.System_Object)
            return "DeepEqual.Generator.Shared.DynamicDeepComparer.AreEqualDynamic(" + l + ", " + r + ", " + ctxVar +
                   ")";

        if (type.IsValueType && type.SpecialType != SpecialType.None)
            return type.SpecialType switch
            {
                SpecialType.System_Int32 or SpecialType.System_UInt32 or
                    SpecialType.System_Int16 or SpecialType.System_UInt16 or
                    SpecialType.System_Byte or SpecialType.System_SByte or
                    SpecialType.System_Int64 or SpecialType.System_UInt64 or
                    SpecialType.System_Boolean or SpecialType.System_Char
                    => l + "==" + r,
                _ => l + ".Equals(" + r + ")"
            };

        if (type.TypeKind == TypeKind.Interface || type is INamedTypeSymbol { IsAbstract: true })
        {
            var ts = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return "DeepEqual.Generator.Shared.ComparisonHelpers.DeepComparePolymorphic<" + ts + ">(" + l + ", " + r +
                   ", " + ctxVar + ")";
        }

        if (type is INamedTypeSymbol nts && GenCommon.IsUserObjectType(nts))
            return GetHelperMethodName(nts) + "(" + l + ", " + r + ", " + ctxVar + ")";

        return "object.Equals(" + l + ", " + r + ")";
    }

    internal static bool IsUserObjectType(ITypeSymbol type)
    {

        if (type is not INamedTypeSymbol n)
        {
            return false;
        }

        if (n.SpecialType != SpecialType.None)
        {
            return false;
        }

        var ns = n.ContainingNamespace?.ToDisplayString() ?? "";
        if (ns.StartsWith("System", StringComparison.Ordinal))
        {
            return false;
        }

        var asm = n.ContainingAssembly?.Name ?? "";
        if (asm is "mscorlib" or "System.Private.CoreLib" or "System.Runtime")
        {
            return false;
        }

        var ok = n.TypeKind is TypeKind.Class or TypeKind.Struct;
        return ok;
    }

    internal static string EnsureComparerStruct(HashSet<string> emitted, List<string[]> declarations, ITypeSymbol elementType,
        EffectiveKind elementKind, string hint, string? customComparerVar = null)
    {
        var elFqn = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var cmpName = "__Cmp__" + GenCommon.SanitizeIdentifier(elFqn) + "__" + hint;
        if (!emitted.Add(cmpName)) return cmpName;

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
    internal static bool ResolveOrderInsensitive(bool orderInsensitiveCollection, AttributeData? memberAttribute,
        ITypeSymbol elementType,
        INamedTypeSymbol? containingType)
    {
        if (memberAttribute is not null)
        {
            var opt = memberAttribute.NamedArguments.FirstOrDefault(a => a.Key == "OrderInsensitive").Value;
            if (opt.Value is bool b) return b;
        }

        var typeAttr = elementType.OriginalDefinition.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepComparableAttributeMetadataName);
        if (typeAttr is not null)
        {
            var opt = typeAttr.NamedArguments.FirstOrDefault(a => a.Key == "OrderInsensitiveCollections").Value;
            if (opt.Value is bool b) return b;
        }

        if (containingType is not null)
        {
            var containerAttr = containingType.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepComparableAttributeMetadataName);
            if (containerAttr is not null)
            {
                var opt = containerAttr.NamedArguments.FirstOrDefault(a => a.Key == "OrderInsensitiveCollections")
                    .Value;
                if (opt.Value is bool b) return b;
            }
        }

        return orderInsensitiveCollection;
    }
}

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

internal sealed class EqualityEmitter
{
    private const string DeepCompareAttributeName = GenCommon.DeepCompareAttributeMetadataName;

    public void EmitForRoot(SourceProductionContext spc, EqualityTarget root, string? hintOverride = null)
    {
        var ns = root.Type.ContainingNamespace.IsGlobalNamespace
            ? null
            : root.Type.ContainingNamespace.ToDisplayString();
        var rootFqn = root.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var helperClass = root.Type.Name + "DeepEqual";
        var hintName = hintOverride ?? GenCommon.SanitizeFileName(rootFqn + "_DeepEqual.g.cs");

        var reachable = GenCommon.BuildReachableTypeClosure(root);
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

        if (ns is not null) w.Open("namespace " + ns);

        w.Open(accessibility + " static class " + helperClass + typeParams);


        w.Open("static " + helperClass + "()");
        foreach (var t in reachable.Where(t => GenCommon.IsTypeAccessibleFromRoot(t, root))
                     .OrderBy(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal))
        {
            var fqn = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var helper = GenCommon.GetHelperMethodName(t);
            w.Line("GeneratedHelperRegistry.RegisterComparer<" + fqn + ">((l, r, c) => " + helper + "(l, r, c));");
        }

        w.Close();
        w.Line();

        if (root.Type.IsValueType)
            w.Open(accessibility + " static bool AreDeepEqual(" + rootFqn + " left, " + rootFqn + " right)");
        else
            w.Open(accessibility + " static bool AreDeepEqual(" + rootFqn + "? left, " + rootFqn + "? right)");

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
        w.Line("return " + GenCommon.GetHelperMethodName(root.Type) + "(left, right, context);");
        w.Close();
        w.Line();

        if (root.Type.IsValueType)
            w.Open(accessibility + " static bool AreDeepEqual(" + rootFqn + " left, " + rootFqn +
                   " right, DeepEqual.Generator.Shared.ComparisonOptions options)");
        else
            w.Open(accessibility + " static bool AreDeepEqual(" + rootFqn + "? left, " + rootFqn +
                   "? right, DeepEqual.Generator.Shared.ComparisonOptions options)");

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
        w.Line("return " + GenCommon.GetHelperMethodName(root.Type) + "(left, right, context);");
        w.Close();
        w.Line();

        if (root.Type.IsValueType)
            w.Open(accessibility + " static bool AreDeepEqual(" + rootFqn + " left, " + rootFqn +
                   " right, DeepEqual.Generator.Shared.ComparisonContext context)");
        else
            w.Open(accessibility + " static bool AreDeepEqual(" + rootFqn + "? left, " + rootFqn +
                   "? right, DeepEqual.Generator.Shared.ComparisonContext context)");

        if (!root.Type.IsValueType)
        {
            w.Open("if (object.ReferenceEquals(left, right))");
            w.Line("return true;");
            w.Close();
            w.Open("if (left is null || right is null)");
            w.Line("return false;");
            w.Close();
        }

        w.Line("return " + GenCommon.GetHelperMethodName(root.Type) + "(left, right, context);");
        w.Close();
        w.Line();

        var emittedComparers = new HashSet<string>(StringComparer.Ordinal);
        var comparerDeclarations = new List<string[]>();

        foreach (var t in reachable.OrderBy(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                     StringComparer.Ordinal))
            EmitHelperForType(w, t, root, trackCycles, emittedComparers, comparerDeclarations, spc);

        if (comparerDeclarations.Count > 0)
            foreach (var block in comparerDeclarations)
                foreach (var line in block)
                    w.Line(line);

        w.Close();

        string OpenGenericSuffix(int arity)
        {
            if (arity <= 0) return "";
            if (arity == 1) return "<>";
            return "<" + new string(',', arity - 1) + ">";
        }

        var openSuffix = OpenGenericSuffix(root.Type.Arity);
        var initTypeName = "__" + GenCommon.SanitizeIdentifier(helperClass) + "_ModuleInit_" +
                           Math.Abs(root.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).GetHashCode());

        w.Open("internal static class " + initTypeName);
        w.Line("[System.Runtime.CompilerServices.ModuleInitializer]");
        w.Open("internal static void Init()");
        w.Line("System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(" + helperClass + openSuffix +
               ").TypeHandle);");
        w.Close();
        w.Close();

        if (ns is not null) w.Close();
        var text = w.ToString();


        spc.AddSource(hintName, SourceText.From(text, Encoding.UTF8));
    }

    private void EmitHelperForType(CodeWriter w, INamedTypeSymbol type, EqualityTarget root, bool trackCycles,
        HashSet<string> emittedComparers, List<string[]> comparerDeclarations, SourceProductionContext spc)
    {
        var fqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var helper = GenCommon.GetHelperMethodName(type);
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

        var schema = GenCommon.GetTypeSchema(type);
        var inc = schema.IncludeMembers;
        var ign = schema.IgnoreMembers;
        if (inc.Count > 0 && ign.Count > 0)
            for (var i = 0; i < inc.Count; i++)
            {
                var name = inc[i];
                if (ign.Contains(name, StringComparer.Ordinal))
                {
                    var attr = type.GetAttributes()
                        .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepCompareAttributeName);
                    var loc = attr?.ApplicationSyntaxReference?.GetSyntax(spc.CancellationToken).GetLocation() ??
                              type.Locations.FirstOrDefault();
                    if (loc is not null) spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.EQ001, loc, name));

                    break;
                }
            }

        if (inc.Count > 0 && ign.Count > 0)
            for (var i = 0; i < inc.Count; i++)
            {
                var name = inc[i];
                if (ign.Contains(name, StringComparer.Ordinal))
                {
                    var attr = type.GetAttributes()
                        .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepCompareAttributeName);
                    var loc = attr?.ApplicationSyntaxReference?.GetSyntax(spc.CancellationToken).GetLocation() ??
                              type.Locations.FirstOrDefault();
                    if (loc is not null) spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.EQ001, loc, name));

                    break;
                }
            }

        foreach (var member in GenCommon.OrderMembers(GenCommon.EnumerateMembers(type, root.IncludeInternals, root.IncludeBaseMembers,
                     schema))) EmitMember(w, type, member, root, emittedComparers, comparerDeclarations, spc);

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

    private void EmitMember(CodeWriter w, INamedTypeSymbol owner, EqualityMemberSymbol equalityMember,
        EqualityTarget root,
        HashSet<string> emittedComparers, List<string[]> comparerDeclarations, SourceProductionContext spc)
    {
        var leftExpr = "left." + equalityMember.Name;
        var rightExpr = "right." + equalityMember.Name;
        var deepAttr = GenCommon.GetDeepCompareAttribute(equalityMember.Symbol);
        var kind = GenCommon.GetEffectiveKind(equalityMember.Type, deepAttr);
        {
            var all = equalityMember.Symbol.GetAttributes()
                .Where(a => a.AttributeClass?.ToDisplayString() == DeepCompareAttributeName).ToArray();
            if (all.Length > 1)
            {
                var loc = all[0].ApplicationSyntaxReference?.GetSyntax(spc.CancellationToken).GetLocation() ??
                          equalityMember.Symbol.Locations.FirstOrDefault();
                if (loc is not null)
                    spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.EQ001, loc, equalityMember.Name));
            }

            if (kind == EffectiveKind.Deep)
            {
                var t = equalityMember.Type;
                if (t is INamedTypeSymbol n && GenCommon.IsUserObjectType(n) && !GenCommon.IsTypeAccessibleFromRoot(n, root))
                {
                    var attr = all.FirstOrDefault();
                    var loc2 = attr?.ApplicationSyntaxReference?.GetSyntax(spc.CancellationToken).GetLocation() ??
                               equalityMember.Symbol.Locations.FirstOrDefault();
                    if (loc2 is not null)
                        spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.EQ002, loc2,
                            n.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                }
            }
        }

        if (kind == EffectiveKind.Skip)
        {
            w.Line();
            return;
        }

        var isString = equalityMember.Type.SpecialType == SpecialType.System_String;
        if (!equalityMember.Type.IsValueType && !isString)
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

        var directCustomCmp = GenCommon.GetEffectiveComparerType(equalityMember.Type, deepAttr);
        if (directCustomCmp is not null)
        {
            var cmpFqn = directCustomCmp.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var tFqn = equalityMember.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var customVar = "__cmp_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" +
                            GenCommon.SanitizeIdentifier(equalityMember.Name);
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

        if (equalityMember.Type is INamedTypeSymbol nnt0 &&
            nnt0.OriginalDefinition.ToDisplayString() == "System.Nullable<T>")
        {
            var valueT = nnt0.TypeArguments[0];
            var customCmpT = GenCommon.GetEffectiveComparerType(valueT, deepAttr);
            string? customVar = null;
            if (customCmpT is not null)
            {
                var cmpFqn = customCmpT.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var elFqn2 = valueT.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                customVar = "__cmp_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" +
                            GenCommon.SanitizeIdentifier(equalityMember.Name);
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

        if (GenCommon.TryEmitWellKnownStructCompare(w, leftExpr, rightExpr, equalityMember.Type))
        {
            w.Line();
            return;
        }

        if (equalityMember.Type.SpecialType == SpecialType.System_String)
        {
            w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualStrings(" + leftExpr + ", " + rightExpr +
                   ", context))");
            w.Line("return false;");
            w.Close();
            w.Line();
            return;
        }

        if (equalityMember.Type.TypeKind == TypeKind.Enum)
        {
            var enumFqn = equalityMember.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualEnum<" + enumFqn + ">(" + leftExpr +
                   ", " + rightExpr + "))");
            w.Line("return false;");
            w.Close();
            w.Line();
            return;
        }

        if (GenCommon.IsNumericWithTolerance(equalityMember.Type))
        {
            var call = GenCommon.GetNumericCall(equalityMember.Type, leftExpr, rightExpr, "context");
            w.Open("if (!" + call + ")");
            w.Line("return false;");
            w.Close();
            w.Line();
            return;
        }

        if (GenCommon.TryGetReadOnlyMemory(equalityMember.Type, out var romEl))
        {
            var elFqn = romEl!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var elKind = GenCommon.GetEffectiveKind(romEl, null);
            var elemCustomCmpT = GenCommon.GetEffectiveComparerType(romEl, deepAttr);
            string? elemCustomVar = null;
            if (elemCustomCmpT is not null)
            {
                var cmpFqn = elemCustomCmpT.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                elemCustomVar = "__cmpE_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" +
                                GenCommon.SanitizeIdentifier(equalityMember.Name);
                w.Line("var " + elemCustomVar + " = (System.Collections.Generic.IEqualityComparer<" + elFqn +
                       ">)System.Activator.CreateInstance(typeof(" + cmpFqn + "))!;");
            }

            var cmpName = GenCommon.EnsureComparerStruct(emittedComparers, comparerDeclarations, romEl, elKind,
                "M_" + GenCommon.SanitizeIdentifier(owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)) +
                "_" +
                equalityMember.Name, elemCustomVar);
            w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualReadOnlyMemory<" + elFqn + ", " +
                   cmpName + ">(" + leftExpr + ", " + rightExpr + ", new " + cmpName + "(" + (elemCustomVar ?? "") +
                   "), context))");
            w.Line("return false;");
            w.Close();
            w.Line();
            return;
        }

        if (GenCommon.TryGetMemory(equalityMember.Type, out var memEl))
        {
            var elFqn = memEl!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var elKind = GenCommon.GetEffectiveKind(memEl, null);
            var elemCustomCmpT = GenCommon.GetEffectiveComparerType(memEl, deepAttr);
            string? elemCustomVar = null;
            if (elemCustomCmpT is not null)
            {
                var cmpFqn = elemCustomCmpT.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                elemCustomVar = "__cmpE_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" +
                                GenCommon.SanitizeIdentifier(equalityMember.Name);
                w.Line("var " + elemCustomVar + " = (System.Collections.Generic.IEqualityComparer<" + elFqn +
                       ">)System.Activator.CreateInstance(typeof(" + cmpFqn + "))!;");
            }

            var cmpName = GenCommon.EnsureComparerStruct(emittedComparers, comparerDeclarations, memEl, elKind,
                "M_" + GenCommon.SanitizeIdentifier(owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)) +
                "_" +
                equalityMember.Name, elemCustomVar);
            w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualMemory<" + elFqn + ", " + cmpName + ">(" +
                   leftExpr + ", " + rightExpr + ", new " + cmpName + "(" + (elemCustomVar ?? "") + "), context))");
            w.Line("return false;");
            w.Close();
            w.Line();
            return;
        }

        if (equalityMember.Type.IsValueType && equalityMember.Type.SpecialType != SpecialType.None)
        {
            w.Open("if (!" + leftExpr + ".Equals(" + rightExpr + "))");
            w.Line("return false;");
            w.Close();
            w.Line();
            return;
        }
        if (equalityMember.Type is IArrayTypeSymbol arr)
        {
            var el = arr.ElementType;
            var elFqn = el.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var unordered = arr.Rank == 1 && GenCommon.ResolveOrderInsensitive(root.OrderInsensitiveCollections, deepAttr, el, owner);
            var elKind = GenCommon.GetEffectiveKind(el, null);

            var elemCustomCmpT = GenCommon.GetEffectiveComparerType(el, deepAttr);
            string? elemCustomVar = null;
            if (elemCustomCmpT is not null)
            {
                var cmpFqn = elemCustomCmpT.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                elemCustomVar = "__cmpE_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" +
                                GenCommon.SanitizeIdentifier(equalityMember.Name);
                w.Line("var " + elemCustomVar + " = (System.Collections.Generic.IEqualityComparer<" + elFqn +
                       ">)System.Activator.CreateInstance(typeof(" + cmpFqn + "))!;");
            }

            var cmpName = GenCommon.EnsureComparerStruct(emittedComparers, comparerDeclarations, el, elKind,
                "M_" + GenCommon.SanitizeIdentifier(owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)) +
                "_" + equalityMember.Name, elemCustomVar);

            // Keyed unordered (rank==1 only) — compare arrays by grouping on the key, no IReadOnlyList casts.
            if (unordered && GenCommon.TryGetKeySpec(el, deepAttr, root, out var keyTypeFqn, out var keyExprFmt))
            {
                // a[] / b[] strong-typed views
                var aArr = "__arrA_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" + GenCommon.SanitizeIdentifier(equalityMember.Name);
                var bArr = "__arrB_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" + GenCommon.SanitizeIdentifier(equalityMember.Name);

                // groupings
                var dictA = "__ka_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" + GenCommon.SanitizeIdentifier(equalityMember.Name);
                var dictB = "__kb_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" + GenCommon.SanitizeIdentifier(equalityMember.Name);

                // temps
                var tmpA = "__eA_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" + GenCommon.SanitizeIdentifier(equalityMember.Name);
                var tmpB = "__eB_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" + GenCommon.SanitizeIdentifier(equalityMember.Name);

                w.Line("var " + aArr + " = " + leftExpr + " as " + elFqn + "[];");
                w.Line("var " + bArr + " = " + rightExpr + " as " + elFqn + "[];");

                w.Open("if (!object.ReferenceEquals(" + aArr + ", " + bArr + "))");
                w.Open("if (" + aArr + " is null || " + bArr + " is null)");
                w.Line("return false;");
                w.Close();

                w.Open("if (" + aArr + ".Length != " + bArr + ".Length)");
                w.Line("return false;");
                w.Close();

                w.Line("var " + dictA + " = new System.Collections.Generic.Dictionary<" + keyTypeFqn +
                       ", System.Collections.Generic.List<" + elFqn + ">>();");
                w.Line("var " + dictB + " = new System.Collections.Generic.Dictionary<" + keyTypeFqn +
                       ", System.Collections.Generic.List<" + elFqn + ">>();");

                // Build A buckets
                w.Open("for (int __i = 0; __i < " + aArr + ".Length; __i++)");
                w.Line("var " + tmpA + " = " + aArr + "[__i];");
                w.Line("var __k = " + string.Format(keyExprFmt, tmpA) + ";");
                w.Open("if (!" + dictA + ".TryGetValue(__k, out var __lst))");
                w.Line("__lst = " + dictA + "[__k] = new System.Collections.Generic.List<" + elFqn + ">();");
                w.Close();
                w.Line("__lst.Add(" + tmpA + ");");
                w.Close();

                // Build B buckets
                w.Open("for (int __j = 0; __j < " + bArr + ".Length; __j++)");
                w.Line("var " + tmpB + " = " + bArr + "[__j];");
                w.Line("var __k = " + string.Format(keyExprFmt, tmpB) + ";");
                w.Open("if (!" + dictB + ".TryGetValue(__k, out var __lst))");
                w.Line("__lst = " + dictB + "[__k] = new System.Collections.Generic.List<" + elFqn + ">();");
                w.Close();
                w.Line("__lst.Add(" + tmpB + ");");
                w.Close();

                w.Line("if (" + dictA + ".Count != " + dictB + ".Count) return false;");
                w.Open("foreach (var __kv in " + dictA + ")");
                w.Open("if (!" + dictB + ".TryGetValue(__kv.Key, out var __lstB))");
                w.Line("return false;");
                w.Close();

                w.Line("if (__kv.Value.Count != __lstB.Count) return false;");

                // match within each bucket using element comparer
                var matchMask = "__m_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" + GenCommon.SanitizeIdentifier(equalityMember.Name);
                w.Line("var " + matchMask + " = new bool[__lstB.Count];");
                w.Line("var __cmp = new " + cmpName + "(" + (elemCustomVar ?? "") + ");");

                w.Open("for (int __x = 0; __x < __kv.Value.Count; __x++)");
                w.Line("bool __f = false;");
                w.Open("for (int __y = 0; __y < __lstB.Count; __y++)");
                w.Open("if (!" + matchMask + "[__y])");
                w.Open("if (__cmp.Invoke(__kv.Value[__x], __lstB[__y], context))");
                w.Line(matchMask + "[__y] = (__f = true);");
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
            else if (unordered && GenCommon.IsHashFriendly(el))
            {
                // Fast multiset compare for hash-friendly elements (string/primitive/enums etc.)
                var eqExpr = GenCommon.GetEqualityComparerExprForHash(el, "context", elemCustomVar);

                w.Open("if (!object.ReferenceEquals(" + leftExpr + ", " + rightExpr + "))");
                w.Open("if (" + leftExpr + " is null || " + rightExpr + " is null)");
                w.Line("return false;");
                w.Close();
                w.Open("if (" + leftExpr + ".Length != " + rightExpr + ".Length)");
                w.Line("return false;");
                w.Close();

                w.Line("var __ra = new System.Collections.Generic.List<" + elFqn + ">(" + leftExpr + ");");
                w.Line("var __rb = new System.Collections.Generic.List<" + elFqn + ">(" + rightExpr + ");");
                w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualSequencesUnorderedHash(__ra, __rb, " + eqExpr + "))");
                w.Line("return false;");
                w.Close();
                w.Close();
            }
            else
            {
                // Ordered comparison; use array helpers
                if (arr.Rank == 1)
                {
                    w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualArrayRank1<" + elFqn + ", " + cmpName + ">("
                           + leftExpr + " as " + elFqn + "[], "
                           + rightExpr + " as " + elFqn + "[], "
                           + "new " + cmpName + "(" + (elemCustomVar ?? "") + "), context))");
                    w.Line("return false;");
                    w.Close();
                }
                else
                {
                    w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualArray<" + elFqn + ", " + cmpName + ">("
                           + "(Array?)" + leftExpr + ", "
                           + "(Array?)" + rightExpr + ", "
                           + "new " + cmpName + "(" + (elemCustomVar ?? "") + "), context))");
                    w.Line("return false;");
                    w.Close();
                }
            }

            w.Line();
            return;
        }


        if (GenCommon.TryGetDictionaryInterface(equalityMember.Type, out var keyT, out var valT))
        {
            var kFqn = keyT!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var vFqn = valT!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var vKind = GenCommon.GetEffectiveKind(valT, null);

            var valCustomCmpT = GenCommon.GetEffectiveComparerType(valT, deepAttr);
            string? valCustomVar = null;
            if (valCustomCmpT is not null)
            {
                var cmpFqn = valCustomCmpT.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                valCustomVar = "__cmpV_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" +
                               GenCommon.SanitizeIdentifier(equalityMember.Name);
                w.Line("var " + valCustomVar + " = (System.Collections.Generic.IEqualityComparer<" + vFqn +
                       ">)System.Activator.CreateInstance(typeof(" + cmpFqn + "))!");
            }

            var cmpAny = GenCommon.EnsureComparerStruct(
                emittedComparers, comparerDeclarations, valT, vKind,
                "M_" + GenCommon.SanitizeIdentifier(owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)) +
                "_" +
                equalityMember.Name + "_Val",
                valCustomVar
            );

            w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDictionariesAny<" + kFqn + ", " + vFqn +
                   ", " + cmpAny + ">(" +
                   leftExpr + ", " + rightExpr + ", new " + cmpAny + "(" + (valCustomVar ?? "") + "), context))");
            w.Line("return false;");
            w.Close();
            w.Line();
            return;
        }

        if (GenCommon.TryGetEnumerableInterface(equalityMember.Type, out var elT))
        {
            var elFqn = elT!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var elKind = GenCommon.GetEffectiveKind(elT, null);
            var unordered = GenCommon.ResolveOrderInsensitive(root.OrderInsensitiveCollections, deepAttr, elT, owner);

            var elemCustomCmpT = GenCommon.GetEffectiveComparerType(elT, deepAttr);
            string? elemCustomVar = null;
            if (elemCustomCmpT is not null)
            {
                var cmpFqn = elemCustomCmpT.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                elemCustomVar = "__cmpE_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" +
                                GenCommon.SanitizeIdentifier(equalityMember.Name);
                w.Line("var " + elemCustomVar + " = (System.Collections.Generic.IEqualityComparer<" + elFqn +
                       ">)System.Activator.CreateInstance(typeof(" + cmpFqn + "))!;");
            }

            if (unordered &&
                GenCommon.TryGetKeySpec(elT, deepAttr, root, out var keyTypeFqn2, out var keyExprFmt2) &&
                GenCommon.IsUserObjectType(elT))
            {
                var la = "__seqA_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" +
                         GenCommon.SanitizeIdentifier(equalityMember.Name);
                var lb = "__seqB_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" +
                         GenCommon.SanitizeIdentifier(equalityMember.Name);
                var da = "__dictA_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" +
                         GenCommon.SanitizeIdentifier(equalityMember.Name);
                var db = "__dictB_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" +
                         GenCommon.SanitizeIdentifier(equalityMember.Name);
                var tmpA = "__eA_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" +
                           GenCommon.SanitizeIdentifier(equalityMember.Name);
                var tmpB = "__eB_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" +
                           GenCommon.SanitizeIdentifier(equalityMember.Name);
                var cmpName = GenCommon.EnsureComparerStruct(
                    emittedComparers, comparerDeclarations, elT, elKind,
                    "M_" +
                    GenCommon.SanitizeIdentifier(owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)) +
                    "_" +
                    equalityMember.Name,
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

            if (unordered && GenCommon.IsHashFriendly(elT))
            {
                var la = "__seqA_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" +
                         GenCommon.SanitizeIdentifier(equalityMember.Name);
                var lb = "__seqB_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" +
                         GenCommon.SanitizeIdentifier(equalityMember.Name);
                var eqExpr = GenCommon.GetEqualityComparerExprForHash(elT, "context", elemCustomVar);

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
                var cmpName = GenCommon.EnsureComparerStruct(
                    emittedComparers, comparerDeclarations, elT, elKind,
                    "M_" +
                    GenCommon.SanitizeIdentifier(owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)) +
                    "_" +
                    equalityMember.Name,
                    elemCustomVar);

                if (unordered)
                {
                    w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualSequencesUnordered<" + elFqn +
                           ", " + cmpName + ">("
                           + leftExpr + " as System.Collections.Generic.IEnumerable<" + elFqn + ">, "
                           + rightExpr + " as System.Collections.Generic.IEnumerable<" + elFqn + ">, "
                           + "new " + cmpName + "(" + (elemCustomVar ?? "") + "), context))");
                    w.Line("return false;");
                    w.Close();
                }
                else
                {
                    var roListA = "__roA_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" +
                                  GenCommon.SanitizeIdentifier(equalityMember.Name);
                    var roListB = "__roB_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" +
                                  GenCommon.SanitizeIdentifier(equalityMember.Name);

                    w.Line("var " + roListA + " = " + leftExpr + " as System.Collections.Generic.IReadOnlyList<" +
                           elFqn + ">;");
                    w.Line("var " + roListB + " = " + rightExpr + " as System.Collections.Generic.IReadOnlyList<" +
                           elFqn + ">;");
                    w.Open("if (" + roListA + " is not null && " + roListB + " is not null)");
                    w.Open("if (" + roListA + ".Count != " + roListB + ".Count)");
                    w.Line("return false;");
                    w.Close();
                    w.Line("var __cmp = new " + cmpName + "(" + (elemCustomVar ?? "") + ");");
                    w.Line("var __n = " + roListA + ".Count;");
                    w.Open("for (int __i = 0; __i < __n; __i++)");
                    w.Open("if (!__cmp.Invoke(" + roListA + "[__i], " + roListB + "[__i], context))");
                    w.Line("return false;");
                    w.Close();
                    w.Close();
                    w.Line("goto __SEQ_OK_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" +
                           GenCommon.SanitizeIdentifier(equalityMember.Name) + ";");
                    w.Close();
                    w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualSequencesOrdered<" + elFqn +
                           ", " + cmpName + ">("
                           + leftExpr + " as System.Collections.Generic.IEnumerable<" + elFqn + ">, "
                           + rightExpr + " as System.Collections.Generic.IEnumerable<" + elFqn + ">, "
                           + "new " + cmpName + "(" + (elemCustomVar ?? "") + "), context))");
                    w.Line("return false;");
                    w.Close();

                    w.Line("__SEQ_OK_" + GenCommon.SanitizeIdentifier(owner.Name) + "_" +
                           GenCommon.SanitizeIdentifier(equalityMember.Name) + ":;");
                }

                w.Line();
                return;
            }


            w.Line();
            return;
        }

        if ((equalityMember.Type.TypeKind == TypeKind.Interface ||
             equalityMember.Type is INamedTypeSymbol { IsAbstract: true })
            && !(GenCommon.TryGetDictionaryInterface(equalityMember.Type, out _, out _) ||
                 GenCommon.TryGetEnumerableInterface(equalityMember.Type, out _)))
        {
            var declFqn = equalityMember.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            w.Open("if (!DeepEqual.Generator.Shared.ComparisonHelpers.DeepComparePolymorphic<" + declFqn + ">(" +
                   leftExpr + ", " + rightExpr + ", context))");
            w.Line("return false;");
            w.Close();
            w.Line();
            return;
        }

        if (equalityMember.Type.SpecialType == SpecialType.System_Object)
        {
            w.Open("if (!DeepEqual.Generator.Shared.DynamicDeepComparer.AreEqualDynamic(" + leftExpr + ", " +
                   rightExpr + ", context))");
            w.Line("return false;");
            w.Close();
            w.Line();
            return;
        }

        if (equalityMember.Type is INamedTypeSymbol nts && GenCommon.IsUserObjectType(nts)
                                                        && !(nts.IsAbstract || nts.TypeKind == TypeKind.Interface))
        {
            var helperExpr = GenCommon.GetHelperMethodName(nts) + "(" + leftExpr + ", " + rightExpr + ", context)";
            w.Open("if (!" + helperExpr + ")");
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

    private void EmitNullableValueCompare_NoCustom(CodeWriter w, string leftExpr, string rightExpr,
        ITypeSymbol valueType)
    {
        if (GenCommon.TryEmitWellKnownStructCompare(w, leftExpr + ".Value", rightExpr + ".Value", valueType)) return;

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

        if (GenCommon.IsNumericWithTolerance(valueType))
        {
            var call = GenCommon.GetNumericCall(valueType, leftExpr + ".Value", rightExpr + ".Value", "context");
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

        if (valueType is INamedTypeSymbol namedTypeSymbol && GenCommon.IsUserObjectType(namedTypeSymbol)
                                                          && !(namedTypeSymbol.IsAbstract ||
                                                               namedTypeSymbol.TypeKind == TypeKind.Interface))
        {
            var helperExpr = GenCommon.GetHelperMethodName(namedTypeSymbol) + "(" + leftExpr + ".Value, " + rightExpr +
                             ".Value, context)";
            w.Open("if (!" + helperExpr + ")");
            w.Line("return false;");
            w.Close();
            return;
        }


        w.Open("if (!object.Equals(" + leftExpr + ".Value, " + rightExpr + ".Value))");
        w.Line("return false;");
        w.Close();
    }

}

internal sealed class CodeWriter
{
    private readonly StringBuilder _buffer = new();
    private int _indent;

    public void Line(string text = "")
    {
        if (!string.IsNullOrEmpty(text))
        {
            _buffer.Append(' ', _indent * 4);
            _buffer.AppendLine(text);
        }
        else
        {
            _buffer.AppendLine();
        }
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

internal readonly record struct DiffDeltaMemberSymbol(string Name, ITypeSymbol Type, ISymbol Symbol);

internal sealed record DiffDeltaTypeSchema(
    IReadOnlyList<string> IncludeMembers,
    IReadOnlyList<string> IgnoreMembers,
    CompareKind DefaultKind,
    bool DefaultOrderInsensitive,
    bool DefaultDeltaShallow,
    bool DefaultDeltaSkip);

internal sealed class DiffDeltaEmitter
{
    private const string DeepCompareAttributeName = GenCommon.DeepCompareAttributeMetadataName;

    private readonly Dictionary<INamedTypeSymbol, Dictionary<string, int>> _stableIndexTables =
        new(SymbolEqualityComparer.Default);


    private bool _useStableIndices;

    public void EmitForRoot(SourceProductionContext spc, DiffDeltaTarget root, string? hintOverride = null)
    {
        Diagnostics.DiagnosticPass(spc, root.Type);


        _useStableIndices = root.GenerateDelta &&
                            (root.StableMode == StableMemberIndexMode.On ||
                             root.StableMode == StableMemberIndexMode.Auto);
        _stableIndexTables.Clear();
        var ns = root.Type.ContainingNamespace.IsGlobalNamespace
            ? null
            : root.Type.ContainingNamespace.ToDisplayString();

        var rootFqn = root.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var helperClass = root.Type.Name + "DeepOps";
        var hintName = hintOverride ?? GenCommon.SanitizeFileName(rootFqn + "_DeepOps.g.cs");

        var reachable = BuildReachableTypeClosure(root);

        var w = new CodeWriter();
        w.Line("// <auto-generated/>");
        w.Line("#pragma warning disable");
        w.Line("using System;");
        w.Line("using System.Collections;");
        w.Line("using System.Collections.Generic;");
        w.Line("using DeepEqual.Generator.Shared;");
        w.Line();

        if (ns is not null) w.Open("namespace " + ns);

        var accessibility = root.IncludeInternals || root.Type.DeclaredAccessibility != Accessibility.Public
            ? "internal"
            : "public";

        var typeParams = root.Type.Arity > 0
            ? "<" + string.Join(",", root.Type.TypeArguments.Select(a => a.Name)) + ">"
            : "";

        w.Open(accessibility + " static class " + helperClass + typeParams);

        w.Open("static " + helperClass + "()");
        foreach (var t in reachable.OrderBy(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                     StringComparer.Ordinal))
        {
            var fqn = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (root.GenerateDiff)
                w.Line("GeneratedHelperRegistry.RegisterDiff<" + fqn + ">((l, r, c) => TryGetDiff__" +
                       GenCommon.SanitizeIdentifier(fqn) + "(l, r, c));");
            if (root.GenerateDelta)
                w.Line("GeneratedHelperRegistry.RegisterDelta<" + fqn + ">(ComputeDelta__" +
                       GenCommon.SanitizeIdentifier(fqn) + ", ApplyDelta__" + GenCommon.SanitizeIdentifier(fqn) + ");");
        }

        w.Close();
        w.Line();

        EmitRootApis(w, root.Type, root.CycleTrackingEnabled, root.GenerateDiff, root.GenerateDelta);

        foreach (var t in reachable.OrderBy(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                     StringComparer.Ordinal)) EmitImplementationsForType(w, t, root);

        w.Close();

        string OpenGenericSuffix(int arity)
        {
            if (arity <= 0) return "";

            if (arity == 1) return "<>";

            return "<" + new string(',', arity - 1) + ">";
        }

        var openSuffix = OpenGenericSuffix(root.Type.Arity);
        var initTypeName = "__" + GenCommon.SanitizeIdentifier(helperClass) + "_ModuleInit_" +
                           Math.Abs(root.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).GetHashCode());

        w.Open("internal static class " + initTypeName);
        w.Line("[System.Runtime.CompilerServices.ModuleInitializer]");
        w.Open("internal static void Init()");
        w.Line("System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(" + helperClass + openSuffix +
               ").TypeHandle);");
        w.Close();
        w.Close();
        if (ns is not null) w.Close();

        spc.AddSource(hintName, w.ToString());

        if (HasDeltaTrack(root.Type)) EmitDeltaTrackPart(spc, root.Type, root);
    }

    private static void EmitRootApis(
        CodeWriter w,
        INamedTypeSymbol rootType,
        bool cycleTrackingEnabled,
        bool generateDiff,
        bool generateDelta)
    {
        var fqn = rootType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var id = GenCommon.SanitizeIdentifier(fqn);
        var nullSuffix = rootType.IsValueType ? "" : "?";

        w.Open(
            $"public static bool AreDeepEqual({fqn}{nullSuffix} left, {fqn}{nullSuffix} right, DeepEqual.Generator.Shared.ComparisonContext context)");
        if (!rootType.IsValueType)
        {
            w.Line("if (object.ReferenceEquals(left, right)) return true;");
            w.Line("if (left is null || right is null) return false;");
        }

        if (generateDiff)
        {
            if (cycleTrackingEnabled && !rootType.IsValueType)
            {
                w.Line("if (!context.Enter(left!, right!)) return true;");
                w.Open("try");
            }

            w.Line($"var r = TryGetDiff__{id}(left, right, context);");
            w.Line("return !r.hasDiff;");
            if (cycleTrackingEnabled && !rootType.IsValueType)
            {
                w.Close();
                w.Open("finally");
                w.Line("context.Exit(left!, right!);");
                w.Close();
            }
        }
        else
        {
            if (cycleTrackingEnabled && !rootType.IsValueType)
            {
                w.Line("if (!context.Enter(left!, right!)) return true;");
                w.Open("try");
            }

            w.Line("// Fallback: deep-compare via polymorphic helper");
            w.Line("return DeepEqual.Generator.Shared.ComparisonHelpers.DeepComparePolymorphic(left, right, context);");
            if (cycleTrackingEnabled && !rootType.IsValueType)
            {
                w.Close();
                w.Open("finally");
                w.Line("context.Exit(left!, right!);");
                w.Close();
            }
        }

        w.Close();
        w.Line();

        if (generateDiff)
        {
            w.Open(
                $"public static (bool hasDiff, DeepEqual.Generator.Shared.Diff<{fqn}> diff) GetDiff({fqn}{nullSuffix} left, {fqn}{nullSuffix} right, DeepEqual.Generator.Shared.ComparisonContext context)");
            w.Line($"return TryGetDiff__{id}(left, right, context);");
            w.Close();
            w.Line();
        }

        if (generateDelta)
        {
            w.Line(
                "/// <summary>Computes a delta (patch) from <paramref name=\"left\"/> to <paramref name=\"right\"/>.</summary>");
            w.Line("/// <remarks>");
            w.Line("/// Collections policy:");
            w.Line("/// <list type=\"bullet\">");
            w.Line(
                "/// <item><description><b>Arrays</b>: treated as replace-on-change. Any detected difference emits a single <c>SetMember</c> for that member.</description></item>");
            w.Line(
                "/// <item><description><b>IList&lt;T&gt;</b>: granular sequence ops (<c>SeqReplaceAt</c>/<c>SeqAddAt</c>/<c>SeqRemoveAt</c>) are emitted.</description></item>");
            w.Line(
                "/// <item><description><b>IDictionary</b>/<b>IReadOnlyDictionary</b>: granular key ops (<c>DictSet</c>/<c>DictRemove</c>/<c>DictNested</c>) are emitted.</description></item>");
            w.Line("/// </list>");
            w.Line("/// This mirrors <see cref=\"ApplyDelta(ref " + fqn + nullSuffix +
                   ", DeepEqual.Generator.Shared.DeltaDocument)\"/> behavior, where arrays are not patched item-by-item.</remarks>");
            w.Open(
                $"public static DeepEqual.Generator.Shared.DeltaDocument ComputeDelta({fqn}{nullSuffix} left, {fqn}{nullSuffix} right, DeepEqual.Generator.Shared.ComparisonContext context)");
            w.Line("var doc = new DeepEqual.Generator.Shared.DeltaDocument();");
            w.Line("var writer = new DeepEqual.Generator.Shared.DeltaWriter(doc);");
            w.Line($"ComputeDelta__{id}(left, right, context, ref writer);");
            w.Line("return doc;");
            w.Close();
            w.Line();

            w.Line("/// <summary>Applies a previously computed delta to <paramref name=\"target\"/>.</summary>");
            w.Line("/// <remarks>");
            w.Line("/// Collections policy during application:");
            w.Line("/// <list type=\"bullet\">");
            w.Line(
                "/// <item><description><b>Arrays</b>: always replaced as a whole when a <c>SetMember</c> op is present. Sequence ops are ignored for arrays.</description></item>");
            w.Line(
                "/// <item><description><b>IList&lt;T&gt;</b>: sequence ops are applied in-place (replace/add/remove).</description></item>");
            w.Line(
                "/// <item><description><b>IDictionary</b>/<b>IReadOnlyDictionary</b>: key ops are applied (set/remove) and nested deltas are applied when present.</description></item>");
            w.Line("/// </list>");
            w.Line("/// This matches the generator’s policy in delta computation.</remarks>");
            w.Open(
                $"public static void ApplyDelta(ref {fqn}{nullSuffix} target, DeepEqual.Generator.Shared.DeltaDocument delta)");
            w.Line("var reader = new DeepEqual.Generator.Shared.DeltaReader(delta);");
            w.Line($"ApplyDelta__{id}(ref target, ref reader);");
            w.Close();
            w.Line();
        }

        w.Open($"public static bool AreDeepEqual({fqn}{nullSuffix} left, {fqn}{nullSuffix} right)");
        w.Line("var ctx = new DeepEqual.Generator.Shared.ComparisonContext();");
        w.Line("return AreDeepEqual(left, right, ctx);");
        w.Close();
        w.Line();

        if (generateDiff)
        {
            w.Open(
                $"public static (bool hasDiff, DeepEqual.Generator.Shared.Diff<{fqn}> diff) GetDiff({fqn}{nullSuffix} left, {fqn}{nullSuffix} right)");
            w.Line("var ctx = new DeepEqual.Generator.Shared.ComparisonContext();");
            w.Line("return GetDiff(left, right, ctx);");
            w.Close();
            w.Line();
        }

        if (generateDelta)
        {
            w.Open(
                $"public static DeepEqual.Generator.Shared.DeltaDocument ComputeDelta({fqn}{nullSuffix} left, {fqn}{nullSuffix} right)");
            w.Line("var ctx = new DeepEqual.Generator.Shared.ComparisonContext();");
            w.Line("return ComputeDelta(left, right, ctx);");
            w.Close();
            w.Line();
        }
    }

    private static bool IsExpando(ITypeSymbol t)
    {
        return t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Dynamic.ExpandoObject";
    }

    private void EmitDeltaTrackPart(SourceProductionContext spc, INamedTypeSymbol type, DiffDeltaTarget root)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var fqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var typeParams = type.Arity > 0 ? "<" + string.Join(",", type.TypeArguments.Select(a => a.Name)) + ">" : "";
        var hint = GenCommon.SanitizeFileName(fqn + ".__DeltaTrack.g.cs");

        var threadSafe = false;
        foreach (var a in type.GetAttributes())
        {
            var full = a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (full == "DeepEqual.Generator.Shared.DeltaTrackAttribute")
                foreach (var kv in a.NamedArguments)
                    if (kv is { Key: "ThreadSafe", Value.Value: bool b })
                        threadSafe = b;
        }

        var schema = GetTypeSchema(type);
        var members = OrderMembers(EnumerateMembers(type, root.IncludeInternals, root.IncludeBaseMembers, schema))
            .ToArray();

        var w = new CodeWriter();
        w.Line("// <auto-generated/>");
        w.Line("#pragma warning disable");
        w.Line("using System;");
        w.Line("using System.Numerics;");
        w.Line("using System.Runtime.CompilerServices;");
        w.Line("using System.Threading;");

        if (ns is not null) w.Open("namespace " + ns);

        var decl = type.DeclaredAccessibility == Accessibility.Public ? "public" : "internal";
        w.Open(decl + " partial class " + type.Name + typeParams);

        w.Line("private long __dirty0;");
        w.Line("private long[]? __dirtyEx;");
        w.Line("internal const int __DirtyWordShift = 6;");
        w.Line("internal const int __DirtyWordMask = 63;");

        if (threadSafe)
        {
            w.Open("private static long __AtomicOr(ref long location, long mask)");
            w.Line("long initial, computed;");
            w.Open("do");
            w.Line("initial = Volatile.Read(ref location);");
            w.Line("computed = initial | mask;");
            w.Close();
            w.Line("while (Interlocked.CompareExchange(ref location, computed, initial) != initial);");
            w.Line("return computed;");
            w.Close();

            w.Open("private static long __AtomicAnd(ref long location, long mask)");
            w.Line("long initial, computed;");
            w.Open("do");
            w.Line("initial = Volatile.Read(ref location);");
            w.Line("computed = initial & mask;");
            w.Close();
            w.Line("while (Interlocked.CompareExchange(ref location, computed, initial) != initial);");
            w.Line("return computed;");
            w.Close();
        }

        w.Open("[MethodImpl(MethodImplOptions.AggressiveInlining)] internal void __MarkDirty(int bit)");
        w.Open("if ((uint)bit <= 63)");
        if (threadSafe)
            w.Line("__AtomicOr(ref __dirty0, 1L << bit);");
        else
            w.Line("__dirty0 |= 1L << bit;");

        w.Close();
        w.Open("else");
        w.Line("var word = bit >> __DirtyWordShift;");
        w.Line("var idx = word - 1;");
        w.Line("var arr = __dirtyEx;");
        w.Open("if (arr is null || idx >= arr.Length)");
        w.Line("Array.Resize(ref __dirtyEx, arr is null ? Math.Max(1, idx + 1) : Math.Max(arr.Length * 2, idx + 1));");
        w.Close();
        if (threadSafe)
            w.Line("__AtomicOr(ref __dirtyEx![idx], 1L << (bit & __DirtyWordMask));");
        else
            w.Line("__dirtyEx![idx] |= 1L << (bit & __DirtyWordMask);");

        w.Close();
        w.Close();
        w.Open("[MethodImpl(MethodImplOptions.AggressiveInlining)] internal bool __TryPopNextDirty(out int bit)");
        w.Line("var w0 = Volatile.Read(ref __dirty0);");
        w.Open("if (w0 != 0)");
        w.Line("var u = (ulong)w0;");
        w.Line("var tz = BitOperations.TrailingZeroCount(u);");
        if (threadSafe)
            w.Line("__AtomicAnd(ref __dirty0, ~(1L << tz));");
        else
            w.Line("__dirty0 &= ~(1L << tz);");

        w.Line("bit = tz;");
        w.Line("return true;");
        w.Close();
        w.Line("var ex = __dirtyEx;");
        w.Open("if (ex is not null)");
        w.Open("for (int i = 0; i < ex.Length; i++)");
        w.Line("var wi = Volatile.Read(ref ex[i]);");
        w.Open("if (wi != 0)");
        w.Line("var u2 = (ulong)wi;");
        w.Line("var tz2 = BitOperations.TrailingZeroCount(u2);");
        if (threadSafe)
            w.Line("__AtomicAnd(ref ex[i], ~(1L << tz2));");
        else
            w.Line("ex[i] &= ~(1L << tz2);");

        w.Line("bit = ((i + 1) << __DirtyWordShift) + tz2;");
        w.Line("return true;");
        w.Close();
        w.Close();
        w.Close();
        w.Line("bit = -1;");
        w.Line("return false;");
        w.Close();
        w.Open("[MethodImpl(MethodImplOptions.AggressiveInlining)] internal void __ClearDirtyBit(int bit)");
        w.Open("if ((uint)bit <= 63)");
        if (threadSafe)
            w.Line("__AtomicAnd(ref __dirty0, ~(1L << bit));");
        else
            w.Line("__dirty0 &= ~(1L << bit);");

        w.Close();
        w.Open("else");
        w.Line("var word = bit >> __DirtyWordShift;");
        w.Line("var idx = word - 1;");
        w.Open("if (__dirtyEx is null || idx >= __dirtyEx.Length)");
        w.Line("return;");
        w.Close();
        if (threadSafe)
            w.Line("__AtomicAnd(ref __dirtyEx![idx], ~(1L << (bit & __DirtyWordMask)));");
        else
            w.Line("__dirtyEx![idx] &= ~(1L << (bit & __DirtyWordMask));");

        w.Close();
        w.Close();
        w.Open("[MethodImpl(MethodImplOptions.AggressiveInlining)] internal bool __HasAnyDirty()");
        w.Line("if (Volatile.Read(ref __dirty0) != 0) return true;");
        w.Line("var ex2 = __dirtyEx;");
        w.Open("if (ex2 is not null)");
        w.Open("for (int i = 0; i < ex2.Length; i++)");
        w.Line("if (Volatile.Read(ref ex2[i]) != 0) return true;");
        w.Close();
        w.Close();
        w.Line("return false;");
        w.Close();
        for (var i = 0; i < members.Length; i++)
            w.Line($"internal const int __Bit_{GenCommon.SanitizeIdentifier(members[i].Name)} = {i};");

        w.Close();
        if (ns is not null) w.Close();

        spc.AddSource(hint, w.ToString());
    }

    private static bool HasDeltaTrack(INamedTypeSymbol type)
    {
        const string deltaTrackAttr = "DeepEqual.Generator.Shared.DeltaTrackAttribute";
        foreach (var a in type.GetAttributes())
        {
            var n1 = a.AttributeClass?.ToDisplayString();
            var n2 = a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (n1 == deltaTrackAttr || n2 == deltaTrackAttr) return true;
        }

        return false;
    }
    private void EmitImplementationsForType(CodeWriter w, INamedTypeSymbol type, DiffDeltaTarget root)
    {
        var fqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var id = GenCommon.SanitizeIdentifier(fqn);
        var nullSuffix = type.IsValueType ? "" : "?";
        var schema = GetTypeSchema(type);
        var deltaTracked = HasDeltaTrack(type);

        // Stable indices table (if needed for delta)
        if (_useStableIndices && !_stableIndexTables.ContainsKey(type))
        {
            var orderedStable = OrderMembers(EnumerateMembers(type, root.IncludeInternals, root.IncludeBaseMembers, schema)).ToArray();
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < orderedStable.Length; i++) map[orderedStable[i].Name] = i;
            _stableIndexTables[type] = map;
        }

        // -------- DIFF: TryGetDiff__{id} --------
        if (root.GenerateDiff)
        {
            w.Open(
                $"private static (bool hasDiff, DeepEqual.Generator.Shared.Diff<{fqn}> diff) TryGetDiff__{id}({fqn}{nullSuffix} left, {fqn}{nullSuffix} right, DeepEqual.Generator.Shared.ComparisonContext context)");

            if (!type.IsValueType)
            {
                w.Open("if (object.ReferenceEquals(left, right))");
                w.Line($"return (false, DeepEqual.Generator.Shared.Diff<{fqn}>.Empty);");
                w.Close();

                w.Open("if (left is null && right is not null)");
                w.Line($"return (true, DeepEqual.Generator.Shared.Diff<{fqn}>.Replacement(right));");
                w.Close();

                w.Open("if (left is not null && right is null)");
                w.Line($"return (true, DeepEqual.Generator.Shared.Diff<{fqn}>.Replacement(right));");
                w.Close();

                if (root.CycleTrackingEnabled)
                {
                    w.Open("if (!context.Enter(left!, right!))");
                    w.Line($"return (false, DeepEqual.Generator.Shared.Diff<{fqn}>.Empty);");
                    w.Close();
                    w.Open("try");
                }
            }

            w.Line("var changes = new System.Collections.Generic.List<DeepEqual.Generator.Shared.MemberChange>();");

            // Emit per-member DIFF
            foreach (var m in OrderMembers(EnumerateMembers(type, root.IncludeInternals, root.IncludeBaseMembers, schema)))
                EmitMemberDiff(w, type, m, root);

            w.Line(
                $"return changes.Count == 0 ? (false, DeepEqual.Generator.Shared.Diff<{fqn}>.Empty) : (true, DeepEqual.Generator.Shared.Diff<{fqn}>.Members(changes));");

            if (!type.IsValueType && root.CycleTrackingEnabled)
            {
                w.Close();
                w.Open("finally");
                w.Line("context.Exit(left!, right!);");
                w.Close();
            }

            w.Close();
            w.Line();
        }

        // -------- DELTA: ComputeDelta__{id} --------
        if (root.GenerateDelta)
        {
            w.Open(
                $"private static void ComputeDelta__{id}({fqn}{nullSuffix} left, {fqn}{nullSuffix} right, DeepEqual.Generator.Shared.ComparisonContext context, ref DeepEqual.Generator.Shared.DeltaWriter writer)");

            if (!type.IsValueType)
            {
                w.Open("if (object.ReferenceEquals(left, right))");
                w.Line("return;");
                w.Close();

                w.Open("if (left is null && right is not null)");
                w.Line("writer.WriteReplaceObject(right);");
                w.Line("return;");
                w.Close();

                w.Open("if (left is not null && right is null)");
                w.Line("writer.WriteReplaceObject(right);");
                w.Line("return;");
                w.Close();

                if (root.CycleTrackingEnabled)
                {
                    w.Open("if (!context.Enter(left!, right!))");
                    w.Line("return;");
                    w.Close();
                    w.Open("try");
                }
            }

            var ordered = OrderMembers(EnumerateMembers(type, root.IncludeInternals, root.IncludeBaseMembers, schema)).ToArray();

            if (deltaTracked && !type.IsValueType)
            {
                // Dirty-track emission (unchanged from your version; shortened comment)
                w.Line("var __validate = context.Options.ValidateDirtyOnEmit;");
                w.Line("var __r = right;");
                w.Open("if (__r is not null && __r.__HasAnyDirty())");
                foreach (var m in ordered)
                {
                    var idx = GetStableMemberIndex(type, m);
                    w.Line($"bool __emitted_m{idx} = false;");
                }
                w.Open("while (__r.__TryPopNextDirty(out var __bit))");
                w.Open("switch (__bit)");
                for (var idx = 0; idx < ordered.Length; idx++)
                {
                    var mem = ordered[idx];
                    var stable = GetStableMemberIndex(type, mem);
                    var leftExpr = "left." + mem.Name;
                    var rightExpr = "right." + mem.Name;

                    // In your original code, unordered/keyed lists were forced to Set; keep same behavior here
                    var (kind, orderInsensitive, keys, dShallow, dSkip) = ResolveEffectiveSettings(mem);
                    if (!dSkip && (orderInsensitive || keys.Length > 0))
                    {
                        w.Line($"writer.WriteSetMember({stable}, {rightExpr});");
                        w.Line("break;");
                        continue;
                    }

                    w.Open("case " + idx + ":");

                    if (kind == CompareKind.Skip || dSkip)
                    {
                        w.Line($"__emitted_m{stable} = true;");
                        w.Line("break;");
                        w.Close();
                        continue;
                    }

                    if (TryGetDictionaryTypes(mem.Type, out var kType, out var vType))
                    {
                        var kFqn = kType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        var vFqn = vType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        var nestedExpr = IsValueLike(vType) ? "false" : "true";
                        w.Line($"DeepEqual.Generator.Shared.DeltaHelpers.ComputeDictDelta<{kFqn}, {vFqn}>({leftExpr}, {rightExpr}, {stable}, ref writer, {nestedExpr}, context);");
                        w.Line($"__emitted_m{stable} = true;");
                        w.Line("break;");
                        w.Close();
                        continue;
                    }

                    if (TryGetEnumerableElement(mem.Type, out var elemType) && TryGetListInterface(mem.Type, out _))
                    {
                        var (_, ordIns, keyMembers, _, dSk) = ResolveEffectiveSettings(mem);
                        if (!dSk && (ordIns || keyMembers.Length > 0))
                        {
                            w.Line($"writer.WriteSetMember({stable}, {rightExpr});");
                            w.Line($"__emitted_m{stable} = true;");
                            w.Line("break;");
                            w.Close();
                            continue;
                        }

                        var elFqn = elemType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        var deepAttr = GetDeepCompareAttribute(mem.Symbol);
                        var custom = GetEffectiveComparerType(elemType, deepAttr);

                        w.Open("else");
                        if (custom != null)
                        {
                            var cfqn = custom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                            var cmpVar = "__cmpE_" + SanitizeIdentifier(type.Name) + "_" + SanitizeIdentifier(mem.Name);
                            w.Line($"var {cmpVar} = (System.Collections.Generic.IEqualityComparer<{elFqn}>)System.Activator.CreateInstance(typeof({cfqn}))!;");
                            w.Line($"DeepEqual.Generator.Shared.DeltaHelpers.ComputeListDelta<{elFqn}, DeepEqual.Generator.Shared.DelegatingElementComparer<{elFqn}>>({leftExpr}, {rightExpr}, {stable}, ref writer, new DeepEqual.Generator.Shared.DelegatingElementComparer<{elFqn}>({cmpVar}), context);");
                        }
                        else if (IsValueLike(elemType))
                        {
                            w.Line($"DeepEqual.Generator.Shared.DeltaHelpers.ComputeListDelta<{elFqn}, DeepEqual.Generator.Shared.DefaultElementComparer<{elFqn}>>({leftExpr}, {rightExpr}, {stable}, ref writer, new DeepEqual.Generator.Shared.DefaultElementComparer<{elFqn}>(), context);");
                        }
                        else
                        {
                            w.Line($"DeepEqual.Generator.Shared.DeltaHelpers.ComputeListDeltaNested<{elFqn}>({leftExpr}, {rightExpr}, {stable}, ref writer, context);");
                        }
                        w.Line($"__emitted_m{stable} = true;");
                        w.Line("break;");
                        w.Close();
                        w.Close();
                        continue;
                    }

                    if (TryGetEnumerableElement(mem.Type, out _))
                    {
                        w.Line($"writer.WriteSetMember({stable}, {rightExpr});");
                        w.Line($"__emitted_m{stable} = true;");
                        w.Line("break;");
                        w.Close();
                        continue;
                    }

                    if (IsValueLike(mem.Type) || kind == CompareKind.Reference || kind == CompareKind.Shallow || dShallow)
                    {
                        w.Line($"writer.WriteSetMember({stable}, {rightExpr});");
                        w.Line($"__emitted_m{stable} = true;");
                        w.Line("break;");
                        w.Close();
                        continue;
                    }

                    // Concrete class nested delta
                    if (mem.Type is INamedTypeSymbol { IsGenericType: false, IsAnonymousType: false })
                    {
                        var ltmp = $"__l_{stable}";
                        var rtmp = $"__r_{stable}";
                        w.Line($"var {ltmp} = {leftExpr};");
                        w.Line($"var {rtmp} = {rightExpr};");
                        w.Line("var __doc = new DeepEqual.Generator.Shared.DeltaDocument();");
                        w.Line("var __w = new DeepEqual.Generator.Shared.DeltaWriter(__doc);");
                        w.Open($"if (!object.ReferenceEquals({ltmp}, {rtmp}) && {ltmp} is not null && {rtmp} is not null)");
                        w.Line($"var __t = {ltmp}.GetType();");
                        w.Open($"if (object.ReferenceEquals(__t, {rtmp}.GetType()))");
                        w.Line($"GeneratedHelperRegistry.ComputeDeltaSameType(__t, {ltmp}, {rtmp}, context, ref __w);");
                        w.Close();
                        w.Close();
                        w.Open("if (!__doc.IsEmpty)");
                        w.Line($"writer.WriteNestedMember({stable}, __doc);");
                        w.Close();
                        w.Open("else");
                        w.Line($"writer.WriteSetMember({stable}, {rightExpr});");
                        w.Close();
                        w.Line("break;");
                        w.Close();
                        continue;
                    }

                    w.Line($"writer.WriteSetMember({stable}, {rightExpr});");
                    w.Line("break;");
                    w.Close();
                }
                w.Close(); // switch
                w.Close(); // while

                // Post-loop catch-up for unordered/keyed lists (unchanged policy)
                foreach (var mem in ordered)
                {
                    if (TryGetEnumerableElement(mem.Type, out _) && TryGetListInterface(mem.Type, out _))
                    {
                        var (_, ordIns, keyMembers, _, dSkip2) = ResolveEffectiveSettings(mem);
                        if (!dSkip2 && (ordIns || keyMembers.Length > 0))
                        {
                            var stable = GetStableMemberIndex(type, mem);
                            var rightExpr = "right." + mem.Name;
                            w.Open($"if (!__emitted_m{stable})");
                            w.Line($"writer.WriteSetMember({stable}, {rightExpr});");
                            w.Close();
                        }
                    }
                }
                w.Close(); // if tracked
                w.Open("else");
                foreach (var m in ordered) EmitMemberDelta(w, type, m, root);
                w.Close();

            }
            else
            {
                // No dirty-track: just emit member delta for each
                foreach (var m in ordered) EmitMemberDelta(w, type, m, root);
            }

            if (!type.IsValueType && root.CycleTrackingEnabled)
            {
                w.Close();
                w.Open("finally");
                w.Line("context.Exit(left!, right!);");
                w.Close();
            }
            w.Close();
            w.Line();
        }

        // -------- DELTA: ApplyDelta__{id} --------
        if (root.GenerateDelta)
        {
            // Local helpers (inside emitted method scope)
            void EmitInPlaceReplaceForGetOnlyCollection(string targetExpr, string elFqn, string kind)
            {
                switch (kind)
                {
                    case "ISet":
                        w.Line($"var __dobj = {targetExpr};");
                        w.Open($"if (__dobj is System.Collections.Generic.ISet<{elFqn}> __set)");
                        w.Line($"__set.Clear();");
                        w.Open($"if (op.Value is System.Collections.Generic.IEnumerable<{elFqn}> __src)");
                        w.Line($"foreach (var __v in __src) __set.Add(__v);");
                        w.Close(); w.Close();
                        break;

                    case "LinkedList":
                        w.Line($"var __ldst = {targetExpr};");
                        w.Open($"if (__ldst is System.Collections.Generic.LinkedList<{elFqn}> __ll)");
                        w.Line($"__ll.Clear();");
                        w.Open($"if (op.Value is System.Collections.Generic.IEnumerable<{elFqn}> __src)");
                        w.Line($"foreach (var __v in __src) __ll.AddLast(__v);");
                        w.Close(); w.Close();
                        break;

                    case "Queue":
                        w.Line($"var __qdst = {targetExpr};");
                        w.Open($"if (__qdst is System.Collections.Generic.Queue<{elFqn}> __q)");
                        w.Line($"__q.Clear();");
                        w.Open($"if (op.Value is System.Collections.Generic.IEnumerable<{elFqn}> __src)");
                        w.Line($"foreach (var __v in __src) __q.Enqueue(__v);");
                        w.Close(); w.Close();
                        break;

                    case "Stack":
                        w.Line($"var __sdst = {targetExpr};");
                        w.Open($"if (__sdst is System.Collections.Generic.Stack<{elFqn}> __s)");
                        w.Line($"__s.Clear();");
                        w.Open($"if (op.Value is System.Collections.Generic.IEnumerable<{elFqn}> __src)");
                        w.Line($"var __tmp = __src as System.Collections.Generic.IList<{elFqn}> ?? new System.Collections.Generic.List<{elFqn}>(__src);");
                        w.Line($"for (int __i = __tmp.Count - 1; __i >= 0; __i--) __s.Push(__tmp[__i]);");
                        w.Close(); w.Close();
                        break;

                    case "ICollection":
                        w.Line($"var __cdst = {targetExpr};");
                        w.Open($"if (__cdst is System.Collections.Generic.ICollection<{elFqn}> __c)");
                        w.Line($"__c.Clear();");
                        w.Open($"if (op.Value is System.Collections.Generic.IEnumerable<{elFqn}> __src)");
                        w.Line($"foreach (var __v in __src) __c.Add(__v);");
                        w.Close(); w.Close();
                        break;
                }
            }

            string BclKind(ITypeSymbol t, out ITypeSymbol? e)
            {
                e = null;
                if (!TryGetEnumerableElement(t, out var te)) return "";
                e = te;
                var f = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (f.StartsWith("global::System.Collections.Generic.HashSet<")) return "ISet";
                if (f.StartsWith("global::System.Collections.Generic.SortedSet<")) return "ISet";
                if (f.StartsWith("global::System.Collections.Generic.LinkedList<")) return "LinkedList";
                if (f.StartsWith("global::System.Collections.Generic.Queue<")) return "Queue";
                if (f.StartsWith("global::System.Collections.Generic.Stack<")) return "Stack";
                foreach (var i in (t as INamedTypeSymbol)?.AllInterfaces ?? [])
                {
                    var ifqn = i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (ifqn.StartsWith("global::System.Collections.Generic.ISet<")) return "ISet";
                    if (ifqn.StartsWith("global::System.Collections.Generic.ICollection<")) return "ICollection";
                }
                return "";
            }

            w.Open(
                $"private static void ApplyDelta__{id}(ref {fqn}{nullSuffix} target, ref DeepEqual.Generator.Shared.DeltaReader reader)");
            w.Line("var __ops = reader.AsSpan();");

            // presize pass (unchanged)
            var __preByOrdinal = OrderMembers(
                    EnumerateMembers(type, root.IncludeInternals, root.IncludeBaseMembers, schema))
                .Select(m => (ms: m, idx: GetStableMemberIndex(type, m)))
                .ToArray();

            var __hasPresizableMembers =
                __preByOrdinal.Any(t =>
                    (TryGetListInterface(t.ms.Type, out _) && !(t.ms.Type is IArrayTypeSymbol)) ||
                    (TryGetDictionaryTypes(t.ms.Type, out _, out _) && !IsExpando(t.ms.Type)));

            w.Open("if (" + (__hasPresizableMembers ? "true" : "false") + ")");
            foreach (var t2 in __preByOrdinal)
            {
                var m = t2.ms;
                var idx = t2.idx;

                if (TryGetListInterface(m.Type, out _) && m.Type is not IArrayTypeSymbol)
                    w.Line($"int __adds_m{idx} = 0;");
                else if (TryGetDictionaryTypes(m.Type, out _, out _) && !IsExpando(m.Type))
                    w.Line($"int __dictSets_m{idx} = 0;");
            }
            w.Open("for (int __i=0; __i<__ops.Length; __i++)");
            w.Line("ref readonly var __o = ref __ops[__i];");
            w.Open("switch (__o.MemberIndex)");
            foreach (var t2 in __preByOrdinal)
            {
                var m = t2.ms;
                var idx = t2.idx;

                if (TryGetListInterface(m.Type, out _) && m.Type is not IArrayTypeSymbol)
                    w.Line($"case {idx}: if (__o.Kind==DeepEqual.Generator.Shared.DeltaKind.SeqAddAt) __adds_m{idx}++; break;");
                else if (TryGetDictionaryTypes(m.Type, out _, out _) && !IsExpando(m.Type))
                    w.Line($"case {idx}: if (__o.Kind==DeepEqual.Generator.Shared.DeltaKind.DictSet) __dictSets_m{idx}++; break;");
            }
            w.Close(); // switch
            w.Close(); // for

            foreach (var t2 in __preByOrdinal)
            {
                var m = t2.ms;
                var idx = t2.idx;
                var propAccess = "target." + m.Name;

                if (TryGetListInterface(m.Type, out var elTypeForPresize) && m.Type is not IArrayTypeSymbol)
                {
                    var elFqnPS = elTypeForPresize.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    w.Line($"{{ var __obj = {propAccess}; if (__adds_m{idx} > 0 && __obj is System.Collections.Generic.List<{elFqnPS}> __l) __l.EnsureCapacity(__l.Count + __adds_m{idx}); }}");
                }
                else if (TryGetDictionaryTypes(m.Type, out var preKType, out var preVType) && !IsExpando(m.Type))
                {
                    var kFqnPS = preKType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var vFqnPS = preVType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    w.Line($"{{ var __obj = {propAccess}; if (__dictSets_m{idx} > 0 && __obj is System.Collections.Generic.Dictionary<{kFqnPS}, {vFqnPS}> __d) __d.EnsureCapacity(__d.Count + __dictSets_m{idx}); }}");
                }
            }
            w.Close(); // if presize

            // apply pass
            w.Open("for (int __ai=0; __ai<__ops.Length; __ai++)");
            w.Line("ref readonly var op = ref __ops[__ai];");
            w.Open("switch (op.MemberIndex)");

            // ReplaceObject
            w.Open("case -1:");
            w.Open("if (op.Kind == DeepEqual.Generator.Shared.DeltaKind.ReplaceObject)");
            w.Line($"target = ({fqn}{nullSuffix})op.Value;");
            w.Line("return;");
            w.Close();
            w.Line("break;");
            w.Close();

            // Each member
            var byOrdinal = OrderMembers(EnumerateMembers(type, root.IncludeInternals, root.IncludeBaseMembers, schema))
                .Select((ms, i) => (ms, i)).ToArray();

            foreach (var t2 in byOrdinal)
            {
                var member = t2.ms;
                var ordinal = t2.i;
                var memberIdx = GetStableMemberIndex(type, member);
                var propAccess = "target." + member.Name;
                var typeFqn = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var nullableQ = member.Type.IsReferenceType ? "?" : "";
                var hasSetterForMember = member.Symbol is IPropertySymbol __ps2 && __ps2.SetMethod is not null;

                w.Open($"case {memberIdx}:");
                w.Open("switch (op.Kind)");
             
                if (IsExpando(member.Type)) // you already have IsExpando(ITypeSymbol) in this file
                {
                    // emit only the expando cases, then close and continue
                    EmitApplyForExpando(w, propAccess, clearDirty: !type.IsValueType && deltaTracked, ordinal);
                    w.Line("default: { break; }");
                    w.Close();        // switch (op.Kind)
                    w.Line("break;"); // case memberIdx
                    w.Close();
                    continue;         // move to next member
                }

                // SetMember
                w.Open("case DeepEqual.Generator.Shared.DeltaKind.SetMember:");
                if (member.Type is IArrayTypeSymbol)
                {
                    w.Line($"{propAccess} = ({typeFqn}{nullableQ})op.Value;");
                }
                else if (TryGetDictionaryTypes(member.Type, out var kForSet, out var vForSet))
                {
                    var kFqn = kForSet.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var vFqn = vForSet.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    if (hasSetterForMember)
                    {
                        w.Line($"{propAccess} = ({$"global::System.Collections.Generic.Dictionary<{kFqn}, {vFqn}>"}{nullableQ})op.Value;");
                    }
                    else
                    {
                        w.Line($"var __dobj = {propAccess};");
                        w.Open($"if (__dobj is global::System.Collections.Generic.IDictionary<{kFqn}, {vFqn}> __d)");
                        w.Line($"__d.Clear();");
                        w.Open($"if (op.Value is global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.KeyValuePair<{kFqn}, {vFqn}>> __src)");
                        w.Line($"foreach (var __kv in __src) __d[__kv.Key] = __kv.Value;");
                        w.Close(); w.Close();
                    }
                }
                else
                {
                    // handle get-only BCL collections
                    var bcl = BclKind(member.Type, out var elT);
                    if (!hasSetterForMember && !string.IsNullOrEmpty(bcl))
                    {
                        var eFqn = elT!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        EmitInPlaceReplaceForGetOnlyCollection(propAccess, eFqn, bcl);
                    }
                    else
                    {
                        w.Line($"{propAccess} = ({typeFqn})op.Value;");
                    }
                }
                if (!type.IsValueType && deltaTracked) w.Line($"target.__ClearDirtyBit({ordinal});");
                w.Line("break;");
                w.Close(); // SetMember

                // Seq ops
                if (TryGetListInterface(member.Type, out var elType) /* && not array */)
                {
                    var elFqn = elType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    void EmitListOp(string opName, string tmp)
                    {
                        w.Open($"case DeepEqual.Generator.Shared.DeltaKind.{opName}:");
                        w.Line($"object? {tmp} = {propAccess};");
                        w.Line($"DeepEqual.Generator.Shared.DeltaHelpers.ApplyListOpCloneIfNeeded<{elFqn}>(ref {tmp}, in op);");
                        if (hasSetterForMember) w.Line($"{propAccess} = ({typeFqn}){tmp};");
                        if (!type.IsValueType && deltaTracked) w.Line($"target.__ClearDirtyBit({ordinal});");
                        w.Line("break;");
                        w.Close();
                    }

                    EmitListOp("SeqReplaceAt", "__obj_seq_r");
                    EmitListOp("SeqAddAt", "__obj_seq_a");
                    EmitListOp("SeqRemoveAt", "__obj_seq_d");
                    EmitListOp("SeqNestedAt", "__obj_seq_n");
                }

                // Dict ops
                if (TryGetDictionaryTypes(member.Type, out var kType2, out var vType2))
                {
                    var kFqn2 = kType2.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var vFqn2 = vType2.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    void EmitDictOp(string opName, string tmp)
                    {
                        w.Open($"case DeepEqual.Generator.Shared.DeltaKind.{opName}:");
                        w.Line($"object? {tmp} = {propAccess};");
                        w.Line($"DeepEqual.Generator.Shared.DeltaHelpers.ApplyDictOpCloneIfNeeded<{kFqn2}, {vFqn2}>(ref {tmp}, in op);");
                        if (hasSetterForMember) w.Line($"{propAccess} = ({$"global::System.Collections.Generic.Dictionary<{kFqn2}, {vFqn2}>"}){tmp};");
                        if (!type.IsValueType && deltaTracked) w.Line($"target.__ClearDirtyBit({ordinal});");
                        w.Line("break;");
                        w.Close();
                    }

                    EmitDictOp("DictSet", "__obj_dict_set");
                    EmitDictOp("DictRemove", "__obj_dict_rm");
                    EmitDictOp("DictNested", "__obj_dict_n");
                }
                else
                {
                    w.Open("case DeepEqual.Generator.Shared.DeltaKind.DictSet:"); w.Line("break;"); w.Close();
                    w.Open("case DeepEqual.Generator.Shared.DeltaKind.DictRemove:"); w.Line("break;"); w.Close();
                    w.Open("case DeepEqual.Generator.Shared.DeltaKind.DictNested:"); w.Line("break;"); w.Close();
                }

                // Nested member (objects only)
                {
                    bool isPlainRef =
                        member.Type.IsReferenceType &&
                        member.Type.SpecialType != SpecialType.System_String &&
                        !(member.Type is IArrayTypeSymbol) &&
                        !TryGetListInterface(member.Type, out _) &&
                        !TryGetDictionaryTypes(member.Type, out _, out _);

                    if (isPlainRef)
                    {
                        w.Open("case DeepEqual.Generator.Shared.DeltaKind.NestedMember:");
                        w.Line($"object? __obj = {propAccess};");
                        w.Line("var __subReader = new DeepEqual.Generator.Shared.DeltaReader(op.Nested!);");
                        w.Open("if (__obj != null)");
                        w.Line("var __t = __obj.GetType();");
                        w.Line("GeneratedHelperRegistry.TryApplyDeltaSameType(__t, ref __obj, ref __subReader);");
                        w.Close();
                        if (hasSetterForMember) w.Line($"{propAccess} = ({typeFqn}{nullableQ})__obj;");
                        if (!type.IsValueType && deltaTracked) w.Line($"target.__ClearDirtyBit({ordinal});");
                        w.Line("break;");
                        w.Close();
                    }
                }

                w.Line("default: { break; }");
                w.Close(); // switch op.Kind
                w.Line("break;");
                w.Close(); // case memberIdx
            }

            w.Open("default:"); w.Line("break;"); w.Close();
            w.Close(); // switch memberIndex
            w.Close(); // for ops
            w.Close(); // ApplyDelta
            w.Line();
        }
    }
    // Emits the ApplyDelta cases for a property of type global::System.Dynamic.ExpandoObject
    private static void EmitApplyForExpando(CodeWriter w, string propAccess, bool clearDirty, int ordinal)
    {
        // SetMember: assign the Expando directly
        w.Open("case DeepEqual.Generator.Shared.DeltaKind.SetMember:");
        w.Line($"{propAccess} = (global::System.Dynamic.ExpandoObject?)op.Value;");
        if (clearDirty) w.Line($"target.__ClearDirtyBit({ordinal});");
        w.Line("break;");
        w.Close();

        // DictSet/DictRemove/DictNested: Apply op into a temp dict, then copy into a fresh Expando
        string[] dictCases = { "DictSet", "DictRemove", "DictNested" };
        foreach (var k in dictCases)
        {
            w.Open($"case DeepEqual.Generator.Shared.DeltaKind.{k}:");
            w.Line("object? __tmp = " + propAccess + ";");
            w.Line("DeepEqual.Generator.Shared.DeltaHelpers.ApplyDictOpCloneIfNeeded<string, object>(ref __tmp, in op);");
            w.Line("var __src = (System.Collections.Generic.IDictionary<string, object?>)__tmp!;");
            w.Line("var __exp = new global::System.Dynamic.ExpandoObject();");
            w.Line("var __dst = (System.Collections.Generic.IDictionary<string, object?>)__exp;");
            w.Line("__dst.Clear();");
            w.Line("foreach (var __kv in __src) __dst[__kv.Key] = __kv.Value;");
            w.Line($"{propAccess} = __exp;");
            if (clearDirty) w.Line($"target.__ClearDirtyBit({ordinal});");
            w.Line("break;");
            w.Close();
        }
    }

    private void EmitMemberDiff(CodeWriter w, INamedTypeSymbol owner, DiffDeltaMemberSymbol member, DiffDeltaTarget root)
    {
        var idx = GetStableMemberIndex(owner, member);
        var left = "left." + member.Name;
        var right = "right." + member.Name;

        var (effKind, orderInsensitive, _, deltaShallow, deltaSkip) = ResolveEffectiveSettings(member);
        if (effKind == CompareKind.Skip || deltaSkip) return;

        // Arrays: replace-on-change (keep DIFF simple for arrays)
        if (member.Type is IArrayTypeSymbol arr)
        {
            var el = arr.ElementType;
            var elFqn = el.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            var elemCmpType = $"DeepEqual.Generator.Shared.DefaultElementComparer<{elFqn}>";
            var elemCmpCtor = $"new {elemCmpType}()";

            if (arr.Rank != 1)
            {
                w.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualArray<{elFqn}, {elemCmpType}>((System.Array?){left}, (System.Array?){right}, {elemCmpCtor}, context))");
                w.Line($"changes.Add(new DeepEqual.Generator.Shared.MemberChange({idx}, DeepEqual.Generator.Shared.MemberChangeKind.Set, {right}));");
                w.Close();
                return;
            }
            else
            {
                w.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualArrayRank1<{elFqn}, {elemCmpType}>({left} as {elFqn}[], {right} as {elFqn}[], {elemCmpCtor}, context))");
                w.Line($"changes.Add(new DeepEqual.Generator.Shared.MemberChange({idx}, DeepEqual.Generator.Shared.MemberChangeKind.Set, {right}));");
                w.Close();
                return;
            }
        }

        // Value-like (incl. string/double/float/decimal/Nullable<>, Memory<T>)
        if (IsValueLike(member.Type))
        {
            var eq = GetValueLikeEqualsInvocation(member.Type, left, right);
            w.Open($"if (!({eq}))");
            w.Line($"changes.Add(new DeepEqual.Generator.Shared.MemberChange({idx}, DeepEqual.Generator.Shared.MemberChangeKind.Set, {right}));");
            w.Close();
            return;
        }

        // Ref-equality only
        if (effKind == CompareKind.Reference && member.Type.IsReferenceType)
        {
            w.Open($"if (!object.ReferenceEquals({left}, {right}))");
            w.Line($"changes.Add(new DeepEqual.Generator.Shared.MemberChange({idx}, DeepEqual.Generator.Shared.MemberChangeKind.Set, {right}));");
            w.Close();
            return;
        }

        // Shallow equality
        if (effKind == CompareKind.Shallow)
        {
            var tfqn = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            w.Open($"if (!System.Collections.Generic.EqualityComparer<{tfqn}>.Default.Equals({left}, {right}))");
            w.Line($"changes.Add(new DeepEqual.Generator.Shared.MemberChange({idx}, DeepEqual.Generator.Shared.MemberChangeKind.Set, {right}));");
            w.Close();
            return;
        }

        // deltaShallow: single deep polymorphic equality check
        if (deltaShallow)
        {
            w.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.DeepComparePolymorphic({left}, {right}, context))");
            w.Line($"changes.Add(new DeepEqual.Generator.Shared.MemberChange({idx}, DeepEqual.Generator.Shared.MemberChangeKind.Set, {right}));");
            w.Close();
            return;
        }

        // ✅ Dictionaries → build DeltaDocument and emit CollectionOps
        if (TryGetDictionaryTypes(member.Type, out var kType, out var vType))
        {
            var kFqn = kType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var vFqn = vType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var allowNested = IsValueLike(vType) ? "false" : "true"; // nested only when values are not value-like

            var la = $"__dictA_{SanitizeIdentifier(owner.Name)}_{SanitizeIdentifier(member.Name)}";
            var lb = $"__dictB_{SanitizeIdentifier(owner.Name)}_{SanitizeIdentifier(member.Name)}";

            // Bind as object to avoid CS8121 for sealed types like ExpandoObject
            w.Line($"object {la} = {left};");
            w.Line($"object {lb} = {right};");

            w.Open($"if (!object.ReferenceEquals({la}, {lb}))");
            w.Open($"if ({la} is null || {lb} is null)");
            w.Line($"changes.Add(new DeepEqual.Generator.Shared.MemberChange({idx}, DeepEqual.Generator.Shared.MemberChangeKind.Set, {right}));");
            w.Close();
            w.Open("else");
            w.Line("var __doc = new DeepEqual.Generator.Shared.DeltaDocument();");
            w.Line("var __w = new DeepEqual.Generator.Shared.DeltaWriter(__doc);");

            // Try IDictionary<,> first (covers ExpandoObject)
            w.Open($"if ({la} is System.Collections.Generic.IDictionary<{kFqn}, {vFqn}> __rwa && {lb} is System.Collections.Generic.IDictionary<{kFqn}, {vFqn}> __rwb)");
            w.Line($"DeepEqual.Generator.Shared.DeltaHelpers.ComputeDictDelta<{kFqn}, {vFqn}>(__rwa, __rwb, 0, ref __w, {allowNested}, context);");
            w.Close();

            // Then IReadOnlyDictionary<,>
            w.Open($"else if ({la} is System.Collections.Generic.IReadOnlyDictionary<{kFqn}, {vFqn}> __roa && {lb} is System.Collections.Generic.IReadOnlyDictionary<{kFqn}, {vFqn}> __rob)");
            w.Line($"DeepEqual.Generator.Shared.DeltaHelpers.ComputeReadOnlyDictDelta<{kFqn}, {vFqn}>(__roa, __rob, 0, ref __w, {allowNested}, context);");
            w.Close();

            w.Open("else");
            w.Line($"changes.Add(new DeepEqual.Generator.Shared.MemberChange({idx}, DeepEqual.Generator.Shared.MemberChangeKind.Set, {right}));");
            w.Line("goto __end_dict_" + idx + ";");
            w.Close();

            w.Open("if (!__doc.IsEmpty)");
            w.Line($"changes.Add(new DeepEqual.Generator.Shared.MemberChange({idx}, DeepEqual.Generator.Shared.MemberChangeKind.CollectionOps, __doc));");
            w.Close();

            w.Line("__end_dict_" + idx + ": ;");
            w.Close(); // else non-null
            w.Close(); // if !refEq
            return;
        }

        // ✅ List-like (IList<T> / IReadOnlyList<T> / IEnumerable<T>) → CollectionOps
        if (TryGetEnumerableElement(member.Type, out var elemType) && member.Type is not IArrayTypeSymbol)
        {
            var elFqn = elemType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var la = $"__l_{SanitizeIdentifier(owner.Name)}_{SanitizeIdentifier(member.Name)}";
            var lb = $"__r_{SanitizeIdentifier(owner.Name)}_{SanitizeIdentifier(member.Name)}";

            w.Line($"var {la} = {left};");
            w.Line($"var {lb} = {right};");

            w.Open($"if (!object.ReferenceEquals({la}, {lb}))");
            w.Open($"if ({la} is null || {lb} is null)");
            w.Line($"changes.Add(new DeepEqual.Generator.Shared.MemberChange({idx}, DeepEqual.Generator.Shared.MemberChangeKind.Set, {right}));");
            w.Close(); // null

            // Optional order-insensitive suppression for value-like elements
            if (orderInsensitive && IsValueLike(elemType))
            {
                // local multiset compare without extra helpers
                var mapVar = "__ms_" + idx;
                w.Line($"var __ena_{idx} = {la} as System.Collections.Generic.IEnumerable<{elFqn}>;");
                w.Line($"var __enb_{idx} = {lb} as System.Collections.Generic.IEnumerable<{elFqn}>;");
                w.Open($"if (__ena_{idx} is not null && __enb_{idx} is not null)");
                w.Line($"var {mapVar} = new System.Collections.Generic.Dictionary<{elFqn}, int>();");
                w.Line($"foreach (var __v in __ena_{idx}) {{ {mapVar}.TryGetValue(__v, out var c); {mapVar}[__v] = c + 1; }}");
                w.Line($"bool __eqms_{idx} = true;");
                w.Line($"foreach (var __v in __enb_{idx}) {{ if (!{mapVar}.TryGetValue(__v, out var c) || c == 0) {{ __eqms_{idx} = false; break; }} {mapVar}[__v] = c - 1; }}");
                w.Line($"if (__eqms_{idx}) {{ /* multisets equal -> no change */ goto __end_seq_{idx}; }}");
                w.Close(); // if enumerables

                // fall through to structural ops if not equal as multiset
            }

            // Materialize to IList<T>
            w.Line($"var __ena = {la} as System.Collections.Generic.IEnumerable<{elFqn}>;");
            w.Line($"var __enb = {lb} as System.Collections.Generic.IEnumerable<{elFqn}>;");
            w.Line($"var __la = __ena as System.Collections.Generic.IList<{elFqn}> ?? (__ena is null ? null : new System.Collections.Generic.List<{elFqn}>(__ena));");
            w.Line($"var __lb = __enb as System.Collections.Generic.IList<{elFqn}> ?? (__enb is null ? null : new System.Collections.Generic.List<{elFqn}>(__enb));");

            w.Open("if (__la is null || __lb is null)");
            w.Line($"changes.Add(new DeepEqual.Generator.Shared.MemberChange({idx}, DeepEqual.Generator.Shared.MemberChangeKind.Set, {right}));");
            w.Close();
            w.Open("else");
            w.Line("var __doc = new DeepEqual.Generator.Shared.DeltaDocument();");
            w.Line("var __w = new DeepEqual.Generator.Shared.DeltaWriter(__doc);");
            if (IsValueLike(elemType))
            {
                var cmpType = $"DeepEqual.Generator.Shared.DefaultElementComparer<{elFqn}>";
                w.Line($"DeepEqual.Generator.Shared.DeltaHelpers.ComputeListDelta<{elFqn}, {cmpType}>(__la, __lb, 0, ref __w, new {cmpType}(), context);");
            }
            else
            {
                w.Line($"DeepEqual.Generator.Shared.DeltaHelpers.ComputeListDeltaNested<{elFqn}>(__la, __lb, 0, ref __w, context);");
            }
            w.Open("if (!__doc.IsEmpty)");
            w.Line($"changes.Add(new DeepEqual.Generator.Shared.MemberChange({idx}, DeepEqual.Generator.Shared.MemberChangeKind.CollectionOps, __doc));");
            w.Close();
            w.Close(); // else

            if (orderInsensitive && IsValueLike(elemType))
                w.Line($"__end_seq_{idx}: ;");

            w.Close(); // if !refEq
            return;
        }

        // Objects → try nested typed diff; only emit when non-empty
        {
            var ltmp = $"__l_{idx}";
            var rtmp = $"__r_{idx}";
            w.Line($"var {ltmp} = {left};");
            w.Line($"var {rtmp} = {right};");

            w.Open($"if (object.ReferenceEquals({ltmp}, {rtmp}))");
            w.Line("// no change");
            w.Close();

            w.Open($"else if ({ltmp} is null || {rtmp} is null)");
            w.Line($"changes.Add(new DeepEqual.Generator.Shared.MemberChange({idx}, DeepEqual.Generator.Shared.MemberChangeKind.Set, {right}));");
            w.Close();

            w.Line("else {");
            w.Line($"    var __tL = {ltmp}.GetType();");
            w.Line($"    var __tR = {rtmp}.GetType();");
            w.Open("    if (!object.ReferenceEquals(__tL, __tR))");
            w.Line($"    changes.Add(new DeepEqual.Generator.Shared.MemberChange({idx}, DeepEqual.Generator.Shared.MemberChangeKind.Set, {right}));");
            w.Close();
            w.Line("    else {");
            w.Line("        if (GeneratedHelperRegistry.TryGetDiffSameType(__tL, " + ltmp + ", " + rtmp + ", context, out var __idiff)) {");
            w.Line("            if (!__idiff.IsEmpty) changes.Add(new DeepEqual.Generator.Shared.MemberChange(" + idx + ", DeepEqual.Generator.Shared.MemberChangeKind.Nested, __idiff));");
            w.Line("        } else {");
            w.Open("            if (!DeepEqual.Generator.Shared.ComparisonHelpers.DeepComparePolymorphic(" + ltmp + ", " + rtmp + ", context))");
            w.Line($"            changes.Add(new DeepEqual.Generator.Shared.MemberChange({idx}, DeepEqual.Generator.Shared.MemberChangeKind.Set, {right}));");
            w.Close();
            w.Line("        }");
            w.Line("    }");
            w.Line("}");
        }
    }

    private void EmitMemberDelta(CodeWriter w, INamedTypeSymbol owner, DiffDeltaMemberSymbol member,
        DiffDeltaTarget root)
    {
        var idx = GetStableMemberIndex(owner, member);
        var left = "left." + member.Name;
        var right = "right." + member.Name;
        var hasSetterForMember = member.Symbol is IPropertySymbol __ps && __ps.SetMethod is not null;

        var (effKind, orderInsensitive, keys, deltaShallow, deltaSkip) = ResolveEffectiveSettings(member);
        if (effKind == CompareKind.Skip || deltaSkip) return;

        if (IsValueLike(member.Type))
        {
            var cmp = GetValueLikeEqualsInvocation(member.Type, left, right);
            w.Open($"if (!({cmp}))");
            w.Line($"writer.WriteSetMember({idx}, {right});");
            w.Close();
            return;
        }

        if (effKind == CompareKind.Reference && member.Type.IsReferenceType)
        {
            w.Open($"if (!object.ReferenceEquals({left}, {right}))");
            w.Line($"writer.WriteSetMember({idx}, {right});");
            w.Close();
            return;
        }

        if (effKind == CompareKind.Shallow)
        {
            var tfqn = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            w.Open($"if (!System.Collections.Generic.EqualityComparer<{tfqn}>.Default.Equals({left}, {right}))");
            w.Line($"writer.WriteSetMember({idx}, {right});");
            w.Close();
            return;
        }

        if (deltaShallow)
        {
            w.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.DeepComparePolymorphic({left}, {right}, context))");
            w.Line($"writer.WriteSetMember({idx}, {right});");
            w.Close();
            return;
        }

        // Arrays: replace-on-change; no structural ops
        if (member.Type is IArrayTypeSymbol arrT)
        {
            if (!arrT.IsSZArray || arrT.Rank != 1)
            {
                // multi-d: do not walk, just replace
                w.Open($"if (!object.ReferenceEquals({left}, {right}))");
                w.Open($"if ({left} is null || {right} is null)");
                w.Line($"writer.WriteSetMember({idx}, {right});");
                w.Close();
                w.Open("else");
                w.Open($"if ({left}.Length != {right}.Length)");
                w.Line($"writer.WriteSetMember({idx}, {right});");
                w.Close();
                w.Line($"writer.WriteSetMember({idx}, {right});");
                w.Close();
                w.Close();
                return;
            }

            // rank==1
            var el = arrT.ElementType;
            var elFqn = el.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            w.Open($"if (!object.ReferenceEquals({left}, {right}))");
            w.Open($"if ({left} is null || {right} is null)");
            w.Line($"writer.WriteSetMember({idx}, {right});");
            w.Close();
            w.Open("else");
            w.Open($"if ({left}.Length != {right}.Length)");
            w.Line($"writer.WriteSetMember({idx}, {right});");
            w.Close();
            if (IsValueLike(el))
            {
                w.Open($"for (int __i = 0; __i < {left}.Length; __i++)");
                w.Open($"if (!System.Collections.Generic.EqualityComparer<{elFqn}>.Default.Equals({left}[__i], {right}[__i]))");
                w.Line($"writer.WriteSetMember({idx}, {right});");
                w.Line("break;");
                w.Close();
                w.Close();
            }
            else
            {
                w.Line($"writer.WriteSetMember({idx}, {right});");
            }
            w.Close();
            w.Close();
            return;
        }

        // Dictionary: allow structural ops only if settable or concrete Dictionary<,>, otherwise SetMember or nested-only
        if (TryGetDictionaryTypes(member.Type, out var kType, out var vType))
        {
            var kFqn = kType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var vFqn = vType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            bool isConcreteDict = member.Type is INamedTypeSymbol nd &&
                nd.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                "global::System.Collections.Generic.Dictionary<TKey, TValue>";

            bool allowStructural = hasSetterForMember || isConcreteDict;

            if (!allowStructural)
            {
                // No structural ops at this level; fallback to SetMember (safe and simple)
                w.Line($"writer.WriteSetMember({idx}, {right});");
                return;
            }

            var la = $"__dictA_{SanitizeIdentifier(owner.Name)}_{SanitizeIdentifier(member.Name)}";
            var lb = $"__dictB_{SanitizeIdentifier(owner.Name)}_{SanitizeIdentifier(member.Name)}";
            var nestedExpr = IsValueLike(vType) ? "false" : "true";

            var isExpando =
                member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                "global::System.Dynamic.ExpandoObject";

            var isStringObjectDict =
                kType.SpecialType == SpecialType.System_String &&
                (vType.SpecialType == SpecialType.System_Object ||
                 vType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Object");

            w.Line($"var {la} = {left};");
            w.Line($"var {lb} = {right};");

            w.Open($"if (!object.ReferenceEquals({la}, {lb}))");

            w.Open($"if ({la} is null || {lb} is null)");
            w.Line($"writer.WriteSetMember({idx}, {right});");
            w.Close();

            if (isExpando || isStringObjectDict)
            {
                w.Line($"var __ida = (System.Collections.Generic.IDictionary<string, object?>){la};");
                w.Line($"var __idb = (System.Collections.Generic.IDictionary<string, object?>){lb};");
                w.Line($"DeepEqual.Generator.Shared.DeltaHelpers.ComputeDictDelta<string, object?>(__ida, __idb, {idx}, ref writer, {nestedExpr}, context);");
            }
            else
            {
                w.Open($"else if ({la} is System.Collections.Generic.IReadOnlyDictionary<{kFqn}, {vFqn}> __roa && {lb} is System.Collections.Generic.IReadOnlyDictionary<{kFqn}, {vFqn}> __rob)");
                w.Line($"DeepEqual.Generator.Shared.DeltaHelpers.ComputeReadOnlyDictDelta<{kFqn}, {vFqn}>(__roa, __rob, {idx}, ref writer, {nestedExpr}, context);");
                w.Close();

                w.Open($"else if ({la} is System.Collections.Generic.IDictionary<{kFqn}, {vFqn}> __rwa && {lb} is System.Collections.Generic.IDictionary<{kFqn}, {vFqn}> __rwb)");
                w.Line($"DeepEqual.Generator.Shared.DeltaHelpers.ComputeDictDelta<{kFqn}, {vFqn}>(__rwa, __rwb, {idx}, ref writer, {nestedExpr}, context);");
                w.Close();

                w.Open("else");
                w.Line($"writer.WriteSetMember({idx}, {right});");
                w.Close();
            }

            w.Close();
            return;
        }

        // List: allow structural ops only if settable or concrete List<T>, otherwise nested-only or SetMember
        if (TryGetEnumerableElement(member.Type, out var elemType) && TryGetListInterface(member.Type, out _))
        {
            if (!deltaSkip && (orderInsensitive || keys.Length > 0))
            {
                w.Line($"writer.WriteSetMember({idx}, {right});");
                return;
            }
            var elFqn = elemType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            bool isConcreteList = member.Type is INamedTypeSymbol nl &&
                nl.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                "global::System.Collections.Generic.List<T>";

            bool allowStructural = hasSetterForMember || isConcreteList;

            if (allowStructural)
            {
                if (IsValueLike(elemType))
                {
                    w.Line($"DeepEqual.Generator.Shared.DeltaHelpers.ComputeListDelta<{elFqn}, DeepEqual.Generator.Shared.DefaultElementComparer<{elFqn}>>({left}, {right}, {idx}, ref writer, new DeepEqual.Generator.Shared.DefaultElementComparer<{elFqn}>(), context);");
                }
                else
                {
                    w.Line($"DeepEqual.Generator.Shared.DeltaHelpers.ComputeListDeltaNested<{elFqn}>({left}, {right}, {idx}, ref writer, context);");
                }
            }
            else
            {
                if (IsValueLike(elemType))
                {
                    // read-only shape of value-like: any change -> replace whole
                    w.Open($"if (!object.ReferenceEquals({left}, {right}))");
                    w.Line($"writer.WriteSetMember({idx}, {right});");
                    w.Close();
                }
                else
                {
                    // read-only shape of reference-like: nested-only updates
                    w.Line($"DeepEqual.Generator.Shared.DeltaHelpers.ComputeListDeltaNested<{elFqn}>({left}, {right}, {idx}, ref writer, context);");
                }
            }

            return;
        }

        if (TryGetEnumerableElement(member.Type, out _))
        {
            w.Open($"if (!object.ReferenceEquals({left}, {right}))");
            w.Line($"writer.WriteSetMember({idx}, {right});");
            w.Close();
            return;
        }

        // object member
        {
            var ltmp = "__l_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
            var rtmp = "__r_" + SanitizeIdentifier(owner.Name) + "_" + SanitizeIdentifier(member.Name);
            w.Line($"var {ltmp} = {left};");
            w.Line($"var {rtmp} = {right};");

            w.Open($"if (!DeepEqual.Generator.Shared.ComparisonHelpers.DeepComparePolymorphic({ltmp}, {rtmp}, context))");
            w.Open($"if ({ltmp} is null || {rtmp} is null)");
            w.Line($"writer.WriteSetMember({idx}, {right});");
            w.Close();

            w.Open("else");
            w.Line($"var __tL = {ltmp}.GetType();");
            w.Line($"var __tR = {rtmp}.GetType();");
            w.Open("if (!object.ReferenceEquals(__tL, __tR))");
            w.Line($"writer.WriteSetMember({idx}, {right});");
            w.Close();

            w.Open("else");
            w.Line($"var __scope = writer.BeginNestedMember({idx}, out var __w);");
            w.Line($"GeneratedHelperRegistry.ComputeDeltaSameType(__tL, {ltmp}, {rtmp}, context, ref __w);");
            w.Line("var __had = !__w.Document.IsEmpty;");
            w.Line("__scope.Dispose();");
            w.Open("if (!__had)");
            w.Line($"writer.WriteSetMember({idx}, {right});");
            w.Close();
            w.Close();
            w.Close();
            w.Close();
        }
    }

    private static AttributeData? GetDeepCompareAttribute(ISymbol symbol)
    {
        return symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepCompareAttributeName);
    }

    private static INamedTypeSymbol? GetEffectiveComparerType(ITypeSymbol comparedType, AttributeData? memberAttribute)
    {
        INamedTypeSymbol? fromMember = null;

        if (memberAttribute is not null)
            foreach (var kv in memberAttribute.NamedArguments)
                if (kv is { Key: "ComparerType", Value.Value: INamedTypeSymbol ts } &&
                    ImplementsIEqualityComparerFor(ts, comparedType))
                {
                    fromMember = ts;
                    break;
                }

        if (fromMember is not null) return fromMember;

        var typeAttr = comparedType.OriginalDefinition.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DeepCompareAttributeName);
        if (typeAttr is not null)
            foreach (var kv in typeAttr.NamedArguments)
                if (kv is { Key: "ComparerType", Value.Value: INamedTypeSymbol ts2 } &&
                    ImplementsIEqualityComparerFor(ts2, comparedType))
                    return ts2;

        return null;
    }

    private static bool ImplementsIEqualityComparerFor(INamedTypeSymbol comparerType, ITypeSymbol argument)
    {
        foreach (var i in comparerType.AllInterfaces)
            if (i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEqualityComparer<T>" &&
                SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], argument))
                return true;

        return false;
    }

    private static bool TryGetEnumerableElement(ITypeSymbol t, out ITypeSymbol element)
    {
        element = null!;
        if (t is IArrayTypeSymbol at)
        {
            element = at.ElementType;
            return true;
        }

        foreach (var i in t.AllInterfaces)
            if (i != null &&
                i.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                "global::System.Collections.Generic.IEnumerable<T>")
            {
                element = i.TypeArguments[0];
                return true;
            }

        if (t is INamedTypeSymbol nt &&
            nt.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
            "global::System.Collections.Generic.IEnumerable<T>")
        {
            element = nt.TypeArguments[0];
            return true;
        }

        return false;
    }

    private static bool TryGetListInterface(ITypeSymbol t, out ITypeSymbol element)
    {
        element = null!;
        foreach (var i in t.AllInterfaces)
            if (i != null &&
                i.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                "global::System.Collections.Generic.IList<T>")
            {
                element = i.TypeArguments[0];
                return true;
            }

        if (t is INamedTypeSymbol nt &&
            nt.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
            "global::System.Collections.Generic.List<T>")
        {
            element = nt.TypeArguments[0];
            return true;
        }

        return false;
    }

    private static bool TryGetDictionaryTypes(ITypeSymbol t, out ITypeSymbol key, out ITypeSymbol value)
    {
        key = value = null!;

        static bool IsDict(INamedTypeSymbol x)
        {
            return x.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                   "global::System.Collections.Generic.IDictionary<TKey, TValue>" ||
                   x.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                   "global::System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>";
        }

        foreach (var i in t.AllInterfaces)
            if (i != null && IsDict(i))
            {
                key = i.TypeArguments[0];
                value = i.TypeArguments[1];
                return true;
            }

        if (t is INamedTypeSymbol nt && IsDict(nt))
        {
            key = nt.TypeArguments[0];
            value = nt.TypeArguments[1];
            return true;
        }

        return false;
    }

    private static IEnumerable<DiffDeltaMemberSymbol> EnumerateMembers(
        INamedTypeSymbol type, bool includeInternals, bool includeBase, DiffDeltaTypeSchema schema)
    {
        var allowed = new HashSet<Accessibility> { Accessibility.Public };
        if (includeInternals) allowed.Add(Accessibility.Internal);

        var collected = new List<DiffDeltaMemberSymbol>();
        var t = type;

        while (t is not null)
        {
            foreach (var m in t.GetMembers())
            {
                if (m is IPropertySymbol p && !p.IsStatic && p.GetMethod is not null && allowed.Contains(p.GetMethod.DeclaredAccessibility))
                {
                    // Accept read-only for collections/dictionaries (we can apply ops in-place).
                    var hasSetter = p.SetMethod is not null;

                    bool isDictLike = TryGetDictionaryTypes(p.Type, out _, out _);
                    bool isListLike = TryGetEnumerableElement(p.Type, out _) && p.Type is not IArrayTypeSymbol;

                    if (!hasSetter && !(isDictLike || isListLike))
                        continue;

                    collected.Add(new DiffDeltaMemberSymbol(p.Name, p.Type, p));
                }
                else if (m is IFieldSymbol f && !f.IsStatic && allowed.Contains(f.DeclaredAccessibility))
                {
                    if (f.IsReadOnly) continue;   // no point generating setters/ops for readonly fields
                    collected.Add(new DiffDeltaMemberSymbol(f.Name, f.Type, f));
                }
            }

            if (!includeBase) break;
            t = t.BaseType;
        }

        // Apply schema include/ignore filters
        var includes = schema.IncludeMembers;
        var ignores = schema.IgnoreMembers;

        IEnumerable<DiffDeltaMemberSymbol> result = collected;
        if (includes.Count > 0)
            result = result.Where(ms => includes.Contains(ms.Name, StringComparer.Ordinal));
        result = result.Where(ms => !ignores.Contains(ms.Name, StringComparer.Ordinal));

        return result;
    }

    private static IEnumerable<DiffDeltaMemberSymbol> OrderMembers(IEnumerable<DiffDeltaMemberSymbol> members)
    {
        return members.OrderBy(m => m.Name, StringComparer.Ordinal);
    }

    private DiffDeltaTypeSchema GetTypeSchema(INamedTypeSymbol type)
    {
        var defKind = CompareKind.Deep;
        var defOrderInsensitive = false;
        var defDeltaShallow = false;
        var defDeltaSkip = false;
        var includes = new List<string>();
        var ignores = new List<string>();

        foreach (var a in type.GetAttributes())
            if (a.AttributeClass?.ToDisplayString() == DeepCompareAttributeName)
                foreach (var kv in a.NamedArguments)
                    switch (kv.Key)
                    {
                        case "Kind": defKind = (CompareKind)kv.Value.Value!; break;
                        case "OrderInsensitive": defOrderInsensitive = (bool)kv.Value.Value!; break;
                        case "Members": includes.AddRange(kv.Value.Values.Select(v => (string)v.Value!)); break;
                        case "IgnoreMembers": ignores.AddRange(kv.Value.Values.Select(v => (string)v.Value!)); break;
                        case "DeltaShallow": defDeltaShallow = (bool)kv.Value.Value!; break;
                        case "DeltaSkip": defDeltaSkip = (bool)kv.Value.Value!; break;
                    }

        return new DiffDeltaTypeSchema(includes, ignores, defKind, defOrderInsensitive, defDeltaShallow, defDeltaSkip);
    }

    private (CompareKind kind, bool orderInsensitive, string[] keys, bool deltaShallow, bool deltaSkip)
        ResolveEffectiveSettings(DiffDeltaMemberSymbol member)
    {
        var kind = CompareKind.Deep;
        var orderInsensitive = false;
        var deltaShallow = false;
        var deltaSkip = false;
        string[] keys = [];

        foreach (var a in member.Symbol.GetAttributes())
            if (a.AttributeClass?.ToDisplayString() == DeepCompareAttributeName)
                foreach (var kv in a.NamedArguments)
                    switch (kv.Key)
                    {
                        case "Kind": kind = (CompareKind)kv.Value.Value!; break;
                        case "OrderInsensitive": orderInsensitive = (bool)kv.Value.Value!; break;
                        case "KeyMembers": keys = kv.Value.Values.Select(v => (string)v.Value!).ToArray(); break;
                        case "DeltaShallow": deltaShallow = (bool)kv.Value.Value!; break;
                        case "DeltaSkip": deltaSkip = (bool)kv.Value.Value!; break;
                    }

        return (kind, orderInsensitive, keys, deltaShallow, deltaSkip);
    }

    private static bool IsValueLike(ITypeSymbol t)
    {
        if (t.SpecialType == SpecialType.System_String) return true;

        if (t.IsValueType) return true;

        if (t is INamedTypeSymbol nn &&
            nn.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
            "global::System.Nullable<T>") return true;

        return false;
    }

    private static string GetValueLikeEqualsInvocation(ITypeSymbol t, string leftExpr, string rightExpr)
    {
        // Nullable<T>: unwrap and reuse the inner type's comparison
        if (t is INamedTypeSymbol nt &&
            nt.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Nullable<T>")
        {
            var u = nt.TypeArguments[0];
            var inner = GetValueLikeEqualsInvocation(u, leftExpr + ".Value", rightExpr + ".Value");
            // equal when both null, or both have value and inner compares equal
            return $"(({leftExpr}.HasValue == {rightExpr}.HasValue) && (!{leftExpr}.HasValue || ({inner})))";
        }

        if (t.SpecialType == SpecialType.System_String)
            return $"DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualStrings({leftExpr}, {rightExpr}, context)";

        if (t.SpecialType == SpecialType.System_Double)
            return $"DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDouble({leftExpr}, {rightExpr}, context)";

        if (t.SpecialType == SpecialType.System_Single)
            return $"DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualSingle({leftExpr}, {rightExpr}, context)";

        if (t.SpecialType == SpecialType.System_Decimal)
            return $"DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDecimal({leftExpr}, {rightExpr}, context)";

        // Content equality for Memory<T>/ReadOnlyMemory<T>
        if (t is INamedTypeSymbol nt2 &&
            (nt2.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.ReadOnlyMemory<T>" ||
             nt2.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Memory<T>"))
        {
            var el = nt2.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return $"System.MemoryExtensions.SequenceEqual<{el}>({leftExpr}.Span, {rightExpr}.Span)";
        }

        var tfqn = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return $"System.Collections.Generic.EqualityComparer<{tfqn}>.Default.Equals({leftExpr}, {rightExpr})";
    }

    private int GetStableMemberIndex(INamedTypeSymbol owner, DiffDeltaMemberSymbol member)
    {
        if (_useStableIndices && _stableIndexTables.TryGetValue(owner, out var map) &&
            map.TryGetValue(member.Name, out var idx)) return idx;

        unchecked
        {
            var h = 17;
            foreach (var ch in owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                h = h * 31 + ch;
            foreach (var ch in member.Name)
                h = h * 31 + ch;
            return (h & 0x7FFFFFFF) % 1_000_000_007;
        }
    }

    private static string SanitizeIdentifier(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s) sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        return sb.ToString();
    }


    // In DiffDeltaEmitter (or the class that builds the set of types to generate)
    private static HashSet<INamedTypeSymbol> BuildReachableTypeClosure(DiffDeltaTarget root)
    {
        var set = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var q = new Queue<ITypeSymbol>();

        void Enq(ITypeSymbol t)
        {
            // unwrap Nullable<T>
            if (t is INamedTypeSymbol nt &&
                nt.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Nullable<T>")
                t = nt.TypeArguments[0];

            q.Enqueue(t);
        }

        Enq(root.Type);

        INamespaceSymbol asmRoot = root.Type.ContainingAssembly.GlobalNamespace;

        while (q.Count > 0)
        {
            var cur = q.Dequeue();

            // Arrays → element
            if (cur is IArrayTypeSymbol at)
            {
                Enq(at.ElementType);
                continue;
            }

            // Dict → key, value
            if (TryGetDictionaryTypes(cur, out var kT, out var vT))
            {
                Enq(kT); Enq(vT);
                continue;
            }

            // IEnumerable<T> → T
            if (TryGetEnumerableElement(cur, out var elT))
                Enq(elT);

            // Memory<T> / ReadOnlyMemory<T> → T
            if (GenCommon.TryGetReadOnlyMemory(cur, out var romT))
                Enq(romT!);
            if (GenCommon.TryGetMemory(cur, out var memT))
                Enq(memT!);

            // If it's a concrete user object, accessible in the same assembly → include and traverse its members
            if (cur is INamedTypeSymbol user &&
                GenCommon.IsUserObjectType(user) &&
                IsTypeAccessibleFromRoot(user, root) &&
                set.Add(user))
            {
                // IMPORTANT: use the equality-side enumerator (broad),
                // not the apply/delta enumerator that may drop get-only members.
                var schema = GenCommon.GetTypeSchema(user);
                foreach (var m in GenCommon.EnumerateMembers(user, root.IncludeInternals, root.IncludeBaseMembers, schema))
                    Enq(m.Type);
            }

            // If it's an interface or abstract, scan this assembly for concrete implementations and enqueue them
            if (cur is INamedTypeSymbol ia && (ia.TypeKind == TypeKind.Interface || ia.IsAbstract))
            {
                foreach (var impl in EnumerateAllNamedTypes(asmRoot))
                {
                    if (!GenCommon.IsUserObjectType(impl) || impl.IsAbstract) continue;
                    if (!IsTypeAccessibleFromRoot(impl, root)) continue;

                    bool matches =
                        ia.TypeKind == TypeKind.Interface
                            ? impl.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, ia))
                            : IsDerivedFrom(impl, ia);

                    if (matches && set.Add(impl))
                        Enq(impl);
                }
            }
        }

        return set;

        // SAME-ASSEMBLY accessibility: allow public and internal (and nesting chain must not be private/protected)
        static bool IsTypeAccessibleFromRoot(INamedTypeSymbol t, DiffDeltaTarget root)
        {
            if (!SymbolEqualityComparer.Default.Equals(t.ContainingAssembly, root.Type.ContainingAssembly)) return false;

            var c = t;
            while (c is not null)
            {
                var acc = c.DeclaredAccessibility;
                if (acc is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedAndInternal)
                {
                    c = c.ContainingType;
                    continue;
                }
                return false;
            }
            return true;
        }

        static bool IsDerivedFrom(INamedTypeSymbol impl, INamedTypeSymbol baseType)
        {
            for (var bt = impl.BaseType; bt is not null && bt.SpecialType != SpecialType.System_Object; bt = bt.BaseType)
                if (SymbolEqualityComparer.Default.Equals(bt, baseType)) return true;
            return false;
        }

        static IEnumerable<INamedTypeSymbol> EnumerateAllNamedTypes(INamespaceSymbol ns)
        {
            foreach (var m in ns.GetMembers())
            {
                if (m is INamespaceSymbol sub)
                {
                    foreach (var t in EnumerateAllNamedTypes(sub)) yield return t;
                }
                else if (m is INamedTypeSymbol t)
                {
                    yield return t;
                    foreach (var nested in EnumerateNested(t)) yield return nested;
                }
            }
        }
        static IEnumerable<INamedTypeSymbol> EnumerateNested(INamedTypeSymbol t)
        {
            foreach (var m in t.GetMembers())
                if (m is INamedTypeSymbol nt)
                {
                    yield return nt;
                    foreach (var deeper in EnumerateNested(nt)) yield return deeper;
                }
        }
    }


}

internal enum StableMemberIndexMode
{
    Auto = 0,
    On = 1,
    Off = 2
}

public enum CompareKind
{
    Deep,
    Shallow,
    Reference,
    Skip
}

internal readonly record struct EqualityTarget(
    INamedTypeSymbol Type,
    bool IncludeInternals,
    bool OrderInsensitiveCollections,
    bool CycleTrackingEnabled,
    bool IncludeBaseMembers);

internal readonly record struct EqualityMemberSymbol(string Name, ITypeSymbol Type, ISymbol Symbol);

internal sealed record EqualityTypeSchema(IReadOnlyList<string> IncludeMembers, IReadOnlyList<string> IgnoreMembers);

internal enum EffectiveKind
{
    Deep = 0,
    Shallow = 1,
    Reference = 2,
    Skip = 3
}