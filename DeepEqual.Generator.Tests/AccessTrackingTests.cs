using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DeepEqual.Generator.Shared;
using Xunit;

namespace DeepEqual.Generator.Tests;

public sealed class AccessTrackingTests : IDisposable
{
    public AccessTrackingTests()
    {
        ResetConfig();
    }

    public void Dispose()
    {
        ResetConfig();
    }

    private static void ResetConfig()
    {
        AccessTracking.Configure(
            defaultMode: AccessMode.None,
            defaultGranularity: AccessGranularity.Bits,
            defaultLogCapacity: 0,
            trackingEnabled: true,
            countsEnabled: true,
            lastEnabled: true,
            logEnabled: true,
            callersEnabled: true);
    }

    [Fact]
    public void BitsTrackingRecordsWrite()
    {
        var model = new BitsModel();

        using (var before = JsonDocument.Parse(model.__DumpAccessJson()))
        {
            var member = GetMember(before, nameof(BitsModel.Value));
            Assert.False(member.GetProperty("written").GetBoolean());
        }

        model.Value = 42;

        var jsonAfter = model.__DumpAccessJson(reset: true);
        Console.WriteLine(jsonAfter);
        using var after = JsonDocument.Parse(jsonAfter);
        var tracked = GetMember(after, nameof(BitsModel.Value));
        Assert.True(tracked.GetProperty("written").GetBoolean());
        Assert.False(tracked.TryGetProperty("count", out _));
    }

    [Fact]
    public void CountsTrackingAccumulates()
    {
        var model = new CountsModel();
        model.Value = 1;
        model.Value = 2;
        model.Value = 3;

        using var doc = JsonDocument.Parse(model.__DumpAccessJson(reset: true));
        var member = GetMember(doc, nameof(CountsModel.Value));
        Assert.True(member.GetProperty("written").GetBoolean());
        Assert.Equal(3, member.GetProperty("count").GetInt32());
    }

    [Fact]
    public void CountsAndLastCapturesTimestamp()
    {
        var model = new CountsAndLastModel();
        model.Value = 10;
        using var after = JsonDocument.Parse(model.__DumpAccessJson(reset: true));
        var member = GetMember(after, nameof(CountsAndLastModel.Value));
        var last = member.GetProperty("last").GetString();
        Assert.NotNull(last);
        Assert.NotEqual("—", last);
        Assert.True(member.GetProperty("count").GetInt32() >= 1);
    }

    [Fact]
    public void PropertyOverrideRespectsGranularity()
    {
        var model = new OverrideModel();
        model.Primary = 5;
        model.Secondary = 6;
        model.Secondary = 7;

        using var doc = JsonDocument.Parse(model.__DumpAccessJson(reset: true));
        var primary = GetMember(doc, nameof(OverrideModel.Primary));
        var secondary = GetMember(doc, nameof(OverrideModel.Secondary));

        Assert.False(primary.TryGetProperty("count", out _));
        Assert.True(secondary.TryGetProperty("count", out var countProperty));
        Assert.Equal(2, countProperty.GetInt32());
        Assert.NotEqual("—", secondary.GetProperty("last").GetString());
    }

    [Fact]
    public void GlobalDisableSkipsTracking()
    {
        var model = new CountsModel();
        AccessTracking.Configure(
            defaultMode: AccessMode.None,
            defaultGranularity: AccessGranularity.Bits,
            defaultLogCapacity: 0,
            trackingEnabled: false,
            countsEnabled: true,
            lastEnabled: true,
            logEnabled: true,
            callersEnabled: true);

        model.Value = 1;

        using var doc = JsonDocument.Parse(model.__DumpAccessJson(reset: true));
        Assert.False(doc.RootElement.GetProperty("enabled").GetBoolean());
        var member = GetMember(doc, nameof(CountsModel.Value));
        Assert.False(member.GetProperty("written").GetBoolean());
        Assert.False(member.TryGetProperty("count", out _));
    }

    [Fact]
    public void CallerScopesAggregateTopK()
    {
        AccessTracking.Configure(
            defaultMode: AccessMode.Write,
            defaultGranularity: AccessGranularity.Counts,
            defaultLogCapacity: 128,
            trackingEnabled: true,
            countsEnabled: true,
            lastEnabled: true,
            logEnabled: true,
            callersEnabled: true);

        var model = new CountsModel();

        using (AccessTracking.PushScope(label: "outer"))
        {
            model.Value = 1;
            model.Value = 2;
        }

        using (AccessTracking.PushScope(label: "outer"))
        {
            using (AccessTracking.PushScope(label: "inner"))
            {
                model.Value = 3;
            }
        }

        var a = model.__DumpAccessJson();
        using var doc = JsonDocument.Parse(model.__DumpAccessJson(reset: true));

        var member = GetMember(doc, nameof(CountsModel.Value));
        Assert.True(member.TryGetProperty("callers", out var callersProperty));
        var callers = callersProperty.EnumerateArray().ToList();
        Assert.True(callers.Count >= 1);
        var outer = callers.First(e => e.TryGetProperty("label", out var label) && label.GetString() == "outer");
        Assert.Equal(3, outer.GetProperty("count").GetInt32());
        var inner = callers.First(e => e.TryGetProperty("label", out var label) && label.GetString() == "inner");
        Assert.Equal(1, inner.GetProperty("count").GetInt32());
    }

    [Fact]
    public void EventLogRespectsCapacity()
    {
        var model = new LoggingModel();
        for (var i = 0; i < 6; i++)
        {
            using (AccessTracking.PushScope(label: $"scope-{i}"))
            {
                model.Value = i;
            }
        }

        using var doc = JsonDocument.Parse(model.__DumpAccessJson(reset: true));
        Assert.True(doc.RootElement.TryGetProperty("events", out var eventsProperty));
        var events = eventsProperty.EnumerateArray().ToArray();
        Assert.Equal(LoggingModel.Capacity, events.Length);
        var labels = events.Select(e => e.TryGetProperty("label", out var label) ? label.GetString() : null).ToArray();
        Assert.Equal(new[] { "scope-5", "scope-4", "scope-3" }, labels);
    }

    [Fact]
    public void ResetClearsState()
    {
        var model = new CountsModel();
        model.Value = 1;
        model.Value = 2;
        model.__ResetAccessTracking();

        using var doc = JsonDocument.Parse(model.__DumpAccessJson());
        var member = GetMember(doc, nameof(CountsModel.Value));
        Assert.False(member.GetProperty("written").GetBoolean());
        Assert.Equal(0, member.GetProperty("count").GetInt32());
    }

    [Fact]
    public void CountsHandleConcurrency()
    {
        var model = new CountsModel();
        Parallel.For(0, 1000, i => model.Value = i);

        using var doc = JsonDocument.Parse(model.__DumpAccessJson(reset: true));
        var member = GetMember(doc, nameof(CountsModel.Value));
        Assert.Equal(1000, member.GetProperty("count").GetInt32());
    }

    private static JsonElement GetMember(JsonDocument doc, string name)
    {
        foreach (var member in doc.RootElement.GetProperty("members").EnumerateArray())
        {
            if (member.GetProperty("name").GetString() == name)
            {
                return member;
            }
        }

        throw new KeyNotFoundException($"Member '{name}' not found in dump.");
    }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true, StableMemberIndex = StableMemberIndexMode.Auto)]
[DeltaTrack(AccessTrack = AccessMode.Write, AccessGranularity = AccessGranularity.Bits)]
public partial class BitsModel
{
    private int _value;
    public int Value
    {
        get => _value;
        set
        {
            _value = value;
            __MarkDirty(__Bit_Value);
        }
    }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true, StableMemberIndex = StableMemberIndexMode.Auto)]
[DeltaTrack(AccessTrack = AccessMode.Write, AccessGranularity = AccessGranularity.Counts)]
public partial class CountsModel
{
    private int _value;
    public int Value
    {
        get => _value;
        set
        {
            _value = value;
            __MarkDirty(__Bit_Value);
        }
    }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true, StableMemberIndex = StableMemberIndexMode.Auto)]
[DeltaTrack(AccessTrack = AccessMode.Write, AccessGranularity = AccessGranularity.CountsAndLast)]
public partial class CountsAndLastModel
{
    private int _value;
    public int Value
    {
        get => _value;
        set
        {
            _value = value;
            __MarkDirty(__Bit_Value);
        }
    }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true, StableMemberIndex = StableMemberIndexMode.Auto)]
[DeltaTrack(AccessTrack = AccessMode.Write)]
public partial class OverrideModel
{
    private int _primary;
    private int _secondary;

    public int Primary
    {
        get => _primary;
        set
        {
            _primary = value;
            __MarkDirty(__Bit_Primary);
        }
    }

    [AccessTrack(Granularity = AccessGranularity.CountsAndLast)]
    public int Secondary
    {
        get => _secondary;
        set
        {
            _secondary = value;
            __MarkDirty(__Bit_Secondary);
        }
    }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true, StableMemberIndex = StableMemberIndexMode.Auto)]
[DeltaTrack(AccessTrack = AccessMode.Write, AccessGranularity = AccessGranularity.CountsAndLast, AccessLogCapacity = Capacity)]
public partial class LoggingModel
{
    public const int Capacity = 3;
    private int _value;
    public int Value
    {
        get => _value;
        set
        {
            _value = value;
            __MarkDirty(__Bit_Value);
        }
    }
}


