using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.DiffDeltaTests;

public class NewTests
{
    [DeepComparable(GenerateDiff = true, GenerateDelta = true, CycleTracking = true)]
    public class Zoo1
    {
        public Dictionary<string, Animal1> Animals { get; set; } = new();
    }

    [DeepComparable(GenerateDiff = true, GenerateDelta = true, CycleTracking = true)]
    public class Animal1
    {
        public string? Tag { get; set; }
    }

    [DeepComparable(GenerateDiff = true, GenerateDelta = true, CycleTracking = true)]
    public class Dog1 : Animal1
    {
        public Zoo1? Home { get; set; }
        public int Age { get; set; }
    }

    public sealed class DictionaryRuntimeDispatchCycleTests
    {
        [Fact]
        public void DictionaryValue_RuntimeDispatch_WithCycle_RoundTrips()
        {
            var z1 = new Zoo1();
            var d1 = new Dog1 { Tag = "fido", Age = 3, Home = z1 };
            z1.Animals["fido"] = d1;

            var z2 = new Zoo1();
            var d2 = new Dog1 { Tag = "fido", Age = 4, Home = z2 };
            z2.Animals["fido"] = d2;

            var doc = Zoo1DeepOps.ComputeDelta(z1, z2);
            Assert.False(doc.IsEmpty);

            Zoo1DeepOps.ApplyDelta(ref z1, doc);

            Assert.True(Zoo1DeepEqual.AreDeepEqual(z1, z2));
        }
    }

    [DeepComparable(GenerateDiff = true, GenerateDelta = true, CycleTracking = true)]
    public class Container1
    {
        public object? Payload { get; set; }
    }

    [DeepComparable(GenerateDiff = true, GenerateDelta = true, CycleTracking = true)]
    public class Holder : Base1
    {
        public Container1? Owner { get; set; }
        public int Age { get; set; }
    }

    [DeepComparable(GenerateDiff = true, GenerateDelta = true, CycleTracking = true)]
    public class Base1
    {
        public string? Name { get; set; }
    }

    public sealed class RuntimeDispatchCycleTests
    {
        [Fact]
        public void ObjectMember_RuntimeDispatch_WithCycle_RoundTrips()
        {
            var c1 = new Container1();
            var h1 = new Holder { Name = "x", Age = 1, Owner = c1 };
            c1.Payload = h1;

            var c2 = new Container1();
            var h2 = new Holder { Name = "x", Age = 2, Owner = c2 };
            c2.Payload = h2;

            var doc = Container1DeepOps.ComputeDelta(c1, c2);
            Assert.False(doc.IsEmpty);

            Container1DeepOps.ApplyDelta(ref c1, doc);

            Assert.True(Container1DeepEqual.AreDeepEqual(c1, c2));
        }
    }

    [DeepComparable(GenerateDiff = true, GenerateDelta = true, CycleTracking = true)]
    public sealed class Node1
    {
        public int Value { get; set; }
        public Node1? Next { get; set; }
    }

    public sealed class CycleDeltaTests
    {
        [Fact]
        public void SelfCycle_Delta_RoundTrips()
        {
            var a = new Node1 { Value = 1 };
            a.Next = a;

            var b = new Node1 { Value = 2 };
            b.Next = b;

            var doc = Node1DeepOps.ComputeDelta(a, b);
            Assert.False(doc.IsEmpty);

            Node1DeepOps.ApplyDelta(ref a, doc);

            Assert.True(Node1DeepEqual.AreDeepEqual(a, b));
        }

        [Fact]
        public void MutualCycle_Delta_RoundTrips()
        {
            var a1 = new A1 { V = 10 };
            var b1 = new B1 { W = 100 };
            a1.B = b1; b1.A = a1;

            var a2 = new A1 { V = 11 };
            var b2 = new B1 { W = 100 };
            a2.B = b2; b2.A = a2;

            var doc = A1DeepOps.ComputeDelta(a1, a2);
            Assert.False(doc.IsEmpty);

            A1DeepOps.ApplyDelta(ref a1, doc);

            Assert.True(A1DeepEqual.AreDeepEqual(a1, a2));
        }

        [DeepComparable(GenerateDiff = true, GenerateDelta = true, CycleTracking = true)]
        public sealed class A1
        {
            public int V { get; set; }
            public B1? B { get; set; }
        }

        [DeepComparable(GenerateDiff = true, GenerateDelta = true, CycleTracking = true)]
        public sealed class B1
        {
            public int W { get; set; }
            public A1? A { get; set; }
        }
    }
}
