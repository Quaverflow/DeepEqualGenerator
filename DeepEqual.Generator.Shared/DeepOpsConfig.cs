namespace DeepEqual;

/// <summary>
/// The user creates this type in their project and places all fluent calls inside <see cref="Configure"/>.
/// </summary>
public static partial class DeepOpsConfig
{
    /// <summary>
    /// The generator will find this method and statically analyze the fluent calls.
    /// This method is never executed at runtime.
    /// </summary>
    public static void Configure(DeepOpsBuilder b) { }
}