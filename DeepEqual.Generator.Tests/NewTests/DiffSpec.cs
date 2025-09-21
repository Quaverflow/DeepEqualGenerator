// SPDX-License-Identifier: MIT

using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.NewTests;

public class DiffSpec
{
    [Fact]
    public void Diff_ValueLike_NoChange_NoDiff()
    {
        var a = new S_Address { Street = "S", City = "C" };
        var b = new S_Address { Street = "S", City = "C" };
        var (hasDiff, diff) = S_AddressDeepOps.GetDiff(a, b);
        Assert.False(hasDiff);
        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void Diff_ValueLike_Change_Records_Set()
    {
        var a = new S_Address { Street = "S", City = "C1" };
        var b = new S_Address { Street = "S", City = "C2" };
        var (hasDiff, diff) = S_AddressDeepOps.GetDiff(a, b);
        Assert.True(hasDiff);
        Assert.True(diff.HasChanges);
        Assert.Contains(diff.MemberChanges!, mc => mc.Kind == MemberChangeKind.Set);
    }

    [Fact]
    public void Diff_NestedObject_Records_Nested()
    {
        var a = new S_Customer { Id = 1, Name = "A", Home = new S_Address { Street = "S1", City = "C" } };
        var b = new S_Customer { Id = 1, Name = "A", Home = new S_Address { Street = "S2", City = "C" } };
        var (hasDiff, diff) = S_CustomerDeepOps.GetDiff(a, b);
        Assert.True(hasDiff);
        Assert.Contains(diff.MemberChanges!, mc => mc.Kind == MemberChangeKind.Nested);
    }

    [Fact]
    public void Diff_Collections_Granular_Still_Registers_Change()
    {
        var a = SpecFactories.NewOrder();
        var b = SpecFactories.Clone(a);
        b.Items![1].Qty++;
        var (hasDiff, diff) = S_OrderDeepOps.GetDiff(a, b);
        Assert.True(hasDiff);
        Assert.True(diff.HasChanges);
    }

    [Fact]
    public void Diff_Polymorphic_RuntimeType_Nested()
    {
        var a = new S_Zoo { Pet = new S_Dog { Name = "fido", Bones = 1 } };
        var b = new S_Zoo { Pet = new S_Dog { Name = "fido", Bones = 2 } };
        var (hasDiff, diff) = S_ZooDeepOps.GetDiff(a, b);
        Assert.True(hasDiff);
        Assert.True(diff.HasChanges);
    }

    [Fact]
    public void Diff_Polymorphic_TypeChange_FallsBack_To_Set()
    {
        var a = new S_Zoo { Pet = new S_Dog { Name = "n", Bones = 1 } };
        var b = new S_Zoo { Pet = new S_Cat { Name = "n", Mice = 3 } };
        var (hasDiff, diff) = S_ZooDeepOps.GetDiff(a, b);
        Assert.True(hasDiff);
        Assert.True(diff.HasChanges);
    }

    [Fact]
    public void Diff_Polymorphic_Unregistered_Runtime_Type_FallsBack_To_Set()
    {
        var a = new S_Zoo { Pet = new S_Parrot { Name = "p", Seeds = 1 } };
        var b = new S_Zoo { Pet = new S_Parrot { Name = "p", Seeds = 2 } };
        var (hasDiff, diff) = S_ZooDeepOps.GetDiff(a, b);
        Assert.True(hasDiff);
        Assert.True(diff.HasChanges);
    }

    [Fact]
    public void Diff_Cycles_No_Overflow_And_Reports_When_Changed()
    {
        var (a, _, c) = SpecFactories.MakeCyclicTriplet();
        var (hasDiff1, d1) = S_NodeDeepOps.GetDiff(a, a);
        Assert.False(hasDiff1);
        Assert.True(d1.IsEmpty);

        var (hasDiff2, d2) = S_NodeDeepOps.GetDiff(a, c);
        Assert.True(hasDiff2);
        Assert.True(d2.HasChanges);
    }

    [Fact]
    public void Diff_And_Delta_Consistency()
    {
        var a = SpecFactories.NewOrder();
        var b = SpecFactories.Clone(a);
        b.Notes = "changed";
        var doc = S_OrderDeepOps.ComputeDelta(a, b);
        Assert.False(doc.IsEmpty);
        var (hasDiff, diff) = S_OrderDeepOps.GetDiff(a, b);
        Assert.True(hasDiff);
        Assert.True(diff.HasChanges);
    }
}