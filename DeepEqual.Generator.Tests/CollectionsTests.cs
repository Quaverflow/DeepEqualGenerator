using System.Linq;
using DeepEqual;
using Xunit;
using static DeepEqual.RewrittenTests.UnifiedFixture;

namespace DeepEqual.RewrittenTests;

public class CollectionsTests
{
    [Fact]
    public void Queue_And_Stack_Are_Ordered()
    {
        var a = MakeBaseline();
        var b = Clone(a);

        // Mutate queue order by dequeue+enqueue
        var first = b.Queue.Dequeue();
        b.Queue.Enqueue(first);

        Assert.False(a.AreDeepEqual(b));

        // Mutate stack by pop+push
        var pop = b.Stack.Pop();
        b.Stack.Push(99);
        Assert.False(a.AreDeepEqual(b));
    }

    [Fact]
    public void Sets_Are_Insensitive_To_Order_But_Sensitive_To_Membership()
    {
        var a = MakeBaseline();
        var b = Clone(a);

        // Re-create 'b' flags with different insertion order
        b.Flags.Clear();
        foreach (var f in a.Flags.Reverse()) b.Flags.Add(f);
        Assert.True(a.AreDeepEqual(b));

        b.Flags.Add("Z");
        Assert.False(a.AreDeepEqual(b));
    }
}