namespace DeepEqual.Generator.Tests.Models;

public sealed class DoubleEpsComparer(double eps) : IEqualityComparer<double>
{
    public DoubleEpsComparer() : this(1e-6) { }
    public bool Equals(double x, double y) => Math.Abs(x - y) <= eps || double.IsNaN(x) && double.IsNaN(y);
    public int GetHashCode(double obj) => 0;
}