using DeepEqual.Generator.Tests.Models;

namespace DeepEqual.Generator.Tests.Tests;

public class SetTypeTests
{
    [Fact]
    public void HashSet_Int_Order_Irrelevant_Content_Must_Match()
    {
        var a = new IntSetHolder { Set = [1, 2, 3] };
        var b = new IntSetHolder { Set = [3, 2, 1] };
        var c = new IntSetHolder { Set = [1, 2] };

        Assert.True(IntSetHolderDeepEqual.AreDeepEqual(a, b));
        Assert.False(IntSetHolderDeepEqual.AreDeepEqual(a, c));
    }

    [Fact]
    public void ISet_Of_Pocos_Deep_Element_Compare()
    {
        var a = new PersonSetHolder { People = new HashSet<Person> { new() { Name = "a", Age = 1 }, new() { Name = "b", Age = 2 } } };
        var b = new PersonSetHolder { People = new HashSet<Person> { new() { Name = "b", Age = 2 }, new() { Name = "a", Age = 1 } } };
        var c = new PersonSetHolder { People = new HashSet<Person> { new() { Name = "a", Age = 1 }, new() { Name = "b", Age = 99 } } };

        Assert.True(PersonSetHolderDeepEqual.AreDeepEqual(a, b));
        Assert.False(PersonSetHolderDeepEqual.AreDeepEqual(a, c));
    }
}