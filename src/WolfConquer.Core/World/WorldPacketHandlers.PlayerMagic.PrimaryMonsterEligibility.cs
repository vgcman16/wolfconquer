namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool PassesLegacyRootMonsterPrimaryEligibility(
        WorldPacketHandlerContext context, WorldSession expected, MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell, out string reason)
    {
        reason = string.Empty;
        if (spell.Sort is not (LegacyMagicSort.Attack or LegacyMagicSort.DirectAttack)) return true;
        // Preserve native raw-map Monster -> Player -> SOB resolution priority.
        // Area spells may discard a protected recipient and hit other targets;
        // direct monster spells require their selected primary to be eligible.
        if (FindLegacyHellVortexSelectedMonster(context, expected, query.OpponentUid) is null) return true;
        return PassesLegacyPlayerMonsterMagicPreResourceRangeGate(context, expected, query, spell, out reason);
    }
}
