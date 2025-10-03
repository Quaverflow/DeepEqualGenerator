using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DeepEqual.Generator.Shared;

public static class AccessTracking
{
#if ACCESS_TRACK
    private static readonly AsyncLocal<ScopeContext?> s_current = new();
    private static readonly object s_callerLock = new();
    private static readonly Dictionary<CallerKey, int> s_callerIds = new();
    private static readonly List<CallerInfo> s_callerInfos = new() { default };
    private static int s_nextCallerId;

    private static volatile AccessMode s_defaultMode = AccessMode.None;
    private static volatile AccessGranularity s_defaultGranularity = AccessGranularity.Bits;
    private static volatile int s_defaultLogCapacity;

    private static volatile bool s_trackingEnabled = true;
    private static volatile bool s_countsEnabled = true;
    private static volatile bool s_lastEnabled = true;
    private static volatile bool s_logEnabled = true;
    private static volatile bool s_callersEnabled = true;

    private static int s_configVersion = 1;

    private sealed class ScopeHandle : IDisposable
    {
        private readonly ScopeContext? _previous;
        private bool _disposed;

        public ScopeHandle(ScopeContext? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            s_current.Value = _previous;
        }
    }    internal sealed class ScopeContext
    {
        public ScopeContext(int id, ScopeContext? parent)
        {
            Id = id;
            Parent = parent;
        }

        public int Id { get; }
        public ScopeContext? Parent { get; }
    }

    private readonly struct CallerKey : IEquatable<CallerKey>
    {
        public CallerKey(string? label, string member, string file, int line)
        {
            Label = label;
            Member = member;
            File = file;
            Line = line;
        }

        public string? Label { get; }
        public string Member { get; }
        public string File { get; }
        public int Line { get; }

        public bool Equals(CallerKey other) => Line == other.Line
            && string.Equals(Label, other.Label, StringComparison.Ordinal)
            && string.Equals(Member, other.Member, StringComparison.Ordinal)
            && string.Equals(File, other.File, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is CallerKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + (Label is null ? 0 : Label.GetHashCode(StringComparison.Ordinal));
                hash = (hash * 31) + Member.GetHashCode(StringComparison.Ordinal);
                hash = (hash * 31) + File.GetHashCode(StringComparison.Ordinal);
                hash = (hash * 31) + Line;
                return hash;
            }
        }
    }
#else
    private sealed class NoopScope : IDisposable
    {
        public void Dispose() { }
    }

    private static readonly IDisposable s_noopScope = new NoopScope();

    internal sealed class ScopeContext
    {
        public int Id => 0;
        public ScopeContext? Parent => null;
    }
#endif

    public static int CurrentCallerId
    {
#if ACCESS_TRACK
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => s_current.Value?.Id ?? 0;
#else
        get => 0;
#endif
    }

#if ACCESS_TRACK
    internal static ScopeContext? CurrentScope => s_current.Value;
#else
    internal static ScopeContext? CurrentScope => null;
#endif

    public static IDisposable PushScope(string? label = null, [CallerMemberName] string? member = null, [CallerFilePath] string? file = null, [CallerLineNumber] int line = 0)
    {
#if ACCESS_TRACK
        member ??= string.Empty;
        file ??= string.Empty;
        var previous = s_current.Value;

        if (!s_callersEnabled || !s_trackingEnabled)
        {
            return new ScopeHandle(previous);
        }

        var key = new CallerKey(label, member, file, line);
        int id;
        lock (s_callerLock)
        {
            if (!s_callerIds.TryGetValue(key, out id))
            {
                id = ++s_nextCallerId;
                s_callerIds[key] = id;
                s_callerInfos.Add(new CallerInfo(id, label, member, file, line));
            }
        }

        var context = new ScopeContext(id, previous);
        s_current.Value = context;
        return new ScopeHandle(previous);
#else
        return s_noopScope;
#endif
    }

    public static void Configure(AccessMode defaultMode, AccessGranularity defaultGranularity, int defaultLogCapacity, bool trackingEnabled = true, bool countsEnabled = true, bool lastEnabled = true, bool logEnabled = true, bool callersEnabled = true)
    {
#if ACCESS_TRACK
        s_defaultMode = defaultMode;
        s_defaultGranularity = defaultGranularity;
        s_defaultLogCapacity = Math.Max(0, defaultLogCapacity);
        s_trackingEnabled = trackingEnabled;
        s_countsEnabled = countsEnabled;
        s_lastEnabled = lastEnabled;
        s_logEnabled = logEnabled;
        s_callersEnabled = callersEnabled;
        RaiseConfigurationChanged();
#else
        _ = defaultMode;
        _ = defaultGranularity;
        _ = defaultLogCapacity;
        _ = trackingEnabled;
        _ = countsEnabled;
        _ = lastEnabled;
        _ = logEnabled;
        _ = callersEnabled;
#endif
    }

#if ACCESS_TRACK
    internal static void RaiseConfigurationChanged() => Interlocked.Increment(ref s_configVersion);

    internal static AccessMemberFlags EnabledFeatures
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (!s_trackingEnabled)
            {
                return AccessMemberFlags.None;
            }

            var mask = AccessMemberFlags.Track;
            if (s_countsEnabled)
                mask |= AccessMemberFlags.Counts;
            if (s_lastEnabled)
                mask |= AccessMemberFlags.Last;
            if (s_callersEnabled)
                mask |= AccessMemberFlags.Caller;
            return mask;
        }
    }

    internal static bool IsTrackingEnabled => s_trackingEnabled;
    internal static bool CountsEnabled => s_countsEnabled && s_trackingEnabled;
    internal static bool LastEnabled => s_lastEnabled && s_trackingEnabled;
    internal static bool CallersEnabled => s_callersEnabled && s_trackingEnabled;
    internal static bool LogEnabled => s_logEnabled && s_trackingEnabled;

    internal static int ConfigurationVersion => Volatile.Read(ref s_configVersion);
    internal static AccessMode DefaultMode => s_defaultMode;
    internal static AccessGranularity DefaultGranularity => s_defaultGranularity;
    internal static int DefaultLogCapacity => s_defaultLogCapacity;

    public readonly struct CallerInfo
    {
        public CallerInfo(int id, string? label, string member, string file, int line)
        {
            Id = id;
            Label = label;
            Member = member;
            File = file;
            Line = line;
        }

        public int Id { get; }
        public string? Label { get; }
        public string Member { get; }
        public string File { get; }
        public int Line { get; }
    }

    internal static bool TryGetCallerInfo(int callerId, out CallerInfo info)
    {
        if (callerId <= 0)
        {
            info = default;
            return false;
        }

        lock (s_callerLock)
        {
            if (callerId < s_callerInfos.Count)
            {
                info = s_callerInfos[callerId];
                return true;
            }
        }

        info = default;
        return false;
    }
#else
    internal static void RaiseConfigurationChanged() { }
    internal static AccessMemberFlags EnabledFeatures => AccessMemberFlags.None;
    internal static bool IsTrackingEnabled => false;
    internal static bool CountsEnabled => false;
    internal static bool LastEnabled => false;
    internal static bool CallersEnabled => false;
    internal static bool LogEnabled => false;
    internal static int ConfigurationVersion => 0;
    internal static AccessMode DefaultMode => AccessMode.None;
    internal static AccessGranularity DefaultGranularity => AccessGranularity.Bits;
    internal static int DefaultLogCapacity => 0;

    public readonly struct CallerInfo
    {
        public int Id => 0;
        public string? Label => null;
        public string Member => string.Empty;
        public string File => string.Empty;
        public int Line => 0;
    }

    internal static bool TryGetCallerInfo(int callerId, out CallerInfo info)
    {
        _ = callerId;
        info = default;
        return false;
    }
#endif
}









