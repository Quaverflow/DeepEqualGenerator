namespace DeepEqual.Generator.Shared;

/// <summary> Marker for any diff payload. </summary>
public interface IDiff
{
    bool IsEmpty { get; }
}