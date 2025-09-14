namespace DeepEqual.Generator.Tests.Models;

public sealed class DoubleEpsComparer : IEqualityComparer<double>
{
    private readonly double _eps;
    public DoubleEpsComparer() : this(1e-6) { }
    public DoubleEpsComparer(double eps) { _eps = eps; }
    public bool Equals(double x, double y) => Math.Abs(x - y) <= _eps || double.IsNaN(x) && double.IsNaN(y);
    public int GetHashCode(double obj) => 0;
}