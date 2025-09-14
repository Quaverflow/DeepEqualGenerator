namespace DeepEqual.Generator.Shared;

public interface IElementComparer<in T>
{
    bool Invoke(T left, T right, ComparisonContext context);
}