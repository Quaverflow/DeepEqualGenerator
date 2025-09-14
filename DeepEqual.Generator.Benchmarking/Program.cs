using BenchmarkDotNet.Running;

namespace DeepEqual.Generator.Benchmarking;

internal class Program
{
    static void Main()
    {
        _ = BenchmarkRunner.Run<MegaBenchmarks>();
    }
}