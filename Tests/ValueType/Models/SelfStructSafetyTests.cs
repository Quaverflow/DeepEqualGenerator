namespace DeepEqual.Tests;

public sealed class SelfStructSafetyTests
{
    [Fact]
    public void Deep_walk_handles_struct_self_cycles_via_reference_indirection()
    {
        var boxA = new SelfBox { Value = new SelfStruct { X = 2, Next = null } };
        var a = new SelfStructRoot { Payload = new SelfStruct { X = 1, Next = boxA } };
        boxA.Value = new SelfStruct { X = 2, Next = new SelfBox { Value = a.Payload } };

        var boxB = new SelfBox { Value = new SelfStruct { X = 2, Next = null } };
        var b = new SelfStructRoot { Payload = new SelfStruct { X = 1, Next = boxB } };
        boxB.Value = new SelfStruct { X = 2, Next = new SelfBox { Value = b.Payload } };

        Assert.True(SelfStructRootDeepEqual.AreDeepEqual(a, b));

        boxB.Value = new SelfStruct { X = 3, Next = boxB.Value.Next }; // tweak X
        Assert.False(SelfStructRootDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Nullable_like_behavior_via_indirection_still_deep_compares()
    {
        var a = new SelfStructRoot { Payload = new SelfStruct { X = 10, Next = null } };
        var b = new SelfStructRoot { Payload = new SelfStruct { X = 10, Next = null } };
        Assert.True(SelfStructRootDeepEqual.AreDeepEqual(a, b));

        b.Payload = new SelfStruct { X = 11, Next = null };
        Assert.False(SelfStructRootDeepEqual.AreDeepEqual(a, b));
    }
}