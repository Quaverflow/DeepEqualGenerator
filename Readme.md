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

| Method              |    Equal  | Allocations |
| ------------------- | --------: | ----------: |
| **Generated**       |   0.3 µs  |       120 B |
| Handwritten (Linq)  |   2.1 µs  |      3.5 KB |
| JSON (STJ)          |  1.401 ms |      1.4 MB |
| Compare-Net-Objects |  2.099 ms |      3.4 MB |
| ObjectsComparer     | 13.527 ms |       13 MB |
| FluentAssertions    | 10.818 ms |       21 MB |

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


## 🔍 Generated Code Example

Given the graph:

```
    public enum Region { NA, EU, APAC }

    [DeepComparable(CycleTracking = false)]
    public sealed class Address
    {
        public string Line1 { get; set; } = "";
        public string City { get; set; } = "";
        public string Postcode { get; set; } = "";
        public string Country { get; set; } = "";
        public ExpandoObject Countr3y { get; set; }
    }

    [DeepComparable(CycleTracking = false)]
    public sealed class OrderLine
    {
        public string Sku { get; set; } = "";
        public int Qty { get; set; }
        public decimal LineTotal { get; set; }
    }

    [DeepComparable(CycleTracking = false)]
    public sealed class Order
    {
        public Guid Id { get; set; }
        public DateTimeOffset Created { get; set; }
        public List<OrderLine> Lines { get; set; } = new();
        public Dictionary<string, string> Meta { get; set; } = new(StringComparer.Ordinal);
    }

    [DeepComparable(CycleTracking = false)]
    public sealed class Customer
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = "";
        public Region Region { get; set; }
        public Address ShipTo { get; set; } = new();
        public List<Order> Orders { get; set; } = new();
    }

    [DeepComparable(CycleTracking = false)]
    public sealed class MidGraph
    {
        public string Title { get; set; } = "";
        public List<Customer> Customers { get; set; } = new();
        public Dictionary<string, decimal> PriceIndex { get; set; } = new(StringComparer.Ordinal);
        public object? Polymorph { get; set; }
        public IDictionary<string, object?> Extra { get; set; } = new ExpandoObject();
    }
```

The generated code is:

```
// <auto-generated/>
#pragma warning disable
using System;
using System.Collections;
using System.Collections.Generic;
using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Benchmarking
{
    public static class MidGraphDeepEqual
    {
        static MidGraphDeepEqual()
        {
            GeneratedHelperRegistry.Register<global::DeepEqual.Generator.Benchmarking.Address>((l, r, c) => AreDeepEqual__global__DeepEqual_Generator_Benchmarking_Address(l, r, c));
            GeneratedHelperRegistry.Register<global::DeepEqual.Generator.Benchmarking.Customer>((l, r, c) => AreDeepEqual__global__DeepEqual_Generator_Benchmarking_Customer(l, r, c));
            GeneratedHelperRegistry.Register<global::DeepEqual.Generator.Benchmarking.MidGraph>((l, r, c) => AreDeepEqual__global__DeepEqual_Generator_Benchmarking_MidGraph(l, r, c));
            GeneratedHelperRegistry.Register<global::DeepEqual.Generator.Benchmarking.Order>((l, r, c) => AreDeepEqual__global__DeepEqual_Generator_Benchmarking_Order(l, r, c));
            GeneratedHelperRegistry.Register<global::DeepEqual.Generator.Benchmarking.OrderLine>((l, r, c) => AreDeepEqual__global__DeepEqual_Generator_Benchmarking_OrderLine(l, r, c));
        }

        public static bool AreDeepEqual(global::DeepEqual.Generator.Benchmarking.MidGraph? left, global::DeepEqual.Generator.Benchmarking.MidGraph? right)
        {
            if (object.ReferenceEquals(left, right))
            {
                return true;
            }
            if (left is null || right is null)
            {
                return false;
            }
            var context = DeepEqual.Generator.Shared.ComparisonContext.NoTracking;
            return AreDeepEqual__global__DeepEqual_Generator_Benchmarking_MidGraph(left, right, context);
        }

        public static bool AreDeepEqual(global::DeepEqual.Generator.Benchmarking.MidGraph? left, global::DeepEqual.Generator.Benchmarking.MidGraph? right, DeepEqual.Generator.Shared.ComparisonOptions options)
        {
            if (object.ReferenceEquals(left, right))
            {
                return true;
            }
            if (left is null || right is null)
            {
                return false;
            }
            var context = new DeepEqual.Generator.Shared.ComparisonContext(options);
            return AreDeepEqual__global__DeepEqual_Generator_Benchmarking_MidGraph(left, right, context);
        }

        public static bool AreDeepEqual(global::DeepEqual.Generator.Benchmarking.MidGraph? left, global::DeepEqual.Generator.Benchmarking.MidGraph? right, DeepEqual.Generator.Shared.ComparisonContext context)
        {
            if (object.ReferenceEquals(left, right))
            {
                return true;
            }
            if (left is null || right is null)
            {
                return false;
            }
            return AreDeepEqual__global__DeepEqual_Generator_Benchmarking_MidGraph(left, right, context);
        }

        private static bool AreDeepEqual__global__DeepEqual_Generator_Benchmarking_Address(global::DeepEqual.Generator.Benchmarking.Address left, global::DeepEqual.Generator.Benchmarking.Address right, DeepEqual.Generator.Shared.ComparisonContext context)
        {
            if (object.ReferenceEquals(left, right))
            {
                return true;
            }
            if (left is null || right is null)
            {
                return false;
            }
            if (!object.ReferenceEquals(left.City, right.City))
            {
                if (left.City is null || right.City is null)
                {
                    return false;
                }
            }
            if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualStrings(left.City, right.City, context))
            {
                return false;
            }

            if (!object.ReferenceEquals(left.Country, right.Country))
            {
                if (left.Country is null || right.Country is null)
                {
                    return false;
                }
            }
            if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualStrings(left.Country, right.Country, context))
            {
                return false;
            }

            if (!object.ReferenceEquals(left.Line1, right.Line1))
            {
                if (left.Line1 is null || right.Line1 is null)
                {
                    return false;
                }
            }
            if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualStrings(left.Line1, right.Line1, context))
            {
                return false;
            }

            if (!object.ReferenceEquals(left.Postcode, right.Postcode))
            {
                if (left.Postcode is null || right.Postcode is null)
                {
                    return false;
                }
            }
            if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualStrings(left.Postcode, right.Postcode, context))
            {
                return false;
            }

            return true;
        }

        private static bool AreDeepEqual__global__DeepEqual_Generator_Benchmarking_Customer(global::DeepEqual.Generator.Benchmarking.Customer left, global::DeepEqual.Generator.Benchmarking.Customer right, DeepEqual.Generator.Shared.ComparisonContext context)
        {
            if (object.ReferenceEquals(left, right))
            {
                return true;
            }
            if (left is null || right is null)
            {
                return false;
            }
            if (!object.ReferenceEquals(left.FullName, right.FullName))
            {
                if (left.FullName is null || right.FullName is null)
                {
                    return false;
                }
            }
            if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualStrings(left.FullName, right.FullName, context))
            {
                return false;
            }

            if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualEnum<global::DeepEqual.Generator.Benchmarking.Region>(left.Region, right.Region))
            {
                return false;
            }

            if (!left.Id.Equals(right.Id))
            {
                return false;
            }

            if (!object.ReferenceEquals(left.ShipTo, right.ShipTo))
            {
                if (left.ShipTo is null || right.ShipTo is null)
                {
                    return false;
                }
            }
            if (!(DeepEqual.Generator.Shared.ComparisonHelpers.DeepComparePolymorphic<global::DeepEqual.Generator.Benchmarking.Address>(left.ShipTo, right.ShipTo, context)))
            {
                return false;
            }

            if (!object.ReferenceEquals(left.Orders, right.Orders))
            {
                if (left.Orders is null || right.Orders is null)
                {
                    return false;
                }
            }
            if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualSequencesOrdered<global::DeepEqual.Generator.Benchmarking.Order, __Cmp__global__DeepEqual_Generator_Benchmarking_Order__M_global__DeepEqual_Generator_Benchmarking_Customer_Orders>(left.Orders as IEnumerable<global::DeepEqual.Generator.Benchmarking.Order>, right.Orders as IEnumerable<global::DeepEqual.Generator.Benchmarking.Order>, new __Cmp__global__DeepEqual_Generator_Benchmarking_Order__M_global__DeepEqual_Generator_Benchmarking_Customer_Orders(), context))
            {
                return false;
            }

            return true;
        }

        private static bool AreDeepEqual__global__DeepEqual_Generator_Benchmarking_MidGraph(global::DeepEqual.Generator.Benchmarking.MidGraph left, global::DeepEqual.Generator.Benchmarking.MidGraph right, DeepEqual.Generator.Shared.ComparisonContext context)
        {
            if (object.ReferenceEquals(left, right))
            {
                return true;
            }
            if (left is null || right is null)
            {
                return false;
            }
            if (!object.ReferenceEquals(left.Title, right.Title))
            {
                if (left.Title is null || right.Title is null)
                {
                    return false;
                }
            }
            if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualStrings(left.Title, right.Title, context))
            {
                return false;
            }

            if (!object.ReferenceEquals(left.Polymorph, right.Polymorph))
            {
                if (left.Polymorph is null || right.Polymorph is null)
                {
                    return false;
                }
            }
            if (!DeepEqual.Generator.Shared.DynamicDeepComparer.AreEqualDynamic(left.Polymorph, right.Polymorph, context))
            {
                return false;
            }

            if (!object.ReferenceEquals(left.Extra, right.Extra))
            {
                if (left.Extra is null || right.Extra is null)
                {
                    return false;
                }
            }
            var __roMapA_MidGraph_Extra = left.Extra as global::System.Collections.Generic.IDictionary<string, object>;
            var __roMapB_MidGraph_Extra = right.Extra as global::System.Collections.Generic.IDictionary<string, object>;
            if (__roMapA_MidGraph_Extra is not null && __roMapB_MidGraph_Extra is not null)
            {
                if (__roMapA_MidGraph_Extra.Count != __roMapB_MidGraph_Extra.Count)
                {
                    return false;
                }
                foreach (var __kv in __roMapA_MidGraph_Extra)
                {
                    if (!__roMapB_MidGraph_Extra.TryGetValue(__kv.Key, out var __rv))
                    {
                        return false;
                    }
                    if (!(DeepEqual.Generator.Shared.DynamicDeepComparer.AreEqualDynamic(__kv.Value, __rv, context)))
                    {
                        return false;
                    }
                }
                return true;
            }
            var __rwMapA_MidGraph_Extra = left.Extra as global::System.Collections.Generic.IDictionary<string, object>;
            var __rwMapB_MidGraph_Extra = right.Extra as global::System.Collections.Generic.IDictionary<string, object>;
            if (__rwMapA_MidGraph_Extra is not null && __rwMapB_MidGraph_Extra is not null)
            {
                if (__rwMapA_MidGraph_Extra.Count != __rwMapB_MidGraph_Extra.Count)
                {
                    return false;
                }
                foreach (var __kv in __rwMapA_MidGraph_Extra)
                {
                    if (!__rwMapB_MidGraph_Extra.TryGetValue(__kv.Key, out var __rv))
                    {
                        return false;
                    }
                    if (!(DeepEqual.Generator.Shared.DynamicDeepComparer.AreEqualDynamic(__kv.Value, __rv, context)))
                    {
                        return false;
                    }
                }
                return true;
            }
            if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDictionariesAny<string, object, __Cmp__object__M_global__DeepEqual_Generator_Benchmarking_MidGraph_Extra_Val>(left.Extra, right.Extra, new __Cmp__object__M_global__DeepEqual_Generator_Benchmarking_MidGraph_Extra_Val(), context))
            {
                return false;
            }

            if (!object.ReferenceEquals(left.PriceIndex, right.PriceIndex))
            {
                if (left.PriceIndex is null || right.PriceIndex is null)
                {
                    return false;
                }
            }
            var __roMapA_MidGraph_PriceIndex = left.PriceIndex as global::System.Collections.Generic.IDictionary<string, decimal>;
            var __roMapB_MidGraph_PriceIndex = right.PriceIndex as global::System.Collections.Generic.IDictionary<string, decimal>;
            if (__roMapA_MidGraph_PriceIndex is not null && __roMapB_MidGraph_PriceIndex is not null)
            {
                if (__roMapA_MidGraph_PriceIndex.Count != __roMapB_MidGraph_PriceIndex.Count)
                {
                    return false;
                }
                foreach (var __kv in __roMapA_MidGraph_PriceIndex)
                {
                    if (!__roMapB_MidGraph_PriceIndex.TryGetValue(__kv.Key, out var __rv))
                    {
                        return false;
                    }
                    if (!(DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDecimal(__kv.Value, __rv, context)))
                    {
                        return false;
                    }
                }
                return true;
            }
            var __rwMapA_MidGraph_PriceIndex = left.PriceIndex as global::System.Collections.Generic.IDictionary<string, decimal>;
            var __rwMapB_MidGraph_PriceIndex = right.PriceIndex as global::System.Collections.Generic.IDictionary<string, decimal>;
            if (__rwMapA_MidGraph_PriceIndex is not null && __rwMapB_MidGraph_PriceIndex is not null)
            {
                if (__rwMapA_MidGraph_PriceIndex.Count != __rwMapB_MidGraph_PriceIndex.Count)
                {
                    return false;
                }
                foreach (var __kv in __rwMapA_MidGraph_PriceIndex)
                {
                    if (!__rwMapB_MidGraph_PriceIndex.TryGetValue(__kv.Key, out var __rv))
                    {
                        return false;
                    }
                    if (!(DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDecimal(__kv.Value, __rv, context)))
                    {
                        return false;
                    }
                }
                return true;
            }
            if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDictionariesAny<string, decimal, __Cmp__decimal__M_global__DeepEqual_Generator_Benchmarking_MidGraph_PriceIndex_Val>(left.PriceIndex, right.PriceIndex, new __Cmp__decimal__M_global__DeepEqual_Generator_Benchmarking_MidGraph_PriceIndex_Val(), context))
            {
                return false;
            }

            if (!object.ReferenceEquals(left.Customers, right.Customers))
            {
                if (left.Customers is null || right.Customers is null)
                {
                    return false;
                }
            }
            if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualSequencesOrdered<global::DeepEqual.Generator.Benchmarking.Customer, __Cmp__global__DeepEqual_Generator_Benchmarking_Customer__M_global__DeepEqual_Generator_Benchmarking_MidGraph_Customers>(left.Customers as IEnumerable<global::DeepEqual.Generator.Benchmarking.Customer>, right.Customers as IEnumerable<global::DeepEqual.Generator.Benchmarking.Customer>, new __Cmp__global__DeepEqual_Generator_Benchmarking_Customer__M_global__DeepEqual_Generator_Benchmarking_MidGraph_Customers(), context))
            {
                return false;
            }

            return true;
        }

        private static bool AreDeepEqual__global__DeepEqual_Generator_Benchmarking_Order(global::DeepEqual.Generator.Benchmarking.Order left, global::DeepEqual.Generator.Benchmarking.Order right, DeepEqual.Generator.Shared.ComparisonContext context)
        {
            if (object.ReferenceEquals(left, right))
            {
                return true;
            }
            if (left is null || right is null)
            {
                return false;
            }
            if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDateTimeOffset(left.Created, right.Created))
            {
                return false;
            }

            if (!left.Id.Equals(right.Id))
            {
                return false;
            }

            if (!object.ReferenceEquals(left.Meta, right.Meta))
            {
                if (left.Meta is null || right.Meta is null)
                {
                    return false;
                }
            }
            var __roMapA_Order_Meta = left.Meta as global::System.Collections.Generic.IDictionary<string, string>;
            var __roMapB_Order_Meta = right.Meta as global::System.Collections.Generic.IDictionary<string, string>;
            if (__roMapA_Order_Meta is not null && __roMapB_Order_Meta is not null)
            {
                if (__roMapA_Order_Meta.Count != __roMapB_Order_Meta.Count)
                {
                    return false;
                }
                foreach (var __kv in __roMapA_Order_Meta)
                {
                    if (!__roMapB_Order_Meta.TryGetValue(__kv.Key, out var __rv))
                    {
                        return false;
                    }
                    if (!(DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualStrings(__kv.Value, __rv, context)))
                    {
                        return false;
                    }
                }
                return true;
            }
            var __rwMapA_Order_Meta = left.Meta as global::System.Collections.Generic.IDictionary<string, string>;
            var __rwMapB_Order_Meta = right.Meta as global::System.Collections.Generic.IDictionary<string, string>;
            if (__rwMapA_Order_Meta is not null && __rwMapB_Order_Meta is not null)
            {
                if (__rwMapA_Order_Meta.Count != __rwMapB_Order_Meta.Count)
                {
                    return false;
                }
                foreach (var __kv in __rwMapA_Order_Meta)
                {
                    if (!__rwMapB_Order_Meta.TryGetValue(__kv.Key, out var __rv))
                    {
                        return false;
                    }
                    if (!(DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualStrings(__kv.Value, __rv, context)))
                    {
                        return false;
                    }
                }
                return true;
            }
            if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDictionariesAny<string, string, __Cmp__string__M_global__DeepEqual_Generator_Benchmarking_Order_Meta_Val>(left.Meta, right.Meta, new __Cmp__string__M_global__DeepEqual_Generator_Benchmarking_Order_Meta_Val(), context))
            {
                return false;
            }

            if (!object.ReferenceEquals(left.Lines, right.Lines))
            {
                if (left.Lines is null || right.Lines is null)
                {
                    return false;
                }
            }
            if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualSequencesOrdered<global::DeepEqual.Generator.Benchmarking.OrderLine, __Cmp__global__DeepEqual_Generator_Benchmarking_OrderLine__M_global__DeepEqual_Generator_Benchmarking_Order_Lines>(left.Lines as IEnumerable<global::DeepEqual.Generator.Benchmarking.OrderLine>, right.Lines as IEnumerable<global::DeepEqual.Generator.Benchmarking.OrderLine>, new __Cmp__global__DeepEqual_Generator_Benchmarking_OrderLine__M_global__DeepEqual_Generator_Benchmarking_Order_Lines(), context))
            {
                return false;
            }

            return true;
        }

        private static bool AreDeepEqual__global__DeepEqual_Generator_Benchmarking_OrderLine(global::DeepEqual.Generator.Benchmarking.OrderLine left, global::DeepEqual.Generator.Benchmarking.OrderLine right, DeepEqual.Generator.Shared.ComparisonContext context)
        {
            if (object.ReferenceEquals(left, right))
            {
                return true;
            }
            if (left is null || right is null)
            {
                return false;
            }
            if (!object.ReferenceEquals(left.Sku, right.Sku))
            {
                if (left.Sku is null || right.Sku is null)
                {
                    return false;
                }
            }
            if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualStrings(left.Sku, right.Sku, context))
            {
                return false;
            }

            if (!DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDecimal(left.LineTotal, right.LineTotal, context))
            {
                return false;
            }

            if (!left.Qty.Equals(right.Qty))
            {
                return false;
            }

            return true;
        }

        private readonly struct __Cmp__global__DeepEqual_Generator_Benchmarking_Order__M_global__DeepEqual_Generator_Benchmarking_Customer_Orders : DeepEqual.Generator.Shared.IElementComparer<global::DeepEqual.Generator.Benchmarking.Order>
        {
            public __Cmp__global__DeepEqual_Generator_Benchmarking_Order__M_global__DeepEqual_Generator_Benchmarking_Customer_Orders(System.Collections.Generic.IEqualityComparer<global::DeepEqual.Generator.Benchmarking.Order> _ = null) {}
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
            public bool Invoke(global::DeepEqual.Generator.Benchmarking.Order l, global::DeepEqual.Generator.Benchmarking.Order r, DeepEqual.Generator.Shared.ComparisonContext c)
            {
                return DeepEqual.Generator.Shared.ComparisonHelpers.DeepComparePolymorphic<global::DeepEqual.Generator.Benchmarking.Order>(l, r, c);
            }
        }

        private readonly struct __Cmp__object__M_global__DeepEqual_Generator_Benchmarking_MidGraph_Extra_Val : DeepEqual.Generator.Shared.IElementComparer<object>
        {
            public __Cmp__object__M_global__DeepEqual_Generator_Benchmarking_MidGraph_Extra_Val(System.Collections.Generic.IEqualityComparer<object> _ = null) {}
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
            public bool Invoke(object l, object r, DeepEqual.Generator.Shared.ComparisonContext c)
            {
                return DeepEqual.Generator.Shared.DynamicDeepComparer.AreEqualDynamic(l, r, c);
            }
        }

        private readonly struct __Cmp__decimal__M_global__DeepEqual_Generator_Benchmarking_MidGraph_PriceIndex_Val : DeepEqual.Generator.Shared.IElementComparer<decimal>
        {
            public __Cmp__decimal__M_global__DeepEqual_Generator_Benchmarking_MidGraph_PriceIndex_Val(System.Collections.Generic.IEqualityComparer<decimal> _ = null) {}
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
            public bool Invoke(decimal l, decimal r, DeepEqual.Generator.Shared.ComparisonContext c)
            {
                return DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualDecimal(l, r, c);
            }
        }

        private readonly struct __Cmp__global__DeepEqual_Generator_Benchmarking_Customer__M_global__DeepEqual_Generator_Benchmarking_MidGraph_Customers : DeepEqual.Generator.Shared.IElementComparer<global::DeepEqual.Generator.Benchmarking.Customer>
        {
            public __Cmp__global__DeepEqual_Generator_Benchmarking_Customer__M_global__DeepEqual_Generator_Benchmarking_MidGraph_Customers(System.Collections.Generic.IEqualityComparer<global::DeepEqual.Generator.Benchmarking.Customer> _ = null) {}
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
            public bool Invoke(global::DeepEqual.Generator.Benchmarking.Customer l, global::DeepEqual.Generator.Benchmarking.Customer r, DeepEqual.Generator.Shared.ComparisonContext c)
            {
                return DeepEqual.Generator.Shared.ComparisonHelpers.DeepComparePolymorphic<global::DeepEqual.Generator.Benchmarking.Customer>(l, r, c);
            }
        }

        private readonly struct __Cmp__string__M_global__DeepEqual_Generator_Benchmarking_Order_Meta_Val : DeepEqual.Generator.Shared.IElementComparer<string>
        {
            public __Cmp__string__M_global__DeepEqual_Generator_Benchmarking_Order_Meta_Val(System.Collections.Generic.IEqualityComparer<string> _ = null) {}
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
            public bool Invoke(string l, string r, DeepEqual.Generator.Shared.ComparisonContext c)
            {
                return DeepEqual.Generator.Shared.ComparisonHelpers.AreEqualStrings(l, r, c);
            }
        }

        private readonly struct __Cmp__global__DeepEqual_Generator_Benchmarking_OrderLine__M_global__DeepEqual_Generator_Benchmarking_Order_Lines : DeepEqual.Generator.Shared.IElementComparer<global::DeepEqual.Generator.Benchmarking.OrderLine>
        {
            public __Cmp__global__DeepEqual_Generator_Benchmarking_OrderLine__M_global__DeepEqual_Generator_Benchmarking_Order_Lines(System.Collections.Generic.IEqualityComparer<global::DeepEqual.Generator.Benchmarking.OrderLine> _ = null) {}
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
            public bool Invoke(global::DeepEqual.Generator.Benchmarking.OrderLine l, global::DeepEqual.Generator.Benchmarking.OrderLine r, DeepEqual.Generator.Shared.ComparisonContext c)
            {
                return DeepEqual.Generator.Shared.ComparisonHelpers.DeepComparePolymorphic<global::DeepEqual.Generator.Benchmarking.OrderLine>(l, r, c);
            }
        }

    }
    static class __MidGraphDeepEqual_ModuleInit
    {
        [System.Runtime.CompilerServices.ModuleInitializer]
        internal static void Init()
        {
            _ = typeof(MidGraphDeepEqual);
        }
    }
}

```
