using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.Models;

[DeepComparable]
public sealed class MultiDimArrayHolder
{
    public int[,] Matrix { get; set; } = new int[0, 0];
}