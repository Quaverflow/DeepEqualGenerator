using DeepEqual.Generator.Shared;

namespace DeepEqual.Tests;

[DeepComparable]
public class SelfStructRoot
{
    public SelfStruct Payload { get; set; }
}