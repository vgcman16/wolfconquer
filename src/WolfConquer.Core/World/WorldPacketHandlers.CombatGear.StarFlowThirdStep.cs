namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<(WorldSession Session, LegacyPlayerMagicReceiveAbortReason AbortReason)>
        SendLegacyCombatGearStoneCrackerStarFlowTriggerAsync(
            WorldPacketHandlerContext context, WorldSession session, CancellationToken cancellationToken)
    {
        var uid = session.Character.Uid;
        var current = await RemoveLegacyArrowBladesFlagAsync(
                context, session, uid, LegacyArrowBladesFlagRegistration.StarFlowFlag, cancellationToken)
            .ConfigureAwait(false);
        if (current is null) return (session, LegacyPlayerMagicReceiveAbortReason.AttackerLost);
        current = await ReplaceLegacyArrowBladesFlagAsync(
                context with { Session = current }, current, uid,
                LegacyArrowBladesFlagRegistration.StarFlowTriggerFlag, cancellationToken).ConfigureAwait(false);
        if (current is null) return (session, LegacyPlayerMagicReceiveAbortReason.AttackerLost);

        // MyCombatGear constructs10950's query from CURRENT owner coordinates after both flag sends.
        var spell = await SendLegacyCombatGearStoneCrackerStarFlowSpellAsync(
                context with { Session = current }, current, cancellationToken).ConfigureAwait(false);
        if (spell.AbortReason != LegacyPlayerMagicReceiveAbortReason.None) return spell;
        var updatedSession = spell.Session;
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref updatedSession))
            return (updatedSession, LegacyPlayerMagicReceiveAbortReason.AttackerLost);
        // Source rereads StarFlow after ProcescMagic; callbacks/nested receives may already change it.
        if (ReadAttributeUInt32(updatedSession.Character.Attributes, RuntimeLegacyCombatGearStarFlowAttribute) == 4)
            updatedSession = WithUpdatedLegacyCombatGearAttackerAttribute(
                context with { Session = updatedSession }, updatedSession, RuntimeLegacyCombatGearStarFlowAttribute, 0);
        return (updatedSession, LegacyPlayerMagicReceiveAbortReason.None);
    }
}
