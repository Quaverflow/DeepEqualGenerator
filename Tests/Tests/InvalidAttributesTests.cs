using DeepEqual.Generator.Tests.Models;

namespace DeepEqual.Generator.Tests.Tests;

public class InvalidAttributesTests
{
    [Fact]
    public void Bad_KeyMembers_Falls_Back_To_Unkeyed_Unordered()
    {
        var A = new BadKeyMembersBag { Items = new() { new Item { Name = "a", X = 1 }, new Item { Name = "b", X = 2 } } };
        var B = new BadKeyMembersBag { Items = new() { new Item { Name = "b", X = 2 }, new Item { Name = "a", X = 1 } } };
        var C = new BadKeyMembersBag { Items = new() { new Item { Name = "a", X = 1 } } };

        Assert.True(BadKeyMembersBagDeepEqual.AreDeepEqual(A, B));
        Assert.False(BadKeyMembersBagDeepEqual.AreDeepEqual(A, C));
    }
}