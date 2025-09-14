# DeepEqual.Generator

A C# source generator that creates **super-fast, allocation-free deep equality comparers** for your classes and structs.

Stop writing `Equals` by hand. Stop serializing to JSON just to compare objects.
Just add an attribute, and you get a complete deep comparer generated at compile time.

---

## ✨ Why use this?

* **Simple** – annotate your models, and you’re done.
* **Correct** – all members are compared deeply, even nested collections, dictionaries, and objects.
* **Fast** – 3–4× faster than careful manual comparers, 10–13× fewer allocations.
* **Flexible** – opt-in options for unordered collections, numeric tolerances, string case sensitivity, custom comparers.
* **Safe** – no runtime reflection, no “oops forgot a field” bugs.

---

## 🚀 Getting started

Install the NuGet package:

```powershell
dotnet add package DeepEqual.Generator
```

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

## 🔍 What gets compared?

* **Primitives & enums** – by value.
* **Strings** – configurable (ordinal, ignore case, culture aware).
* **DateTime / DateTimeOffset** – strict: both the `Kind`/`Offset` and `Ticks` must match.
* **Guid, TimeSpan, DateOnly, TimeOnly** – by value.
* **Nullable<T>** – compared only if both have a value.
* **Arrays & collections** – element by element.
* **Dictionaries** – key/value pairs deeply compared.
* **Jagged & multidimensional arrays** – handled correctly.
* **Object** properties – compared polymorphically if the runtime type has a generated helper.
* **Dynamics / ExpandoObject** – compared as dictionaries of keys/values.
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

### On individual members or types

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

**Defaults:**

* `Kind` → **Deep**
* `OrderInsensitive` → **false**
* `Members` → empty (all members included)
* `IgnoreMembers` → empty
* `ComparerType` → null (no custom comparer)
* `KeyMembers` → empty (no key-based matching)

---

## 📚 Ordered vs Unordered collections

By default, **collections are compared in order**. That means element by element, position matters:

```csharp
var a = new[] { 1, 2, 3 };
var b = new[] { 3, 2, 1 };
// Equal? false (different order)
```

If you want a collection to be compared ignoring order (treating it like a bag or set), you can:

* Enable it globally for the type:

```csharp
[DeepComparable(OrderInsensitiveCollections = true)]
public sealed class OrderBatch { public List<int> Ids { get; set; } = new(); }
```

* Or mark specific members:

```csharp
public sealed class TagSet
{
    [DeepCompare(OrderInsensitive = true)]
    public List<string> Tags { get; set; } = new();
}
```

* Or let the element type decide:

```csharp
[DeepComparable(OrderInsensitiveCollections = true)]
public sealed class Tag { public string Name { get; set; } = ""; }

public sealed class TagHolder { public List<Tag> Tags { get; set; } = new(); }
```

### Key-based matching

For unordered collections of objects, you can mark certain properties as keys:

```csharp
[DeepCompare(KeyMembers = new[] { "Id" })]
public sealed class Customer { public string Id { get; set; } = ""; public string Name { get; set; } = ""; }
```

Now two `List<Customer>` collections are equal if they contain the same customers by `Id`, regardless of order.

---

## ⚡ Numeric & string options

```csharp
var opts = new ComparisonOptions
{
    FloatEpsilon = 0f,          // default
    DoubleEpsilon = 0d,         // default
    DecimalEpsilon = 0m,        // default
    TreatNaNEqual = false,      // default
    StringComparison = StringComparison.Ordinal // default
};
```

Defaults are strict equality for numbers and case-sensitive ordinal for strings.

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

NodeDeepEqual.AreDeepEqual(a, b); // true, no stack overflow
```

---

## 📊 Benchmarks

On a large object graph (hundreds of products, customers, and nested orders):

| Case             | Manual comparer | Generated comparer |
| ---------------- | --------------: | -----------------: |
| Equal            |        1,700 ns |         **410 ns** |
| Not equal deep   |        1,300 ns |         **390 ns** |
| Allocations (eq) |       \~5,500 B |          **424 B** |

Generated code is consistently **3–4× faster** and **10–13× less allocation** than handwritten comparers.
For shallow mismatches at the root, manual can be a few nanoseconds faster — but those cases are trivial.

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

---
