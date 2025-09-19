using System.Collections.Concurrent;
using DeepEqual.Generator.Shared;
using NBomber.Contracts;
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

        // ---- comparison context (fast path) ----
        var ctxFast = new ComparisonContext(new ComparisonOptions { ValidateDirtyOnEmit = false });

        // ---- NBomber v4.1.2 scenario ----
        var scenario = Scenario.Create("traffic", async ctx =>
        {
            int id = Random.Shared.Next(ObjectCount);

            // EXCLUSIVE access for this order: prevents any overlap on the same object's members
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
        }).WithWarmUpDuration(TimeSpan.FromSeconds(10)).WithLoadSimulations(
            Simulation.KeepConstant(copies: 128, during: TimeSpan.FromMinutes(1))
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .WithReportFileName("delta-live")
            .WithReportFolder("reports")
            .WithTestSuite("delta")
            .WithTestName("traffic")
            .WithReportingInterval(TimeSpan.FromSeconds(5))
            .Run();
    }

    // ---------- POCOs (generator emits DeepOps for these) ----------
    // IMPORTANT: DeltaShallow=true on collections => ApplyDelta REPLACES the collection,
    // not mutates it in place. This removes concurrency hazards on List/Dictionary.

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

        [DeepCompare(DeltaShallow = true)] // <-- replace entire list on apply (no in-place edits)
        public List<OrderItem>? Items { get; set; }

        [DeepCompare(DeltaShallow = true)] // <-- replace entire dict on apply
        public Dictionary<string, string>? Meta { get; set; }

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
            .ToList(),
        Meta = new Dictionary<string, string> { ["env"] = "prod", ["src"] = "bench" }
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
        Items = s.Items?.Select(i => new OrderItem { Sku = i.Sku, Qty = i.Qty }).ToList(),
        Meta = s.Meta is null ? null : new Dictionary<string, string>(s.Meta)
    };

    static void Mutate(Order o)
    {
        var r = Random.Shared.NextDouble();

        // 70%: scalar change
        if (r < 0.70)
        {
            o.Notes = "n" + Random.Shared.Next(1000);
            if (Random.Shared.NextDouble() < 0.15) o.Id ^= 1;
            return;
        }

        // 20%: list element change (we still edit the list; apply will replace the list atomically)
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
