# DeepEqual.Generator

⚡ **High-performance, zero-boilerplate deep equality for .NET**  
via a Roslyn incremental source generator.

[![NuGet](https://img.shields.io/nuget/v/DeepEqual.Generator.svg)](https://www.nuget.org/packages/DeepEqual.Generator)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

---

## ✨ Features

- 🔍 **True deep equality**: Compares entire object graphs recursively.
- 🏷️ **Opt-in root marker**: Only mark your root models with `[DeepComparable]`; the generator builds helpers for the entire reachable graph automatically.
- 🛑 **Cycle-safe**: Detects and avoids infinite loops in reference graphs.
- 💪 **Struct-aware**: Deep compares nested structs and `Nullable<T>`.
- 📦 **Collections supported**: Arrays, `IEnumerable<T>`, `List<T>`, `Dictionary<TKey,TValue>`.
- ⚙️ **Configurable**: Use `[DeepCompare]` to override behavior per member or type.
- 🧩 **Schema overrides**: Restrict or ignore specific members at the type level.
- 🚀 **Blazing fast**: ~50× faster than reflection-based libraries (see benchmarks below).
- 🧑‍💻 **Readable generated code**: Easy to inspect and debug.
- 🛠️ **No runtime reflection**: Everything is compile-time generated.

---

## 📦 Installation

Add the package to your project:

```bash
dotnet add package DeepEqual.Generator
```

Or reference the project directly in your solution with:

```xml
<ProjectReference Include="..\DeepEqual.Generator\DeepEqual.Generator.csproj" 
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

Also add a reference to the shared runtime helpers:

```xml
<ProjectReference Include="..\DeepEqualGenerator.Attributes\DeepEqual.Generator.Shared.csproj" />
```

---

## 🏷️ Usage

### Mark a root

Annotate a root type with `[DeepComparable]`.  
The generator emits a `*DeepEqual` class with a static method `AreDeepEqual`.

```csharp
using DeepEqual.Generator.Shared;

[DeepComparable]
public partial class Customer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public List<Order> Orders { get; set; } = new();
}

public class Order
{
    public Guid Id { get; set; }
    public DateTimeOffset Created { get; set; }
    public List<OrderLine> Lines { get; set; } = new();
}

public class OrderLine
{
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
```

### Compare instances

```csharp
var a = new Customer { Id = Guid.NewGuid(), Name = "Alice" };
var b = new Customer { Id = a.Id, Name = "Alice" };

bool equal = CustomerDeepEqual.AreDeepEqual(a, b);
// true
```

---

## ⚙️ Customization

### Per-member overrides

```csharp
public class Product
{
    [DeepCompare(Kind = CompareKind.Skip)]
    public string? DebugNotes { get; set; } 
    [DeepCompare(Kind = CompareKind.Reference)]
    public byte[]? Blob { get; set; } 
    [DeepCompare(Kind = CompareKind.Shallow)]
    public Uri? Url { get; set; } }
```

### Collection order

- Default: **ordered** (`List<T>`, arrays, `IEnumerable<T>`)
- Override per member:

```csharp
[DeepCompare(OrderInsensitive = true)]
public List<string> Tags { get; set; } = new(); ```

- Or set globally at the root:

```csharp
[DeepComparable(OrderInsensitiveCollections = true)]
public partial class Root { ... }
```

### Type-level schema

Define identity with `[DeepCompare]` on the type itself:

```csharp
[DeepCompare(Members = new[] { "Sku", "Price" })]
public class Product
{
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";     public decimal Price { get; set; }
}
```

Or ignore fields:

```csharp
[DeepCompare(IgnoreMembers = new[] { "Z" })]
public class Sample
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; } }
```

---

## 🛑 Cycle safety

Cycles in reference graphs are automatically tracked and skipped:

```csharp
var a = new Node { Name = "root" };
a.Child = a; 
var b = new Node { Name = "root" };
b.Child = b;

NodeDeepEqual.AreDeepEqual(a, b); ```

---

## 🔬 Benchmarks

Environment: .NET 8.0, Intel i7, Windows 11, BenchmarkDotNet 0.13.12  
Graph: ~200 products, ~200 customers, nested orders/lines.

| Method                      | Mean      | Allocated |
|-----------------------------|----------:|----------:|
| **Generated_Equal**         | 215 µs    | 108 KB    |
| **Generated_NotEqual_Deep** | 196 µs    |  98 KB    |
| Compare-NET-Objects (equal) | 10,900 µs | 17 MB     |
| JToken.DeepEquals (equal)   | 13,300 µs | 10 MB     |
| STJ Serialize+Compare       | 1,600 µs  | 900 KB    |

💡 **Result**: Your generator is ~**50× faster** and ~**150× less memory-hungry** than reflection-based libraries.

---

## 📖 FAQ

**Q: Do I need `[DeepComparable]` on every class?**  
A: No — only roots. The generator builds helpers for the entire reachable graph automatically.

**Q: What about structs?**  
A: Structs and `Nullable<T>` are fully supported. Self-typed fields (which would cause recursion) are skipped unless explicitly included.

**Q: How are `DateTime` and `DateTimeOffset` handled?**  
A: With strict equality: both `Ticks` and `Kind`/`Offset` must match.

**Q: Can I use this in production?**  
A: Yes. Generated code is plain C#, no reflection, and readable in `obj/generated`.

---

## 🛠️ Roadmap

- [ ] Diagnostic warnings for typos in `Members`/`IgnoreMembers`  
- [ ] Optional structural hash code generation (for use in dictionaries/sets)  
- [ ] Configurable fast-path for shallow mismatches  
- [ ] Interface-member deep-walk hints  

---

## 📜 License

MIT ©Quaverflow

