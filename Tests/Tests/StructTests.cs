using DeepEqual.Generator.Tests.Models;

namespace DeepEqual.Tests3;

public class StructTests
{
    [Fact]
    public void Structs_With_Different_DateTimeKind_Fail()
    {
        var a = new SimpleStruct { WhenUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        var b = new SimpleStruct { WhenUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Local) };
        Assert.False(SimpleStructDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void DateTime_Strict_Cases()
    {
        var tU1 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var tU2 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(DateTimeHolderDeepEqual.AreDeepEqual(new DateTimeHolder { When = tU1 }, new DateTimeHolder { When = tU2 }));

        var tKindDiff = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Local);
        Assert.False(DateTimeHolderDeepEqual.AreDeepEqual(new DateTimeHolder { When = tU1 }, new DateTimeHolder { When = tKindDiff }));

        var tTickDiff = new DateTime(2025, 1, 1, 12, 0, 1, DateTimeKind.Utc);
        Assert.False(DateTimeHolderDeepEqual.AreDeepEqual(new DateTimeHolder { When = tU1 }, new DateTimeHolder { When = tTickDiff }));
    }

    [Fact]
    public void DateTimeOffset_Strict_Cases()
    {
        var dt1 = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.FromHours(1));
        var dt2 = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.FromHours(1));
        Assert.True(DateTimeOffsetHolderDeepEqual.AreDeepEqual(new DateTimeOffsetHolder { When = dt1 }, new DateTimeOffsetHolder { When = dt2 }));

        var dtoOff = dt1.ToOffset(TimeSpan.FromHours(2));
        Assert.False(DateTimeOffsetHolderDeepEqual.AreDeepEqual(new DateTimeOffsetHolder { When = dt1 }, new DateTimeOffsetHolder { When = dtoOff }));
    }

    [Fact]
    public void Nullable_DateTime_Cases()
    {
        Assert.True(NullableDateTimeHolderDeepEqual.AreDeepEqual(new NullableDateTimeHolder { When = null }, new NullableDateTimeHolder { When = null }));
        Assert.False(NullableDateTimeHolderDeepEqual.AreDeepEqual(new NullableDateTimeHolder { When = null }, new NullableDateTimeHolder { When = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }));
        Assert.False(NullableDateTimeHolderDeepEqual.AreDeepEqual(new NullableDateTimeHolder { When = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }, new NullableDateTimeHolder { When = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Local) }));
    }

    [Fact]
    public void Enum_Equality_By_Value()
    {
        var a = new EnumHolder { Shade = Color.Blue };
        var b = new EnumHolder { Shade = Color.Blue };
        var c = new EnumHolder { Shade = Color.Red };
        Assert.True(EnumHolderDeepEqual.AreDeepEqual(a, b));
        Assert.False(EnumHolderDeepEqual.AreDeepEqual(a, c));
    }

    [Fact]
    public void Guid_Equality_By_Value()
    {
        var g = Guid.NewGuid();
        var a = new GuidHolder { Id = g };
        var b = new GuidHolder { Id = g };
        var c = new GuidHolder { Id = Guid.NewGuid() };
        Assert.True(GuidHolderDeepEqual.AreDeepEqual(a, b));
        Assert.False(GuidHolderDeepEqual.AreDeepEqual(a, c));
    }

    [Fact]
    public void DateOnly_TimeOnly_Strict()
    {
        var a = new DateOnlyTimeOnlyHolder { D = new DateOnly(2025, 1, 1), T = new TimeOnly(12, 0, 0) };
        var b = new DateOnlyTimeOnlyHolder { D = new DateOnly(2025, 1, 1), T = new TimeOnly(12, 0, 0) };
        var c = new DateOnlyTimeOnlyHolder { D = new DateOnly(2025, 1, 2), T = new TimeOnly(12, 0, 0) };
        var d = new DateOnlyTimeOnlyHolder { D = new DateOnly(2025, 1, 1), T = new TimeOnly(12, 0, 1) };
        Assert.True(DateOnlyTimeOnlyHolderDeepEqual.AreDeepEqual(a, b));
        Assert.False(DateOnlyTimeOnlyHolderDeepEqual.AreDeepEqual(a, c));
        Assert.False(DateOnlyTimeOnlyHolderDeepEqual.AreDeepEqual(a, d));
    }
}