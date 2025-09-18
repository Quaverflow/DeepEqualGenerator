using DeepEqual.Generator.Shared;
using System.Drawing;

// Generate deep ops for our external types
[assembly: ExternalDeepComparable(typeof(ListHolder1), GenerateDiff = true, GenerateDelta = true, CycleTracking = false)]
[assembly: ExternalDeepComparable(typeof(ArrayHolder1), GenerateDiff = true, GenerateDelta = true, CycleTracking = false)]
[assembly: ExternalDeepComparable(typeof(DictHolder1), GenerateDiff = true, GenerateDelta = true, CycleTracking = false)]
[assembly: ExternalDeepComparable(typeof(MixedHolder), GenerateDiff = true, GenerateDelta = true, CycleTracking = false)]
[assembly: ExternalDeepCompare(typeof(ListHolder1), "Points.X")]
[assembly: ExternalDeepCompare(typeof(ArrayHolder1), "Points.X")]
[assembly: ExternalDeepCompare(typeof(DictHolder1), "PointsById<Value>.Y")]
[assembly: ExternalDeepCompare(typeof(MixedHolder), "Routes<Value>.X")]

public sealed class ListHolder1
{
    public List<Point> Points { get; set; } = new();
}

public sealed class ArrayHolder1
{
    public Point[] Points { get; set; } = [];
}

public sealed class DictHolder1
{
    public Dictionary<int, Point> PointsById { get; set; } = new();
}

public sealed class MixedHolder
{
    public Dictionary<int, List<Point>> Routes { get; set; } = new();
    public string Name { get; set; } = "";
}

public class ExternalAttributes_Collections_Tests
{
    [Fact]
    public void ListHolder1_Delta_RoundTrips()
    {
        var before = new ListHolder1 { Points = new List<Point> { new(1, 1), new(2, 2), new(3, 3) } };
        var after = new ListHolder1 { Points = new List<Point> { new(1, 1), new(9, 2), new(3, 3) } };

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        ListHolder1DeepOps.ComputeDelta(before, after, ref w);

        Assert.False(doc.IsEmpty);

        var r = new DeltaReader(doc);
        ListHolder1DeepOps.ApplyDelta(ref before, ref r);
        Assert.True(ListHolder1DeepEqual.AreDeepEqual(before, after));
    }

    [Fact]
    public void ArrayHolder1_Delta_RoundTrips()
    {
        var before = new ArrayHolder1 { Points = new[] { new Point(1, 1), new Point(2, 2) } };
        var after = new ArrayHolder1 { Points = new[] { new Point(1, 1), new Point(7, 2) } };

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        ArrayHolder1DeepOps.ComputeDelta(before, after, ref w);

        Assert.False(doc.IsEmpty);

        var r = new DeltaReader(doc);
        ArrayHolder1DeepOps.ApplyDelta(ref before, ref r);
        Assert.True(ArrayHolder1DeepEqual.AreDeepEqual(before, after));
    }

    [Fact]
    public void DictHolder1_Delta_RoundTrips_With_Value_Path()
    {
        var before = new DictHolder1
        {
            PointsById = new Dictionary<int, Point>
            {
                [1] = new(10, 10),
                [2] = new(20, 20),
            }
        };
        var after = new DictHolder1
        {
            PointsById = new Dictionary<int, Point>
            {
                [1] = new(10, 10),
                [2] = new(99, 20), // change Y via path on <Value>.Y
                [3] = new(30, 30), // added
            }
        };

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        DictHolder1DeepOps.ComputeDelta(before, after, ref w);

        Assert.False(doc.IsEmpty);

        var r = new DeltaReader(doc);
        DictHolder1DeepOps.ApplyDelta(ref before, ref r);
        Assert.True(DictHolder1DeepEqual.AreDeepEqual(before, after));
    }

    [Fact]
    public void MixedHolder_Nested_Dict_List_Element_Path_RoundTrips()
    {
        var before = new MixedHolder
        {
            Name = "alpha",
            Routes = new Dictionary<int, List<Point>>
            {
                [1] = new() { new(1, 1), new(2, 2) },
                [2] = new() { new(3, 3) }
            }
        };
        var after = new MixedHolder
        {
            Name = "alpha",
            Routes = new Dictionary<int, List<Point>>
            {
                [1] = new() { new(1, 1), new(9, 2) }, // change element X under key 1
                [2] = new() { new(3, 3), new(4, 4) }  // add new element under key 2
            }
        };

        var doc = new DeltaDocument();
        var w = new DeltaWriter(doc);
        MixedHolderDeepOps.ComputeDelta(before, after, ref w);

        Assert.False(doc.IsEmpty);

        var r = new DeltaReader(doc);
        MixedHolderDeepOps.ApplyDelta(ref before, ref r);
        Assert.True(MixedHolderDeepEqual.AreDeepEqual(before, after));
    }
}