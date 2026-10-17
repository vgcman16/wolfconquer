using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static uint CalculateLegacyDirectPlayerPhysicalDisplayedDamage(
        WorldSession session,
        WorldDataCatalog? worldData,
        uint preparedDamage)
    {
        // MsgAttackPacket.cs:645-659 scales only the incoming interaction display;
        // the prepared Physical.OnPlayer damage still enters ReceiveAttack unchanged.
        var displayedDamage = MulDivToUInt32(preparedDamage, 22, 27);
        if (!HasLegacyDirectPlayerPhysicalFuryStrike(session, worldData))
        {
            return displayedDamage;
        }

        // Player.FuryStrikeAttack is a byte, populated by IronGuard from Spell.Damage.
        var furyStrikeAttack = unchecked((byte)ReadAttributeUInt32(
            session.Character.Attributes,
            RuntimeLegacyIronGuardFuryStrikeAttackAttribute));
        var boostedDamage = (ulong)displayedDamage + MulDivToUInt32(displayedDamage, furyStrikeAttack, 100);
        return boostedDamage > uint.MaxValue ? uint.MaxValue : (uint)boostedDamage;
    }

    private static bool HasLegacyDirectPlayerPhysicalFuryStrike(
        WorldSession session,
        WorldDataCatalog? worldData)
    {
        return HasLegacyStatusFlag(session.Character, LegacyIronGuardFlag) &&
            worldData is not null &&
            TryGetLegacyEligibleEquippedRuneLevel(session, worldData, "FuryStrike", out _);
    }
}
