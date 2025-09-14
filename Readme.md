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
    FloatEpsilon = 0f,
    DoubleEpsilon = 0d,
    DecimalEpsilon = 0m,
    TreatNaNEqual = false,
    StringComparison = StringComparison.Ordinal
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

NodeDeepEqual.AreDeepEqual(a, b); 
```

---

## 📊 Benchmarks

This document summarizes benchmark results comparing different approaches to deep object equality in .NET.  
Our **source-generated comparer** is listed first, followed by manual implementations and popular libraries.

---

## 🏆 Generated Comparer (this project)
| Scenario   | Time (ms) | Allocations |
|------------|----------:|------------:|
| Equal      | **0.0003** | **120 B**   |
| NotEqual (Shallow) | 0.000004 | 0 |
| NotEqual (Deep)    | **0.0003** | **120 B** |

✅ Fastest overall across equality and deep inequality checks  
✅ Minimal allocations  
✅ Beats manual implementations by **5–7×** on deep checks  
✅ Outperforms libraries and JSON-based approaches by **orders of magnitude**

⚠️ **Note**: For **shallow inequality** (quick “not equal” exit), handwritten code is still faster (fractions of a microsecond), but the difference is negligible in practice.

---

## ✍️ Manual Implementations
### Hand-written (non-LINQ)
| Scenario   | Time (ms) | Allocations |
|------------|----------:|------------:|
| Equal      | 0.0016 | 3,264 B |
| NotEqual (Shallow) | **0.000001** | 0 |
| NotEqual (Deep)    | 0.0016 | 3,264 B |

### Hand-written (LINQ style)
| Scenario   | Time (ms) | Allocations |
|------------|----------:|------------:|
| Equal      | 0.0021 | 3,504 B |
| NotEqual (Shallow) | **0.000001** | 0 |
| NotEqual (Deep)    | 0.0021 | 3,504 B |

⚠️ Slower than generated by **5–8×** in deep checks, with significantly more allocations.  
⚠️ Shallow inequality is slightly faster than generated, but only by fractions of a microsecond.

---

## 📦 JSON Serialization Approaches
### Newtonsoft.Json
| Scenario   | Time (ms) | Allocations |
|------------|----------:|------------:|
| Equal      | 1,613.124 | 2,035,568 B |
| NotEqual (Shallow) | 1,477.597 | 2,032,768 B |
| NotEqual (Deep)    | 1,664.072 | 2,035,568 B |

### System.Text.Json
| Scenario   | Time (ms) | Allocations |
|------------|----------:|------------:|
| Equal      | 1,401.291 | 1,398,629 B |
| NotEqual (Shallow) | 752.367 | 428,893 B |
| NotEqual (Deep)    | 1,385.706 | 1,368,765 B |

⚠️ Thousands of times slower than generated/manual.  
⚠️ Huge allocations (MBs per comparison).  
❌ Only useful for debugging or one-off checks, not performance-critical paths.

---

## 🔍 Library Comparers
### Compare-Net-Objects
| Scenario   | Time (ms) | Allocations |
|------------|----------:|------------:|
| Equal      | 2,099.460 | 3,418,352 B |
| NotEqual (Shallow) | 0.002 | 4,728 B |
| NotEqual (Deep)    | 2,060.454 | 3,352,279 B |

### ObjectsComparer
| Scenario   | Time (ms) | Allocations |
|------------|----------:|------------:|
| Equal      | 13,526.608 | 13,932,553 B |
| NotEqual (Shallow) | 0.002 | 2,208 B |
| NotEqual (Deep)    | 12,964.030 | 13,552,951 B |

### FluentAssertions
| Scenario   | Time (ms) | Allocations |
|------------|----------:|------------:|
| Equal      | 10,817.864 | 21,793,862 B |
| NotEqual (Shallow) | 11,609.765 | 21,891,734 B |
| NotEqual (Deep)    | 11,488.218 | 21,921,875 B |

⚠️ **10–50 seconds per 1,000 calls**.  
⚠️ Allocations in **tens of MBs**.  
✅ Great for **unit tests** (readability), but unsuitable for production performance.

---

## 📊 Takeaways
- **Generated comparer is the clear winner**:  
  - Sub-millisecond performance for deep equality  
  - Near-zero allocations  
  - Outperforms manual and library approaches by wide margins
- Manual comparers are OK for small/shallow checks, and win **slightly** on trivial “not equal” cases — but the difference is negligible compared to their cost in deep checks.
- JSON and library-based solutions are **magnitudes slower** and consume massive memory.
- **FluentAssertions / ObjectsComparer / Compare-Net-Objects** are best kept for **testing** and **diagnostics**, not runtime paths.

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
