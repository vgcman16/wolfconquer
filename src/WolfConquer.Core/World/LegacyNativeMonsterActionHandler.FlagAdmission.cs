namespace WolfConquer.Core.World;

internal partial class LegacyNativeMonsterActionHandler
{
    private static bool TargetIsFlying(LegacyNativePlayerRuntime? player)
    {
        if (player?.BitVector is not { } flags ||
            !flags.TryContainFlag((int)LegacyNativeMsgUpdate.Flags.Fly, out bool present))
            throw new InvalidDataException("Monster target flight status is unavailable.");
        return present;
    }

    private static void InvalidateUnavailableTarget(LegacyNativeMonsterRole monster)
    {
        // This is deliberate fail-closed cleanup, not an unchanged rejected selection.
        monster.Target = null;
        monster.State = LegacyNativeMobStatus.Idle;
    }
}
