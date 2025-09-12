using DeepEqual.Generator.Attributes;

namespace Tests;

[DeepComparable]
public class Holder
{
    public ThirdPartyLike Third { get; set; } = new();
}