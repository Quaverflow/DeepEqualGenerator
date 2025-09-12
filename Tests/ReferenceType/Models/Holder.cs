using DeepEqual.Generator.Shared;

namespace Tests;

[DeepComparable]
public class Holder
{
    public ThirdPartyLike Third { get; set; } = new();
}