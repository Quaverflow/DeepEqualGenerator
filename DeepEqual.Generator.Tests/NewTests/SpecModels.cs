// SPDX-License-Identifier: MIT

using System.Collections.ObjectModel;
using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.NewTests;

public interface IAnimal
{
    string? Name { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class S_Address
{
    public string? Street { get; set; }
    public string? City { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class S_Customer
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public S_Address? Home { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class S_OrderItem
{
    public string? Sku { get; set; }
    public int Qty { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class S_Order
{
    public int Id { get; set; }
    public S_Customer? Customer { get; set; }
    public List<S_OrderItem>? Items { get; set; }
    public Dictionary<string, string>? Meta { get; set; }
    public string? Notes { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class S_Dog : IAnimal
{
    public int Bones { get; set; }
    public string? Name { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class S_Cat : IAnimal
{
    public int Mice { get; set; }
    public string? Name { get; set; }
}

public sealed class S_Parrot : IAnimal
{
    public int Seeds { get; set; }
    public string? Name { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class S_Zoo
{
    public IAnimal? Pet { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class S_ShallowWrap
{
    [DeepCompare(DeltaShallow = true)] public S_Address? Addr { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class S_SkipWrap
{
    [DeepCompare(DeltaSkip = true)] public string? Ignored { get; set; }
    public string? Tracked { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class S_WithArray
{
    public int[]? Values { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class S_DictHost
{
    public Dictionary<string, S_Address>? Map { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class S_ReadOnlyListHost
{
    public IReadOnlyList<S_OrderItem>? Items { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class S_ReadOnlyDictHost
{
    public IReadOnlyDictionary<string, string>? Meta { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class S_CaseDictHost
{
    public Dictionary<string, string>? Meta { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class S_PolyDictHost
{
    public Dictionary<string, IAnimal>? Pets { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class S_ShallowCollectionWrap
{
    [DeepCompare(DeltaShallow = true)] public List<S_OrderItem>? Items { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class S_SkipCollectionWrap
{
    [DeepCompare(DeltaSkip = true)] public List<S_OrderItem>? Items { get; set; }
    [DeepCompare(DeltaSkip = true)] public Dictionary<string, string>? Meta { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class S_IntListHost
{
    public List<int>? Values { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class S_ModuleInitFoo
{
    public string? V { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class S_SealedThing
{
    public int A { get; set; }
    public S_Address? Addr { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true, CycleTracking = true)]
public sealed class S_Node
{
    public int Id { get; set; }
    public S_Node? Next { get; set; }
    public S_Node? Back { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class S_DynamicBox
{
    public object? Value { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class S_RODictGranularHost
{
    public IReadOnlyDictionary<string, string>? Meta { get; set; }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
public sealed class S_RODictNestedHost
{
    public IReadOnlyDictionary<string, S_Address>? Map { get; set; }
}

public static class SpecFactories
{
    public static S_Order NewOrder()
    {
        return new S_Order
        {
            Id = 42,
            Notes = "init",
            Customer = new S_Customer
            {
                Id = 1, Name = "A", Home = new S_Address { Street = "S1", City = "C1" }
            },
            Items = new List<S_OrderItem>
            {
                new() { Sku = "X", Qty = 3 },
                new() { Sku = "Y", Qty = 1 },
                new() { Sku = "Z", Qty = 2 }
            },
            Meta = new Dictionary<string, string> { ["env"] = "test", ["who"] = "user" }
        };
    }

    public static S_Order Clone(S_Order s)
    {
        return new S_Order
        {
            Id = s.Id,
            Notes = s.Notes,
            Customer = s.Customer is null
                ? null
                : new S_Customer
                {
                    Id = s.Customer.Id,
                    Name = s.Customer.Name,
                    Home = s.Customer.Home is null
                        ? null
                        : new S_Address { Street = s.Customer.Home.Street, City = s.Customer.Home.City }
                },
            Items = s.Items is null
                ? null
                : new List<S_OrderItem>(s.Items.ConvertAll(i => new S_OrderItem { Sku = i.Sku, Qty = i.Qty })),
            Meta = s.Meta is null ? null : new Dictionary<string, string>(s.Meta)
        };
    }

    public static List<S_OrderItem> MakeItems(params (string sku, int qty)[] xs)
    {
        var list = new List<S_OrderItem>(xs.Length);
        foreach (var x in xs) list.Add(new S_OrderItem { Sku = x.sku, Qty = x.qty });
        return list;
    }

    public static IReadOnlyDictionary<string, string> RO(params (string k, string v)[] xs)
    {
        return new Dictionary<string, string>(ToDict(xs));
    }

    public static IReadOnlyDictionary<string, string> ROD(params (string k, string v)[] xs)
    {
        return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(ToDict(xs)));
    }

    public static IReadOnlyDictionary<string, S_Address> RO(params (string k, S_Address v)[] xs)
    {
        var d = new Dictionary<string, S_Address>();
        foreach (var x in xs) d[x.k] = x.v;
        return d;
    }

    private static Dictionary<string, string> ToDict((string k, string v)[] xs)
    {
        var d = new Dictionary<string, string>();
        foreach (var x in xs) d[x.k] = x.v;
        return d;
    }

    public static (S_Node a, S_Node bSame, S_Node bDifferent) MakeCyclicTriplet()
    {
        var a1 = new S_Node { Id = 1 };
        var a2 = new S_Node { Id = 2 };
        a1.Next = a2;
        a2.Next = a1;
        a1.Back = a2;
        a2.Back = a1;
        var b1 = new S_Node { Id = 1 };
        var b2 = new S_Node { Id = 2 };
        b1.Next = b2;
        b2.Next = b1;
        b1.Back = b2;
        b2.Back = b1;
        var c1 = new S_Node { Id = 1 };
        var c2 = new S_Node { Id = 99 };
        c1.Next = c2;
        c2.Next = c1;
        c1.Back = c2;
        c2.Back = c1;
        return (a1, b1, c1);
    }
}