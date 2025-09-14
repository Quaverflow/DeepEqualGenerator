using DeepEqual.Generator.Shared;
using DeepEqual.Generator.Tests.Models;

namespace DeepEqual.Tests3;

public class CollectionsTests
{
    [Fact]
    public void Array_Ordered_By_Default()
    {
        var a = new RootOrderSensitiveCollections { Names = new List<string> { "a", "b" } };
        var b = new RootOrderSensitiveCollections { Names = new List<string> { "b", "a" } };
        Assert.False(RootOrderSensitiveCollectionsDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Member_Level_OrderInsensitive_For_Array_Or_List()
    {
        var a = new WithOrderInsensitiveMember { Values = new List<int> { 1, 2, 2, 3 } };
        var b = new WithOrderInsensitiveMember { Values = new List<int> { 2, 3, 2, 1 } };
        Assert.True(WithOrderInsensitiveMemberDeepEqual.AreDeepEqual(a, b));
        var c = new WithOrderInsensitiveMember { Values = new List<int> { 1, 2, 3 } };
        Assert.False(WithOrderInsensitiveMemberDeepEqual.AreDeepEqual(a, c));
    }

    [Fact]
    public void Root_Level_OrderInsensitive_Applies_To_Collections()
    {
        var a = new RootOrderInsensitiveCollections
        {
            Names = new List<string> { "x", "y", "y" },
            People = new List<Person> { new Person { Name = "p1", Age = 1 }, new Person { Name = "p2", Age = 2 } },
            ForcedOrdered = new List<int> { 1, 2, 3 }
        };
        var b = new RootOrderInsensitiveCollections
        {
            Names = new List<string> { "y", "x", "y" },
            People = new List<Person> { new Person { Name = "p2", Age = 2 }, new Person { Name = "p1", Age = 1 } },
            ForcedOrdered = new List<int> { 1, 2, 3 }
        };
        Assert.True(RootOrderInsensitiveCollectionsDeepEqual.AreDeepEqual(a, b));

        Assert.False(RootOrderInsensitiveCollectionsDeepEqual.AreDeepEqual(
            new RootOrderInsensitiveCollections { ForcedOrdered = new List<int> { 1, 2, 3 } },
            new RootOrderInsensitiveCollections { ForcedOrdered = new List<int> { 3, 2, 1 } }));
    }

    [Fact]
    public void Element_Type_OrderInsensitive_Default_Is_Respected()
    {
        var a = new RootWithElementTypeDefaultUnordered
        {
            Tags = new List<TagAsElementDefaultUnordered>
            {
                new TagAsElementDefaultUnordered { Label = "A" },
                new TagAsElementDefaultUnordered { Label = "B" }
            }
        };
        var b = new RootWithElementTypeDefaultUnordered
        {
            Tags = new List<TagAsElementDefaultUnordered>
            {
                new TagAsElementDefaultUnordered { Label = "B" },
                new TagAsElementDefaultUnordered { Label = "A" }
            }
        };
        Assert.True(RootWithElementTypeDefaultUnorderedDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void MultiDimensional_Arrays_Are_Compared_By_Shape_And_Values()
    {
        var m1 = new MultiDimArrayHolder { Matrix = new int[2, 2] { { 1, 2 }, { 3, 4 } } };
        var m2 = new MultiDimArrayHolder { Matrix = new int[2, 2] { { 1, 2 }, { 3, 4 } } };
        var m3 = new MultiDimArrayHolder { Matrix = new int[2, 2] { { 1, 2 }, { 4, 3 } } };
        var m4 = new MultiDimArrayHolder { Matrix = new int[2, 3] { { 1, 2, 0 }, { 3, 4, 0 } } };
        Assert.True(MultiDimArrayHolderDeepEqual.AreDeepEqual(m1, m2));
        Assert.False(MultiDimArrayHolderDeepEqual.AreDeepEqual(m1, m3));
        Assert.False(MultiDimArrayHolderDeepEqual.AreDeepEqual(m1, m4));
    }

    [Fact]
    public void Dictionaries_Use_Deep_Compare_For_Values()
    {
        var a = new DictionaryHolder
        {
            Map = new Dictionary<int, Person>
            {
                [1] = new Person { Name = "A", Age = 10 },
                [2] = new Person { Name = "B", Age = 20 }
            }
        };
        var b = new DictionaryHolder
        {
            Map = new Dictionary<int, Person>
            {
                [2] = new Person { Name = "B", Age = 20 },
                [1] = new Person { Name = "A", Age = 10 }
            }
        };
        var c = new DictionaryHolder
        {
            Map = new Dictionary<int, Person>
            {
                [1] = new Person { Name = "A", Age = 10 },
                [2] = new Person { Name = "B", Age = 99 }
            }
        };
        Assert.True(DictionaryHolderDeepEqual.AreDeepEqual(a, b));
        Assert.False(DictionaryHolderDeepEqual.AreDeepEqual(a, c));
    }

    [Fact]
    public void Unordered_List_Of_Objects_Without_Keys_Still_Equal_When_Swapped()
    {
        var a = new CustomersUnkeyed
        {
            People = new List<Person>
            {
                new Person { Name = "p1", Age = 1 },
                new Person { Name = "p2", Age = 2 }
            }
        };
        var b = new CustomersUnkeyed
        {
            People = new List<Person>
            {
                new Person { Name = "p2", Age = 2 },
                new Person { Name = "p1", Age = 1 }
            }
        };
        Assert.True(CustomersUnkeyedDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Unordered_List_With_KeyMembers_Matches_By_Key()
    {
        var a = new CustomersKeyed
        {
            Customers = new List<CustomerK>
            {
                new CustomerK { Id = "a", Name = "alice" },
                new CustomerK { Id = "b", Name = "bob" }
            }
        };
        var b = new CustomersKeyed
        {
            Customers = new List<CustomerK>
            {
                new CustomerK { Id = "b", Name = "bob" },
                new CustomerK { Id = "a", Name = "alice" }
            }
        };
        Assert.True(CustomersKeyedDeepEqual.AreDeepEqual(a, b));

        var c = new CustomersKeyed
        {
            Customers = new List<CustomerK>
            {
                new CustomerK { Id = "a", Name = "alice" },
                new CustomerK { Id = "b", Name = "BOB!" }
            }
        };
        Assert.False(CustomersKeyedDeepEqual.AreDeepEqual(a, c));

        var d = new CustomersKeyed
        {
            Customers = new List<CustomerK>
            {
                new CustomerK { Id = "a", Name = "alice" }
            }
        };
        Assert.False(CustomersKeyedDeepEqual.AreDeepEqual(a, d));
    }


    [Fact]
    public void Object_Array_Uses_Structural_Equality()
    {
        var a = new ObjArr { Any = new[] { 1, 2, 3 } };
        var b = new ObjArr { Any = new[] { 1, 2, 3 } };
        var c = new ObjArr { Any = new[] { 1, 2, 4 } };
        Assert.True(ObjArrDeepEqual.AreDeepEqual(a, b));
        Assert.False(ObjArrDeepEqual.AreDeepEqual(a, c));
    }

    [Fact]
    public void Interface_Property_Uses_Runtime_Type()
    {
        var a = new Zoo { Animal = new Cat { Age = 3, Name = "Paws" } };
        var b = new Zoo { Animal = new Cat { Age = 3, Name = "Paws" } };
        var c = new Zoo { Animal = new Cat { Age = 3, Name = "Claws" } };
        Assert.True(ZooDeepEqual.AreDeepEqual(a, b));
        Assert.False(ZooDeepEqual.AreDeepEqual(a, c));
    }

    [Fact]
    public void Jagged_Vs_Multidimensional_Arrays_NotEqual()
    {
        var jagged = new int[][] { new[] { 1, 2 }, new[] { 3, 4 } };
        var multi = new int[2, 2] { { 1, 2 }, { 3, 4 } };
        var a = new ArrayHolder { Any = jagged };
        var b = new ArrayHolder { Any = multi };
        Assert.False(ArrayHolderDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Unordered_Keyed_With_Duplicates_Must_Match_PerBucket_Count_And_Values()
    {
        var A = new KeyedBag { Items = new() { new() { Name = "x", X = 1 }, new() { Name = "x", X = 2 }, new() { Name = "y", X = 9 } } };
        var B = new KeyedBag { Items = new() { new() { Name = "x", X = 2 }, new() { Name = "y", X = 9 }, new() { Name = "x", X = 1 } } };
        var C = new KeyedBag { Items = new() { new() { Name = "x", X = 1 }, new() { Name = "y", X = 9 } } }; Assert.True(KeyedBagDeepEqual.AreDeepEqual(A, B));
        Assert.False(KeyedBagDeepEqual.AreDeepEqual(A, C));
    }

    [Fact]
    public void String_And_Number_Options_Are_Respected()
    {
        var a = new OptsHolder { S = "Hello", D = 1.0000001, M = 1.00005m };
        var b = new OptsHolder { S = "hello", D = 1.0000002, M = 1.00001m };

        var loose = new ComparisonOptions
        {
            StringComparison = StringComparison.OrdinalIgnoreCase,
            DoubleEpsilon = 1e-6,
            DecimalEpsilon = 0.0001m
        };
        Assert.True(OptsHolderDeepEqual.AreDeepEqual(a, b, loose));

        var strict = new ComparisonOptions
        {
            StringComparison = StringComparison.Ordinal,
            DoubleEpsilon = 0.0,
            DecimalEpsilon = 0m
        };
        Assert.False(OptsHolderDeepEqual.AreDeepEqual(a, b, strict));
    }

    [DeepComparable] public sealed class OptsHolder { public string? S { get; init; } public double D { get; init; } public decimal M { get; init; } }

    [Fact]
    public void Polymorphism_In_Array_And_ReadOnlyList()
    {
        var arrA = new ZooArray { Animals = new IAnimal[] { new Cat { Age = 4, Name = "Rex" } } };
        var arrB = new ZooArray { Animals = new IAnimal[] { new Cat { Age = 4, Name = "Rex" } } };
        var arrC = new ZooArray { Animals = new IAnimal[] { new Cat { Age = 5, Name = "Rex" } } };
        Assert.True(ZooArrayDeepEqual.AreDeepEqual(arrA, arrB));
        Assert.False(ZooArrayDeepEqual.AreDeepEqual(arrA, arrC));

        var roA = new ZooRoList { Animals = new List<IAnimal> { new Cat { Age = 1, Name = "Kit" }, new Cat { Age = 3, Name = "Mog" } } };
        var roB = new ZooRoList { Animals = new List<IAnimal> { new Cat { Age = 1, Name = "Kit" }, new Cat { Age = 3, Name = "Mog" } } };
        var roC = new ZooRoList { Animals = new List<IAnimal> { new Cat { Age = 1, Name = "Kit" }, new Cat { Age = 2, Name = "Mog" } } };
        Assert.True(ZooRoListDeepEqual.AreDeepEqual(roA, roB));
        Assert.False(ZooRoListDeepEqual.AreDeepEqual(roA, roC));
    }

    [Fact]
    public void Polymorphic_Collections_Handle_Nulls()
    {
        var A = new ZooList { Animals = new() { new Cat { Age = 1, Name = "A" }, null } };
        var B = new ZooList { Animals = new() { new Cat { Age = 1, Name = "A" }, null } };
        var C = new ZooList { Animals = new() { new Cat { Age = 1, Name = "A" }, new Cat { Age = 2, Name = "B" } } };
        Assert.True(ZooListDeepEqual.AreDeepEqual(A, B));
        Assert.False(ZooListDeepEqual.AreDeepEqual(A, C));
    }

    [Fact]
    public void Dictionary_Value_Polymorphism_And_Type_Mismatch()
    {
        var A = new ZooDict { Pets = new() { ["a"] = new Cat { Age = 2, Name = "C" }, ["b"] = new Cat { Age = 5, Name = "D" } } };
        var B = new ZooDict { Pets = new() { ["a"] = new Cat { Age = 2, Name = "C" }, ["b"] = new Cat { Age = 5, Name = "D" } } };
        var C = new ZooDict { Pets = new() { ["a"] = new Cat { Age = 2, Name = "C" }, ["b"] = new Cat { Age = 6, Name = "D" } } };
        Assert.True(ZooDictDeepEqual.AreDeepEqual(A, B));
        Assert.False(ZooDictDeepEqual.AreDeepEqual(A, C));

        var D = new ZooDict { Pets = new() { ["a"] = new Cat { Age = 2, Name = "C" }, ["b"] = new Dog { Age = 5, Name = "D" } } };
        Assert.False(ZooDictDeepEqual.AreDeepEqual(A, D));
    }

    [Fact]
    public void ReadOnlyDictionary_Wraps_Are_Equal()
    {
        var baseMap = new Dictionary<string, IAnimal> { ["x"] = new Cat { Age = 1, Name = "A" } };
        var r1 = new ZooRoDict { Pets = new System.Collections.ObjectModel.ReadOnlyDictionary<string, IAnimal>(baseMap) };
        var r2 = new ZooRoDict { Pets = new System.Collections.ObjectModel.ReadOnlyDictionary<string, IAnimal>(new Dictionary<string, IAnimal>(baseMap)) };
        Assert.True(ZooRoDictDeepEqual.AreDeepEqual(r1, r2));
    }

    [Fact]
    public void Dictionary_Key_Comparer_Case_Sensitivity()
    {
        var a1 = new Dictionary<string, IAnimal>(StringComparer.Ordinal) { ["a"] = new Cat { Age = 1, Name = "X" } };
        var a2 = new Dictionary<string, IAnimal>(StringComparer.Ordinal) { ["A"] = new Cat { Age = 1, Name = "X" } };
        var b1 = new Dictionary<string, IAnimal>(StringComparer.OrdinalIgnoreCase) { ["a"] = new Cat { Age = 1, Name = "X" } };
        var b2 = new Dictionary<string, IAnimal>(StringComparer.OrdinalIgnoreCase) { ["A"] = new Cat { Age = 1, Name = "X" } };

        Assert.False(ZooDictDeepEqual.AreDeepEqual(new ZooDict { Pets = a1 }, new ZooDict { Pets = a2 }));
        Assert.True(ZooDictDeepEqual.AreDeepEqual(new ZooDict { Pets = b1 }, new ZooDict { Pets = b2 }));
    }

    [Fact]
    public void Unordered_List_With_Composite_KeyMembers()
    {
        var A = new CompositeKeyBag { Items = new() { new Item { Name = "a", X = 1 }, new Item { Name = "b", X = 2 } } };
        var B = new CompositeKeyBag { Items = new() { new Item { Name = "b", X = 2 }, new Item { Name = "a", X = 1 } } };
        var C = new CompositeKeyBag { Items = new() { new Item { Name = "b", X = 99 }, new Item { Name = "a", X = 1 } } };
        Assert.True(CompositeKeyBagDeepEqual.AreDeepEqual(A, B));
        Assert.False(CompositeKeyBagDeepEqual.AreDeepEqual(A, C));
    }

    [Fact]
    public void IEnumerable_List_Array_Content_Equality()
    {
        var list = new List<int> { 1, 2, 3 };
        var arr = new[] { 1, 2, 3 };
        var A = new EnumerableHolder { Seq = list };
        var B = new EnumerableHolder { Seq = arr };
        Assert.True(EnumerableHolderDeepEqual.AreDeepEqual(A, B));
    }
}