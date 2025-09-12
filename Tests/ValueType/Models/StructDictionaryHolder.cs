using DeepEqual.Generator.Attributes;

namespace Tests.ValueType.Models;

[DeepComparable]
public partial class StructDictionaryHolder
{
    public Dictionary<string, SimpleStruct> Map { get; set; } = new(StringComparer.Ordinal);
}