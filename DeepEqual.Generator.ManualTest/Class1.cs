using System;
using System.Collections.Generic;
using System.Linq;
using DeepEqual.Generator.Attributes;
using DeepEqual.TestModel;

namespace DeepEqual.Generator.ManualTest
{
    // ---------- Primitive helpers ----------
    public enum Role { Unknown, Developer, Lead, Manager, Director }
    public enum OrderStatus { Pending, Paid, Shipped, Cancelled }

    public readonly struct GeoCoord
    {
        public readonly double Latitude;
        public readonly double Longitude;
        public GeoCoord(double lat, double lon) { Latitude = lat; Longitude = lon; }
        public override string ToString() => $"{Latitude:0.000000},{Longitude:0.000000}";
    }

    public readonly struct Money
    {
        public readonly decimal Amount;
        public readonly string Currency;
        public Money(decimal amount, string currency) { Amount = amount; Currency = currency; }
        public override string ToString() => $"{Amount:0.00} {Currency}";
    }

    // ---------- Recursion (tree) ----------
    public class TreeNode<T>
    {
        public T Value { get; set; }
        public List<TreeNode<T>> Children { get; set; } = new();
        // Useful for DAG/graph style tests (can create cycles if you want)
        public TreeNode<T>? Parent { get; set; }
    }

    // ---------- Cyclic org chart ----------
    public class Employee
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public Role Role { get; set; } = Role.Unknown;
        public Employee? Manager { get; set; }
        public List<Employee> Reports { get; set; } = new();

        public override string ToString() => $"{Name} ({Role})";
    }

    // ---------- Domain-ish model ----------
    public class Address
    {
        public string Line1 { get; set; } = "";
        public string? Line2 { get; set; }
        public string City { get; set; } = "";
        public string Country { get; set; } = "";
        public string Postcode { get; set; } = "";
        public GeoCoord Location { get; set; }
    }

    public class Product
    {
        public string Sku { get; set; } = "";
        public string Name { get; set; } = "";
        public Money Price { get; set; }
        public HashSet<string> Tags { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Attributes { get; set; } = new(StringComparer.Ordinal);
        public byte[]? ImageBytes { get; set; }
    }

    public class OrderLine
    {
        public string Sku { get; set; } = "";
        public int Quantity { get; set; }
        public Money LineTotal { get; set; }
        public int[]? RandomNumbers { get; set; } 
    }

    public class Order
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedUtc { get; set; }
        public OrderStatus Status { get; set; }
        public List<OrderLine> Lines { get; set; } = new();
        public Dictionary<string, List<OrderLine>> LinesByTag { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, string?> Notes { get; set; } = new(StringComparer.Ordinal);
        public TimeSpan? EstimatedDelivery { get; set; }
    }

    public class Customer
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FullName { get; set; } = "";
        public Address BillingAddress { get; set; } = new();
        public Address? ShippingAddress { get; set; }
        public List<Order> Orders { get; set; } = new();
        public Dictionary<string, object?> ArbitraryData { get; set; } = new(StringComparer.Ordinal); // nested object mix
    }

    [DeepComparable]
    public class BigAggregate
    {
        public string Title { get; set; } = "";
        public int Version { get; set; }
        public DateTimeOffset GeneratedAt { get; set; }
        public Employee OrgRoot { get; set; } = new();
        public Dictionary<string, Employee> OrgIndexByName { get; set; } = new(StringComparer.Ordinal);
        public TreeNode<string> Taxonomy { get; set; } = new();
        public Dictionary<string, Product> Catalog { get; set; } = new(StringComparer.Ordinal);
        public List<Customer> Customers { get; set; } = new();
        public Dictionary<string, List<string>> InvertedIndex { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<int, TreeNode<int>> NumberedTrees { get; set; } = new();
        public Dictionary<string, object?> MiscBag { get; set; } = new(StringComparer.Ordinal);
    }

    // ---------- Sample builder ----------
    public static class BigAggregateFactory
    {
        /// <summary>
        /// Creates a large, deep, collection-heavy object with a cycle (manager/reports).
        /// Given the same seed, it produces the same shape & values (deterministic).
        /// </summary>
        public static BigAggregate CreateSample(int seed = 123, bool createCycle = true)
        {
            var rng = new Random(seed);

            // Org with cycle: Alice -> (Bob, Carol); Bob.Manager = Alice; Carol.Manager = Alice
            // Optional extra cycle: Alice.Manager = Carol (uncommon, but stresses cycle handling)
            var alice = new Employee { Name = "Alice", Role = Role.Manager };
            var bob = new Employee { Name = "Bob", Role = Role.Developer, Manager = alice };
            var carol = new Employee { Name = "Carol", Role = Role.Lead, Manager = alice };
            alice.Reports.AddRange(new[] { bob, carol });
            if (createCycle)
            {
                // introduce a small cycle through manager chain (not realistic but good for testing)
                alice.Manager = carol;
            }

            // Org index
            var orgIndex = new Dictionary<string, Employee>(StringComparer.Ordinal)
            {
                ["Alice"] = alice,
                ["Bob"] = bob,
                ["Carol"] = carol
            };

            // Taxonomy tree with 3 levels
            var taxRoot = new TreeNode<string> { Value = "Root" };
            var catA = new TreeNode<string> { Value = "Category-A", Parent = taxRoot };
            var catB = new TreeNode<string> { Value = "Category-B", Parent = taxRoot };
            taxRoot.Children.AddRange(new[] { catA, catB });
            var catA1 = new TreeNode<string> { Value = "A-1", Parent = catA };
            var catA2 = new TreeNode<string> { Value = "A-2", Parent = catA };
            catA.Children.AddRange(new[] { catA1, catA2 });
            var catB1 = new TreeNode<string> { Value = "B-1", Parent = catB };
            catB.Children.Add(catB1);

            // Catalog with products
            var p1 = new Product
            {
                Sku = "SKU-001",
                Name = "Ultra Widget",
                Price = new Money(199.99m, "GBP"),
                Tags = new HashSet<string>(new[] { "widget", "ultra", "new" }, StringComparer.Ordinal),
                Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["color"] = "black",
                    ["size"] = "L",
                },
                ImageBytes = Enumerable.Range(0, 16).Select(i => (byte)(i + seed)).ToArray()
            };
            var p2 = new Product
            {
                Sku = "SKU-002",
                Name = "Nano Thing",
                Price = new Money(49.5m, "GBP"),
                Tags = new HashSet<string>(new[] { "gadget", "nano" }, StringComparer.Ordinal),
                Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["material"] = "aluminium"
                },
                ImageBytes = Enumerable.Range(0, 8).Select(i => (byte)(255 - i - (seed % 10))).ToArray()
            };
            var catalog = new Dictionary<string, Product>(StringComparer.Ordinal)
            {
                [p1.Sku] = p1,
                [p2.Sku] = p2
            };

            // Orders with nested dictionaries and arrays
            var o1 = new Order
            {
                CreatedUtc = DateTime.SpecifyKind(new DateTime(2024, 10, 01, 9, 30, 0), DateTimeKind.Utc),
                Status = OrderStatus.Paid,
                EstimatedDelivery = TimeSpan.FromDays(3),
                Notes = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["gift-wrap"] = "yes",
                    ["message"] = null
                }
            };
            o1.Lines.Add(new OrderLine
            {
                Sku = p1.Sku,
                Quantity = 2,
                LineTotal = new Money(399.98m, "GBP"),
                RandomNumbers = new[] { 4, 8, 15, 16, 23, 42 }
            });
            o1.LinesByTag["widget"] = new List<OrderLine> { o1.Lines[0] };

            var o2 = new Order
            {
                CreatedUtc = DateTime.SpecifyKind(new DateTime(2024, 10, 02, 11, 45, 0), DateTimeKind.Utc),
                Status = OrderStatus.Shipped,
                EstimatedDelivery = null
            };
            o2.Lines.Add(new OrderLine
            {
                Sku = p2.Sku,
                Quantity = 5,
                LineTotal = new Money(247.50m, "GBP"),
                RandomNumbers = new[] { rng.Next(1000), rng.Next(1000), rng.Next(1000) }
            });
            o2.LinesByTag["gadget"] = new List<OrderLine> { o2.Lines[0] };

            // Customers with arbitrary nested data
            var c1 = new Customer
            {
                FullName = "Eve Adams",
                BillingAddress = new Address
                {
                    Line1 = "1 High St",
                    City = "London",
                    Country = "UK",
                    Postcode = "W1 1AA",
                    Location = new GeoCoord(51.5072, -0.1276)
                },
                ShippingAddress = new Address
                {
                    Line1 = "1 High St",
                    City = "London",
                    Country = "UK",
                    Postcode = "W1 1AA",
                    Location = new GeoCoord(51.5072, -0.1276)
                },
                Orders = new List<Order> { o1, o2 },
                ArbitraryData = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["preferred-tags"] = new[] { "widget", "ultra" },
                    ["org-contact"] = alice, // cross-link to org graph
                    ["last-visit-utc"] = new DateTime(2025, 01, 15, 14, 00, 00, DateTimeKind.Utc)
                }
            };

            var c2 = new Customer
            {
                FullName = "Frank Brown",
                BillingAddress = new Address
                {
                    Line1 = "22 River Rd",
                    City = "Manchester",
                    Country = "UK",
                    Postcode = "M1 2AB",
                    Location = new GeoCoord(53.4808, -2.2426)
                },
                ShippingAddress = null,
                Orders = new List<Order>(),
                ArbitraryData = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["notes"] = "VIP",
                    ["favorite-product"] = p1.Sku
                }
            };

            // Inverted index + numbered trees
            var inverted = new Dictionary<string, List<string>>(StringComparer.Ordinal)
            {
                ["widget"] = new List<string> { p1.Sku },
                ["gadget"] = new List<string> { p2.Sku }
            };

            var numberTrees = new Dictionary<int, TreeNode<int>>
            {
                [1] = MakeNumberTree(1, 3),
                [2] = MakeNumberTree(2, 2)
            };

            var misc = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["random-bytes"] = Enumerable.Range(0, 32).Select(i => (byte)(i ^ seed)).ToArray(),
                ["feature-flags"] = new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    ["NewCheckout"] = true,
                    ["A_B_Test_Group"] = false
                },
                ["nullable-int"] = (int?)null,
                ["array-of-doubles"] = new double[] { 3.14, 2.71, 1.618 }
            };

            return new BigAggregate
            {
                Title = "Benchmark Payload",
                Version = 7,
                GeneratedAt = new DateTimeOffset(2025, 09, 12, 10, 00, 00, TimeSpan.Zero),
                OrgRoot = alice,
                OrgIndexByName = orgIndex,
                Taxonomy = taxRoot,
                Catalog = catalog,
                Customers = new List<Customer> { c1, c2 },
                InvertedIndex = inverted,
                NumberedTrees = numberTrees,
                MiscBag = misc
            };
        }

        public static bool AreEqual(BigAggregate l, BigAggregate r) => BigAggregateDeepEqual.AreDeepEqual(l, r);

        /// <summary>
        /// Returns two *equal* instances (deeply equal) built from the same seed.
        /// </summary>
        public static (BigAggregate Left, BigAggregate Right) CreateEqualPair(int seed = 123)
            => (CreateSample(seed), CreateSample(seed));

        /// <summary>
        /// Returns two instances where the second differs a tiny bit (one value change).
        /// </summary>
        public static (BigAggregate Left, BigAggregate Right) CreateNearEqualPairWithOneDifference(int seed = 123)
        {
            var left = CreateSample(seed);
            var right = CreateSample(seed);

            // Introduce a small difference deep in the graph
            var firstOrder = right.Customers[0].Orders[0];
            firstOrder.Lines[0].Quantity += 1; // change one quantity

            return (left, right);
        }
         
        private static TreeNode<int> MakeNumberTree(int root, int depth)
        {
            var r = new TreeNode<int> { Value = root };
            if (depth <= 0) return r;

            var left = new TreeNode<int> { Value = root * 10 + 1, Parent = r };
            var right = new TreeNode<int> { Value = root * 10 + 2, Parent = r };
            r.Children.Add(left);
            r.Children.Add(right);

            if (depth > 1)
            {
                left.Children.Add(new TreeNode<int> { Value = left.Value * 10 + 1, Parent = left });
                right.Children.Add(new TreeNode<int> { Value = right.Value * 10 + 1, Parent = right });
            }
            return r;
        }
    }
}
