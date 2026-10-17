namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<LegacyEquipmentQueryPublicationResult> InterruptLegacyPhysicalMonsterSitAsync(
        WorldPacketHandlerContext context, WorldSession session, LegacyEquipmentQueryPublicationScope scope,
        uint damage, CancellationToken cancellationToken)
    {
        var execution = new LegacyEquipmentQueryExecution(context, session,
            new LegacyEquipmentQueryRequest(LegacyEquipmentRebuildReason.PlayerDeath, scope))
            { CancellationToken = cancellationToken };
        if (!execution.Refresh() || !IsLegacyConquerActionSit(execution.Session.Character))
            return execution.Current;
        var stamina = ReadLegacyRuntimeStamina(execution.Session.Character);
        var remaining = stamina >= 20 ? stamina - 20 : 0;
        if (!execution.Commit(current => current with
            { Character = WithCharacterAttribute(current.Character, "Stamina", remaining) }) ||
            !await execution.SendAsync(LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(
                    CheckedUInt32(scope.OwnerUid, nameof(scope.OwnerUid)), remaining).ToFrameBytes(),
                LegacyEquipmentQueryPublishedPhases.None).ConfigureAwait(false))
            return execution.Current;
        // Source sets Action AFTER the stamina publication, on CURRENT state.
        if (!execution.Commit(current => current with
            { Character = WithCharacterAttribute(current.Character, "Action", LegacyConquerActionNone) }))
            return execution.Current;
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy monster hit sit interruption " +
                $"{FormatSession(execution.Context)} damage={damage} " +
                $"stamina={stamina} remainingStamina={remaining}; " +
                "source=MsgMonster.ActionHandler.CheckForOponnentDead Sit.")
            .ConfigureAwait(false);
        execution.Refresh();
        return execution.Current;
    }
}
