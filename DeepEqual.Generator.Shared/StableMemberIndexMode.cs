namespace DeepEqual.Generator.Shared;

/// <summary>
///     Controls whether member indices used in deltas are stable across builds.
/// </summary>
public enum StableMemberIndexMode
{
    Auto = 0,
    On = 1,
    Off = 2
}