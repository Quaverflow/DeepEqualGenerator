using Tests.ValueType.Models;

namespace DeepEqual.Tests;

public sealed class StructEqualityTests
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
    public void SimpleStruct_ValueDifference_Fails()
    {
        var a = new SimpleStruct { Count = 3, Ratio = 2.5, Price = 9.99m, WhenUtc = DateTime.SpecifyKind(new DateTime(2025, 1, 2, 3, 4, 5), DateTimeKind.Utc), Role = SRole.Alpha };
        var b = new SimpleStruct { Count = 4, Ratio = 2.5, Price = 9.99m, WhenUtc = DateTime.SpecifyKind(new DateTime(2025, 1, 2, 3, 4, 5), DateTimeKind.Utc), Role = SRole.Alpha };

        Assert.False(SimpleStructDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void NestedStruct_DeepComparison_Passes()
    {
        var inner = new SimpleStruct { Count = 1, Ratio = 1.0, Price = 1.00m, WhenUtc = DateTime.SpecifyKind(new DateTime(2025, 1, 1, 0, 0, 0), DateTimeKind.Utc), Role = SRole.Beta };
        var a = new NestedStruct { Inner = inner, Id = 42 };
        var b = new NestedStruct { Inner = inner, Id = 42 };

        Assert.True(NestedStructDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void NestedStruct_InnerDifference_Fails()
    {
        var a = new NestedStruct
        {
            Inner = new SimpleStruct { Count = 1, Ratio = 1.0, Price = 1.00m, WhenUtc = DateTime.SpecifyKind(new DateTime(2025, 1, 1, 0, 0, 0), DateTimeKind.Utc), Role = SRole.Beta },
            Id = 42
        };
        var b = new NestedStruct
        {
            Inner = new SimpleStruct { Count = 2, Ratio = 1.0, Price = 1.00m, WhenUtc = DateTime.SpecifyKind(new DateTime(2025, 1, 1, 0, 0, 0), DateTimeKind.Utc), Role = SRole.Beta },
            Id = 42
        };

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
        var a = new StructArrayHolder
        {
            Items = [new SimpleStruct { Count = 1 }, new SimpleStruct { Count = 2 }]
        };
        var b = new StructArrayHolder
        {
            Items = [new SimpleStruct { Count = 1 }, new SimpleStruct { Count = 3 }]
        };

        Assert.False(StructArrayHolderDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void List_Of_Structs_Ordered()
    {
        var a = new StructListHolder
        {
            Items = [new() { Count = 1 }, new() { Count = 2 }]
        };
        var b = new StructListHolder
        {
            Items = [new() { Count = 2 }, new() { Count = 1 }]
        };

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
    public void Nullable_Struct_In_Class_Null_Both_Pass()
    {
        var a = new NullableStructHolder { Maybe = null };
        var b = new NullableStructHolder { Maybe = null };
        Assert.True(NullableStructHolderDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Nullable_Struct_In_Class_Null_Mismatch_Fails()
    {
        var a = new NullableStructHolder { Maybe = null };
        var b = new NullableStructHolder { Maybe = new SimpleStruct { Count = 1 } };
        Assert.False(NullableStructHolderDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Nullable_Struct_In_Class_Value_Equal_Passes()
    {
        var a = new NullableStructHolder { Maybe = new SimpleStruct { Count = 5, Role = SRole.Beta } };
        var b = new NullableStructHolder { Maybe = new SimpleStruct { Count = 5, Role = SRole.Beta } };
        Assert.True(NullableStructHolderDeepEqual.AreDeepEqual(a, b));
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
    public void DateTime_Strict_Equal_When_Ticks_And_Kind_Match()
    {
        var t1 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var a = new DateTimeHolder { When = t1 };
        var b = new DateTimeHolder { When = t2 };

        Assert.True(DateTimeHolderDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void DateTime_Strict_Fails_When_Kind_Differs()
    {
        var t1 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Local);
        var a = new DateTimeHolder { When = t1 };
        var b = new DateTimeHolder { When = t2 };

        Assert.False(DateTimeHolderDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void DateTime_Strict_Fails_When_Ticks_Differ()
    {
        var t1 = new DateTime(2025, 1, 1, 12, 0, 1, DateTimeKind.Utc);
        var t2 = new DateTime(2025, 1, 1, 12, 0, 2, DateTimeKind.Utc);
        var a = new DateTimeHolder { When = t1 };
        var b = new DateTimeHolder { When = t2 };

        Assert.False(DateTimeHolderDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void DateTimeOffset_Strict_Equal_When_Ticks_And_Offset_Match()
    {
        var t1 = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.FromHours(1));
        var t2 = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.FromHours(1));
        var a = new DateTimeOffsetHolder { When = t1 };
        var b = new DateTimeOffsetHolder { When = t2 };

        Assert.True(DateTimeOffsetHolderDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void DateTimeOffset_Strict_Fails_When_Offsets_Differ()
    {
        var t1 = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var t2 = t1.ToOffset(TimeSpan.FromHours(2));
        var a = new DateTimeOffsetHolder { When = t1 };
        var b = new DateTimeOffsetHolder { When = t2 };

        Assert.False(DateTimeOffsetHolderDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Nullable_DateTime_BothNull_Pass()
    {
        var a = new NullableDateTimeHolder { When = null };
        var b = new NullableDateTimeHolder { When = null };
        Assert.True(NullableDateTimeHolderDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Nullable_DateTime_OneNull_Fails()
    {
        var a = new NullableDateTimeHolder { When = null };
        var b = new NullableDateTimeHolder { When = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        Assert.False(NullableDateTimeHolderDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Nullable_DateTime_Strict_Value_Comparison()
    {
        var a = new NullableDateTimeHolder { When = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        var b = new NullableDateTimeHolder { When = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Local) };
        Assert.False(NullableDateTimeHolderDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Arrays_Lists_Dictionaries_Use_Strict_Time_Comparison()
    {
        var dt1 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dt2 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Local);
        var dto1 = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var dto2 = dto1.ToOffset(TimeSpan.FromHours(1));

        var a = new CollectionsOfTimeHolder
        {
            Snapshots = [dt1, dt1],
            Events = [dt1],
            Index = new Dictionary<string, DateTimeOffset> { ["a"] = dto1 }
        };
        var b = new CollectionsOfTimeHolder
        {
            Snapshots = [dt1, dt2],
            Events = [dt1],
            Index = new Dictionary<string, DateTimeOffset> { ["a"] = dto2 }
        };

        Assert.False(CollectionsOfTimeHolderDeepEqual.AreDeepEqual(a, b));
    }

}
