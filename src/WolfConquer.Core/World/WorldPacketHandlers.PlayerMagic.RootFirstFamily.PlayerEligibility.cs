namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool PassesLegacyRootPlayerBasicEligibility(
        WorldPacketHandlerContext context, WorldSession expected, MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell, out string reason)
    {
        reason = string.Empty;
        if (spell.Sort != LegacyMagicSort.Attack || spell.Id is not (1_000 or 1_001)) return true;
        if (context.Sessions is not { } world) return false;
        var player = FindLegacyMapBlockPlayer(world, expected, query.OpponentUid);
        // A non-player target has a different actual executor/permission owner.
        // This is not an admission result for monsters, SOBs or area shapes.
        if (player is null) return true;
        if (CanLegacyPlayerMagicBasicEligibility(
                expected, player.Session, spell, out _, out var failure, out _)) return true;
        reason = $"playerUid={query.OpponentUid}; {failure}";
        return false;
    }
}
