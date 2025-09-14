using DeepEqual.Generator.Tests.Models;

namespace DeepEqual.Generator.Tests.Tests;

public class SetTypeTests
{
    [Fact]
    public void HashSet_Int_Order_Irrelevant_Content_Must_Match()
    {
        var A = new IntSetHolder { Set = new HashSet<int> { 1, 2, 3 } };
        var B = new IntSetHolder { Set = new HashSet<int> { 3, 2, 1 } };
        var C = new IntSetHolder { Set = new HashSet<int> { 1, 2 } };

        Assert.True(IntSetHolderDeepEqual.AreDeepEqual(A, B));
        Assert.False(IntSetHolderDeepEqual.AreDeepEqual(A, C));
    }

    [Fact]
    public void ISet_Of_Pocos_Deep_Element_Compare()
    {
        var A = new PersonSetHolder { People = new HashSet<Person> { new Person { Name = "a", Age = 1 }, new Person { Name = "b", Age = 2 } } };
        var B = new PersonSetHolder { People = new HashSet<Person> { new Person { Name = "b", Age = 2 }, new Person { Name = "a", Age = 1 } } };
        var C = new PersonSetHolder { People = new HashSet<Person> { new Person { Name = "a", Age = 1 }, new Person { Name = "b", Age = 99 } } };

        Assert.True(PersonSetHolderDeepEqual.AreDeepEqual(A, B));
        Assert.False(PersonSetHolderDeepEqual.AreDeepEqual(A, C));
    }
}