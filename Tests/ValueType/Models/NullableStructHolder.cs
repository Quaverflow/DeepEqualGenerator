using DeepEqual.Generator.Shared;

namespace Tests.ValueType.Models;

[DeepComparable]
public partial class NullableStructHolder
{
    public SimpleStruct? Maybe { get; set; }
}