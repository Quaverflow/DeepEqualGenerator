using DeepEqual.Generator.Shared;

namespace DeepEqual.Tests;

[DeepComparable]
public class StructRoot
{
    public string Label { get; set; } = "";
    public StructChild Value { get; set; }  // unannotated struct
    public StructChild? Maybe { get; set; } // Nullable<T> deep unwrap
    public StructChild[] Array { get; set; } = [];
}