using BenchmarkDotNet.Attributes;

namespace DeepEqual.Generator.Benchmarking;

[MemoryDiagnoser]
[PlainExporter]
public class MegaBenchmarks
{
    [Params(3)] public int OrgBreadth;
    [Params(3)] public int OrgDepth;
    [Params(300)] public int Products;
    [Params(300)] public int Customers;
    [Params(4)] public int OrdersPerCustomer;
    [Params(6)] public int LinesPerOrder;
    [Params(384)] public int EbCount;

    [Params(128)] public int BagelsCount;

    private MegaRoot _eqA = null!;
    private MegaRoot _eqB = null!;
    private MegaRoot _neqShallowA = null!;
    private MegaRoot _neqShallowB = null!;
    private MegaRoot _neqDeepA = null!;
    private MegaRoot _neqDeepB = null!;

    [GlobalSetup]
    public void Setup()
    {
        _eqA = MegaFactory.Create(OrgBreadth, OrgDepth, Products, Customers, OrdersPerCustomer, LinesPerOrder, BagelsCount, seed: 11);
        _eqB = MegaFactory.Create(OrgBreadth, OrgDepth, Products, Customers, OrdersPerCustomer, LinesPerOrder, BagelsCount, seed: 11);

        _neqShallowA = MegaFactory.Create(OrgBreadth, OrgDepth, Products, Customers, OrdersPerCustomer, LinesPerOrder, BagelsCount, seed: 22);
        _neqShallowB = MegaFactory.Create(OrgBreadth, OrgDepth, Products, Customers, OrdersPerCustomer, LinesPerOrder, BagelsCount, seed: 22);
        MegaFactory.MutateShallow(_neqShallowB);

        _neqDeepA = MegaFactory.Create(OrgBreadth, OrgDepth, Products, Customers, OrdersPerCustomer, LinesPerOrder, BagelsCount, seed: 33);
        _neqDeepB = MegaFactory.Create(OrgBreadth, OrgDepth, Products, Customers, OrdersPerCustomer, LinesPerOrder, BagelsCount, seed: 33);
        MegaFactory.MutateDeep(_neqDeepB);
    }

    [Benchmark(Baseline = true)] public bool Generated_Mega_Equal() => MegaRootDeepEqual.AreDeepEqual(_eqA, _eqB);
    [Benchmark] public bool Manual_Mega_Equal() => ManualMegaComparer.AreEqual(_eqA, _eqB);

    [Benchmark] public bool Generated_Mega_NotEqual_Shallow() => MegaRootDeepEqual.AreDeepEqual(_neqShallowA, _neqShallowB);
    [Benchmark] public bool Manual_Mega_NotEqual_Shallow() => ManualMegaComparer.AreEqual(_neqShallowA, _neqShallowB);

    [Benchmark] public bool Generated_Mega_NotEqual_Deep() => MegaRootDeepEqual.AreDeepEqual(_neqDeepA, _neqDeepB);
    [Benchmark] public bool Manual_Mega_NotEqual_Deep() => ManualMegaComparer.AreEqual(_neqDeepA, _neqDeepB);
}