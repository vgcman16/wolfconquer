namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool IsLegacyNativeAttackPlayerCompletion(ushort spellId) =>
        spellId is 1_000 or 1_001;

    private static uint CompleteLegacyNativeAttackPlayerDamage(ushort spellId, uint damage) =>
        // Attack.cs1012/1056: identity CalculateSoul, then unsigned /2 AFTER Magic.OnPlayer.
        // No minimum-one clamp: an entered calculator result1 becomes a real Receive/Hit1 zero.
        IsLegacyNativeAttackPlayerCompletion(spellId) ? damage / 2U : damage;
}
