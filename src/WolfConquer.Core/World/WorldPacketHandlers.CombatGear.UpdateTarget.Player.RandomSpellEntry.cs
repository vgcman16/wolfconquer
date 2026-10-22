namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Only the existing UpdateTarget DAMAGE caller reaches this selected native prefix.
    // WeaponSpells' native loader excludes both20220 and20360, so CanUse does not reset this value.
    private static WorldSession? TryAssignLegacyNestedDamageRandomSpellEntry(
        WorldPacketHandlerContext context, WorldSession session, ushort selectedBookId)
    {
        if (context.Sessions is not { } world)
            return session with { LegacyRandomSpell = selectedBookId };

        var uid = session.Character.Uid;
        if (!LegacyNativeClientPublication.TryCaptureIdentity(
                world, context.Connection, uid, out var identity) ||
            !LegacyNativeClientPublication.TryUpdate(world, context.Connection, uid, identity,
                current => current with { LegacyRandomSpell = selectedBookId }, out var updated))
            return null;
        return updated;
    }
}
