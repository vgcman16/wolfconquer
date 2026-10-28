namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyFullAccountClassTargetLevel = 140;
    private const long LegacyFullAccountClassAttributeBonus = 900;
    private const MsgActionType LegacyFullAccountClassLevelAction = (MsgActionType)92;
    private const MsgActionType LegacyFullAccountClassRemoveSpellAction = (MsgActionType)109;
    private const string LegacyFullAccountClassError = "Error..!";

    private static async ValueTask ApplyLegacyAccountSecurityFullAccountClassAsync(
        WorldPacketHandlerContext context, WorldSession session, byte optionId, CancellationToken cancellationToken)
    {
        if (!LegacyFullAccountClassGrants.TryGetValue(optionId, out var grant)) return;
        cancellationToken.ThrowIfCancellationRequested();
        if (session.Character is null)
        { await context.Log.WriteLineAsync("WorldServer refused FullAccount class reset: character state is missing.").ConfigureAwait(false); return; }
        if (session.Character.Class is < 0 or > uint.MaxValue || session.Character.Reborn is < 0 or > byte.MaxValue ||
            session.Character.Level is < 0 or > ushort.MaxValue)
        { await LogLegacyFullAccountClassFenceAsync(context, optionId, grant, "class-gate-scalar-out-of-range").ConfigureAwait(false); return; }
        if ((session.Character.Reborn != 0 && session.Character.Level != 1) || IsLegacyFullAccountClassFamily(session.Character.Class, grant))
        {
            await context.Connection.SendAsync(LegacyNpcReplyPacket.ForMessageBox(LegacyFullAccountClassError).ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync("WorldServer skipped LegacyNpc AccountSecurity FullAccount class reset " +
                $"{FormatSession(context)} source={LegacyAccountSecurityFullAccountSource} sourceLines={LegacyAccountSecurityFullAccountSourceLines} " +
                $"optionId={optionId} target={grant.Name} reason=source-gate error={LegacyFullAccountClassError}.").ConfigureAwait(false);
            return;
        }

        LegacyFullAccountClassPrepared? prepared = null;
        var committed = false;
        var recoveryRequired = false;
        var reason = "fullAccountClassOwnerUnavailable";
        if (context.Trades is { } trades && context.Sessions is { } sessions)
        {
            // Frame owns its world-operation lease. Follow the actual trade
            // writer order: party -> Store -> actual actor/PIN -> file paths.
            lock (trades.SyncRoot)
            {
                try
                {
                    if (LegacyFullAccountClassOwner.TryCapture(context, session, out var owner, out reason))
                    {
                        prepared = new(owner!, grant);
                        var currentPlan = prepared;
                        reason = "fullAccountClassCurrentSessionChanged";
                        committed = sessions.TryCommitSession(context.Connection, session, currentPlan.Updated,
                            () => currentPlan.TryPersist(cancellationToken, out reason), out var final);
                        if (committed) currentPlan.Updated = final!;
                        recoveryRequired = prepared.DurableCommitted || reason.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal);
                        if (!committed && recoveryRequired)
                            owner!.Recovery.Block(LegacyTradeJournal.RecoveryRequired + ":fullAccountClassPublicationUnproven:" + reason);
                    }
                }
                catch (LegacyCommittedSpellPublicationException)
                {
                    recoveryRequired = true;
                    reason = "ordinarySpellRecoveryRequired:fullAccountClassPublicationUnproven";
                    context.TradeRecovery?.Block(reason);
                }
                catch (Exception error) when (IsLegacyFullAccountClassPreparationFailure(error))
                {
                    recoveryRequired = prepared?.DurableCommitted == true ||
                        reason.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal);
                    reason = !recoveryRequired && error is FullAccountClassRefusal refusal ? refusal.Message :
                        (recoveryRequired ? LegacyTradeJournal.RecoveryRequired + ":fullAccountClass:" : "fullAccountClassPreparation:") + error.GetType().Name;
                    if (recoveryRequired) context.TradeRecovery?.Block(reason);
                }
            }
        }
        else if (context.Sessions is null) reason = "session-store-missing";

        if (!committed || prepared is null)
        {
            if (recoveryRequired)
                await context.Log.WriteLineAsync("WorldServer held LegacyNpc AccountSecurity FullAccount class reset for recovery " +
                    $"{FormatSession(context)} optionId={optionId} target={grant.Name} reason={reason}; durable outcome is not an unchanged rejection.")
                    .ConfigureAwait(false);
            else await LogLegacyFullAccountClassFenceAsync(context, optionId, grant, reason).ConfigureAwait(false);
            return;
        }

        // No precommit catch covers transport/close/log work. A failure here
        // retains the committed files and current session.
        if (!await prepared.PublishAsync(cancellationToken).ConfigureAwait(false))
        {
            await context.Log.WriteLineAsync($"WorldServer committed FullAccount class reset; publication retired optionId={optionId} disconnected=false.")
                .ConfigureAwait(false);
            return;
        }
        await context.Log.WriteLineAsync("WorldServer applied LegacyNpc AccountSecurity FullAccount class reset " +
            $"{FormatSession(context with { Session = prepared.Updated })} source={LegacyAccountSecurityFullAccountSource} " +
            $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId={optionId} target={grant.Name} " +
            $"class={session.Character.Class}->{grant.TargetClass} level={session.Character.Level}->{LegacyFullAccountClassTargetLevel} " +
            $"reborn={session.Character.Reborn}->2 attributes={session.Character.AttributePoints}->{prepared.Projection.UpdatedCharacter.AttributePoints} " +
            $"explicitBonus={LegacyFullAccountClassAttributeBonus} levelBonus={prepared.Projection.LevelAttributePointsAdded} " +
            $"clearSpells={grant.ClearExistingSpells} removed={prepared.Projection.RemovedSpellIds.Count} " +
            $"addPackets={prepared.Projection.AddedSpellPackets.Count} persistedSpells={prepared.Projection.UpdatedSpells.Spells.Count} " +
            "disconnected=true wire=7899 packetOrder=Stats,RemoveSpell*,Level,Action92,LevelStatsOrAttributes,Equipment,Hitpoints,Class,Spell*,Disconnect.")
            .ConfigureAwait(false);
    }

    private static bool IsLegacyFullAccountClassPreparationFailure(Exception error) => error is IOException or
        UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidOperationException or
        FormatException or OverflowException or System.Security.SecurityException;

    private static Task LogLegacyFullAccountClassFenceAsync(WorldPacketHandlerContext context, byte optionId,
        LegacyFullAccountClassGrant grant, string reason) => context.Log.WriteLineAsync(
            "PARITY-GAP LegacyNpc AccountSecurity FullAccount class reset " +
            $"{FormatSession(context)} source={LegacyAccountSecurityFullAccountSource} sourceLines={LegacyAccountSecurityFullAccountSourceLines} " +
            $"optionId={optionId} target={grant.Name} reason={reason}; no character, spell, session, packet, or disconnect mutation was committed.");
}
