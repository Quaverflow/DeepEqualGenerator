using DeepEqual.Generator.Shared;

namespace Tests;

[DeepCompare(Kind = CompareKind.Shallow)]
public class ThirdPartyLike
{
    public string Payload { get; set; } = "";
    public override bool Equals(object? obj) => obj is ThirdPartyLike t && t.Payload == Payload;
    public override int GetHashCode() => Payload.GetHashCode();
}