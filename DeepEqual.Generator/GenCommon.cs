using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

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
            w.If("!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDateTime(" + leftExpr + ", " + rightExpr + ")", () =>
            {
                w.Line("return false;");
            });
            return true;
        }

        var fqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (fqn == "global::System.DateTimeOffset")
        {
            w.If("!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDateTimeOffset(" + leftExpr + ", " + rightExpr + ")", () =>
            {
                w.Line("return false;");
            });
            return true;
        }

        if (fqn == "global::System.TimeSpan")
        {
            w.If(leftExpr + ".Ticks != " + rightExpr + ".Ticks", () =>
            {
                w.Line("return false;");
            });
            return true;
        }

        if (fqn == "global::System.Guid")
        {
            w.If("!" + leftExpr + ".Equals(" + rightExpr + ")", () =>
            {
                w.Line("return false;");
            });
            return true;
        }

        if (fqn == "global::System.DateOnly")
        {
            w.If("!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDateOnly(" + leftExpr + ", " + rightExpr + ")", () =>
            {
                w.Line("return false;");
            });
            return true;
        }

        if (fqn == "global::System.TimeOnly")
        {
            w.If("!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualTimeOnly(" + leftExpr + ", " + rightExpr + ")", () =>
            {
                w.Line("return false;");
            });
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


    internal static IEnumerable<ITypeParameterSymbol> EnumerateAllTypeParameters(INamedTypeSymbol type)
    {
        if (type.ContainingType is not null)
        {
            foreach (var tp in EnumerateAllTypeParameters(type.ContainingType))
                yield return tp;
        }

        foreach (var tp in type.TypeParameters)
            yield return tp;
    }

    internal static string GetTypeParameterList(INamedTypeSymbol type)
    {
        return GetTypeParameterList(EnumerateAllTypeParameters(type));
    }

    internal static string GetTypeParameterList(IEnumerable<ITypeParameterSymbol> typeParameters)
    {
        var array = typeParameters as ITypeParameterSymbol[] ?? typeParameters.ToArray();
        if (array.Length == 0) return string.Empty;
        return "<" + string.Join(", ", array.Select(tp => tp.Name)) + ">";
    }

    internal static string GetTypeConstraintClauses(INamedTypeSymbol type)
    {
        return GetTypeConstraintClauses(EnumerateAllTypeParameters(type));
    }

    internal static string GetTypeConstraintClauses(IEnumerable<ITypeParameterSymbol> typeParameters)
    {
        var sb = new StringBuilder();
        foreach (var tp in typeParameters)
        {
            var clause = GetTypeParameterConstraintClause(tp);
            if (!string.IsNullOrEmpty(clause)) sb.Append(clause);
        }

        return sb.ToString();
    }

    internal static string GetTypeParameterConstraintClause(ITypeParameterSymbol typeParameter)
    {
        var parts = new List<string>();

        if (typeParameter.HasNotNullConstraint)
            parts.Add("notnull");

        if (typeParameter.HasReferenceTypeConstraint)
        {
            var suffix = typeParameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated
                ? "?"
                : string.Empty;
            parts.Add("class" + suffix);
        }
        else if (typeParameter.HasUnmanagedTypeConstraint)
        {
            parts.Add("unmanaged");
        }
        else if (typeParameter.HasValueTypeConstraint)
        {
            parts.Add("struct");
        }

        foreach (var constraint in typeParameter.ConstraintTypes)
            parts.Add(constraint.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        if (typeParameter.HasConstructorConstraint)
            parts.Add("new()");

        if (parts.Count == 0) return string.Empty;
        return " where " + typeParameter.Name + " : " + string.Join(", ", parts);
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