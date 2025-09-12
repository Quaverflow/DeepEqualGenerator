using DeepEqual.Generator.Shared;

namespace Tests;

[DeepComparable]
public class GenericHolder
{
    public Node<int> Node { get; set; } = new();
}