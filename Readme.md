# DeepEqual.Generator

A C# source generator that creates **super-fast, allocation-free deep equality comparers** for your classes and structs.

Stop writing `Equals` by hand. Stop serializing to JSON just to compare objects.
Just add an attribute, and you get a complete deep comparer generated at compile time.

---

## ✨ Why use this?

* **Simple** – annotate your models, and you’re done.
* **Flexible** – opt-in options for unordered collections, numeric tolerances, string case sensitivity, custom comparers.
* **Robust** – covers tricky cases (cycles, sets, dictionaries, polymorphism) that manual code often misses.

---

## ⚡ Why is it faster than handwritten code?

* **Compile-time codegen**: emitted at build time as optimized IL — no reflection, no runtime expression building.
* **Direct member access**: expands equality checks into straight-line code instead of generic loops or helper calls.
* **No allocations**: avoids closures, iterators, or boxing that sneak into LINQ or naive implementations.

Result: consistently **5–7× faster** than handwritten comparers, and orders of magnitude faster than JSON/library approaches.

---

## 🛡️ Why is it more robust?

* **Covers corner cases**: nested collections, dictionaries, sets, polymorphism, reference cycles.
* **Deterministic**: guarantees the same behavior regardless of field order or shape.
* **Safer than manual**: no risk of forgetting a property or comparing the wrong shape.

In short: you get **the speed of hand-tuned code**, but with **the coverage of a well-tested library** — and without runtime overhead.

---

## 📦 Installation

You need **two packages**:

```powershell
dotnet add package DeepEqual.Generator.Shared
dotnet add package DeepEqual.Generator
```

* **Shared** → contains runtime comparers and attributes.
* **Generator** → analyzer that emits the equality code at compile time.

If you install only the generator, builds will fail because the generated code depends on the runtime package.

---

## 🚀 Quick start

Annotate your type:

```csharp
using DeepEqual.Generator.Shared;

[DeepComparable]
public sealed class Person
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
}
```

At compile time, a static helper is generated:

```csharp
PersonDeepEqual.AreDeepEqual(personA, personB);
```

---

## 🔍 Supported comparisons

* **Primitives & enums** – by value.
* **Strings** – configurable (ordinal, ignore case, culture aware).
* **DateTime / DateTimeOffset** – strict (both `Kind`/`Offset` and `Ticks` must match).
* **Guid, TimeSpan, DateOnly, TimeOnly** – by value.
* **Nullable<T>** – compared only if both have a value.
* **Arrays & collections** – element by element.
* **Dictionaries** – key/value pairs deeply compared.
* **Jagged & multidimensional arrays** – handled correctly.
* **Object** properties – compared polymorphically if the runtime type has a generated helper.
* **Dynamics / ExpandoObject** – compared as dictionaries.
* **Cycles** – supported (can be turned off if you know your graph has no cycles).

---

## 🎛 Options

### On the root type

```csharp
[DeepComparable(OrderInsensitiveCollections = true, IncludeInternals = true, IncludeBaseMembers = true)]
public sealed class Order { … }
```

**Defaults:**

* `OrderInsensitiveCollections` → **false**
* `IncludeInternals` → **false**
* `IncludeBaseMembers` → **true**
* `CycleTracking` → **true**

### On individual members

```csharp
public sealed class Person
{
    [DeepCompare(Kind = CompareKind.Shallow)]
    public Address? Home { get; set; }

    [DeepCompare(OrderInsensitive = true)]
    public List<string>? Tags { get; set; }

    [DeepCompare(IgnoreMembers = new[] { "CreatedAt", "UpdatedAt" })]
    public AuditInfo Info { get; set; } = new();
}
```

---

## 📚 Ordered vs unordered collections

By default, collections are compared **in order**. If you want them compared ignoring order (like sets), you can:

* Enable globally:

```csharp
[DeepComparable(OrderInsensitiveCollections = true)]
public sealed class OrderBatch
{
    public List<int> Ids { get; set; } = new();
}
```

* On specific members:

```csharp
public sealed class TagSet
{
    [DeepCompare(OrderInsensitive = true)]
    public List<string> Tags { get; set; } = new();
}
```

* Or use **key-based matching**:

```csharp
[DeepCompare(KeyMembers = new[] { "Id" })]
public sealed class Customer
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}
```

---

## ⚡ Numeric & string options

```csharp
var opts = new ComparisonOptions
{
    FloatEpsilon = 0f,
    DoubleEpsilon = 0d,
    DecimalEpsilon = 0m,
    TreatNaNEqual = false,
    StringComparison = StringComparison.Ordinal
};
```

Defaults: strict equality for numbers and case-sensitive ordinal for strings.

---

## 🌀 Cycles

Cyclic graphs are handled safely:

```csharp
[DeepComparable]
public sealed class Node
{
    public string Id { get; set; } = "";
    public Node? Next { get; set; }
}

var a = new Node { Id = "a" };
var b = new Node { Id = "a" };
a.Next = a;
b.Next = b;

NodeDeepEqual.AreDeepEqual(a, b);
```

---

## 📊 Benchmarks

The generated comparer outperforms handwritten, JSON, and popular libraries by a wide margin:

| Method              |    Equal | Allocations |
| ------------------- | -------: | ----------: |
| **Generated**       |   0.3 µs |       120 B |
| Handwritten (Linq)  |   2.1 µs |      3.5 KB |
| JSON (STJ)          |  1.401 s |      1.4 MB |
| Compare-Net-Objects |  2.099 s |      3.4 MB |
| ObjectsComparer     | 13.527 s |       13 MB |
| FluentAssertions    | 10.818 s |       21 MB |

---

## ✅ When to use

* Large object graphs (domain models, caches, trees).
* Unit/integration tests where you assert deep equality.
* Regression testing with snapshot objects.
* High-throughput APIs needing object deduplication.
* Anywhere you need correctness *and* speed.

---

## 📦 Roadmap

* [x] Strict time semantics
* [x] Numeric tolerances
* [x] String comparison options
* [x] Cycle tracking
* [x] Include internals & base members
* [x] Order-insensitive collections
* [x] Key-based unordered matching
* [x] Custom comparers
* [x] Memory<T> / ReadOnlyMemory<T>
* [x] Benchmarks & tests
* [ ] Analyzer diagnostics
* [ ] Developer guide & samples site


update the benchmark to use the unit next to the number and increase unit ns, ms, s, etc, based on the number size