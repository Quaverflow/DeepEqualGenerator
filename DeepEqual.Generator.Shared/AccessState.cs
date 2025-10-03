using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace DeepEqual.Generator.Shared;

[Flags]
public enum AccessMemberFlags : byte
{
    None = 0,
    Track = 1 << 0,
    Counts = 1 << 1,
    Last = 1 << 2,
    Caller = 1 << 3,
}

[Flags]
public enum AccessLogPolicy : byte
{
    None = 0,
    Allowed = 1 << 0,
    Forced = 1 << 1,
}

public sealed class AccessDescriptor
{
    public AccessDescriptor(string typeName, string[] memberNames, AccessMemberFlags[] memberFlags, AccessLogPolicy[] memberLogPolicies, int typeLogCapacity, int[] memberForcedLogCapacities)
    {
        ArgumentNullException.ThrowIfNull(typeName);
        ArgumentNullException.ThrowIfNull(memberNames);
        ArgumentNullException.ThrowIfNull(memberFlags);
        ArgumentNullException.ThrowIfNull(memberLogPolicies);
        ArgumentNullException.ThrowIfNull(memberForcedLogCapacities);

        if (memberNames.Length != memberFlags.Length || memberNames.Length != memberLogPolicies.Length || memberNames.Length != memberForcedLogCapacities.Length)
            throw new ArgumentException("Access descriptor arrays must have matching length.");

        TypeName = typeName;
        MemberNames = memberNames;
        MemberFlags = memberFlags;
        MemberLogPolicies = memberLogPolicies;
        MemberForcedLogCapacities = memberForcedLogCapacities;
        TypeLogCapacity = typeLogCapacity;
        MemberCount = memberNames.Length;

        var combined = AccessMemberFlags.None;
        var tracked = 0;
        for (var i = 0; i < memberFlags.Length; i++)
        {
            combined |= memberFlags[i];
            if ((memberFlags[i] & AccessMemberFlags.Track) != 0)
                tracked++;
        }

        CombinedFlags = combined;
        TrackedMemberCount = tracked;
    }

    public string TypeName { get; }
    public string[] MemberNames { get; }
    public AccessMemberFlags[] MemberFlags { get; }
    public AccessLogPolicy[] MemberLogPolicies { get; }
    public int[] MemberForcedLogCapacities { get; }
    public int TypeLogCapacity { get; }
    public int MemberCount { get; }
    public int TrackedMemberCount { get; }
    public AccessMemberFlags CombinedFlags { get; }
    public bool HasTrackedMembers => TrackedMemberCount > 0;
}

public readonly struct MappingInfo
{
    public MappingInfo(string typeName, string[] memberNames)
    {
        TypeName = typeName;
        MemberNames = memberNames;
    }

    public string TypeName { get; }
    public string[] MemberNames { get; }
}

public readonly struct AccessEvent
{
    public AccessEvent(ushort memberIndex, long ticks, int callerId)
    {
        MemberIndex = memberIndex;
        Ticks = ticks;
        CallerId = callerId;
    }

    public ushort MemberIndex { get; }
    public long Ticks { get; }
    public int CallerId { get; }
}

internal struct AccessTopEntry
{
    public int CallerId;
    public int Count;
}

public struct AccessState
{
#if ACCESS_TRACK
    private const int TopK = 4;

    private AccessDescriptor? _descriptor;
    private int _appliedConfigVersion;
    private int _instanceId;
    private DateTimeOffset _since;
    private long _sinceTicks;
    private ulong _writeBits0;
    private ulong[]? _writeBitsEx;
#if ACCESS_TRACK_COUNTS
    private int[]? _counts;
#endif
#if ACCESS_TRACK_LAST
    private long[]? _last;
#endif
    private AccessTopEntry[]? _topCallers;
#if ACCESS_TRACK_LOG
    private AccessEvent[]? _log;
    private bool[]? _logMembers;
    private int _logCursor;
    private int _logCount;
#endif
#endif

    public void EnsureConfigured(AccessDescriptor descriptor, object owner)
    {
#if ACCESS_TRACK
        _descriptor = descriptor;
        var currentVersion = AccessTracking.ConfigurationVersion;
        if (_appliedConfigVersion == currentVersion)
            return;
        ApplyConfiguration(owner);
        _appliedConfigVersion = currentVersion;
#else
        _ = descriptor;
        _ = owner;
#endif
    }

    public void RecordWrite(int memberIndex)
    {
#if ACCESS_TRACK
        var descriptor = _descriptor;
        if (descriptor is null)
            return;

        if ((uint)memberIndex >= (uint)descriptor.MemberCount)
            return;

        var flags = descriptor.MemberFlags[memberIndex];
        if ((flags & AccessMemberFlags.Track) == 0)
            return;

        var mask = AccessTracking.EnabledFeatures;
        if ((mask & AccessMemberFlags.Track) == 0)
            return;

        SetWriteBit(memberIndex);

#if ACCESS_TRACK_COUNTS
        if ((flags & AccessMemberFlags.Counts) != 0 && (mask & AccessMemberFlags.Counts) != 0 && _counts is { } counts)
        {
            Interlocked.Increment(ref counts[memberIndex]);
        }
#endif

        long timestamp = 0;
#if ACCESS_TRACK_LAST
        if ((flags & AccessMemberFlags.Last) != 0 && (mask & AccessMemberFlags.Last) != 0 && _last is { } last)
        {
            timestamp = Stopwatch.GetTimestamp();
            Volatile.Write(ref last[memberIndex], timestamp);
        }
#endif

        var callerId = 0;
        if ((flags & AccessMemberFlags.Caller) != 0 && (mask & AccessMemberFlags.Caller) != 0 && _topCallers is { } top)
        {
            var scope = AccessTracking.CurrentScope;
            if (scope is not null)
            {
                callerId = scope.Id;
                for (var ctx = scope; ctx is not null; ctx = ctx.Parent)
                {
                    if (ctx.Id != 0)
                        UpdateTopCallers(top, memberIndex, ctx.Id);
                }
            }
        }

#if ACCESS_TRACK_LOG
        if (_log is { } log && _logMembers is { } logMembers && AccessTracking.LogEnabled)
        {
            if (memberIndex < logMembers.Length && logMembers[memberIndex])
            {
                if (timestamp == 0)
                    timestamp = Stopwatch.GetTimestamp();
                var cursor = Interlocked.Increment(ref _logCursor) - 1;
                var slot = cursor % log.Length;
                if (slot < 0) slot += log.Length;
                log[slot] = new AccessEvent((ushort)memberIndex, timestamp, callerId);
                while (true)
                {
                    var current = Volatile.Read(ref _logCount);
                    if (current >= log.Length)
                        break;
                    if (Interlocked.CompareExchange(ref _logCount, current + 1, current) == current)
                        break;
                }
            }
        }
#endif
#else
        _ = memberIndex;
#endif
    }

    public void Reset()
    {
#if ACCESS_TRACK
        _writeBits0 = 0;
        if (_writeBitsEx is { } ex)
            Array.Clear(ex, 0, ex.Length);
#if ACCESS_TRACK_COUNTS
        if (_counts is { } counts)
            Array.Clear(counts, 0, counts.Length);
#endif
#if ACCESS_TRACK_LAST
        if (_last is { } last)
            Array.Clear(last, 0, last.Length);
#endif
        if (_topCallers is { } top)
            Array.Clear(top, 0, top.Length);
#if ACCESS_TRACK_LOG
        if (_log is { } log)
            Array.Clear(log, 0, log.Length);
        _logCursor = 0;
        _logCount = 0;
#endif
        _since = DateTimeOffset.UtcNow;
        _sinceTicks = Stopwatch.GetTimestamp();
#endif
    }

    public string DumpText(in MappingInfo map, int? topN = null, bool reset = false)
    {
#if !ACCESS_TRACK
        return $"{map.TypeName}: write tracking disabled";
#else
        var descriptor = _descriptor;
        if (descriptor is null)
            return $"{map.TypeName}: write tracking not configured";

        var sb = new StringBuilder();
        sb.Append(map.TypeName).Append(" #").Append(_instanceId).AppendLine();
        sb.Append("since: ").Append(_since.ToString("O")).AppendLine();
        sb.Append("log-capacity: ").Append(_log is null ? 0 : _log.Length).AppendLine();

        var names = map.MemberNames;
        for (var i = 0; i < descriptor.MemberCount; i++)
        {
            var name = i < names.Length ? names[i] : $"#{i}";
            sb.Append(" - ").Append(name).Append(": ");
            var flags = descriptor.MemberFlags[i];
            if ((flags & AccessMemberFlags.Track) == 0 || !AccessTracking.IsTrackingEnabled)
            {
                sb.Append("write: — (disabled)").AppendLine();
                continue;
            }

            var written = ReadWriteBit(i);
            int countValue = 0;
#if ACCESS_TRACK_COUNTS
            if ((flags & AccessMemberFlags.Counts) != 0 && _counts is { } counts)
                countValue = Volatile.Read(ref counts[i]);
#endif
            long lastValue = 0;
#if ACCESS_TRACK_LAST
            if ((flags & AccessMemberFlags.Last) != 0 && _last is { } last)
                lastValue = Volatile.Read(ref last[i]);
#endif
            sb.Append("write: ");
            if ((flags & AccessMemberFlags.Counts) != 0)
                sb.Append(countValue);
            else
                sb.Append(written ? "1" : "—");

            if ((flags & AccessMemberFlags.Last) != 0)
            {
                sb.Append(" (last ");
                if (lastValue != 0)
                    sb.Append(FormatTimestamp(lastValue));
                else
                    sb.Append("—");
                sb.Append(')');
            }
            else
            {
                sb.Append(" (no last)");
            }

            if (_topCallers is { } top)
            {
                var callers = CollectTopCallers(top, i, topN ?? TopK);
                if (callers.Length > 0)
                {
                    sb.Append(" caller: ");
                    for (var idx = 0; idx < callers.Length; idx++)
                    {
                        var entry = callers[idx];
                        if (!AccessTracking.TryGetCallerInfo(entry.CallerId, out var info))
                        {
                            sb.Append('#').Append(entry.CallerId);
                        }
                        else
                        {
                            sb.Append(info.Label ?? info.Member).Append(" (#").Append(info.Id).Append(')');
                        }
                        sb.Append(" x").Append(entry.Count);
                        if (idx + 1 < callers.Length)
                            sb.Append(", ");
                    }
                }
                else
                {
                    sb.Append(" caller: —");
                }
            }
            sb.AppendLine();
        }

#if ACCESS_TRACK_LOG
        if (_log is { } log && _logCount > 0)
        {
            sb.Append("events:").AppendLine();
            var limit = topN ?? Math.Min(log.Length, 16);
            var total = Math.Min(_logCount, log.Length);
            for (var i = 0; i < limit && i < total; i++)
            {
                var slot = ((_logCursor - 1 - i) % log.Length + log.Length) % log.Length;
                var evt = log[slot];
                var name = evt.MemberIndex < names.Length ? names[evt.MemberIndex] : $"#{evt.MemberIndex}";
                sb.Append("  - ").Append(FormatTimestamp(evt.Ticks)).Append(' ')
                  .Append(name).Append(" caller=");
                if (evt.CallerId != 0 && AccessTracking.TryGetCallerInfo(evt.CallerId, out var info))
                    sb.Append(info.Label ?? info.Member).Append(" (#").Append(info.Id).Append(')');
                else
                    sb.Append(evt.CallerId == 0 ? "—" : $"#{evt.CallerId}");
                sb.AppendLine();
            }
        }
#endif

        if (reset)
            Reset();

        return sb.ToString();
#endif
    }

    public string DumpJson(in MappingInfo map, int? topN = null, bool reset = false)
    {
#if !ACCESS_TRACK
        return $"{{\"type\":\"{map.TypeName}\",\"tracking\":false}}";
#else
        var descriptor = _descriptor;
        if (descriptor is null)
            return $"{{\"type\":\"{map.TypeName}\",\"tracking\":false}}";

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("type", map.TypeName);
            writer.WriteNumber("instance", _instanceId);
            writer.WriteBoolean("enabled", AccessTracking.IsTrackingEnabled);
            writer.WriteString("since", _since.ToString("O"));
            writer.WriteNumber("logCapacity", _log is null ? 0 : _log.Length);
            writer.WriteStartArray("members");
            var names = map.MemberNames;
            for (var i = 0; i < descriptor.MemberCount; i++)
            {
                writer.WriteStartObject();
                writer.WriteString("name", i < names.Length ? names[i] : $"#{i}");
                var flags = descriptor.MemberFlags[i];
                if ((flags & AccessMemberFlags.Track) == 0 || !AccessTracking.IsTrackingEnabled)
                {
                    writer.WriteBoolean("tracked", false);
                    writer.WriteBoolean("written", false);
                    writer.WriteEndObject();
                    continue;
                }

                writer.WriteBoolean("tracked", true);
                writer.WriteBoolean("written", ReadWriteBit(i));
#if ACCESS_TRACK_COUNTS
                if ((flags & AccessMemberFlags.Counts) != 0 && _counts is { } counts)
                    writer.WriteNumber("count", Volatile.Read(ref counts[i]));
#endif
#if ACCESS_TRACK_LAST
                if ((flags & AccessMemberFlags.Last) != 0 && _last is { } last)
                {
                    var ticks = Volatile.Read(ref last[i]);
                    if (ticks != 0)
                        writer.WriteString("last", FormatTimestamp(ticks));
                }
#endif
                if (_topCallers is { } top)
                {
                    var callers = CollectTopCallers(top, i, topN ?? TopK);
                    writer.WriteStartArray("callers");

                    if (callers.Length > 0)
                    {
                        var aggregated = new Dictionary<string, (int Id, int Count, string? Label, string? Member, string? File, int Line, bool HasInfo)>(StringComparer.Ordinal);
                        foreach (var entry in callers)
                        {
                            if (entry.Count <= 0) continue;

                            var id = entry.CallerId;
                            string? label = null;
                            string? memberName = null;
                            string? fileName = null;
                            var lineNumber = 0;
                            var hasInfo = false;
                            AccessTracking.CallerInfo info = default;
                            if (id != 0 && AccessTracking.TryGetCallerInfo(id, out info))
                            {
                                hasInfo = true;
                                label = info.Label;
                                memberName = info.Member;
                                fileName = info.File;
                                lineNumber = info.Line;
                            }

                            var key = label is not null ? "L:" + label : "I:" + id;

                            if (aggregated.TryGetValue(key, out var existing))
                            {
                                var newCount = existing.Count + entry.Count;
                                var newLabel = existing.Label ?? label;
                                var newMember = existing.Member ?? memberName;
                                var newFile = existing.File ?? fileName;
                                var newLine = existing.HasInfo ? existing.Line : lineNumber;
                                var newHasInfo = existing.HasInfo || hasInfo;
                                aggregated[key] = (existing.Id, newCount, newLabel, newMember, newFile, newLine, newHasInfo);
                            }
                            else
                            {
                                aggregated[key] = (id, entry.Count, label, memberName, fileName, lineNumber, hasInfo);
                            }
                        }

                        foreach (var agg in aggregated.Values
                                   .OrderByDescending(a => a.Count)
                                   .ThenBy(a => a.Label ?? string.Empty, StringComparer.Ordinal)
                                   .ThenBy(a => a.Id)
                                   .Take(topN ?? TopK))
                        {
                            writer.WriteStartObject();
                            writer.WriteNumber("id", agg.Id);
                            writer.WriteNumber("count", agg.Count);
                            if (agg.Label is not null)
                                writer.WriteString("label", agg.Label);
                            if (agg.HasInfo)
                            {
                                if (agg.Member is not null)
                                    writer.WriteString("member", agg.Member);
                                if (agg.File is not null)
                                    writer.WriteString("file", agg.File);
                                writer.WriteNumber("line", agg.Line);
                            }
                            writer.WriteEndObject();
                        }
                    }

                    writer.WriteEndArray();
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
#if ACCESS_TRACK_LOG
            if (_log is { } log && _logCount > 0)
            {
                writer.WriteStartArray("events");
                var limit = topN ?? Math.Min(log.Length, 16);
                var total = Math.Min(_logCount, log.Length);
                for (var i = 0; i < limit && i < total; i++)
                {
                    var slot = ((_logCursor - 1 - i) % log.Length + log.Length) % log.Length;
                    var evt = log[slot];
                    writer.WriteStartObject();
                    writer.WriteNumber("member", evt.MemberIndex);
                    writer.WriteString("name", evt.MemberIndex < map.MemberNames.Length ? map.MemberNames[evt.MemberIndex] : $"#{evt.MemberIndex}");
                    writer.WriteString("time", FormatTimestamp(evt.Ticks));
                    writer.WriteNumber("callerId", evt.CallerId);
                    if (evt.CallerId != 0 && AccessTracking.TryGetCallerInfo(evt.CallerId, out var info))
                    {
                        if (info.Label is not null)
                            writer.WriteString("label", info.Label);
                        writer.WriteString("memberName", info.Member);
                        writer.WriteString("file", info.File);
                        writer.WriteNumber("line", info.Line);
                    }
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
#endif
            writer.WriteEndObject();
        }

        if (reset)
            Reset();

        var result = Encoding.UTF8.GetString(buffer.WrittenSpan);
        Console.WriteLine("DumpJson result: {result}");
        return result;
#endif
    }

#if ACCESS_TRACK
    private void ApplyConfiguration(object owner)
    {
        if (!AccessTracking.IsTrackingEnabled)
            return;

        var descriptor = _descriptor!;
        _instanceId = RuntimeHelpers.GetHashCode(owner);
        if (_sinceTicks == 0)
        {
            _since = DateTimeOffset.UtcNow;
            _sinceTicks = Stopwatch.GetTimestamp();
        }

        EnsureWriteCapacity(descriptor.MemberCount);

#if ACCESS_TRACK_COUNTS
        if ((descriptor.CombinedFlags & AccessMemberFlags.Counts) != 0)
        {
            _counts = EnsureArray(_counts, descriptor.MemberCount);
        }
#endif
#if ACCESS_TRACK_LAST
        if ((descriptor.CombinedFlags & AccessMemberFlags.Last) != 0)
        {
            _last = EnsureArray(_last, descriptor.MemberCount);
        }
#endif
        if ((descriptor.CombinedFlags & AccessMemberFlags.Caller) != 0)
        {
            _topCallers ??= new AccessTopEntry[descriptor.MemberCount * TopK];
        }

#if ACCESS_TRACK_LOG
        ConfigureLog(descriptor);
#endif
    }

    private void EnsureWriteCapacity(int memberCount)
    {
        var words = (memberCount + 63) >> 6;
        if (words <= 1)
        {
            _writeBitsEx = null;
            return;
        }

        var required = words - 1;
        if (_writeBitsEx is not { Length: var length } || length < required)
            _writeBitsEx = new ulong[required];
    }

    private void SetWriteBit(int index)
    {
        if ((uint)index < 64u)
        {
            var mask = 1UL << index;
            var currentBits = Volatile.Read(ref _writeBits0);
            while (true)
            {
                var updated = currentBits | mask;
                var observed = Interlocked.CompareExchange(ref _writeBits0, updated, currentBits);
                if (observed == currentBits)
                    break;
                currentBits = observed;
            }
            return;
        }

        var arr = _writeBitsEx;
        if (arr is null)
            return;
        var word = (index >> 6) - 1;
        if ((uint)word >= (uint)arr.Length)
            return;
        var maskEx = 1UL << (index & 63);
        var currentWord = Volatile.Read(ref arr[word]);
        while (true)
        {
            var updated = currentWord | maskEx;
            var observed = Interlocked.CompareExchange(ref arr[word], updated, currentWord);
            if (observed == currentWord)
                break;
            currentWord = observed;
        }
    }

    private bool ReadWriteBit(int index)
    {
        if ((uint)index < 64u)
            return (Volatile.Read(ref _writeBits0) & (1UL << index)) != 0;
        var arr = _writeBitsEx;
        if (arr is null)
            return false;
        var word = (index >> 6) - 1;
        if ((uint)word >= (uint)arr.Length)
            return false;
        return (Volatile.Read(ref arr[word]) & (1UL << (index & 63))) != 0;
    }

    private AccessTopEntry[] CollectTopCallers(AccessTopEntry[] source, int memberIndex, int limit)
    {
        Span<AccessTopEntry> span = stackalloc AccessTopEntry[TopK];
        var offset = memberIndex * TopK;
        var count = 0;
        for (var i = 0; i < TopK; i++)
        {
            var entry = source[offset + i];
            if (entry.CallerId == 0 || entry.Count == 0)
                continue;
            span[count++] = entry;
        }

        var slice = span[..count];
        if (count > 1)
        {
            slice.Sort(static (a, b) => b.Count.CompareTo(a.Count));
        }

        if (limit < slice.Length)
        {
            slice = slice[..limit];
        }

        return slice.ToArray();
    }

    private void UpdateTopCallers(AccessTopEntry[] entries, int memberIndex, int callerId)
    {
        var offset = memberIndex * TopK;
        var minIndex = offset;
        for (var i = 0; i < TopK; i++)
        {
            ref var entry = ref entries[offset + i];
            if (entry.CallerId == callerId)
            {
                entry.Count++;
                return;
            }
            if (entry.CallerId == 0)
            {
                entry.CallerId = callerId;
                entry.Count = 1;
                return;
            }
            if (entries[minIndex].Count > entry.Count)
                minIndex = offset + i;
        }

        entries[minIndex].CallerId = callerId;
        entries[minIndex].Count = 1;
    }

#if ACCESS_TRACK_LOG
    private void ConfigureLog(AccessDescriptor descriptor)
    {
        if (!AccessTracking.LogEnabled)
        {
            _log = null;
            _logMembers = null;
            _logCursor = 0;
            _logCount = 0;
            return;
        }

        var memberCount = descriptor.MemberCount;
        var policies = descriptor.MemberLogPolicies;
        var forced = descriptor.MemberForcedLogCapacities;
        var logMembers = _logMembers;
        if (logMembers is null || logMembers.Length != memberCount)
            _logMembers = logMembers = new bool[memberCount];

        var capacity = descriptor.TypeLogCapacity > 0 ? descriptor.TypeLogCapacity : 0;
        var defaultCap = AccessTracking.DefaultLogCapacity;
        var any = false;

        var desiredCap = capacity > 0 ? capacity : defaultCap;

        for (var i = 0; i < memberCount; i++)
        {
            var policy = policies[i];
            var enabled = false;
            var memberCap = 0;

            if ((policy & AccessLogPolicy.Forced) != 0)
            {
                memberCap = forced[i];
                if (memberCap <= 0)
                    memberCap = desiredCap;
                enabled = memberCap > 0;
            }
            else if ((policy & AccessLogPolicy.Allowed) != 0)
            {
                memberCap = desiredCap;
                enabled = memberCap > 0;
            }

            if (enabled && memberCap > capacity)
                capacity = memberCap;

            logMembers[i] = enabled;
            any |= enabled;
        }

        if (!any)
        {
            _log = null;
            _logCursor = 0;
            _logCount = 0;
            return;
        }

        if (capacity == 0)
            capacity = Math.Max(1, defaultCap);
        if (capacity <= 0)
            capacity = 16;

        if (_log is not { Length: var length } || length != capacity)
        {
            _log = new AccessEvent[capacity];
            _logCursor = 0;
            _logCount = 0;
        }
    }
#endif

    private static T[] EnsureArray<T>(T[]? current, int length)
    {
        if (current is { Length: var existing } && existing >= length)
            return current;
        return new T[length];
    }

    private string FormatTimestamp(long ticks)
    {
        if (ticks == 0 || _sinceTicks == 0)
            return "—";
        var delta = ticks - _sinceTicks;
        var seconds = delta / (double)Stopwatch.Frequency;
        var time = _since + TimeSpan.FromSeconds(seconds);
        return time.ToLocalTime().ToString("HH:mm:ss.fff");
    }
#endif
}













