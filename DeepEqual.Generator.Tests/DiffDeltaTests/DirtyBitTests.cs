using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using DeepEqual.Generator.Shared;

namespace DeepEqual.Generator.Tests.DirtyFeature;

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
[DeltaTrack(ThreadSafe = false)]
public partial class DAddress
{
    private string? _Street;
    public string? Street { get => _Street; set { _Street = value; __MarkDirty(__Bit_Street); } }

    private string? _City;
    public string? City { get => _City; set { _City = value; __MarkDirty(__Bit_City); } }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
[DeltaTrack(ThreadSafe = false)]
public partial class DCustomer
{
    private int _Id;
    public int Id { get => _Id; set { _Id = value; __MarkDirty(__Bit_Id); } }

    private string? _Name;
    public string? Name { get => _Name; set { _Name = value; __MarkDirty(__Bit_Name); } }

    private DAddress? _Home;
    public DAddress? Home { get => _Home; set { _Home = value; __MarkDirty(__Bit_Home); } }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
[DeltaTrack(ThreadSafe = false)]
public partial class DItem
{
    private string? _Sku;
    public string? Sku { get => _Sku; set { _Sku = value; __MarkDirty(__Bit_Sku); } }

    private int _Qty;
    public int Qty { get => _Qty; set { _Qty = value; __MarkDirty(__Bit_Qty); } }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
[DeltaTrack(ThreadSafe = false)]
public partial class DOrder
{
    private int _Id;
    public int Id { get => _Id; set { _Id = value; __MarkDirty(__Bit_Id); } }

    private DCustomer? _Customer;
    public DCustomer? Customer { get => _Customer; set { _Customer = value; __MarkDirty(__Bit_Customer); } }

    public List<DItem>? Items { get; set; }

    public Dictionary<string, string>? Meta { get; set; }

    private string? _Notes;
    public string? Notes { get => _Notes; set { _Notes = value; __MarkDirty(__Bit_Notes); } }
}

[DeepComparable(GenerateDiff = true, GenerateDelta = true)]
[DeltaTrack(ThreadSafe = true)]
public partial class DOrder_TS
{
    private int _Id;
    public int Id { get => _Id; set { _Id = value; __MarkDirty(__Bit_Id); } }

    public List<DItem>? Items { get; set; }

    private string? _Notes;
    public string? Notes { get => _Notes; set { _Notes = value; __MarkDirty(__Bit_Notes); } }
}

public sealed class DirtyFeatureTests
{
    private static ComparisonContext CtxFast() => new(new ComparisonOptions { ValidateDirtyOnEmit = false });
    private static ComparisonContext CtxValidate() => new(new ComparisonOptions { ValidateDirtyOnEmit = true });

    private static DOrder NewOrder() => new()
    {
        Id = 1,
        Notes = "init",
        Customer = new DCustomer
        {
            Id = 7,
            Name = "c",
            Home = new DAddress { Street = "s", City = "x" }
        },
        Items =
        [
            new DItem { Sku = "A", Qty = 1 },
            new DItem { Sku = "B", Qty = 2 },
            new DItem { Sku = "C", Qty = 3 }
        ],
        Meta = new Dictionary<string, string> { ["env"] = "t" }
    };

    private static DOrder Clone(DOrder s) => new()
    {
        Id = s.Id,
        Notes = s.Notes,
        Customer = s.Customer is null ? null : new DCustomer
        {
            Id = s.Customer.Id,
            Name = s.Customer.Name,
            Home = s.Customer.Home is null ? null : new DAddress
            {
                Street = s.Customer.Home.Street,
                City = s.Customer.Home.City
            }
        },
        Items = s.Items?.Select(i => new DItem { Sku = i.Sku, Qty = i.Qty }).ToList(),
        Meta = s.Meta is null ? null : new Dictionary<string, string>(s.Meta)
    };

    /// <summary>Pop & clear any pre-existing dirty bits without emitting ops.</summary>
    /// <summary>
    /// Recursively clears all dirty bits on an object graph, without changing values.
    /// Handles cycles; no-ops safely for non-[DeltaTrack] types.
    /// </summary>
    private static void CleanDirty<T>(T obj)
    {
        if (obj is null)
        {
            return;
        }

        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        void Recurse(object? o)
        {
            if (o is null)
            {
                return;
            }

            if (!seen.Add(o))
            {
                return;
            }

            var t = o.GetType();

            var hasAny = t.GetMethod("__HasAnyDirty", BindingFlags.Instance | BindingFlags.NonPublic);
            var tryPop = t.GetMethod("__TryPopNextDirty", BindingFlags.Instance | BindingFlags.NonPublic);
            if (hasAny is not null && tryPop is not null)
            {
                var args = new object?[] { null };
                while ((bool)tryPop.Invoke(o, args)!)
                {
                    args[0] = null;
                }
            }

            foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (p.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                var pt = p.PropertyType;

                if (pt.IsValueType || pt == typeof(string))
                {
                    continue;
                }

                var v = p.GetValue(o);
                if (v is null)
                {
                    continue;
                }

                if (v is IEnumerable en and not string)
                {
                    foreach (var e in en) Recurse(e);
                }
                else
                {
                    Recurse(v);
                }
            }
        }

        Recurse(obj);
    }

    /// <summary>Reference equality comparer for HashSet/Dictionary.</summary>
    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();
        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>Get the stable member index for Notes by making a distinctive change and selecting the best-matching op.</summary>
    private static int ProbeIndex_Notes()
    {
        var a = NewOrder(); CleanDirty(a);
        var b = Clone(a); CleanDirty(b);

        b.Notes = "__PROBE_NOTES__";
        var doc = DOrderDeepOps.ComputeDelta(a, b, CtxFast());

        var op = doc.Operations.First(o => o.Kind == DeltaKind.SetMember && (string?)o.Value == "__PROBE_NOTES__");
        return op.MemberIndex;
    }

    /// <summary>Get the stable member index for Id by making a distinctive change and selecting the op carrying that value.</summary>
    private static int ProbeIndex_Id()
    {
        var a = NewOrder(); CleanDirty(a);
        var b = Clone(a); CleanDirty(b);

        b.Id = 1234567;
        var doc = DOrderDeepOps.ComputeDelta(a, b, CtxFast());

        var op = doc.Operations.First(o => o is { Kind: DeltaKind.SetMember, Value: int and 1234567 });
        return op.MemberIndex;
    }

    [Fact]
    public void Dirty_SingleProperty_Emits_OnlyThatMember()
    {
        var a = NewOrder(); CleanDirty(a);
        var b = Clone(a); CleanDirty(b);

        b.Notes = "x";               
        var doc = DOrderDeepOps.ComputeDelta(a, b, CtxFast());
        Assert.False(doc.IsEmpty);

        var notesIdx = ProbeIndex_Notes();

        var setMembers = doc.Operations.Where(o => o.Kind == DeltaKind.SetMember).ToList();
        Assert.Single(setMembers);
        Assert.Equal(notesIdx, setMembers[0].MemberIndex);

        var t = Clone(a); CleanDirty(t);
        DOrderDeepOps.ApplyDelta(ref t, doc);
        Assert.True(DOrderDeepEqual.AreDeepEqual(b, t));
    }

    [Fact]
    public void Dirty_MultipleProperties_Emits_Both()
    {
        var a = NewOrder(); CleanDirty(a);
        var b = Clone(a); CleanDirty(b);

        b.Id += 1;                           b.Notes = "y";               
        var doc = DOrderDeepOps.ComputeDelta(a, b, CtxFast());

        var idIdx = ProbeIndex_Id();
        var notesIdx = ProbeIndex_Notes();

        var setIdxs = doc.Operations.Where(o => o.Kind == DeltaKind.SetMember).Select(o => o.MemberIndex).ToList();
        Assert.Contains(idIdx, setIdxs);
        Assert.Contains(notesIdx, setIdxs);
        Assert.Equal(2, setIdxs.Count);

        var t = Clone(a); CleanDirty(t);
        DOrderDeepOps.ApplyDelta(ref t, doc);
        Assert.True(DOrderDeepEqual.AreDeepEqual(b, t));
    }

    [Fact]
    public void Dirty_ValidateDirtyOnEmit_Suppresses_NoOp()
    {
        var a = NewOrder(); CleanDirty(a);
        var b = Clone(a); CleanDirty(b);

        b.Notes = a.Notes!;          
        var docFast = DOrderDeepOps.ComputeDelta(a, b, CtxFast());
        Assert.Contains(docFast.Operations, o => o.Kind == DeltaKind.SetMember);

        var docVal = DOrderDeepOps.ComputeDelta(a, b, CtxValidate());
        Assert.True(docVal.IsEmpty);
    }

    [Fact]
    public void Dirty_ThreadSafe_Variant_Correctness()
    {
        var a = new DOrder_TS { Id = 1, Notes = "n", Items = [new DItem { Sku = "A", Qty = 1 }] }; CleanDirty(a);
        var b = new DOrder_TS { Id = 2, Notes = "m", Items = [new DItem { Sku = "A", Qty = 1 }] }; CleanDirty(b);

        var doc = DOrder_TSDeepOps.ComputeDelta(a, b, CtxFast());
        Assert.Contains(doc.Operations, o => o.Kind == DeltaKind.SetMember);

        DOrder_TSDeepOps.ApplyDelta(ref a, doc);
        Assert.True(DOrder_TSDeepEqual.AreDeepEqual(a, b));
    }

    [Fact]
    public void Dirty_ListItem_Edit_No_Container_Bit_Falls_Back_To_Correct_Result()
    {
        var a = NewOrder(); CleanDirty(a);
        var b = Clone(a); CleanDirty(b);

        b.Items![1].Qty += 1; 
        var doc = DOrderDeepOps.ComputeDelta(a, b, CtxFast());
        Assert.False(doc.IsEmpty);

        var looksGranular =
            // direct granular list op on Items
            doc.Operations.Any(o =>
                (o.Kind == DeltaKind.SeqNestedAt || o.Kind == DeltaKind.SeqReplaceAt) &&
                o.Index == 1)
            // OR container-level nested doc that contains a granular list op inside
            || doc.Operations.Any(o =>
                o.Kind == DeltaKind.NestedMember &&
                o.Nested is { } nd &&
                nd.Operations.Any(p =>
                    p.Kind == DeltaKind.SeqNestedAt || p.Kind == DeltaKind.SeqReplaceAt))
            // OR legacy whole-list replacement fallback
            || doc.Operations.Any(o =>
                o.Kind == DeltaKind.SetMember &&
                (o.Value is System.Collections.Generic.IList<DItem> ||
                 o.Value is System.Collections.Generic.IReadOnlyList<DItem>));

        Assert.True(looksGranular, "Expected granular list op, nested granular, or SetMember fallback for Items.");

        var t = Clone(a); CleanDirty(t);
        DOrderDeepOps.ApplyDelta(ref t, doc);
        Assert.True(DOrderDeepEqual.AreDeepEqual(b, t));
    }

    [Fact]
    public void Dirty_Clean_After_Apply_Then_NoOp()
    {
        var a = NewOrder(); CleanDirty(a);
        var b = Clone(a); CleanDirty(b);

        b.Notes = "x";    
        var doc = DOrderDeepOps.ComputeDelta(a, b, CtxFast());
        var t = Clone(a); CleanDirty(t);
        DOrderDeepOps.ApplyDelta(ref t, doc);
        Assert.True(DOrderDeepEqual.AreDeepEqual(b, t));

        var doc2 = DOrderDeepOps.ComputeDelta(t, b, CtxFast());
        Assert.True(doc2.IsEmpty);
    }

    [Fact]
    public void Dirty_NoChange_NoOps()
    {
        var a = NewOrder(); CleanDirty(a);
        var b = Clone(a); CleanDirty(b);

        var doc = DOrderDeepOps.ComputeDelta(a, b, CtxFast());
        Assert.True(doc.IsEmpty);
    }
}
