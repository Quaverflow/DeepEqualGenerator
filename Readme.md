# DeepEqual Source Generator — Tiered Usage & Configuration

This guide explains the DeepEqual source generator in **three tiers of complexity**:

* **Tier 1 — Common use cases** (most developers)
* **Tier 2 — Uncommon / intermediate** (customizations)
* **Tier 3 — Specialized / expert** (advanced scenarios)

All features and configuration knobs are documented, but grouped by when you realistically need them.

---

## Tier 1 — Common use cases

For most developers, you just want deep equality, diffing, or deltas. The generator makes this very simple.

### Quick start

Annotate your model:

```csharp
[DeepComparable]
public partial class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
}
```

### Equality

```csharp
var areEqual = PersonDeepEqual.AreDeepEqual(a, b);
```

### Diff

Enable diff:

```csharp
[DeepComparable(GenerateDiff = true)]
public partial class Person { ... }
```

Usage:

```csharp
var diffs = PersonDeepOps.Diff(left, right);
```

### Delta (patching)

Enable delta:

```csharp
[DeepComparable(GenerateDelta = true)]
public partial class Person { ... }
```

Usage:

```csharp
var doc = DeltaDocument.Rent();
var writer = new DeltaWriter(doc);
PersonDeepOps.ComputeDelta(left, right, default, ref writer);
PersonDeepOps.ApplyDelta(ref target, ref new DeltaReader(doc));
```

✅ That’s it for Tier 1. You can stop here and already have powerful, high-performance equality and delta handling.

---

## Tier 2 — Uncommon / Intermediate use cases

When you need more control over how members are compared, or want runtime tweaks.

### `[DeepCompare]` (member-level overrides)

* `Kind` → `Deep` (default), `Shallow`, `Reference`, `Skip`.
* `OrderInsensitive` → ignore element order in collections.
* `ComparerType` → custom `IEqualityComparer<T>`.
* `KeyMembers` → member(s) used to match items in unordered collections.
* `DeltaShallow` / `DeltaSkip` → influence delta emission.

**Example:**

```csharp
public class Order
{
    [DeepCompare(Kind = DeepCompareKind.Shallow)]
    public byte[] RawBytes { get; set; }
}
```

### Options at runtime (`ComparisonOptions`)

```csharp
var ctx = new ComparisonContext(new ComparisonOptions
{
    StringComparison = StringComparison.OrdinalIgnoreCase,
    TreatNaNEqual = true,
    DoubleEpsilon = 1e-6
});

if (PersonDeepEqual.AreDeepEqual(a, b, ctx)) { ... }
```

Available options:

* `StringComparison`
* `TreatNaNEqual`
* `DoubleEpsilon` / `FloatEpsilon` / `DecimalEpsilon`
* `ValidateDirtyOnEmit`

### Dirty-bit tracking

```csharp
[DeltaTrack(ThreadSafe = true)]
public partial class PlayerState { ... }
```

This enables efficient change tracking for emitting small deltas.

---

## Tier 3 — Specialized / Expert use cases

When you need schema stability, cross-version transport, or custom integration.

### Attributes

* `IncludeInternals` — include internal members of the same assembly.
* `IncludeBaseMembers` — include inherited members.
* `OrderInsensitiveCollections` — collections default to unordered.
* `CycleTracking` — enable cycle detection for graphs.
* `StableMemberIndex` — control stability of member indices in deltas: `Auto`, `On`, `Off`.
* `EmitSchemaSnapshot` — emit schema snapshot code for versioning.

### Assembly-scoped attributes

* `[ExternalDeepComparable(typeof(SomeExternalType))]`
* `[ExternalDeepCompare(typeof(SomeExternalType), "Path.To.Member")]`

### Binary delta codec (`BinaryDeltaOptions`)

* `IncludeHeader` — include header with type/string tables.
* `StableTypeFingerprint` — schema fingerprint (64-bit).
* `UseTypeTable` / `UseStringTable` — optimize binary size.
* `IncludeEnumTypeIdentity` — include enum identity with values.
* Safety caps:

  * `MaxOps` (default 1M)
  * `MaxStringBytes` (default 16MB)
  * `MaxNesting` (default 256)

### Examples

```csharp
// Binary encode with schema fingerprint
var options = new BinaryDeltaOptions
{
    IncludeHeader = true,
    StableTypeFingerprint = 0xDEADBEEFCAFEBABE
};

var buffer = new ArrayBufferWriter<byte>();
doc.ToBinary(buffer, options);
var parsed = DeltaDocument.FromBinary(buffer.WrittenSpan, options);
```

### Runtime APIs (registry)

* `GeneratedHelperRegistry.RegisterComparer<T>()`
* `TryCompare`, `TryCompareSameType`
* `RegisterDelta<T>(compute, apply)`
* `ComputeDeltaSameType`, `TryApplyDeltaSameType`

### Dynamic comparer

* `DynamicDeepComparer.AreEqualDynamic(a, b, ctx)` — fallback for unknown runtime types.

---

## Summary

* **Tier 1 (Common):** `[DeepComparable]`, equality, diff, delta → easy.
* **Tier 2 (Intermediate):** member overrides, runtime options, dirty-bit tracking.
* **Tier 3 (Expert):** schema/versioning, binary codec tuning, external type overrides.

This structure lets new users succeed quickly while giving advanced teams full control when needed.
