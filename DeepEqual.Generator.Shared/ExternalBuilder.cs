using System;

namespace DeepEqual;

/// <summary>Configure types you don't own. No runtime behavior.</summary>
public sealed class ExternalBuilder
{
    internal ExternalBuilder(Type externalRoot) { }

    public ExternalBuilder AdoptAsRoot(bool generateDelta = true, bool generateDiff = true) => this;
    public ExternalBuilder GenerateDelta(bool on = true) => this;
    public ExternalBuilder GenerateDiff(bool on = true) => this;

    public PathBuilder ForPath(string path) => new(path);

    public sealed class PathBuilder
    {
        internal PathBuilder(string path) { }
        public ExternalBuilder AsKey() => new ExternalBuilder(typeof(object));
        public PathBuilder OrderInsensitive(bool on = true) => this;
        public PathBuilder Shallow(bool on = true) => this;
        public ExternalBuilder Skip() => new ExternalBuilder(typeof(object));
    }
}