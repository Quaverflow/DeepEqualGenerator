using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DeepEqual;

/// <summary>Root-scoped configuration builder. No runtime behavior.</summary>
public sealed class RootBuilder
{
    internal RootBuilder(Type root, Preset preset) { }

    public RootBuilder GenerateDelta(bool on = true) => this;
    public RootBuilder GenerateDiff(bool on = true) => this;
    public RootBuilder IncludeInternals(bool on = true) => this;

    /// <summary>Make sequences of <typeparamref name="TElement"/> order-insensitive under this root.</summary>
    public RootBuilder OrderInsensitiveFor<TElement>(bool on = true) => this;

    /// <summary>Declare the stable key for <typeparamref name="TElement"/> used in unordered comparison/delta.</summary>
    public RootBuilder KeyFor<TElement>(Expression<Func<TElement, object?>> key) => this;

    /// <summary>Default comparison kind for <typeparamref name="T"/>.</summary>
    public RootBuilder ShallowFor<T>(bool on = true) => this;
    public RootBuilder ReferenceFor<T>(bool on = true) => this;

    /// <summary>Skip a specific member (e.g., <c>x =&gt; x.Fingerprint</c>).</summary>
    public RootBuilder Skip<T>(Expression<Func<T, object?>> selector) => this;

    /// <summary>Provide a comparer for a value-like type.</summary>
    public RootBuilder Comparer<T>(IEqualityComparer<T> comparer) => this;

    /// <summary>Enable dirty-tracking ([DeltaTrack]) so emitted deltas only include changed members.</summary>
    public RootBuilder TrackMutations(bool on = true) => this;
}