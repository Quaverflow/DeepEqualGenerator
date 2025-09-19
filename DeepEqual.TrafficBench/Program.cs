using System.Collections.Concurrent;
using DeepEqual.Generator.Shared;
using NBomber.CSharp;

namespace TrafficBench;

public static class Program
{
    // ---------- per-id lock to prevent concurrent writes to the same Order ----------
    static class KeyLock
    {
        private static readonly ConcurrentDictionary<int, object> _locks = new();
        public static object For(int id) => _locks.GetOrAdd(id, static _ => new object());
    }

    public static void Main()
    {
        // ---- dataset knobs ----
        const int ObjectCount = 50_000; // number of live orders
        const int LinesPerOrder = 20;     // items per order

        // ---- build dataset ----
        var leftById = new ConcurrentDictionary<int, Order>(Environment.ProcessorCount, ObjectCount);
        var rightById = new ConcurrentDictionary<int, Order>(Environment.ProcessorCount, ObjectCount);

        for (int i = 0; i < ObjectCount; i++)
        {
            var o = SeedOrder(i, LinesPerOrder);
            leftById[i] = Clone(o); // "applied" snapshot (target)
            rightById[i] = o;        // "current" that we mutate per tick
        }

        // ---- comparison context (fast path; toggle ValidateDirtyOnEmit if you want) ----
        var ctxFast = new ComparisonContext(new ComparisonOptions { ValidateDirtyOnEmit = false });

        // ---- NBomber v4.1.2 scenario ----
        var scenario = Scenario.Create("traffic", async ctx =>
        {
            // pick a random order id
            int id = Random.Shared.Next(ObjectCount);

            // EXCLUSIVE access for this order: prevents concurrent list/dict mutation
            lock (KeyLock.For(id))
            {
                var left = leftById[id];
                var right = rightById[id];

                Mutate(right); // 70% scalar, 20% list element, 10% null<->object

                // compute + apply (end-to-end "one op")
                var doc = OrderDeepOps.ComputeDelta(left, right, ctxFast);
                OrderDeepOps.ApplyDelta(ref left, doc);
                leftById[id] = left;
            }

            return Response.Ok();
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(10))
        .WithLoadSimulations(
            // steady 64 concurrent workers for 2 minutes
            Simulation.KeepConstant(copies: 64, during: TimeSpan.FromMinutes(2))
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .WithReportFileName("delta-live") // reports/delta-live-*.html
            .Run();
    }

    // ---------- POCOs (generator emits DeepOps for these) ----------

    [DeepComparable(GenerateDiff = true, GenerateDelta = true)]
    public sealed class Address
    {
        public string? Street { get; set; }
        public string? City { get; set; }
    }

    [DeepComparable(GenerateDiff = true, GenerateDelta = true)]
    public sealed class Customer
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public Address? Home { get; set; }
    }

    [DeepComparable(GenerateDiff = true, GenerateDelta = true)]
    public sealed class OrderItem
    {
        public string? Sku { get; set; }
        public int Qty { get; set; }
    }

    [DeepComparable(GenerateDiff = true, GenerateDelta = true)]
    public sealed class Order
    {
        public int Id { get; set; }
        public Customer? Customer { get; set; }
        public List<OrderItem>? Items { get; set; }
        public string? Notes { get; set; }
    }

    // ---------- helpers ----------

    static Order SeedOrder(int id, int lines) => new()
    {
        Id = id,
        Notes = "init",
        Customer = new Customer
        {
            Id = id,
            Name = "C" + id,
            Home = new Address { Street = "S" + id, City = "City" + (id % 5) }
        },
        Items = Enumerable.Range(0, lines)
            .Select(i => new OrderItem { Sku = "SKU" + i, Qty = i % 5 })
            .ToList()
    };

    static Order Clone(Order s) => new()
    {
        Id = s.Id,
        Notes = s.Notes,
        Customer = s.Customer is null ? null : new Customer
        {
            Id = s.Customer.Id,
            Name = s.Customer.Name,
            Home = s.Customer.Home is null ? null : new Address
            {
                Street = s.Customer.Home.Street,
                City = s.Customer.Home.City
            }
        },
        Items = s.Items?.Select(i => new OrderItem { Sku = i.Sku, Qty = i.Qty }).ToList()
    };

    static void Mutate(Order o)
    {
        var r = Random.Shared.NextDouble();

        // 70%: scalar change (best-case for dirty fast path if you switch to [DeltaTrack] later)
        if (r < 0.70)
        {
            o.Notes = "n" + Random.Shared.Next(1000);
            if (Random.Shared.NextDouble() < 0.15) o.Id ^= 1;
            return;
        }

        // 20%: list element edit (granular diff path; container not marked dirty)
        if (r < 0.90 && o.Items is { Count: > 0 } items)
        {
            var i = Random.Shared.Next(items.Count);
            items[i].Qty++;
            return;
        }

        // 10%: null <-> object toggle
        o.Customer = o.Customer is null ? new Customer { Id = 1, Name = "new" } : null;
    }
}
