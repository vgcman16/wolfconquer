namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private readonly record struct LegacyNormalPlayerDeathCause(
        ushort X, ushort Y, long KillerUid, string KillerName);

    private static async ValueTask<LegacyEquipmentQueryPublicationResult> ExecuteLegacyNormalPlayerDeathPhasesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyEquipmentQueryPublicationScope scope,
        LegacyNativePlayerRuntime? nativePlayerReceiver,
        LegacyNormalPlayerDeathCause cause,
        CancellationToken cancellationToken)
    {
        var death = new LegacyEquipmentQueryExecution(context, session,
            new LegacyEquipmentQueryRequest(LegacyEquipmentRebuildReason.PlayerDeath, scope))
            { CancellationToken = cancellationToken, NormalDeathPlayerReceiver = nativePlayerReceiver };
        if (!death.Refresh()) return death.Current;

        // Source GhostStamp precedes DeadTalk; DeadStamp is a DISTINCT later read.
        var ghostAt = (context.TimeProvider ?? TimeProvider.System).GetUtcNow() + LegacyPlayerDeathState.GhostEffectDelay;
        if (!death.Commit(session => session with
            { Character = WithCharacterAttribute(session.Character, LegacyPlayerDeathState.GhostStampAttribute, ghostAt.ToString("O")) }))
            return death.Current;
        if (!await death.SendAsync(
                LegacyMsgMessagePacket.SystemTalk(LegacyPlayerDeathState.DeadTalkText).ToFrameBytes(),
                LegacyEquipmentQueryPublishedPhases.None).ConfigureAwait(false) ||
            !await death.RemoveNormalDeathFlagAsync(LegacyBlackNameStatusFlag).ConfigureAwait(false) ||
            !await death.RemoveNormalDeathFlagAsync(LegacyFatalStrikeFlag).ConfigureAwait(false))
            return death.Current;

        var deathAt = (context.TimeProvider ?? TimeProvider.System).GetUtcNow();
        if (!death.Commit(session => StageLegacyNormalDeathMetadata(session, cause,
                death.Current.RequiredParticipantSession?.Character, deathAt)))
            return death.Current;
        // ClearFlags tests XPList BEFORE removing registrations, then ALWAYS vectors.
        if (HasLegacyStatusFlag(death.Session.Character, LegacyRuntimeUpdateTicker.XpListFlag) &&
            !await death.SendAsync(LegacyMsgUpdatePacket.CreateLegacyRuntimeXPListUpdate(
                    CheckedUInt32(scope.OwnerUid, nameof(scope.OwnerUid)), value: 0).ToFrameBytes(),
                LegacyEquipmentQueryPublishedPhases.None).ConfigureAwait(false))
            return death.Current;
        if (!death.Commit(ClearLegacyNormalDeathRegistrations) ||
            !await death.SendNormalDeathVectorAsync().ConfigureAwait(false))
            return death.Current;

        var query = await ExecuteLegacyEquipmentQueryAsync(death.Context, death.Session,
                new LegacyEquipmentQueryRequest(LegacyEquipmentRebuildReason.PlayerDeath, scope), cancellationToken)
            .ConfigureAwait(false);
        if (!death.AdoptNormalDeathQuery(query)) return death.Current;
        if (!await death.AssignAsync(0).ConfigureAwait(false)) return death.Current;
        // HP0 setter/team/self pair COMPLETE before source AddFlag(Dead).
        if (!death.Commit(AddLegacyNormalDeathFlag) ||
            !await death.SendNormalDeathVectorAsync().ConfigureAwait(false) ||
            !await death.RemoveNormalDeathFlagAsync(LegacyFreezeFlag).ConfigureAwait(false) ||
            !await death.RemoveNormalDeathFlagAsync(LegacyTyrantAuraStatusFlag).ConfigureAwait(false) ||
            !await death.RemoveNormalDeathFlagAsync(LegacyFendAuraStatusFlag).ConfigureAwait(false))
            return death.Current;

        return death.Current;
    }
}
