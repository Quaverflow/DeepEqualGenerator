using System;

namespace DeepEqual.Generator.Shared;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class DeepCompareAttribute : Attribute
{
    public CompareKind Kind { get; set; } = CompareKind.Deep;
    public bool OrderInsensitive { get; set; } = false;
    public string[] Members { get; set; } = [];
    public string[] IgnoreMembers { get; set; } = [];
    public Type? ComparerType { get; set; } = null;
    public string[] KeyMembers { get; set; } = [];
}