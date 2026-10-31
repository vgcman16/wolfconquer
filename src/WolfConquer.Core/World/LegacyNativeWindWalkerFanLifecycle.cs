namespace WolfConquer.Core.World;

// Only194, layered after the existing native flag6 projection at its real
// publication consumers. It cannot infer a registration from saved aliases.
internal static class LegacyNativeWindWalkerFanLifecycle
{
    internal static ulong[] CaptureVector(LegacyNativePlayerRuntime? player, IReadOnlyList<ulong> words) =>
        WorldSessionStore.CaptureWindWalkerVector(player, words);
}
