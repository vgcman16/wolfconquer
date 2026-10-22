namespace WolfConquer.Core.World;

// The shared native Pool owner, NOT a per-player roll cache. No first-Rate lazy initialization.
public static class LegacyNativePoolRandom
{
    public static DateTime ResetRandom = default;
    public static LegacyNativeSafeRandom GetRandom = new();

    // The actual legacy-game listen producer eagerly touches the shared service before accepting.
    // This does not reconstruct other native Pool static initializers/startup consumers.
    public static void InitializeAtServerStartup() =>
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(LegacyNativePoolRandom).TypeHandle);

    // Port ONLY ThreadPool.ServerFunctions' reset branch. The eventual real server-functions
    // producer must pass its already-sampled native local 'now', AFTER ExitRequested/FullLoading
    // admission and native preceding effects. Do NOT wire this to a per-login/per-player tick.
    // No scheduler is installed by this prerequisite; its first wake/60s cadence remain separate.
    public static void ResetFromServerFunctions(DateTime sampledNativeServerNow)
    {
        if (sampledNativeServerNow > ResetRandom)
        {
            GetRandom.SetSeed(Environment.TickCount);
            ResetRandom = DateTime.Now.AddMinutes(30.0);
        }
    }
}
