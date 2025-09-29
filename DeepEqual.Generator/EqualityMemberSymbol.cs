using Microsoft.CodeAnalysis;

namespace DeepEqual.Generator;

internal readonly record struct EqualityMemberSymbol(string Name, ITypeSymbol Type, ISymbol Symbol);