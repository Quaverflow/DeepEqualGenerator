using System.Globalization;
using DeepEqual.Generator.Tests.Models;

namespace DeepEqual.Generator.Tests.Tests;

public class CrazyCycleTests
{
    [Fact]
    public void Heterogeneous_IAnimal_Cycle_Equal_And_NotEqual()
    {
        var a1 = new PolyCycleNode { Id = 1, Animal = new Cat { Age = 2, Name = "C" } };
        var a2 = new PolyCycleNode { Id = 2, Animal = new Dog { Age = 5, Name = "D" } };
        a1.Next = a2;
        a2.Next = a1;

        var b1 = new PolyCycleNode { Id = 1, Animal = new Cat { Age = 2, Name = "C" } };
        var b2 = new PolyCycleNode { Id = 2, Animal = new Dog { Age = 5, Name = "D" } };
        b1.Next = b2;
        b2.Next = b1;

        Assert.True(PolyCycleNodeDeepEqual.AreDeepEqual(a1, b1));

        var b2diff = new PolyCycleNode { Id = 2, Animal = new Dog { Age = 6, Name = "D" } };
        b1.Next = b2diff;
        b2diff.Next = b1;
        Assert.False(PolyCycleNodeDeepEqual.AreDeepEqual(a1, b1));

        var b2Type = new PolyCycleNode { Id = 2, Animal = new Cat { Age = 5, Name = "D" } };
        b1.Next = b2Type;
        b2Type.Next = b1;
        Assert.False(PolyCycleNodeDeepEqual.AreDeepEqual(a1, b1));
    }

    [Fact]
    public void CultureInfo_In_Cycle_Compares_By_Instance_Semantics()
    {
        var enGb1 = CultureInfo.GetCultureInfo("en-GB");
        var enGb2 = CultureInfo.GetCultureInfo("en-GB");
        var itIt = CultureInfo.GetCultureInfo("it-IT");

        var a = new CultureCycleNode { Culture = enGb1 };
        a.Next = a;

        var b = new CultureCycleNode { Culture = enGb2 };
        b.Next = b;

        var c = new CultureCycleNode { Culture = itIt };
        c.Next = c;

        Assert.True(CultureCycleNodeDeepEqual.AreDeepEqual(a, b));
        Assert.False(CultureCycleNodeDeepEqual.AreDeepEqual(a, c));
    }
}