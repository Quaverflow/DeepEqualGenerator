using DeepEqual.Generator.Tests.Models;

namespace DeepEqual.Generator.Tests.Tests;

public class InvalidAttributesTests
{
    [Fact]
    public void Bad_KeyMembers_Falls_Back_To_Unkeyed_Unordered()
    {
        var a = new BadKeyMembersBag { Items = new() { new Item { Name = "a", X = 1 }, new Item { Name = "b", X = 2 } } };
        var b = new BadKeyMembersBag { Items = new() { new Item { Name = "b", X = 2 }, new Item { Name = "a", X = 1 } } };
        var c = new BadKeyMembersBag { Items = new() { new Item { Name = "a", X = 1 } } };

        Assert.True(BadKeyMembersBagDeepEqual.AreDeepEqual(a, b));
        Assert.False(BadKeyMembersBagDeepEqual.AreDeepEqual(a, c));
    }
}