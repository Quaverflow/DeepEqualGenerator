using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DeepEqual.Generator.Shared;

public sealed class ComparisonContext
{
    private readonly bool _tracking;
    private readonly HashSet<RefPair> _visited = new(RefPair.Comparer.Instance);
    private readonly Stack<RefPair> _stack = new();

    public ComparisonOptions Options { get; }

    public static ComparisonContext NoTracking { get; } = new(false, new ComparisonOptions());

    public ComparisonContext() : this(true, new ComparisonOptions()) { }

    public ComparisonContext(ComparisonOptions? options) : this(true, options ?? new ComparisonOptions()) { }

    private ComparisonContext(bool enableTracking, ComparisonOptions? options)
    {
        _tracking = enableTracking;
        Options = options ?? new ComparisonOptions();
    }

    public bool Enter(object left, object right)
    {
        if (!_tracking) return true;

        var pair = new RefPair(left, right);
        if (!_visited.Add(pair)) return false;

        _stack.Push(pair);
        return true;
    }

    public void Exit(object left, object right)
    {
        if (!_tracking) return;

        if (_stack.Count == 0) return;

        var last = _stack.Pop();
        _visited.Remove(last);
    }

    private readonly struct RefPair(object left, object right)
    {
        private readonly object _left = left;
        private readonly object _right = right;

        public sealed class Comparer : IEqualityComparer<RefPair>
        {
            public static readonly Comparer Instance = new();
            public bool Equals(RefPair x, RefPair y) => ReferenceEquals(x._left, y._left) && ReferenceEquals(x._right, y._right);
            public int GetHashCode(RefPair obj)
            {
                unchecked
                {
                    var a = RuntimeHelpers.GetHashCode(obj._left);
                    var b = RuntimeHelpers.GetHashCode(obj._right);
                    return (a * 397) ^ b;
                }
            }
        }
    }
}

    /// <summary> Marker for any diff payload. </summary>
    public interface IDiff
    {
        bool IsEmpty { get; }
    }

    /// <summary>
    /// A non-generic empty diff — for "no info"/"not supported".
    /// </summary>
    public readonly struct Diff : IDiff
    {
        public static readonly Diff Empty = new();
        public bool IsEmpty => true;
    }

    /// <summary>
    /// Strongly-typed structural diff for T.
    /// </summary>
    public readonly struct Diff<T> : IDiff
    {
        public bool HasChanges { get; }
        public bool IsReplacement { get; }
        public T? NewValue { get; }
        public IReadOnlyList<MemberChange>? MemberChanges { get; }

        private Diff(bool has, bool replace, T? newValue, IReadOnlyList<MemberChange>? changes)
        {
            HasChanges = has;
            IsReplacement = replace;
            NewValue = newValue;
            MemberChanges = changes;
        }

        public static Diff<T> Empty => new(false, false, default, null);

        public static Diff<T> Replacement(T? newValue) => new(true, true, newValue, null);

        public static Diff<T> Members(List<MemberChange> changes)
        {
            if (changes.Count == 0) return Empty;
            return new Diff<T>(true, false, default, changes);
        }

        public bool IsEmpty => !HasChanges;
    }

    /// <summary>
    /// A single member diff: either a shallow replacement or a nested diff/delta object.
    /// </summary>
    public readonly record struct MemberChange(
        int MemberIndex,
        MemberChangeKind Kind,
        object? ValueOrDiff);

    public enum MemberChangeKind
    {
        /// <summary> Replace the whole member value (shallow). </summary>
        Set = 0,

        /// <summary> Nested structural diff (ValueOrDiff is IDiff or DeltaDocument). </summary>
        Nested = 1,

        /// <summary> Sequence or dictionary operation list (ValueOrDiff is a DeltaDocument). </summary>
        CollectionOps = 2
    }

public enum CompareKind
{
    Deep,
    Shallow,
    Reference,
    Skip
}

public interface IElementComparer<in T>
{
    bool Invoke(T left, T right, ComparisonContext context);
}
/// Default element comparer that defers to <see cref="System.Collections.Generic.EqualityComparer{T}.Default"/>.
/// </summary>
public readonly struct DefaultElementComparer<T> : IElementComparer<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Invoke(T left, T right, ComparisonContext context)
    {
        if (left is string sa && right is string sb)
            return ComparisonHelpers.AreEqualStrings(sa, sb, context);

        if (left is double da && right is double db)
            return ComparisonHelpers.AreEqualDouble(da, db, context);

        if (left is float fa && right is float fb)
            return ComparisonHelpers.AreEqualSingle(fa, fb, context);

        if (left is decimal ma && right is decimal mb)
            return ComparisonHelpers.AreEqualDecimal(ma, mb, context);

        return EqualityComparer<T>.Default.Equals(left, right);
    }
}

/// <summary>
/// Element comparer that performs generated deep comparison when possible; falls back appropriately for polymorphic graphs.
/// </summary>
public readonly struct DeepPolymorphicElementComparer<T> : IElementComparer<T>
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public bool Invoke(T left, T right, ComparisonContext context)
        => ComparisonHelpers.DeepComparePolymorphic(left, right, context);
}

/// <summary>
/// Element comparer that delegates to a provided <see cref="System.Collections.Generic.IEqualityComparer{T}"/>.
/// </summary>
public readonly struct DelegatingElementComparer<T> : IElementComparer<T>
{
    private readonly System.Collections.Generic.IEqualityComparer<T> _inner;

    public DelegatingElementComparer(System.Collections.Generic.IEqualityComparer<T> inner)
    {
        _inner = inner ?? System.Collections.Generic.EqualityComparer<T>.Default;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public bool Invoke(T left, T right, ComparisonContext context)
        => _inner.Equals(left, right);
}


/// <summary>Controls encoding profile and safety caps.</summary>
public sealed class BinaryDeltaOptions
{
    /// <summary>
    /// Headerless fast path by default (best in-proc).
    /// When true, a tiny header is written: (magic "BDC1", version=1, stableTypeFingerprint).
    /// In headerful mode, we also enable prefaces (type table for enums, optional string table).
    /// </summary>
    public bool IncludeHeader { get; set; } = false;

    /// <summary>Set when IncludeHeader=true. Optional stable type fingerprint (64-bit) for schema/version gating.</summary>
    public ulong StableTypeFingerprint { get; set; } = 0;

    /// <summary>Pre-announce and reference enum types via compact table (headerful only).</summary>
    public bool UseTypeTable { get; set; } = true;

    /// <summary>Deduplicate strings via a per-document table (headerful only).</summary>
    public bool UseStringTable { get; set; } = true;

    /// <summary>Enum values are encoded with underlying integral + type identity.</summary>
    public bool IncludeEnumTypeIdentity { get; set; } = true;

    /// <summary>Light safety caps to avoid pathological inputs.</summary>
    public Limits Safety { get; } = new Limits();

    public sealed class Limits
    {
        /// <summary>Max operations allowed in a single document (top-level or nested).</summary>
        public int MaxOps { get; set; } = 1_000_000;
        /// <summary>Max encoded UTF-8 string length in bytes.</summary>
        public int MaxStringBytes { get; set; } = 16 * 1024 * 1024;
        /// <summary>Max nesting depth for nested documents (member or dict-nested).</summary>
        public int MaxNesting { get; set; } = 256;
    }

    internal static readonly BinaryDeltaOptions Default = new();
}


/// <summary>Lossless binary codec for <see cref="DeltaDocument"/>.</summary>
/// <summary>Lossless binary codec for <see cref="DeltaDocument"/>.</summary>
public static class BinaryDeltaCodec
{
    // --------- Public writer/reader entry points ---------

    public static void Write(DeltaDocument doc, IBufferWriter<byte> output, BinaryDeltaOptions? options = null)
        => new Writer(output, options ?? BinaryDeltaOptions.Default).WriteDocument(doc);

    public static DeltaDocument Read(ReadOnlySpan<byte> data, BinaryDeltaOptions? options = null)
        => new Reader(data, options ?? BinaryDeltaOptions.Default).ReadDocument();

    // --------- Internal wire tags ---------

    private enum VTag : byte
    {
        Null = 0,
        BoolFalse = 1,
        BoolTrue = 2,
        SByte = 3,
        Byte = 4,
        Int16 = 5,
        UInt16 = 6,
        Int32 = 7,
        UInt32 = 8,
        Int64 = 9,
        UInt64 = 10,
        Char16 = 11,
        Single = 12,
        Double = 13,
        Decimal = 14,
        StringInline = 15,
        StringRef = 16,
        Guid16 = 17,
        DateTimeBin64 = 18,
        TimeSpanTicks = 19,
        DateTimeOffset = 20,
        Enum = 21,
        ByteArray = 22,
        Array = 23,
        List = 24,
        Dictionary = 25,
    }

    private enum TypeSpecKind : byte
    {
        PrimitiveOrKnown = 0,
        Enum = 1,
        Object = 2,
    }

    private enum KnownTypeCode : byte
    {
        SByte, Byte, Int16, UInt16, Int32, UInt32, Int64, UInt64,
        Single, Double, Decimal, Bool, Char, String, Guid, DateTime, TimeSpan, DateTimeOffset,
    }

    // --------- Writer ---------

    private ref struct Writer
    {
        private readonly IBufferWriter<byte> _out;
        private readonly BinaryDeltaOptions _opt;

        private Dictionary<string, int>? _strToId;
        private List<string>? _strings;
        private Dictionary<Type, int>? _typeToId;
        private List<TypeEntry>? _types;

        private int _nesting;

        internal Writer(IBufferWriter<byte> output, BinaryDeltaOptions opt)
        {
            _out = output ?? throw new ArgumentNullException(nameof(output));
            _opt = opt ?? BinaryDeltaOptions.Default;
            _strToId = null;
            _strings = null;
            _typeToId = null;
            _types = null;
            _nesting = 0;
        }

        public void WriteDocument(DeltaDocument doc)
        {
            if (_opt.IncludeHeader)
            {
                BuildTablesIfNeeded(doc);

                var sw = new SpanWriter(_out);

                // Magic "BDC1"
                sw.WriteByte((byte)'B');
                sw.WriteByte((byte)'D');
                sw.WriteByte((byte)'C');
                sw.WriteByte((byte)'1');

                sw.WriteVarUInt(1u); // version
                sw.WriteVarUInt(_opt.StableTypeFingerprint); // fingerprint (0 allowed)

                byte flags =
                    (byte)((_strings is { Count: > 0 } && _opt.UseStringTable ? 0b01 : 0) |
                           ((_types is { Count: > 0 } && _opt.UseTypeTable) ? 0b10 : 0));
                sw.WriteByte(flags);

                if ((_strings is { Count: > 0 }) && _opt.UseStringTable)
                {
                    sw.WriteVarUInt((uint)_strings!.Count);
                    for (int i = 0; i < _strings!.Count; i++)
                        sw.WriteUtf8StringInline(_strings[i]);
                }

                if ((_types is { Count: > 0 }) && _opt.UseTypeTable)
                {
                    sw.WriteVarUInt((uint)_types!.Count);
                    for (int i = 0; i < _types!.Count; i++)
                    {
                        var t = _types[i];
                        WriteTypeEntry(ref sw, in t);
                    }
                }

                sw.WriteVarUInt((uint)doc.Operations.Count);
                foreach (var op in doc.Operations)
                    WriteOp(ref sw, op);
                sw.Commit();
            }
            else
            {
                var sw = new SpanWriter(_out);
                sw.WriteVarUInt((uint)doc.Operations.Count);
                foreach (var op in doc.Operations)
                    WriteOp(ref sw, op);
                sw.Commit();
            }
        }

        private void WriteOp(ref SpanWriter sw, in DeltaOp op)
        {
            sw.WriteVarUInt((uint)op.Kind);
            sw.WriteVarInt(op.MemberIndex);

            if (op.Kind is DeltaKind.SeqReplaceAt or DeltaKind.SeqAddAt or DeltaKind.SeqRemoveAt)
                sw.WriteVarUInt((uint)op.Index);

            if (op.Kind is DeltaKind.DictSet or DeltaKind.DictRemove or DeltaKind.DictNested)
                WriteValue(ref sw, op.Key);

            if (op.Kind is DeltaKind.ReplaceObject or DeltaKind.SetMember or DeltaKind.SeqReplaceAt or DeltaKind.SeqAddAt or DeltaKind.DictSet)
                WriteValue(ref sw, op.Value);

            if (op.Kind is DeltaKind.NestedMember or DeltaKind.DictNested)
                WriteNested(ref sw, op.Nested!);
        }

        private void WriteNested(ref SpanWriter sw, DeltaDocument nested)
        {
            if (++_nesting > _opt.Safety.MaxNesting)
                throw new InvalidOperationException("Max nesting exceeded");

            sw.WriteVarUInt((uint)nested.Operations.Count);
            foreach (var op in nested.Operations)
                WriteOp(ref sw, op);

            _nesting--;
        }

        private void WriteValue(ref SpanWriter sw, object? value)
        {
            if (value is null) { sw.WriteByte((byte)VTag.Null); return; }

            switch (value)
            {
                case bool b:
                    sw.WriteByte((byte)(b ? VTag.BoolTrue : VTag.BoolFalse));
                    return;

                case sbyte sb:
                    sw.WriteByte((byte)VTag.SByte); sw.WriteVarInt(sb);
                    return;

                case byte b8:
                    sw.WriteByte((byte)VTag.Byte); sw.WriteVarUInt(b8);
                    return;

                case short i16:
                    sw.WriteByte((byte)VTag.Int16); sw.WriteVarInt(i16);
                    return;

                case ushort u16:
                    sw.WriteByte((byte)VTag.UInt16); sw.WriteVarUInt(u16);
                    return;

                case int i32:
                    sw.WriteByte((byte)VTag.Int32); sw.WriteVarInt(i32);
                    return;

                case uint u32:
                    sw.WriteByte((byte)VTag.UInt32); sw.WriteVarUInt(u32);
                    return;

                case long i64:
                    sw.WriteByte((byte)VTag.Int64); sw.WriteVarInt(i64);
                    return;

                case ulong u64:
                    sw.WriteByte((byte)VTag.UInt64); sw.WriteVarUInt(u64);
                    return;

                case char ch:
                    sw.WriteByte((byte)VTag.Char16); sw.WriteUInt16((ushort)ch);
                    return;

                case float f32:
                    sw.WriteByte((byte)VTag.Single);
                    sw.WriteUInt32((uint)BitConverter.SingleToInt32Bits(f32));
                    return;

                case double f64:
                    sw.WriteByte((byte)VTag.Double);
                    sw.WriteUInt64((ulong)BitConverter.DoubleToInt64Bits(f64));
                    return;

                case decimal dec:
                    WriteDecimal(ref sw, dec);
                    return;

                case string s:
                    if (_opt.IncludeHeader && _opt.UseStringTable && _strToId is not null && _strToId.TryGetValue(s, out var sid))
                    { sw.WriteByte((byte)VTag.StringRef); sw.WriteVarUInt((uint)sid); }
                    else
                    { sw.WriteByte((byte)VTag.StringInline); sw.WriteUtf8StringInline(s); }
                    return;

                case Guid g:
                    sw.WriteByte((byte)VTag.Guid16);
                    WriteGuid16(ref sw, g);
                    return;

                case DateTime dt:
                    sw.WriteByte((byte)VTag.DateTimeBin64);
                    sw.WriteVarInt(dt.ToBinary());
                    return;

                case TimeSpan ts:
                    sw.WriteByte((byte)VTag.TimeSpanTicks);
                    sw.WriteVarInt(ts.Ticks);
                    return;

                case DateTimeOffset dto:
                    sw.WriteByte((byte)VTag.DateTimeOffset);
                    sw.WriteVarInt(dto.Ticks);
                    sw.WriteVarInt((int)dto.Offset.TotalMinutes);
                    return;
            }

            var t = value.GetType();

            if (t.IsEnum)
            {
                sw.WriteByte((byte)VTag.Enum);
                WriteEnumTypeIdentity(ref sw, t);
                WriteEnumUnderlying(ref sw, value, Enum.GetUnderlyingType(t));
                return;
            }

            if (value is byte[] blob)
            {
                sw.WriteByte((byte)VTag.ByteArray);
                sw.WriteVarUInt((uint)blob.Length);
                sw.WriteBytes(blob);
                return;
            }

            if (value is Array arr && arr.Rank == 1)
            {
                sw.WriteByte((byte)VTag.Array);
                var elemT = t.GetElementType() ?? typeof(object);
                WriteTypeSpec(ref sw, elemT);
                var len = arr.Length;
                sw.WriteVarUInt((uint)len);
                for (int i = 0; i < len; i++)
                    WriteValue(ref sw, arr.GetValue(i));
                return;
            }

            if (IsConcreteList(value, out var elem))
            {
                sw.WriteByte((byte)VTag.List);
                WriteTypeSpec(ref sw, elem);
                var il = (IList)value;
                var cnt = il.Count;
                sw.WriteVarUInt((uint)cnt);
                for (int i = 0; i < cnt; i++) WriteValue(ref sw, il[i]);
                return;
            }

            if (IsConcreteDictionary(value, out var k, out var v))
            {
                sw.WriteByte((byte)VTag.Dictionary);
                WriteTypeSpec(ref sw, k);
                WriteTypeSpec(ref sw, v);
                var id = (IDictionary)value;
                sw.WriteVarUInt((uint)id.Count);
                foreach (DictionaryEntry e in id)
                {
                    WriteValue(ref sw, e.Key);
                    WriteValue(ref sw, e.Value);
                }
                return;
            }

            throw new NotSupportedException($"Cannot serialize value of type '{t.FullName}' in BinaryDelta (v1). Use nested deltas or extend codec.");
        }

        private static bool IsConcreteList(object o, out Type elem)
        {
            var t = o.GetType();
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                elem = t.GetGenericArguments()[0];
                return true;
            }
            elem = typeof(object);
            return false;
        }

        private static bool IsConcreteDictionary(object o, out Type key, out Type val)
        {
            var t = o.GetType();
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var ga = t.GetGenericArguments();
                key = ga[0]; val = ga[1];
                return true;
            }
            key = val = typeof(object);
            return false;
        }

        private static void WriteDecimal(ref SpanWriter sw, decimal v)
        {
            sw.WriteByte((byte)VTag.Decimal);
            var bits = decimal.GetBits(v);
            sw.WriteUInt32((uint)bits[0]);
            sw.WriteUInt32((uint)bits[1]);
            sw.WriteUInt32((uint)bits[2]);
            sw.WriteUInt32((uint)bits[3]);
        }

        private void WriteEnumTypeIdentity(ref SpanWriter sw, Type enumType)
        {
            sw.WriteVarUInt((uint)TypeSpecKind.Enum);
            if (_opt.IncludeHeader && _opt.UseTypeTable && _typeToId is not null && _typeToId.TryGetValue(enumType, out var tid))
            {
                sw.WriteVarUInt((uint)tid);
            }
            else
            {
                WriteInlineEnumTypeDesc(ref sw, enumType);
            }
        }

        private static void WriteEnumUnderlying(ref SpanWriter sw, object boxedEnum, Type underlying)
        {
            if (underlying == typeof(sbyte)) { sw.WriteVarInt((sbyte)boxedEnum); return; }
            if (underlying == typeof(byte)) { sw.WriteVarUInt((byte)boxedEnum); return; }
            if (underlying == typeof(short)) { sw.WriteVarInt((short)boxedEnum); return; }
            if (underlying == typeof(ushort)) { sw.WriteVarUInt((ushort)boxedEnum); return; }
            if (underlying == typeof(int)) { sw.WriteVarInt((int)boxedEnum); return; }
            if (underlying == typeof(uint)) { sw.WriteVarUInt((uint)boxedEnum); return; }
            if (underlying == typeof(long)) { sw.WriteVarInt((long)boxedEnum); return; }
            if (underlying == typeof(ulong)) { sw.WriteVarUInt((ulong)boxedEnum); return; }
            throw new NotSupportedException("Unsupported enum underlying type.");
        }

        private void WriteTypeSpec(ref SpanWriter sw, Type t)
        {
            if (t == typeof(object))
            {
                sw.WriteVarUInt((uint)TypeSpecKind.Object);
                return;
            }

            if (TryWriteKnownTypeSpec(ref sw, t)) return;

            if (t.IsEnum)
            {
                sw.WriteVarUInt((uint)TypeSpecKind.Enum);
                if (_opt.IncludeHeader && _opt.UseTypeTable && _typeToId is not null && _typeToId.TryGetValue(t, out var tid))
                    sw.WriteVarUInt((uint)tid);
                else
                    WriteInlineEnumTypeDesc(ref sw, t);
                return;
            }

            sw.WriteVarUInt((uint)TypeSpecKind.Object);
        }

        private static bool TryWriteKnownTypeSpec(ref SpanWriter sw, Type t)
        {
            KnownTypeCode code;
            if (t == typeof(sbyte)) code = KnownTypeCode.SByte;
            else if (t == typeof(byte)) code = KnownTypeCode.Byte;
            else if (t == typeof(short)) code = KnownTypeCode.Int16;
            else if (t == typeof(ushort)) code = KnownTypeCode.UInt16;
            else if (t == typeof(int)) code = KnownTypeCode.Int32;
            else if (t == typeof(uint)) code = KnownTypeCode.UInt32;
            else if (t == typeof(long)) code = KnownTypeCode.Int64;
            else if (t == typeof(ulong)) code = KnownTypeCode.UInt64;
            else if (t == typeof(float)) code = KnownTypeCode.Single;
            else if (t == typeof(double)) code = KnownTypeCode.Double;
            else if (t == typeof(decimal)) code = KnownTypeCode.Decimal;
            else if (t == typeof(bool)) code = KnownTypeCode.Bool;
            else if (t == typeof(char)) code = KnownTypeCode.Char;
            else if (t == typeof(string)) code = KnownTypeCode.String;
            else if (t == typeof(Guid)) code = KnownTypeCode.Guid;
            else if (t == typeof(DateTime)) code = KnownTypeCode.DateTime;
            else if (t == typeof(TimeSpan)) code = KnownTypeCode.TimeSpan;
            else if (t == typeof(DateTimeOffset)) code = KnownTypeCode.DateTimeOffset;
            else return false;

            sw.WriteVarUInt((uint)TypeSpecKind.PrimitiveOrKnown);
            sw.WriteVarUInt((uint)code);
            return true;
        }

        private static void WriteInlineEnumTypeDesc(ref SpanWriter sw, Type enumType)
        {
            var asm = enumType.Assembly.GetName().Name ?? "";
            var mvid = enumType.Module.ModuleVersionId;
            sw.WriteUtf8StringInline(enumType.FullName ?? enumType.Name);
            sw.WriteUtf8StringInline(asm);

            // write Guid as two little-endian UInt64s
            Span<byte> tmp = stackalloc byte[16];
            mvid.TryWriteBytes(tmp);
            sw.WriteUInt64(BinaryPrimitives.ReadUInt64LittleEndian(tmp));
            sw.WriteUInt64(BinaryPrimitives.ReadUInt64LittleEndian(tmp.Slice(8)));
        }

        private readonly record struct TypeEntry(string FullName, string AssemblySimpleName, Guid Mvid);

        private void BuildTablesIfNeeded(DeltaDocument doc)
        {
            if (!_opt.IncludeHeader) return;

            HashSet<Type>? enumTypes = _opt.UseTypeTable ? new() : null;
            Dictionary<string, int>? counts = _opt.UseStringTable ? new() : null;

            void CountString(string s)
            {
                if (counts is null) return;
                if (counts.TryGetValue(s, out int c)) counts[s] = c + 1;
                else counts[s] = 1;
            }

            void VisitValue(object? v)
            {
                if (v is null) return;

                switch (v)
                {
                    case string s:
                        CountString(s);
                        return;
                    case Array a when a.Rank == 1:
                        for (int i = 0; i < a.Length; i++) VisitValue(a.GetValue(i));
                        return;
                    case IList list:
                        for (int i = 0; i < list.Count; i++) VisitValue(list[i]);
                        return;
                    case IDictionary dict:
                        foreach (DictionaryEntry e in dict) { VisitValue(e.Key); VisitValue(e.Value); }
                        return;
                }

                var t = v.GetType();
                if (t.IsEnum) enumTypes?.Add(t);
            }

            void WalkDoc(DeltaDocument d)
            {
                foreach (var op in d.Operations)
                {
                    if (op.Key is string ks) CountString(ks);
                    else VisitValue(op.Key);

                    VisitValue(op.Value);

                    if (op.Nested is not null) WalkDoc(op.Nested);
                }
            }

            WalkDoc(doc);

            if (counts is { Count: > 0 })
            {
                _strToId = new(counts.Count);
                _strings = new(counts.Count);

                foreach (var kv in counts)
                {
                    if (kv.Value >= 2 || kv.Key.Length >= 8)
                    {
                        _strToId[kv.Key] = _strings.Count;
                        _strings.Add(kv.Key);
                    }
                }

                if (_strings.Count == 0) { _strToId = null; _strings = null; }
            }

            if (enumTypes is { Count: > 0 })
            {
                _typeToId = new(enumTypes.Count);
                _types = new(enumTypes.Count);
                foreach (var t in enumTypes)
                {
                    int id = _types.Count;
                    _typeToId[t] = id;
                    var asm = t.Assembly.GetName().Name ?? "";
                    var mvid = t.Module.ModuleVersionId;
                    _types.Add(new TypeEntry(t.FullName ?? t.Name, asm, mvid));

                    if (_strToId is not null)
                    {
                        if (!_strToId.ContainsKey(t.FullName ?? t.Name))
                            _strToId[t.FullName ?? t.Name] = _strings!.AddAndReturnIndex(t.FullName ?? t.Name);
                        if (!_strToId.ContainsKey(asm))
                            _strToId[asm] = _strings!.AddAndReturnIndex(asm);
                    }
                }
            }
        }

        private static void WriteTypeEntry(ref SpanWriter sw, in TypeEntry t)
        {
            sw.WriteUtf8StringInline(t.FullName);
            sw.WriteUtf8StringInline(t.AssemblySimpleName);

            Span<byte> tmp = stackalloc byte[16];
            t.Mvid.TryWriteBytes(tmp);
            sw.WriteUInt64(BinaryPrimitives.ReadUInt64LittleEndian(tmp));
            sw.WriteUInt64(BinaryPrimitives.ReadUInt64LittleEndian(tmp.Slice(8)));
        }
    }

    // --------- Reader ---------

    private ref struct Reader
    {
        private ReadOnlySpan<byte> _data;
        private readonly BinaryDeltaOptions _opt;

        private string[]? _strings;
        private Type[]? _enumTypes;

        private int _nesting;

        public Reader(ReadOnlySpan<byte> data, BinaryDeltaOptions opt)
        {
            _data = data;
            _opt = opt ?? BinaryDeltaOptions.Default;
            _strings = null;
            _enumTypes = null;
            _nesting = 0;
        }

        public DeltaDocument ReadDocument()
        {
            var sr = new SpanReader(_data);

            if (_opt.IncludeHeader)
            {
                sr.RequireBytes(4);
                if (sr.ReadByte() != (byte)'B' || sr.ReadByte() != (byte)'D' ||
                    sr.ReadByte() != (byte)'C' || sr.ReadByte() != (byte)'1')
                    throw new InvalidOperationException("Invalid BinaryDelta header magic.");

                var version = sr.ReadVarUInt();
                if (version != 1) throw new NotSupportedException($"Unsupported BinaryDelta version {version}.");

                _ = sr.ReadVarUInt(); // fingerprint

                var flags = sr.ReadByte();
                bool hasStrings = (flags & 0b01) != 0;
                bool hasTypes = (flags & 0b10) != 0;

                if (hasStrings)
                {
                    var n = (int)sr.ReadVarUIntChecked(_opt.Safety.MaxOps);
                    _strings = new string[n];
                    for (int i = 0; i < n; i++) _strings[i] = sr.ReadUtf8StringInlineChecked(_opt.Safety.MaxStringBytes);
                }
                if (hasTypes)
                {
                    var n = (int)sr.ReadVarUIntChecked(_opt.Safety.MaxOps);
                    _enumTypes = new Type[n];
                    for (int i = 0; i < n; i++) _enumTypes[i] = ReadTypeEntry(ref sr);
                }
            }

            var opCount = (int)sr.ReadVarUIntChecked(_opt.Safety.MaxOps);
            var doc = new DeltaDocument();
            var ops = doc.Ops;
            for (int i = 0; i < opCount; i++) ops.Add(ReadOp(ref sr));

            _data = sr.Remaining;
            return doc;
        }

        private DeltaOp ReadOp(ref SpanReader sr)
        {
            var kind = (DeltaKind)sr.ReadVarUInt();

            // memberIndex is encoded as zigzag-varint (long) -> must fit in int
            long mi64 = sr.ReadVarInt();
            if (mi64 < int.MinValue || mi64 > int.MaxValue)
                throw new InvalidOperationException("memberIndex out of Int32 range.");
            int memberIndex = (int)mi64;

            int index = -1;
            object? key = null;
            object? value = null;
            DeltaDocument? nested = null;

            if (kind is DeltaKind.SeqReplaceAt or DeltaKind.SeqAddAt or DeltaKind.SeqRemoveAt)
            {
                ulong idx = sr.ReadVarUInt();
                if (idx > int.MaxValue)
                    throw new InvalidOperationException("sequence index out of Int32 range.");
                index = (int)idx;
            }

            if (kind is DeltaKind.DictSet or DeltaKind.DictRemove or DeltaKind.DictNested)
                key = ReadValue(ref sr);

            if (kind is DeltaKind.ReplaceObject or DeltaKind.SetMember or DeltaKind.SeqReplaceAt or DeltaKind.SeqAddAt or DeltaKind.DictSet)
                value = ReadValue(ref sr);

            if (kind is DeltaKind.NestedMember or DeltaKind.DictNested)
                nested = ReadNested(ref sr);

            return new DeltaOp(memberIndex, kind, index, key, value, nested);
        }


        private DeltaDocument ReadNested(ref SpanReader sr)
        {
            if (++_nesting > _opt.Safety.MaxNesting)
                throw new InvalidOperationException("Max nesting exceeded");

            var count = (int)sr.ReadVarUIntChecked(_opt.Safety.MaxOps);
            var doc = new DeltaDocument();
            var list = doc.Ops;
            for (int i = 0; i < count; i++) list.Add(ReadOp(ref sr));
            _nesting--;
            return doc;
        }

        private object? ReadValue(ref SpanReader sr)
        {
            var tag = (VTag)sr.ReadByte();

            switch (tag)
            {
                case VTag.Null: return null;
                case VTag.BoolFalse: return false;
                case VTag.BoolTrue: return true;

                case VTag.SByte: return (sbyte)sr.ReadVarInt();
                case VTag.Byte: return (byte)sr.ReadVarUInt();
                case VTag.Int16: return (short)sr.ReadVarInt();
                case VTag.UInt16: return (ushort)sr.ReadVarUInt();
                case VTag.Int32: return (int)sr.ReadVarInt();
                case VTag.UInt32: return (uint)sr.ReadVarUInt();
                case VTag.Int64: return (long)sr.ReadVarInt();
                case VTag.UInt64: return (ulong)sr.ReadVarUInt();
                case VTag.Char16: return (char)sr.ReadUInt16();

                case VTag.Single: { var u = sr.ReadUInt32(); return BitConverter.Int32BitsToSingle((int)u); }
                case VTag.Double: { var u = sr.ReadUInt64(); return BitConverter.Int64BitsToDouble((long)u); }
                case VTag.Decimal: return ReadDecimal(ref sr);

                case VTag.StringInline: return sr.ReadUtf8StringInlineChecked(_opt.Safety.MaxStringBytes);
                case VTag.StringRef:
                    {
                        var sid = (int)sr.ReadVarUIntChecked(_strings?.Length ?? 0);
                        if (_strings is null) throw new InvalidOperationException("No string table in stream.");
                        return _strings[sid];
                    }

                case VTag.Guid16:
                    return ReadGuid16(ref sr);

                case VTag.DateTimeBin64:
                    return DateTime.FromBinary(sr.ReadVarInt());

                case VTag.TimeSpanTicks:
                    return new TimeSpan(sr.ReadVarInt());

                case VTag.DateTimeOffset:
                    {
                        long ticks = sr.ReadVarInt();
                        var offMin = (int)sr.ReadVarInt();
                        return new DateTimeOffset(ticks, TimeSpan.FromMinutes(offMin));
                    }

                case VTag.Enum:
                    {
                        var (enumT, underlying) = ReadEnumTypeIdentity(ref sr);
                        return ReadEnumValue(ref sr, enumT, underlying);
                    }

                case VTag.ByteArray:
                    {
                        var len = (int)sr.ReadVarUIntChecked(_opt.Safety.MaxStringBytes);
                        var bytes = new byte[len];
                        sr.ReadBytes(bytes);
                        return bytes;
                    }

                case VTag.Array: return ReadArray(ref sr);
                case VTag.List: return ReadList(ref sr);
                case VTag.Dictionary: return ReadDict(ref sr);

                default:
                    throw new InvalidOperationException($"Unknown value tag: {tag}");
            }
        }

        private static decimal ReadDecimal(ref SpanReader sr)
        {
            var a = (int)sr.ReadUInt32();
            var b = (int)sr.ReadUInt32();
            var c = (int)sr.ReadUInt32();
            var d = (int)sr.ReadUInt32();
            return new decimal(new[] { a, b, c, d });
        }

        private Array ReadArray(ref SpanReader sr)
        {
            var elem = ReadTypeSpec(ref sr);
            var len = (int)sr.ReadVarUIntChecked(_opt.Safety.MaxOps);

            if (TryCreateArray(elem, len, out var arr, out bool objecty))
            {
                if (objecty)
                {
                    for (int i = 0; i < len; i++) arr.SetValue(ReadValue(ref sr), i);
                }
                else
                {
                    for (int i = 0; i < len; i++)
                        arr.SetValue(ConvertValueForElement(ReadValue(ref sr), elem), i);
                }
                return arr;
            }

            var o = new object?[len];
            for (int i = 0; i < len; i++) o[i] = ReadValue(ref sr);
            return o;
        }

        private object ReadList(ref SpanReader sr)
        {
            var elem = ReadTypeSpec(ref sr);
            var len = (int)sr.ReadVarUIntChecked(_opt.Safety.MaxOps);
            var list = CreateList(elem, len);
            var il = (IList)list;
            for (int i = 0; i < len; i++) il.Add(ConvertValueForElement(ReadValue(ref sr), elem));
            return list;
        }

        private object ReadDict(ref SpanReader sr)
        {
            var keyT = ReadTypeSpec(ref sr);
            var valT = ReadTypeSpec(ref sr);
            var len = (int)sr.ReadVarUIntChecked(_opt.Safety.MaxOps);
            var dict = CreateDictionary(keyT, valT, len);
            var id = (IDictionary)dict;
            for (int i = 0; i < len; i++)
            {
                var k = ConvertValueForElement(ReadValue(ref sr), keyT);
                var v = ConvertValueForElement(ReadValue(ref sr), valT);
                id[k!] = v;
            }
            return dict;
        }

        private static bool TryCreateArray(Type t, int len, out Array arr, out bool isObject)
        {
            if (t == typeof(object)) { arr = new object?[len]!; isObject = true; return true; }
            try { arr = Array.CreateInstance(t, len); isObject = false; return true; }
            catch { arr = Array.Empty<object>(); isObject = true; return false; }
        }

        private static object CreateList(Type t, int capacity)
        {
            if (t == typeof(object)) return new List<object?>(capacity);
            try { return Activator.CreateInstance(typeof(List<>).MakeGenericType(t), capacity)!; }
            catch { return new List<object?>(capacity); }
        }

        private static object CreateDictionary(Type kt, Type vt, int capacity)
        {
            try { return Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(kt, vt), capacity)!; }
            catch { return new Dictionary<object?, object?>(capacity); }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static object? ConvertValueForElement(object? val, Type target)
        {
            if (val is null || target == typeof(object)) return val;
            if (target.IsInstanceOfType(val)) return val;
            return Convert.ChangeType(val, target);
        }

        private (Type enumType, Type underlying) ReadEnumTypeIdentity(ref SpanReader sr)
        {
            var kind = (TypeSpecKind)sr.ReadVarUInt();
            switch (kind)
            {
                case TypeSpecKind.Enum:
                    if (_opt.IncludeHeader && _enumTypes is { Length: > 0 })
                    {
                        var tid = (int)sr.ReadVarUIntChecked(_enumTypes.Length);
                        var t = _enumTypes[tid];
                        return (t, Enum.GetUnderlyingType(t));
                    }
                    else
                    {
                        var full = sr.ReadUtf8StringInlineChecked(_opt.Safety.MaxStringBytes);
                        var asm = sr.ReadUtf8StringInlineChecked(_opt.Safety.MaxStringBytes);
                        var mvid = ReadGuid16(ref sr);

                        var t = ResolveType(full, asm, mvid) ?? throw new TypeLoadException($"Enum type '{full}' not found in assembly '{asm}' ({mvid}).");
                        return (t, Enum.GetUnderlyingType(t));
                    }
                default:
                    throw new InvalidOperationException("Bad enum type identity.");
            }
        }

        private static Type? ResolveType(string full, string asmSimple, Guid mvid)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var n = asm.GetName();
                if (!string.Equals(n.Name, asmSimple, StringComparison.Ordinal)) continue;
                try
                {
                    if (asm.ManifestModule?.ModuleVersionId == mvid)
                        return asm.GetType(full, throwOnError: false, ignoreCase: false);
                }
                catch { }
            }
            return null;
        }

        private Type ReadTypeSpec(ref SpanReader sr)
        {
            var kind = (TypeSpecKind)sr.ReadVarUInt();
            switch (kind)
            {
                case TypeSpecKind.Object: return typeof(object);
                case TypeSpecKind.PrimitiveOrKnown:
                    return ReadKnownType(ref sr);
                case TypeSpecKind.Enum:
                    if (_opt.IncludeHeader && _enumTypes is { Length: > 0 })
                    {
                        var tid = (int)sr.ReadVarUIntChecked(_enumTypes.Length);
                        return _enumTypes[tid];
                    }
                    else
                    {
                        var full = sr.ReadUtf8StringInlineChecked(_opt.Safety.MaxStringBytes);
                        var asm = sr.ReadUtf8StringInlineChecked(_opt.Safety.MaxStringBytes);

                        var mvid = ReadGuid16(ref sr);

                        return ResolveType(full, asm, mvid) ?? typeof(object);
                    }
                default:
                    throw new InvalidOperationException("Unknown TypeSpecKind.");
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static object ReadEnumValue(ref SpanReader sr, Type enumType, Type underlying)
        {
            if (underlying == typeof(sbyte))
            {
                var v = (sbyte)sr.ReadVarInt();
                return Enum.ToObject(enumType, v);
            }
            if (underlying == typeof(byte))
            {
                var v = (byte)sr.ReadVarUInt();
                return Enum.ToObject(enumType, v);
            }
            if (underlying == typeof(short))
            {
                var v = (short)sr.ReadVarInt();
                return Enum.ToObject(enumType, v);
            }
            if (underlying == typeof(ushort))
            {
                var v = (ushort)sr.ReadVarUInt();
                return Enum.ToObject(enumType, v);
            }
            if (underlying == typeof(int))
            {
                var v = (int)sr.ReadVarInt();
                return Enum.ToObject(enumType, v);
            }
            if (underlying == typeof(uint))
            {
                var v = (uint)sr.ReadVarUInt();
                return Enum.ToObject(enumType, v);
            }
            if (underlying == typeof(long))
            {
                var v = sr.ReadVarInt();
                return Enum.ToObject(enumType, v);
            }
            if (underlying == typeof(ulong))
            {
                var v = sr.ReadVarUInt();
                return Enum.ToObject(enumType, v);
            }

            throw new NotSupportedException("Unsupported enum underlying type.");
        }

        private static Type ReadKnownType(ref SpanReader sr)
        {
            var code = (KnownTypeCode)sr.ReadVarUInt();
            return code switch
            {
                KnownTypeCode.SByte => typeof(sbyte),
                KnownTypeCode.Byte => typeof(byte),
                KnownTypeCode.Int16 => typeof(short),
                KnownTypeCode.UInt16 => typeof(ushort),
                KnownTypeCode.Int32 => typeof(int),
                KnownTypeCode.UInt32 => typeof(uint),
                KnownTypeCode.Int64 => typeof(long),
                KnownTypeCode.UInt64 => typeof(ulong),
                KnownTypeCode.Single => typeof(float),
                KnownTypeCode.Double => typeof(double),
                KnownTypeCode.Decimal => typeof(decimal),
                KnownTypeCode.Bool => typeof(bool),
                KnownTypeCode.Char => typeof(char),
                KnownTypeCode.String => typeof(string),
                KnownTypeCode.Guid => typeof(Guid),
                KnownTypeCode.DateTime => typeof(DateTime),
                KnownTypeCode.TimeSpan => typeof(TimeSpan),
                KnownTypeCode.DateTimeOffset => typeof(DateTimeOffset),
                _ => typeof(object)
            };
        }

        private static Type ReadTypeEntry(ref SpanReader sr)
        {
            var full = sr.ReadUtf8StringInlineChecked(int.MaxValue);
            var asm = sr.ReadUtf8StringInlineChecked(int.MaxValue);

            var mvid = ReadGuid16(ref sr);

            return ResolveType(full, asm, mvid) ?? throw new TypeLoadException($"Type '{full}' not found in '{asm}' ({mvid}).");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Guid ReadGuid16(ref SpanReader sr)
        {
            Span<byte> tmp = stackalloc byte[16];
            BinaryPrimitives.WriteUInt64LittleEndian(tmp, sr.ReadUInt64());
            BinaryPrimitives.WriteUInt64LittleEndian(tmp.Slice(8), sr.ReadUInt64());
            return new Guid(tmp);
        }
    }

    // --------- Span writer/reader + varint helpers ---------

    private ref struct SpanWriter
    {
        private IBufferWriter<byte> _bw;
        private Span<byte> _span;
        private int _pos;

        public SpanWriter(IBufferWriter<byte> bw)
        {
            _bw = bw;
            _span = bw.GetSpan(256);
            _pos = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte(byte b)
        {
            if (_pos >= _span.Length) Grow(1);
            _span[_pos++] = b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytes(ReadOnlySpan<byte> src)
        {
            int needed = src.Length;
            if (_pos + needed > _span.Length) Grow(needed);
            src.CopyTo(_span.Slice(_pos));
            _pos += needed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt16(ushort v)
        {
            if (_pos + 2 > _span.Length) Grow(2);
            BinaryPrimitives.WriteUInt16LittleEndian(_span.Slice(_pos), v);
            _pos += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt32(uint v)
        {
            if (_pos + 4 > _span.Length) Grow(4);
            BinaryPrimitives.WriteUInt32LittleEndian(_span.Slice(_pos), v);
            _pos += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt64(ulong v)
        {
            if (_pos + 8 > _span.Length) Grow(8);
            BinaryPrimitives.WriteUInt64LittleEndian(_span.Slice(_pos), v);
            _pos += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVarUInt(ulong v)
        {
            while (v >= 0x80)
            {
                WriteByte((byte)(v | 0x80));
                v >>= 7;
            }
            WriteByte((byte)v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVarUInt(uint v) => WriteVarUInt((ulong)v);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVarInt(long value)
        {
            ulong zigzag = (ulong)((value << 1) ^ (value >> 63));
            WriteVarUInt(zigzag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVarInt(int value) => WriteVarInt((long)value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf8StringInline(string s)
        {
            var byteCount = Encoding.UTF8.GetByteCount(s);
            WriteVarUInt((uint)byteCount);
            if (_pos + byteCount > _span.Length) Grow(byteCount);
            _ = Encoding.UTF8.GetBytes(s.AsSpan(), _span.Slice(_pos));
            _pos += byteCount;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Grow(int needed)
        {
            _bw.Advance(_pos);
            _span = _bw.GetSpan(Math.Max(needed, _span.Length * 2));
            _pos = 0;
        }

        // NEW: commit remaining bytes at the end
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Commit()
        {
            _bw.Advance(_pos);
            _pos = 0;
        }
    }

    private ref struct SpanReader
    {
        private ReadOnlySpan<byte> _data;

        public SpanReader(ReadOnlySpan<byte> data) => _data = data;

        public ReadOnlySpan<byte> Remaining => _data;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
        {
            var b = _data[0];
            _data = _data.Slice(1);
            return b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadBytes(Span<byte> dst)
        {
            _data.Slice(0, dst.Length).CopyTo(dst);
            _data = _data.Slice(dst.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUInt16()
        {
            var v = BinaryPrimitives.ReadUInt16LittleEndian(_data);
            _data = _data.Slice(2);
            return v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt32()
        {
            var v = BinaryPrimitives.ReadUInt32LittleEndian(_data);
            _data = _data.Slice(4);
            return v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadUInt64()
        {
            var v = BinaryPrimitives.ReadUInt64LittleEndian(_data);
            _data = _data.Slice(8);
            return v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadVarUInt()
        {
            ulong result = 0;
            int shift = 0;
            while (true)
            {
                byte b = ReadByte();
                result |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) break;
                shift += 7;
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadVarInt()
        {
            var u = ReadVarUInt();
            long val = (long)((u >> 1) ^ (~(u & 1) + 1));
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadVarUIntChecked(int max)
        {
            var v = ReadVarUInt();
            if (v > (ulong)max) throw new InvalidOperationException("Bound exceeded.");
            return (uint)v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ReadUtf8StringInlineChecked(int maxLen)
        {
            var len = (int)ReadVarUIntChecked(maxLen);
            var bytes = _data.Slice(0, len);
            var s = Encoding.UTF8.GetString(bytes);
            _data = _data.Slice(len);
            return s;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RequireBytes(int n)
        {
            if (_data.Length < n) throw new InvalidOperationException("Unexpected EOF");
        }
    }

    private static int AddAndReturnIndex(this List<string> list, string s)
    {
        int idx = list.Count; list.Add(s); return idx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteGuid16(ref SpanWriter sw, in Guid g)
    {
        Span<byte> tmp = stackalloc byte[16];
        g.TryWriteBytes(tmp);
        sw.WriteUInt64(BinaryPrimitives.ReadUInt64LittleEndian(tmp));
        sw.WriteUInt64(BinaryPrimitives.ReadUInt64LittleEndian(tmp.Slice(8)));
    }
}

/// <summary>Ergonomic bridge for tests and adapters.</summary>
public static class DeltaDocumentBinaryExtensions
{
    /// <summary>Encode to binary.</summary>
    public static void ToBinary(this DeltaDocument doc, IBufferWriter<byte> output, BinaryDeltaOptions? options = null)
        => BinaryDeltaCodec.Write(doc, output, options);

    /// <summary>Decode from binary.</summary>
    public static DeltaDocument FromBinary(ReadOnlySpan<byte> data, BinaryDeltaOptions? options = null)
        => BinaryDeltaCodec.Read(data, options);
}