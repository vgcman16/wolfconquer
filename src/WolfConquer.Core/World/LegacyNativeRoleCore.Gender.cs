namespace WolfConquer.Core.World;

internal static partial class LegacyNativeRoleCore
{
    // Entire native Core14..22. These are exact body IDs, not an inferred general gender classifier.
    public static bool IsBoy(uint mesh) { return mesh == 1008; }
    public static bool IsGirl(uint mesh) { return mesh == 2007; }
}
