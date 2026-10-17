using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryExecuteLegacyDirectPhysicalCombatGearSpellAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Id == LegacyCombatGearDragonhowlTripleAttackSpellId)
        {
            return await ExecuteLegacyPreparedCombatGearTriple17490Async(
                    context, session, query, playerSpell, spell, cancellationToken)
                .ConfigureAwait(false);
        }

        if (spell.Id == 17_650)
        {
            return await ExecuteLegacyPreparedCombatGearIronbone17650Async(
                    context, session, query, playerSpell, spell, cancellationToken)
                .ConfigureAwait(false);
        }

        if (spell.Id == LegacyCombatGearStoneCrackerWaterAttackSpellId)
        {
            return await ExecuteLegacyPreparedCombatGearArrowBlades18220Async(
                    context, session, query, playerSpell, spell, cancellationToken)
                .ConfigureAwait(false);
        }

        if (IsLegacyCombatGearDirectLineAttackSpell(spell.Id))
        {
            return await ExecuteLegacyPreparedCombatGearDirectLineAttackMagicAsync(
                    context, session, query, playerSpell, spell, cancellationToken)
                .ConfigureAwait(false);
        }

        if (IsLegacyCombatGearWidthLineAttackSpell(spell.Id))
        {
            return await ExecuteLegacyPreparedCombatGearWidthLineAttackMagicAsync(
                    context, session, query, playerSpell, spell, cancellationToken)
                .ConfigureAwait(false);
        }

        if (IsLegacyCombatGearCenteredAreaAttackSpell(spell.Id))
        {
            return await ExecuteLegacyPreparedCombatGearCenteredAreaAttackMagicAsync(
                    context, session, query, playerSpell, spell, cancellationToken)
                .ConfigureAwait(false);
        }

        return await TryHandleLegacyCombatGearPirateFloorPassiveAsync(
                context, session, query, playerSpell, spell, cancellationToken)
            .ConfigureAwait(false);
    }
}
