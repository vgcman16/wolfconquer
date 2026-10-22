using WolfConquer.Core.World;
using NativeFlag = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32.Flag;
using NativeVector = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

// Explicit test-only MODEL operations, not Player methods, production clock adapters,
// earned Team leadership, or proof of the native acquisition/scheduler clock.
internal static class NativeFlag6TestModel
{
    internal const int Flag = 6;
    internal const int NativeSeconds = 2_592_000;

    internal static NativeFlag? Current(LegacyNativePlayerRuntime player) => Current(player.BitVector);
    internal static NativeFlag? Current(NativeVector vector) =>
        vector.ArrayFlags.TryGetValue(Flag, out var row) ? row : null;

    internal static bool Visible(LegacyNativePlayerRuntime player) => player.BitVector.Contain(Flag);

    internal static bool Seed(LegacyNativePlayerRuntime player, DateTime modelTimer) =>
        Seed(player.BitVector, modelTimer);

    internal static bool Seed(NativeVector vector, DateTime modelTimer)
    {
        // Genuine native allocation/publication first; then set the public Timer as fixture prestate.
        // A duplicate does not rewrite the existing row, timer, seconds, policy or visible bit.
        if (!vector.TryAdd(Flag, NativeSeconds, false, 0)) return false;
        vector.ArrayFlags[Flag].Timer = modelTimer;
        return true;
    }

    internal static bool AddFlagFixture(LegacyNativePlayerRuntime player, DateTime modelTimer)
    {
        // MODEL of the outer Player.AddFlag containment and ignored inner result.
        if (player.BitVector.ContainFlag(Flag)) return false;
        _ = Seed(player, modelTimer);
        return true;
    }

    internal static bool RemoveFlagFixture(LegacyNativePlayerRuntime player)
    {
        // MODEL of the outer Player.RemoveFlag containment and ignored inner result.
        if (!player.BitVector.ContainFlag(Flag)) return false;
        _ = player.BitVector.TryRemove(Flag);
        return true;
    }

    internal static void ClearDeathRemovableFixture(LegacyNativePlayerRuntime player) =>
        player.BitVector.GetClear();

    internal static NativeFlag? CaptureBuffersRegistrationFixture(LegacyNativeGameClientRuntime client) =>
        client.Player.BitVector.GetFlags().FirstOrDefault(row => row.Key == Flag);

    internal static bool ConsumeBuffersExpiryFixture(
        LegacyNativeGameClientRuntime client, NativeFlag? capturedRegistration, DateTime sampledModelNow) =>
        capturedRegistration is not null && capturedRegistration.Expire(sampledModelNow) &&
        RemoveFlagFixture(client.Player);
}
