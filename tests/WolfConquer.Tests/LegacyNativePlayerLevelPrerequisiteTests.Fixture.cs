using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativePlayerLevelPrerequisiteTests
{
    private sealed class Scope : IDisposable
    {
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime oldReset = LegacyNativePoolRandom.ResetRandom;
        internal LegacyNativeSafeRandom Actual { get; } = new LegacyNativeSafeRandom(71);
        internal LegacyNativeSafeRandom Control { get; } = new LegacyNativeSafeRandom(71);
        internal DateTime ResetMarker { get; } = new DateTime(2040, 5, 6, 7, 8, 9, DateTimeKind.Utc);

        internal Scope()
        {
            // Actual native RNG instances with independent identical seeds; no
            // forced draw provider, clock injection or private PRNG-state access.
            LegacyNativePoolRandom.GetRandom = Actual;
            LegacyNativePoolRandom.ResetRandom = ResetMarker;
        }

        public void Dispose()
        {
            LegacyNativePoolRandom.GetRandom = oldRandom;
            LegacyNativePoolRandom.ResetRandom = oldReset;
        }
    }
}
