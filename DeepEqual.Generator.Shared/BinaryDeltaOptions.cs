namespace DeepEqual.Generator.Shared;

/// <summary>Controls encoding profile and safety caps.</summary>
public sealed class BinaryDeltaOptions
{
    internal static readonly BinaryDeltaOptions Default = new();

    /// <summary>
    ///     Headerless fast path by default (best in-proc).
    ///     When true, a tiny header is written: (magic "BDC1", version=1, stableTypeFingerprint).
    ///     In headerful mode, we also enable prefaces (type table for enums, optional string table).
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
    public Limits Safety { get; } = new();

    public sealed class Limits
    {
        /// <summary>Max operations allowed in a single document (top-level or nested).</summary>
        public int MaxOps { get; set; } = 1_000_000;

        /// <summary>Max encoded UTF-8 string length in bytes.</summary>
        public int MaxStringBytes { get; set; } = 16 * 1024 * 1024;

        /// <summary>Max nesting depth for nested documents (member or dict-nested).</summary>
        public int MaxNesting { get; set; } = 256;
    }
}