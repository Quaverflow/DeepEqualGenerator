using Microsoft.CodeAnalysis;

namespace DeepEqual.Generator;

internal readonly record struct DiffDeltaMemberSymbol(string Name, ITypeSymbol Type, ISymbol Symbol);