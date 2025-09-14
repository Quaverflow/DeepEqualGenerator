using System;
using System.Collections.Generic;
using DeepEqual.Generator.Shared;
using Xunit;

namespace DeepEqual.Tests;

public sealed class StructTests
{
    [Fact]
    public void SimpleStruct_ValueEquality_Passes()
    {
        var a = new SimpleStruct
        {
            Count = 3,
            Ratio = 2.5,
            Price = 9.99m,
            WhenUtc = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            Role = SRole.Alpha
        };
        var b = new SimpleStruct
        {
            Count = 3,
            Ratio = 2.5,
            Price = 9.99m,
            WhenUtc = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            Role = SRole.Alpha
        };

        Assert.True(SimpleStructDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void SimpleStruct_ValueDifference_Fails()     {
        var a = new SimpleStruct { Count = 3, Ratio = 2.5, Price = 9.99m, WhenUtc = DateTime.SpecifyKind(new DateTime(2025, 1, 2, 3, 4, 5), DateTimeKind.Utc), Role = SRole.Alpha };
        var b = new SimpleStruct { Count = 4, Ratio = 2.5, Price = 9.99m, WhenUtc = DateTime.SpecifyKind(new DateTime(2025, 1, 2, 3, 4, 5), DateTimeKind.Utc), Role = SRole.Alpha };
        Assert.False(SimpleStructDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void NestedStruct_DeepComparison_Passes()
    {
        var inner = new SimpleStruct { Count = 1, Ratio = 1.0, Price = 1.00m, WhenUtc = DateTime.SpecifyKind(new DateTime(2025, 1, 1), DateTimeKind.Utc), Role = SRole.Beta };
        var a = new NestedStruct { Inner = inner, Id = 42 };
        var b = new NestedStruct { Inner = inner, Id = 42 };
        Assert.True(NestedStructDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void NestedStruct_InnerDifference_Fails()
    {
        var a = new NestedStruct { Inner = new SimpleStruct { Count = 1 }, Id = 1 };
        var b = new NestedStruct { Inner = new SimpleStruct { Count = 2 }, Id = 1 };
        Assert.False(NestedStructDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void StructWithNullable_AllNulls_Pass()
    {
        var a = new StructWithNullable { MaybeInt = null, MaybeTime = null, MaybeRole = null };
        var b = new StructWithNullable { MaybeInt = null, MaybeTime = null, MaybeRole = null };
        Assert.True(StructWithNullableDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void StructWithNullable_MixedNulls_Fail()
    {
        var a = new StructWithNullable { MaybeInt = 5, MaybeTime = null, MaybeRole = SRole.Alpha };
        var b = new StructWithNullable { MaybeInt = 5, MaybeTime = DateTime.SpecifyKind(new DateTime(2025, 2, 3), DateTimeKind.Utc), MaybeRole = SRole.Alpha };
        Assert.False(StructWithNullableDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void StructWithReference_StringOrdinalComparison()
    {
        var a = new StructWithReference { Name = "Test", Code = "A1" };
        var b = new StructWithReference { Name = "Test", Code = "A1" };
        Assert.True(StructWithReferenceDeepEqual.AreDeepEqual(a, b));

        b.Name = "test";
        Assert.False(StructWithReferenceDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Array_Of_Structs_Equal()
    {
        var arr1 = new[]
        {
            new SimpleStruct { Count = 1, Ratio = 1, Price = 1m, WhenUtc = DateTime.SpecifyKind(new DateTime(2025,1,1), DateTimeKind.Utc), Role = SRole.None },
            new SimpleStruct { Count = 2, Ratio = 2, Price = 2m, WhenUtc = DateTime.SpecifyKind(new DateTime(2025,1,2), DateTimeKind.Utc), Role = SRole.Alpha }
        };
        var arr2 = new[]
        {
            new SimpleStruct { Count = 1, Ratio = 1, Price = 1m, WhenUtc = DateTime.SpecifyKind(new DateTime(2025,1,1), DateTimeKind.Utc), Role = SRole.None },
            new SimpleStruct { Count = 2, Ratio = 2, Price = 2m, WhenUtc = DateTime.SpecifyKind(new DateTime(2025,1,2), DateTimeKind.Utc), Role = SRole.Alpha }
        };
        var a = new StructArrayHolder { Items = arr1 };
        var b = new StructArrayHolder { Items = arr2 };

        Assert.True(StructArrayHolderDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Array_Of_Structs_Difference_Fails()
    {
        var a = new StructArrayHolder { Items = new[] { new SimpleStruct { Count = 1 }, new SimpleStruct { Count = 2 } } };
        var b = new StructArrayHolder { Items = new[] { new SimpleStruct { Count = 1 }, new SimpleStruct { Count = 3 } } };
        Assert.False(StructArrayHolderDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void List_Of_Structs_Ordered()
    {
        var a = new StructListHolder { Items = new List<SimpleStruct> { new() { Count = 1 }, new() { Count = 2 } } };
        var b = new StructListHolder { Items = new List<SimpleStruct> { new() { Count = 2 }, new() { Count = 1 } } };
        Assert.False(StructListHolderDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Dictionary_With_Struct_Values_Deep()
    {
        var a = new StructDictionaryHolder
        {
            Map = new Dictionary<string, SimpleStruct>
            {
                ["x"] = new SimpleStruct { Count = 1, Ratio = 1 },
                ["y"] = new SimpleStruct { Count = 2, Ratio = 2 }
            }
        };
        var b = new StructDictionaryHolder
        {
            Map = new Dictionary<string, SimpleStruct>
            {
                ["x"] = new SimpleStruct { Count = 1, Ratio = 1 },
                ["y"] = new SimpleStruct { Count = 2, Ratio = 3 }
            }
        };
        Assert.False(StructDictionaryHolderDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Nullable_Struct_In_Class_Cases()
    {
        var bothNullA = new NullableStructHolder { Maybe = null };
        var bothNullB = new NullableStructHolder { Maybe = null };
        Assert.True(NullableStructHolderDeepEqual.AreDeepEqual(bothNullA, bothNullB));

        var oneNullA = new NullableStructHolder { Maybe = null };
        var oneNullB = new NullableStructHolder { Maybe = new SimpleStruct { Count = 1 } };
        Assert.False(NullableStructHolderDeepEqual.AreDeepEqual(oneNullA, oneNullB));

        var equalA = new NullableStructHolder { Maybe = new SimpleStruct { Count = 5, Role = SRole.Beta } };
        var equalB = new NullableStructHolder { Maybe = new SimpleStruct { Count = 5, Role = SRole.Beta } };
        Assert.True(NullableStructHolderDeepEqual.AreDeepEqual(equalA, equalB));
    }

    [Fact]
    public void Default_Structs_Are_Equal()
    {
        var a = default(SimpleStruct);
        var b = default(SimpleStruct);
        Assert.True(SimpleStructDeepEqual.AreDeepEqual(a, b));
    }

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
    public void Collections_Of_Time_Use_Strict_Comparison()
    {
        var dtU = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dtL = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Local);
        var dto0 = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var dto1 = dto0.ToOffset(TimeSpan.FromHours(1));

        var a = new CollectionsOfTimeHolder { Snapshots = new[] { dtU, dtU }, Events = new List<DateTime> { dtU }, Index = new Dictionary<string, DateTimeOffset> { ["a"] = dto0 } };
        var b = new CollectionsOfTimeHolder { Snapshots = new[] { dtU, dtL }, Events = new List<DateTime> { dtU }, Index = new Dictionary<string, DateTimeOffset> { ["a"] = dto1 } };
        Assert.False(CollectionsOfTimeHolderDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void MultiDimensional_Arrays_Validate_Shape()
    {
        var a = new MultiDimArrayHolder { Grid = new int[,] { { 1, 2 }, { 3, 4 } } };
        var b = new MultiDimArrayHolder { Grid = new int[,] { { 1, 2, 3 }, { 4, 0, 0 } } };
        Assert.False(MultiDimArrayHolderDeepEqual.AreDeepEqual(a, b));     }

    [Fact]
    public void Unordered_Array_Member_Uses_Multiset_Semantics()
    {
        var r1 = new UnorderedArrayHolder { Items = new[] { 1, 2, 2, 3 } };
        var r2 = new UnorderedArrayHolder { Items = new[] { 2, 3, 2, 1 } };
        Assert.True(UnorderedArrayHolderDeepEqual.AreDeepEqual(r1, r2));

        r1.Ordered = new[] { 1, 2, 3 };
        r2.Ordered = new[] { 3, 2, 1 };
        Assert.False(UnorderedArrayHolderDeepEqual.AreDeepEqual(r1, r2));
    }



    [Fact]
    public void DateOnly_TimeOnly_Compare_By_Value()
    {
        var a = new DateOnlyTimeOnlyHolder { Day = new DateOnly(2025, 1, 1), Time = new TimeOnly(12, 0, 0) };
        var b = new DateOnlyTimeOnlyHolder { Day = new DateOnly(2025, 1, 1), Time = new TimeOnly(12, 0, 0) };
        Assert.True(DateOnlyTimeOnlyHolderDeepEqual.AreDeepEqual(a, b));

        b.Time = new TimeOnly(12, 0, 1);
        Assert.False(DateOnlyTimeOnlyHolderDeepEqual.AreDeepEqual(a, b));
    }
}

/* ------------ Models for struct tests (self-contained) ------------------- */

public enum SRole { None, Alpha, Beta }

[DeepComparable]
public struct SimpleStruct
{
    public int Count;
    public double Ratio;
    public decimal Price;
    public DateTime WhenUtc;
    public SRole Role;
}

[DeepComparable]
public struct NestedStruct
{
    public SimpleStruct Inner;
    public int Id;
}

[DeepComparable]
public struct StructWithNullable
{
    public int? MaybeInt;
    public DateTime? MaybeTime;
    public SRole? MaybeRole;
}

[DeepComparable]
public struct StructWithReference
{
    public string? Name;
    public string? Code;
}

[DeepComparable]
public sealed class StructArrayHolder
{
    public SimpleStruct[]? Items { get; set; }
}

[DeepComparable]
public sealed class StructListHolder
{
    public List<SimpleStruct>? Items { get; set; }
}

[DeepComparable]
public sealed class StructDictionaryHolder
{
    public Dictionary<string, SimpleStruct>? Map { get; set; }
}

[DeepComparable]
public sealed class NullableStructHolder
{
    public SimpleStruct? Maybe { get; set; }
}

[DeepComparable]
public sealed class DateTimeHolder
{
    public DateTime When { get; set; }
}

[DeepComparable]
public sealed class DateTimeOffsetHolder
{
    public DateTimeOffset When { get; set; }
}

[DeepComparable]
public sealed class NullableDateTimeHolder
{
    public DateTime? When { get; set; }
}

[DeepComparable]
public sealed class CollectionsOfTimeHolder
{
    public DateTime[]? Snapshots { get; set; }
    public List<DateTime>? Events { get; set; }
    public Dictionary<string, DateTimeOffset>? Index { get; set; }
}

[DeepComparable]
public sealed class MultiDimArrayHolder
{
    public int[,]? Grid { get; set; }
}

[DeepComparable]
public sealed class UnorderedArrayHolder
{
    [DeepCompare(OrderInsensitive = true)]
    public int[]? Items { get; set; }

    [DeepCompare(OrderInsensitive = false)]
    public int[]? Ordered { get; set; }
}



[DeepComparable]
public sealed class DateOnlyTimeOnlyHolder
{
    public DateOnly Day { get; set; }
    public TimeOnly Time { get; set; }
}
