using DeepEqual.Generator.Attributes;

namespace Tests;

[DeepComparable]
public class GenericHolder
{
    public Node<int> Node { get; set; } = new();
}