using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using DeepEqual.RewrittenTests.Domain;

namespace DeepEqual.RewrittenTests;

public static class UnifiedFixture
{
    public static Order MakeBaseline()
    {
        var o = new Order
        {
            Id = "ORD-1",
            Status = OrderStatus.Submitted,
            CreatedUtc = new DateTime(2024, 01, 02, 03, 04, 05, DateTimeKind.Utc),
            Offset = new DateTimeOffset(2024, 01, 02, 03, 04, 05, TimeSpan.FromHours(2)),
            Span = TimeSpan.FromMinutes(123),
            MaybeDiscount = 5.0m,
            MaybeWhen = new DateTime(2024, 01, 03, 00, 00, 00, DateTimeKind.Utc),
            Blob = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3 }),
            Bytes = new byte[] { 10, 11, 12 },
            Grid = new int[,] { { 1, 2 }, { 3, 4 } },
            Shape = new Circle { Radius = 2.0 },
            External = new ExternalRoot { ExternalId = "EXT-1", Meta = { ["k"] = "v" } }
        };

        o.Customer = new Customer
        {
            Name = "Ada",
            Vip = true,
            Tags = new[] { "alpha", "beta" }
        };

        o.Lines.AddRange(new[]
        {
        new OrderLine { Sku = "A", Qty = 1, Price = 2.5m, Notes = "n1" },
        new OrderLine { Sku = "B", Qty = 2, Price = 3.0m, Notes = "n2" },
        new OrderLine { Sku = "C", Qty = 3, Price = 1.99m }
    });

        o.Widgets.AddRange(new[]
        {
        new Widget { Id = "W1", Count = 1 },
        new Widget { Id = "W2", Count = 2 },
    });

        o.Notes = new[] { "one", "two" };

        o.Props["threshold"] = 0.75;
        o.Props["feature"] = "x";
        o.Props["child"] = new Dictionary<string, object?> { ["sub"] = 123 };

        o.Bag["bagv"] = "v1";

        // Robust Expando construction (dictionary style)
        dynamic ex = new ExpandoObject();
        var exDict = (IDictionary<string, object?>)ex;
        exDict["path"] = "root";
        var nested = new ExpandoObject();
        ((IDictionary<string, object?>)nested)["flag"] = true;
        exDict["nested"] = nested;
        o.Expando = ex;

        // Ensure ordering differences are observable (≥2 items)
        o.Queue.Enqueue("q1");
        o.Queue.Enqueue("q2");

        o.Stack.Push(1);
        o.Stack.Push(2);

        o.Linked.AddLast("l1");
        o.Linked.AddLast("l2");

        o.Flags.Add("X");
        o.Flags.Add("Y");

        o.Sorted.Add(5);
        o.Sorted.Add(1);

        o.Meta[Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")] = "A";
        o.Meta[Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")] = "B";

        return o;
    }


    public static Order Clone(Order o)
    {
        var c = new Order
        {
            Id = o.Id,
            Status = o.Status,
            Customer = new Customer
            {
                Name = o.Customer.Name,
                Vip = o.Customer.Vip,
                Tags = o.Customer.Tags.ToArray()
            },
            Bytes = o.Bytes.ToArray(),
            Blob = new ReadOnlyMemory<byte>(o.Blob.ToArray()),
            CreatedUtc = o.CreatedUtc,
            Offset = o.Offset,
            Span = o.Span,
            MaybeDiscount = o.MaybeDiscount,
            MaybeWhen = o.MaybeWhen,
            Notes = o.Notes.ToArray(),
            Grid = (int[,])o.Grid.Clone(),
            Shape = o.Shape is Circle ci ? new Circle { Radius = ci.Radius }
                 : o.Shape is Square sq ? new Square { Side = sq.Side }
                 : null,
            External = o.External is null ? null : new ExternalRoot
            {
                ExternalId = o.External.ExternalId,
                Meta = new Dictionary<string, string>(o.External.Meta)
            }
        };

        foreach (var l in o.Lines)
            c.Lines.Add(new OrderLine { Sku = l.Sku, Qty = l.Qty, Price = l.Price, Notes = l.Notes });

        foreach (var w in o.Widgets)
            c.Widgets.Add(new Widget { Id = w.Id, Count = w.Count });

        // Props deep copy: clone nested dictionaries (1 level is enough for tests)
        foreach (var kv in o.Props)
        {
            if (kv.Value is Dictionary<string, object?> d)
                c.Props[kv.Key] = new Dictionary<string, object?>(d);
            else
                c.Props[kv.Key] = kv.Value;
        }

        // Bag shallow (object values), but copy the dictionary container itself
        foreach (var kv in o.Bag)
            c.Bag[kv.Key] = kv.Value;

        // Expando deep copy (top + nested ExpandoObject)
        dynamic ex = new ExpandoObject();
        var exDict = (IDictionary<string, object?>)ex;
        var srcDict = (IDictionary<string, object?>)o.Expando;
        foreach (var kv in srcDict)
        {
            if (kv.Value is ExpandoObject e2)
            {
                var e2Clone = new ExpandoObject();
                var e2Src = (IDictionary<string, object?>)e2;
                var e2Dst = (IDictionary<string, object?>)e2Clone;
                foreach (var kv2 in e2Src)
                    e2Dst[kv2.Key] = kv2.Value;
                exDict[kv.Key] = e2Clone;
            }
            else if (kv.Value is IDictionary<string, object?> d2)
            {
                var d2Clone = new Dictionary<string, object?>(d2);
                exDict[kv.Key] = d2Clone;
            }
            else
            {
                exDict[kv.Key] = kv.Value;
            }
        }
        c.Expando = ex;

        foreach (var x in o.Queue) c.Queue.Enqueue(x);
        foreach (var x in o.Stack.Reverse()) c.Stack.Push(x);
        foreach (var x in o.Linked) c.Linked.AddLast(x);
        foreach (var x in o.Flags) c.Flags.Add(x);
        foreach (var x in o.Sorted) c.Sorted.Add(x);
        foreach (var kv in o.Meta) c.Meta[kv.Key] = kv.Value;

        return c;
    }

}