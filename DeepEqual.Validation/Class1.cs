// Design-time contract only. Do not implement. Do not call at runtime.
// The source generator inspects usages of these interfaces to emit
// reflection-free, high-performance validators.
using DeepEqual.Generator.Shared;
using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace DeepOps.Validation;

/// <summary>
/// Entry point for defining validation specs. You can have multiple Configure(IVSpec) methods
/// across files/classes; the generator unions them.
/// </summary>
public interface IVSpec
{
    ITypeSpec<T> For<T>(System.Func<ITypeSpec<T>, ITypeSpec<T>> config);
    // Optional global knobs (e.g., default severity, stop-on-first-error). Purely hints for codegen.
    IVSpec WithDefaults(System.Func<Defaults, Defaults> configure);
}

/// <summary> Per-assembly/project default hints for codegen. </summary>
public sealed class Defaults
{
    public ValidationSeverity DefaultSeverity { get; init; } = ValidationSeverity.Error;
    public bool StopOnFirstError { get; init; } = false;
    public string? MessagePrefix { get; init; } = null;
}

public enum ValidationSeverity { Info, Warning, Error }

/// <summary>
/// Type-level builder. Chain member rules, collection/dictionary rules, conditionals,
/// cross-member comparisons, aggregates, and reusable rule packs.
/// </summary>
public interface ITypeSpec<T>
{
    // Scalar/complex member rule
    IMemberRule<T, TProp> Rule<TProp>(System.Linq.Expressions.Expression<System.Func<T, TProp>> selector);

    // Per-item rules for a collection member
    ITypeSpec<T> ForEach<TItem>(
        System.Linq.Expressions.Expression<System.Func<T, System.Collections.Generic.IEnumerable<TItem>>> selector,
        System.Func<ITypeSpec<TItem>, ITypeSpec<TItem>> itemRules);

    // Dictionary member: key/value rules
    ITypeSpec<T> ForDictionary<TKey, TValue>(
        System.Linq.Expressions.Expression<System.Func<T, System.Collections.Generic.IDictionary<TKey, TValue>>> selector,
        System.Func<IDictionarySpec<TKey, TValue>, IDictionarySpec<TKey, TValue>> dictRules);

    // Collection-level constraints (count, distinct/unique)
    ITypeSpec<T> ForCollection<TItem>(
        System.Linq.Expressions.Expression<System.Func<T, System.Collections.Generic.IEnumerable<TItem>>> selector,
        System.Func<ICollectionSpec<TItem>, ICollectionSpec<TItem>> collRules);

    // Uniqueness on a child collection by key(s)
    ITypeSpec<T> Unique<TItem>(
        System.Linq.Expressions.Expression<System.Func<T, System.Collections.Generic.IEnumerable<TItem>>> selector)
        => Unique(selector, u => u);

    ITypeSpec<T> Unique<TItem>(
        System.Linq.Expressions.Expression<System.Func<T, System.Collections.Generic.IEnumerable<TItem>>> selector,
        System.Func<IUniqueSpec<TItem>, IUniqueSpec<TItem>> byKeys);

    // Conditional rules (if/else)
    ITypeSpec<T> When(
        System.Linq.Expressions.Expression<System.Func<T, bool>> condition,
        System.Func<ITypeSpec<T>, ITypeSpec<T>> then);

    ITypeSpec<T> WhenElse(
        System.Linq.Expressions.Expression<System.Func<T, bool>> condition,
        System.Func<ITypeSpec<T>, ITypeSpec<T>> then,
        System.Func<ITypeSpec<T>, ITypeSpec<T>> @else);

    // Cross-member comparisons
    ITypeSpec<T> Compare<TLeft>(
        System.Linq.Expressions.Expression<System.Func<T, TLeft>> left,
        System.Func<ICompareBuilder<T, TLeft>, ICompareBuilder<T, TLeft>> cmp);

    // Aggregates over child collections (sum, avg, etc.)
    ITypeSpec<T> Aggregate<TItem, TVal>(
        System.Linq.Expressions.Expression<System.Func<T, System.Collections.Generic.IEnumerable<TItem>>> items,
        System.Func<IAggregateSpec<T, TItem, TVal>, IAggregateSpec<T, TItem, TVal>> agg);

    // Reusable rule packs
    ITypeSpec<T> Use(IRulePack<T> pack);

    // Execution hints at type scope
    ITypeSpec<T> WithSeverity(ValidationSeverity severity);
    ITypeSpec<T> StopOnFirstError(bool stop = true);
}

/// <summary> Chain of rules for a single member (property/field). </summary>
public interface IMemberRule<TRoot, TProp>
{
    // Presence & shape
    IMemberRule<TRoot, TProp> Required();            // not null
    IMemberRule<TRoot, TProp> NotDefault();          // != default(TProp)
    IMemberRule<TRoot, TProp> NotEmpty();            // strings/collections
    IMemberRule<TRoot, TProp> NotWhitespace();       // strings

    // Numbers & booleans
    IMemberRule<TRoot, TProp> Range(TProp min, TProp max, bool inclusive = true);
    IMemberRule<TRoot, TProp> GreaterThan(TProp bound);
    IMemberRule<TRoot, TProp> GreaterOrEqual(TProp bound);
    IMemberRule<TRoot, TProp> LessThan(TProp bound);
    IMemberRule<TRoot, TProp> LessOrEqual(TProp bound);
    IMemberRule<TRoot, TProp> MultipleOf(TProp step);    // e.g., 0.5
    IMemberRule<TRoot, TProp> PrecisionScale(int precision, int scale); // decimals

    // Strings & text
    IMemberRule<TRoot, TProp> Length(int min, int max);
    IMemberRule<TRoot, TProp> MinLength(int min);
    IMemberRule<TRoot, TProp> MaxLength(int max);
    IMemberRule<TRoot, TProp> Pattern(string regex);
    IMemberRule<TRoot, TProp> Allowed(params string[] values);
    IMemberRule<TRoot, TProp> Disallowed(params string[] values);
    IMemberRule<TRoot, TProp> Email();
    IMemberRule<TRoot, TProp> Url();
    IMemberRule<TRoot, TProp> Hostname();
    IMemberRule<TRoot, TProp> IpAddress();
    IMemberRule<TRoot, TProp> Slug();

    // Enums & flags
    IMemberRule<TRoot, TProp> IsDefinedEnum(); // enum value is in range
    IMemberRule<TRoot, TProp> AllowedEnums(params TProp[] allowed); // for enum TProp
    IMemberRule<TRoot, TProp> DisallowedEnums(params TProp[] disallowed);
    IMemberRule<TRoot, TProp> RequireFlags(TProp mask); // [Flags]
    IMemberRule<TRoot, TProp> ForbidFlags(TProp mask);
    IMemberRule<TRoot, TProp> AllowedFlags(TProp mask);

    // Dates & times
    IMemberRule<TRoot, TProp> InPast();
    IMemberRule<TRoot, TProp> InFuture();
    IMemberRule<TRoot, TProp> Between(TProp min, TProp max, bool inclusive = true);
    IMemberRule<TRoot, TProp> AtLeastFromNow(System.TimeSpan delta);
    IMemberRule<TRoot, TProp> AtMostFromNow(System.TimeSpan delta);
    IMemberRule<TRoot, TProp> Weekday();
    IMemberRule<TRoot, TProp> Weekend();

    // Collections on this member (when TProp is IEnumerable)
    IMemberRule<TRoot, TProp> CountBetween(int min, int max);
    IMemberRule<TRoot, TProp> MinCount(int min);
    IMemberRule<TRoot, TProp> MaxCount(int max);
    IMemberRule<TRoot, TProp> Distinct();  // by value
    IMemberRule<TRoot, TProp> DistinctBy<TKey>(System.Func<object, TKey> keySelector); // design-time only

    // Custom predicates (pure, analyzable)
    IMemberRule<TRoot, TProp> Must(System.Linq.Expressions.Expression<System.Func<TRoot, TProp, bool>> predicate, string? code = null);

    // Messaging / severity overrides
    IMemberRule<TRoot, TProp> WithMessage(string template);
    IMemberRule<TRoot, TProp> WithCode(string code);
    IMemberRule<TRoot, TProp> WithSeverity(ValidationSeverity severity);

    // Execution hints
    IMemberRule<TRoot, TProp> StopOnFirstError(bool stop = true);
}

/// <summary> Collection-level constraints for a specific member. </summary>
public interface ICollectionSpec<TItem>
{
    ICollectionSpec<TItem> NotEmpty();
    ICollectionSpec<TItem> CountBetween(int min, int max);
    ICollectionSpec<TItem> MinCount(int min);
    ICollectionSpec<TItem> MaxCount(int max);

    // Distinctness (by equality) and uniqueness by key(s)
    ICollectionSpec<TItem> Distinct();
    ICollectionSpec<TItem> DistinctBy<TKey>(System.Linq.Expressions.Expression<System.Func<TItem, TKey>> key);
    IUniqueSpec<TItem> Unique();
    ICollectionSpec<TItem> Unique(System.Func<IUniqueSpec<TItem>, IUniqueSpec<TItem>> byKeys);

    // Ordering constraints
    ICollectionSpec<TItem> SortedAsc<TKey>(System.Linq.Expressions.Expression<System.Func<TItem, TKey>> key);
    ICollectionSpec<TItem> SortedDesc<TKey>(System.Linq.Expressions.Expression<System.Func<TItem, TKey>> key);
}

/// <summary> Dictionary-level constraints for keys and values. </summary>
public interface IDictionarySpec<TKey, TValue>
{
    IDictionarySpec<TKey, TValue> NotEmpty();
    IDictionarySpec<TKey, TValue> MinCount(int min);
    IDictionarySpec<TKey, TValue> MaxCount(int max);

    // Key rules
    IDictionarySpec<TKey, TValue> Keys(System.Func<IMemberRule<object, TKey>, IMemberRule<object, TKey>> keyRules);

    // Value rules
    IDictionarySpec<TKey, TValue> Values(System.Func<IMemberRule<object, TValue>, IMemberRule<object, TValue>> valueRules);

    // Key↔Value relationship invariant (analyzable expression)
    IDictionarySpec<TKey, TValue> KeyValueMust(System.Linq.Expressions.Expression<System.Func<TKey, TValue, bool>> predicate, string? code = null);
}

/// <summary> Specify uniqueness keys for elements (maps cleanly to KeyMembers). </summary>
public interface IUniqueSpec<TItem>
{
    IUniqueSpec<TItem> By<TKey>(System.Linq.Expressions.Expression<System.Func<TItem, TKey>> key);
    // Composite keys
    IUniqueSpec<TItem> By<TKey1, TKey2>(
        System.Linq.Expressions.Expression<System.Func<TItem, TKey1>> k1,
        System.Linq.Expressions.Expression<System.Func<TItem, TKey2>> k2);
    IUniqueSpec<TItem> By<TKey1, TKey2, TKey3>(
        System.Linq.Expressions.Expression<System.Func<TItem, TKey1>> k1,
        System.Linq.Expressions.Expression<System.Func<TItem, TKey2>> k2,
        System.Linq.Expressions.Expression<System.Func<TItem, TKey3>> k3);
}

/// <summary> Cross-member comparisons builder (A ? B). </summary>
public interface ICompareBuilder<T, TLeft>
{
    ICompareBuilder<T, TLeft> LessThan<TRight>(System.Linq.Expressions.Expression<System.Func<T, TRight>> right);
    ICompareBuilder<T, TLeft> LessOrEqual<TRight>(System.Linq.Expressions.Expression<System.Func<T, TRight>> right);
    ICompareBuilder<T, TLeft> GreaterThan<TRight>(System.Linq.Expressions.Expression<System.Func<T, TRight>> right);
    ICompareBuilder<T, TLeft> GreaterOrEqual<TRight>(System.Linq.Expressions.Expression<System.Func<T, TRight>> right);
    ICompareBuilder<T, TLeft> EqualTo<TRight>(System.Linq.Expressions.Expression<System.Func<T, TRight>> right);
    ICompareBuilder<T, TLeft> NotEqualTo<TRight>(System.Linq.Expressions.Expression<System.Func<T, TRight>> right);

    // Messaging / severity
    ICompareBuilder<T, TLeft> WithMessage(string template);
    ICompareBuilder<T, TLeft> WithCode(string code);
    ICompareBuilder<T, TLeft> WithSeverity(ValidationSeverity severity);
}

/// <summary> Aggregate rules over a child collection. </summary>
public interface IAggregateSpec<T, TItem, TVal>
{
    // Select value to aggregate
    IAggregateSpec<T, TItem, TVal> Select(System.Linq.Expressions.Expression<System.Func<TItem, TVal>> selector);

    // Aggregate kinds
    IAggregateSpec<T, TItem, TVal> SumBetween(TVal min, TVal max, bool inclusive = true);
    IAggregateSpec<T, TItem, TVal> AverageBetween(TVal min, TVal max, bool inclusive = true);
    IAggregateSpec<T, TItem, TVal> MinAtLeast(TVal min, bool inclusive = true);
    IAggregateSpec<T, TItem, TVal> MaxAtMost(TVal max, bool inclusive = true);
    IAggregateSpec<T, TItem, TVal> CountBetween(int min, int max);

    // Custom aggregate predicate (analyzable)
    IAggregateSpec<T, TItem, TVal> Must(System.Linq.Expressions.Expression<System.Func<System.Collections.Generic.IEnumerable<TItem>, bool>> predicate, string? code = null);

    // Messaging / severity
    IAggregateSpec<T, TItem, TVal> WithMessage(string template);
    IAggregateSpec<T, TItem, TVal> WithCode(string code);
    IAggregateSpec<T, TItem, TVal> WithSeverity(ValidationSeverity severity);
}

/// <summary>
/// Reusable pack of rules that can be applied to a type spec. Implemented by the user as a simple adapter
/// (no runtime execution is required; the generator sees the call graph).
/// </summary>
public interface IRulePack<T>
{
    ITypeSpec<T> Apply(ITypeSpec<T> spec);
}

public readonly record struct ValidationError(
    string Path,
    string Code,
    string Message,
    ValidationSeverity Severity = ValidationSeverity.Error,
    IReadOnlyList<int>? StableIndices = null // optional: stable member-index path
);

public sealed class ValidationResult
{
    private readonly List<ValidationError> _errors = new();

    public IReadOnlyList<ValidationError> Errors => _errors;
    public bool IsValid => _errors.Count == 0;

    public void Add(ValidationError error) => _errors.Add(error);

    public void AddRange(IEnumerable<ValidationError> errors)
    {
        if (errors is null) return;
        _errors.AddRange(errors);
    }

    public static ValidationResult Merge(params ValidationResult[] results)
    {
        var r = new ValidationResult();
        if (results is null) return r;
        foreach (var x in results) if (x is not null && !x.IsValid) r._errors.AddRange(x._errors);
        return r;
    }
}

public sealed class ValidationException(ValidationResult result, string? message = null)
    : Exception(message ?? "Validation failed.")
{
    public ValidationResult Result { get; } = result;
}
public sealed class ValidationContext
{
    public ComparisonContext Comparison { get; }
    public IClock Clock { get; }
    public IValidationMessageProvider Messages { get; }
    public bool StopOnFirstError { get; }
    public CultureInfo? Culture { get; }

    public ValidationContext(
        ComparisonContext? comparison = null,
        IClock? clock = null,
        IValidationMessageProvider? messages = null,
        bool stopOnFirstError = false,
        CultureInfo? culture = null)
    {
        Comparison = comparison ?? new ComparisonContext();
        Clock = clock ?? SystemClock.Instance;
        Messages = messages ?? DefaultValidationMessageProvider.Instance;
        StopOnFirstError = stopOnFirstError;
        Culture = culture;
    }
}

public interface IClock
{
    DateTimeOffset Now();
}

public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();
    private SystemClock() { }
    public DateTimeOffset Now() => DateTimeOffset.UtcNow;
}
/// <summary>Lightweight placeholder bag for message formatting.</summary>
public readonly struct MessageArgs
{
    public readonly string Name;      // e.g., "Email"
    public readonly string Path;      // e.g., "Customer.Email"
    public readonly IReadOnlyDictionary<string, object?> Data;

    public MessageArgs(string name, string path, IReadOnlyDictionary<string, object?> data)
    {
        Name = name;
        Path = path;
        Data = data;
    }

    public object? this[string key] => Data != null && Data.TryGetValue(key, out var v) ? v : null;
}

public interface IValidationMessageProvider
{
    string GetMessage(string code, in MessageArgs args, CultureInfo? culture);
}

public sealed class DefaultValidationMessageProvider : IValidationMessageProvider
{
    public static readonly DefaultValidationMessageProvider Instance = new();

    private readonly Dictionary<string, string> _templates = new(StringComparer.Ordinal)
    {
        // Presence / shape
        ["Required"] = "{Name} is required.",
        ["NotDefault"] = "{Name} must not be the default value.",
        ["NotEmpty"] = "{Name} must not be empty.",
        ["NotWhitespace"] = "{Name} must not be whitespace.",

        // Numbers
        ["Range"] = "{Name} must be between {Min} and {Max}.",
        ["GreaterThan"] = "{Name} must be greater than {Bound}.",
        ["GreaterOrEqual"] = "{Name} must be greater than or equal to {Bound}.",
        ["LessThan"] = "{Name} must be less than {Bound}.",
        ["LessOrEqual"] = "{Name} must be less than or equal to {Bound}.",
        ["MultipleOf"] = "{Name} must be a multiple of {Step}.",
        ["PrecisionScale"] = "{Name} must fit precision {Precision} and scale {Scale}.",

        // Strings
        ["Length"] = "{Name} length must be between {Min} and {Max} characters.",
        ["MinLength"] = "{Name} length must be at least {Min} characters.",
        ["MaxLength"] = "{Name} length must be at most {Max} characters.",
        ["Pattern"] = "{Name} is not in the expected format.",
        ["Allowed"] = "{Name} must be one of the allowed values.",
        ["Disallowed"] = "{Name} contains a disallowed value.",
        ["Email"] = "{Name} is not a valid email.",
        ["Url"] = "{Name} is not a valid URL.",
        ["Hostname"] = "{Name} is not a valid hostname.",
        ["IpAddress"] = "{Name} is not a valid IP address.",
        ["Slug"] = "{Name} is not a valid slug.",

        // Enums & flags
        ["EnumDefined"] = "{Name} is not a defined value.",
        ["FlagsAllowed"] = "{Name} contains disallowed flags.",
        ["FlagsRequire"] = "{Name} must include required flags.",

        // Dates & times
        ["InPast"] = "{Name} must be in the past.",
        ["InFuture"] = "{Name} must be in the future.",
        ["Between"] = "{Name} must be between {Min} and {Max}.",
        ["AtLeastFromNow"] = "{Name} must be at least {Delta} from now.",
        ["AtMostFromNow"] = "{Name} must be at most {Delta} from now.",
        ["Weekday"] = "{Name} must be a weekday.",
        ["Weekend"] = "{Name} must be a weekend day.",

        // Collections / dictionaries
        ["CountBetween"] = "{Name} must contain between {Min} and {Max} items.",
        ["MinCount"] = "{Name} must contain at least {Min} items.",
        ["MaxCount"] = "{Name} must contain at most {Max} items.",
        ["Distinct"] = "{Name} must not contain duplicates.",
        ["UniqueBy"] = "{Name} must have unique elements by {Keys}.",
        ["SortedAsc"] = "{Name} must be sorted ascending.",
        ["SortedDesc"] = "{Name} must be sorted descending.",
        ["DictNotEmpty"] = "{Name} must contain at least one entry.",
        ["DictKeyRule"] = "{Name} has an invalid key.",
        ["DictValueRule"] = "{Name} has an invalid value.",

        // Cross-member & aggregates
        ["Compare"] = "{Left} must be {Op} {Right}.",
        ["SumBetween"] = "Sum of {Name} must be between {Min} and {Max}.",
        ["AverageBetween"] = "Average of {Name} must be between {Min} and {Max}.",
        ["MinAtLeast"] = "Minimum of {Name} must be at least {Min}.",
        ["MaxAtMost"] = "Maximum of {Name} must be at most {Max}.",

        // Custom predicate
        ["Must"] = "{Name} is invalid."
    };

    private DefaultValidationMessageProvider() { }

    public string GetMessage(string code, in MessageArgs args, CultureInfo? culture)
    {
        if (!_templates.TryGetValue(code, out var tpl))
            tpl = "{Name} is invalid.";

        // Very light token replacement; generator can pre-format for hotspots if desired.
        string s = tpl
            .Replace("{Name}", args.Name ?? "Value")
            .Replace("{Path}", args.Path ?? "");

        if (args.Data != null)
        {
            foreach (var kv in args.Data)
            {
                var token = "{" + kv.Key + "}";
                s = s.Replace(token, kv.Value?.ToString() ?? "");
            }
        }

        return s;
    }
}


public interface IPathFormatter
{
    /// <summary>Compose "dot + [index]" style paths, e.g. Customer.Address.Zip or Lines[2].Sku.</summary>
    string Append(string parent, string? memberName = null, int? index = null);
}

public sealed class DotPathFormatter : IPathFormatter
{
    public static readonly DotPathFormatter Instance = new();
    private DotPathFormatter() { }

    public string Append(string parent, string? memberName = null, int? index = null)
    {
        if (memberName is null && index is null) return parent;
        if (string.IsNullOrEmpty(parent))
        {
            if (memberName is not null)
                return index is null ? memberName : $"{memberName}[{index}]";
            return $"[{index}]";
        }

        if (memberName is not null)
            return index is null ? $"{parent}.{memberName}" : $"{parent}.{memberName}[{index}]";

        return $"{parent}[{index}]";
    }
}
internal ref struct PathBuilder
{
    private Span<char> _buffer;
    private int _len;
    private char[]? _pool;

    public PathBuilder(Span<char> initial)
    {
        _buffer = initial;
        _len = 0;
        _pool = null;
    }

    public void AppendDotMember(string name)
    {
        if (_len > 0) AppendChar('.');
        AppendString(name);
    }

    public void AppendIndex(int idx)
    {
        AppendChar('[');
        AppendString(idx.ToString());
        AppendChar(']');
    }

    public override string ToString()
    {
        return _buffer[.._len].ToString();
    }

    private void AppendChar(char c)
    {
        Ensure(1);
        _buffer[_len++] = c;
    }

    private void AppendString(string s)
    {
        Ensure(s.Length);
        s.AsSpan().CopyTo(_buffer[_len..]);
        _len += s.Length;
    }

    private void Ensure(int need)
    {
        if (_len + need <= _buffer.Length) return;
        var newLen = Math.Max(_buffer.Length * 2, _len + need);
        var arr = ArrayPool<char>.Shared.Rent(newLen);
        _buffer[.._len].CopyTo(arr);
        if (_pool is not null) ArrayPool<char>.Shared.Return(_pool);
        _pool = arr;
        _buffer = arr;
    }

    public void Dispose()
    {
        if (_pool is not null) ArrayPool<char>.Shared.Return(_pool);
        _pool = null;
    }
}

public static class RegexCache
{
    private static readonly ConcurrentDictionary<(string, int), Regex> _cache = new();

    public static Regex Get(string pattern, RegexOptions options =
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)
    {
        var key = (pattern, (int)options);
        return _cache.GetOrAdd(key, k => new Regex(pattern, options));
    }
}
public static class EnumHelpers
{
    public static bool IsDefined<TEnum>(TEnum value) where TEnum : struct, Enum
        => Enum.IsDefined(value);

    public static bool HasAllFlags<TEnum>(TEnum value, TEnum mask) where TEnum : struct, Enum
    {
        var v = Convert.ToUInt64(value);
        var m = Convert.ToUInt64(mask);
        return (v & m) == m;
    }

    public static bool HasAnyFlags<TEnum>(TEnum value, TEnum mask) where TEnum : struct, Enum
    {
        var v = Convert.ToUInt64(value);
        var m = Convert.ToUInt64(mask);
        return (v & m) != 0;
    }
}