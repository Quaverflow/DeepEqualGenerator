using DeepEqual.Generator.Shared;

// Assembly-scoped external annotations — pretend these types are in a 3rd-party lib:
[assembly: ExternalDeepComparable(typeof(ThirdParty.A2), GenerateDiff = true, GenerateDelta = true, CycleTracking = true)]
[assembly: ExternalDeepComparable(typeof(ThirdParty.B2), GenerateDiff = true, GenerateDelta = true, CycleTracking = true)]

// A small dict case to sanity-check <Key>/<Value> path hopping too:
[assembly: ExternalDeepComparable(typeof(ThirdParty.Bag), GenerateDiff = true, GenerateDelta = true, CycleTracking = false)]
[assembly: ExternalDeepCompare(typeof(ThirdParty.Bag), "Items<Value>.Name")]

namespace ThirdParty
{
    // Simulated 3rd-party types (no in-source attributes here):
    public sealed class A2
    {
        public int V { get; set; }
        public B2? B { get; set; }
    }

    public sealed class B2
    {
        public int W { get; set; }
        public A2? A { get; set; }
    }

    public sealed class Item
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    public sealed class Bag
    {
        public Dictionary<int, Item> Items { get; set; } = new();
    }
}

public class ExternalAttributes_SmokeTests
{
    [Fact]
    public void MutualCycle_Delta_RoundTrips_With_ExternalAttributes()
    {
        var a1 = new ThirdParty.A2 { V = 10 };
        var b1 = new ThirdParty.B2 { W = 100 };
        a1.B = b1; b1.A = a1;

        var a2 = new ThirdParty.A2 { V = 11 };
        var b2 = new ThirdParty.B2 { W = 100 };
        a2.B = b2; b2.A = a2;

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        ThirdParty.A2DeepOps.ComputeDelta(a1, a2, ref w);

        Assert.False(doc.IsEmpty);

        var r = new DeltaReader(doc);
        ThirdParty.A2DeepOps.ApplyDelta(ref a1, ref r);

        Assert.True(ThirdParty.A2DeepEqual.AreDeepEqual(a1, a2));
    }

    [Fact]
    public void Dictionary_Value_Path_Smoke_Writes_And_Applies()
    {
        var before = new ThirdParty.Bag
        {
            Items = new Dictionary<int, ThirdParty.Item>
            {
                { 1, new ThirdParty.Item { Id = 1, Name = "alpha" } },
                { 2, new ThirdParty.Item { Id = 2, Name = "beta"  } },
            }
        };
        var after = new ThirdParty.Bag
        {
            Items = new Dictionary<int, ThirdParty.Item>
            {
                { 1, new ThirdParty.Item { Id = 1, Name = "ALPHA" } }, // changed
                { 2, new ThirdParty.Item { Id = 2, Name = "beta"  } },
            }
        };

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        ThirdParty.BagDeepOps.ComputeDelta(before, after, ref w);

        Assert.False(doc.IsEmpty);

        var r = new DeltaReader(doc);
        ThirdParty.BagDeepOps.ApplyDelta(ref before, ref r);

        Assert.True(ThirdParty.BagDeepEqual.AreDeepEqual(before, after));
    }
}
