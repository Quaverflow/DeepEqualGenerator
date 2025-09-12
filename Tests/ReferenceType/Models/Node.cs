namespace Tests;

public class Node<T>
{
    public T Value { get; set; } = default!;
    public List<Node<T>> Children { get; set; } = [];
    public Node<T>? Parent { get; set; }
}