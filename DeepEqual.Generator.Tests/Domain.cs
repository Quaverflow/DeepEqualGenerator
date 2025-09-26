using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Linq;
using System.Text;
using DeepEqual.Generator.Shared;

namespace DeepEqual.RewrittenTests.Domain;

public enum OrderStatus { Draft, Submitted, Completed }

public interface IShape
{
    double Area();
}
[DeepComparable]
public sealed class Circle : IShape
{
    public double Radius { get; set; }
    public double Area() => Math.PI * Radius * Radius;
}
[DeepComparable]
public sealed class Square : IShape
{
    public double Side { get; set; }
    public double Area() => Side * Side;
}

public sealed class Customer
{
    public string Name { get; set; } = "";
    public bool Vip { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
}

public sealed class OrderLine
{
    public string Sku { get; set; } = "";
    public int Qty { get; set; }
    public decimal Price { get; set; }
    public string? Notes { get; set; }
}

public sealed class Widget
{
    public string Id { get; set; } = "";
    public int Count { get; set; }
}

public sealed class ExternalRoot
{
    public string ExternalId { get; set; } = "";
    public Dictionary<string, string> Meta { get; set; } = new();
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true, IncludeInternals = true, CycleTracking = true)]
public sealed class Order
{
    public string Id { get; set; } = "";
    public OrderStatus Status { get; set; }
    public Customer Customer { get; set; } = new();
    [DeepCompare(OrderInsensitive = true, KeyMembers = new[] { "Sku" })]
    public List<OrderLine> Lines { get; } = new();            // unordered & keyed by Sku via Fluent config
    public List<Widget> Widgets { get; set; } = new();             // ordered by default

    public Dictionary<string, object?> Props { get; } = new();
    public IDictionary<string, object?> Bag { get; } = new Dictionary<string, object?>();

    public ExpandoObject Expando { get; set; } = new();

    public byte[] Bytes { get; set; } = Array.Empty<byte>();
    public ReadOnlyMemory<byte> Blob { get; set; } = ReadOnlyMemory<byte>.Empty;

    public DateTime CreatedUtc { get; set; }
    public DateTimeOffset Offset { get; set; }
    public TimeSpan Span { get; set; }

    public decimal? MaybeDiscount { get; set; }
    public DateTime? MaybeWhen { get; set; }

    public IShape? Shape { get; set; }

    public string[] Notes { get; set; } = Array.Empty<string>();

    public Queue<string> Queue { get; } = new();
    public Stack<int> Stack { get; } = new();
    public LinkedList<string> Linked { get; } = new();
    [DeepCompare(OrderInsensitive = true)]
    public HashSet<string> Flags { get; } = new(StringComparer.OrdinalIgnoreCase);
    public SortedSet<int> Sorted { get; } = new();
    public Dictionary<Guid, string> Meta { get; set; } = new();

    public int[,] Grid { get; set; } = new int[0,0];

    public ExternalRoot? External { get; set; }
}