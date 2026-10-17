namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyFullAccountClassTargetLevel = 140;
    private const long LegacyFullAccountClassAttributeBonus = 900;
    private const MsgActionType LegacyFullAccountClassLevelAction = (MsgActionType)92;
    private const MsgActionType LegacyFullAccountClassRemoveSpellAction = (MsgActionType)109;
    private const string LegacyFullAccountClassError = "Error..!";

    private static async ValueTask ApplyLegacyAccountSecurityFullAccountClassAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!LegacyFullAccountClassGrants.TryGetValue(optionId, out var grant))
        {
            return;
        }

        if (session.Character.Class is < 0 or > uint.MaxValue ||
            session.Character.Reborn is < 0 or > byte.MaxValue ||
            session.Character.Level is < 0 or > ushort.MaxValue)
        {
            await LogLegacyFullAccountClassFenceAsync(context, optionId, grant, "class-gate-scalar-out-of-range")
                .ConfigureAwait(false);
            return;
        }

        if ((session.Character.Reborn != 0 && session.Character.Level != 1) ||
            IsLegacyFullAccountClassFamily(session.Character.Class, grant))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox(LegacyFullAccountClassError).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc AccountSecurity FullAccount class reset " +
                    $"{FormatSession(context)} source={LegacyAccountSecurityFullAccountSource} " +
                    $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId={optionId} " +
                    $"target={grant.Name} reason=source-gate error={LegacyFullAccountClassError}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await LogLegacyFullAccountClassFenceAsync(context, optionId, grant, "session-store-missing")
                .ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(session.UsersRoot))
        {
            await LogLegacyFullAccountClassFenceAsync(context, optionId, grant, "users-root-missing")
                .ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(context.PlayerSpellsRoot))
        {
            await LogLegacyFullAccountClassFenceAsync(context, optionId, grant, "player-spells-root-missing")
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.Level != LegacyFullAccountClassTargetLevel && context.WorldData is null)
        {
            await LogLegacyFullAccountClassFenceAsync(context, optionId, grant, "world-data-missing-for-update-level")
                .ConfigureAwait(false);
            return;
        }

        if (!TryCreateLegacyFullAccountClassProjection(
                context,
                session,
                grant,
                out var projection,
                out var projectionReason))
        {
            await LogLegacyFullAccountClassFenceAsync(context, optionId, grant, projectionReason)
                .ConfigureAwait(false);
            return;
        }

        var updatedSession = session with
        {
            Character = projection.UpdatedCharacter,
            PlayerSpells = projection.UpdatedSpells,
        };
        var characterValues = CreateLegacyFullAccountClassPersistenceValues(projection.UpdatedCharacter);

        cancellationToken.ThrowIfCancellationRequested();
        if (!TryPersistLegacyFullAccountClassMutation(
                context,
                updatedSession,
                characterValues,
                out var persistenceReason))
        {
            await LogLegacyFullAccountClassFenceAsync(context, optionId, grant, persistenceReason)
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        var uid = CheckedUInt32(projection.UpdatedCharacter.Uid, nameof(projection.UpdatedCharacter.Uid));

        foreach (var update in CreateLegacyFullAccountInitialStatUpdates(uid, projection.InitialCharacter))
        {
            await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        }

        foreach (var spellId in projection.RemovedSpellIds)
        {
            await context.Connection.SendAsync(
                    new MsgActionPacket
                    {
                        ObjId = uid,
                        DwParam = spellId,
                        Type = LegacyFullAccountClassRemoveSpellAction,
                    }.ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (projection.LevelChanged)
        {
            await context.Connection.SendAsync(
                    LegacyMsgUpdatePacket
                        .CreateLegacyRuntimeLevelUpdate(uid, checked((ulong)LegacyFullAccountClassTargetLevel))
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);

            await SendFrameToPlayerAndVisiblePeersAsync(
                    updatedContext,
                    context.Connection,
                    updatedSession,
                    new MsgActionPacket
                    {
                        ObjId = uid,
                        Type = LegacyFullAccountClassLevelAction,
                        PositionX = checked((uint)LegacyFullAccountClassTargetLevel),
                    }.ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);

            if (projection.SendLevelSourceStats)
            {
                foreach (var update in CreateLegacyFullAccountLevelSourceStatUpdates(uid, projection.LeveledCharacter))
                {
                    await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                }
            }
            else if (projection.LevelAttributePointsAdded != 0)
            {
                await context.Connection.SendAsync(
                        LegacyMsgUpdatePacket
                            .CreateLegacyRuntimeAttributePointsUpdate(
                                uid,
                                CheckedUInt64(
                                    projection.LeveledCharacter.AttributePoints,
                                    nameof(projection.LeveledCharacter.AttributePoints)))
                            .ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var preHealHitpoints = Math.Min(
                session.Character.MinHitPoints,
                projection.LeveledCharacter.MinHitPoints);
            var preHealSession = session with
            {
                Character = WithLegacyFullAccountClassAttributes(
                    projection.LeveledCharacter with { MinHitPoints = preHealHitpoints }),
            };
            await SendLegacyEquipmentRuntimeRefreshAsync(
                    context with { Session = preHealSession },
                    preHealSession,
                    cancellationToken)
                .ConfigureAwait(false);

            var maxHitpoints = CheckedUInt32(
                projection.LeveledCharacter.MinHitPoints,
                nameof(projection.LeveledCharacter.MinHitPoints));
            foreach (var update in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(
                         uid,
                         maxHitpoints,
                         maxHitpoints))
            {
                await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            }
        }

        await context.Connection.SendAsync(
                new LegacyMsgUpdatePacket(
                    uid,
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Class, grant.TargetClass)]).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var spell in projection.AddedSpellPackets)
        {
            await context.Connection.SendAsync(
                    LegacySpellPacket.FromSnapshot(spell).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AccountSecurity FullAccount class reset " +
                $"{FormatSession(updatedContext)} source={LegacyAccountSecurityFullAccountSource} " +
                $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId={optionId} " +
                $"target={grant.Name} class={session.Character.Class}->{grant.TargetClass} " +
                $"level={session.Character.Level}->{LegacyFullAccountClassTargetLevel} " +
                $"reborn={session.Character.Reborn}->2 attributes={session.Character.AttributePoints}->" +
                $"{projection.UpdatedCharacter.AttributePoints} explicitBonus={LegacyFullAccountClassAttributeBonus} " +
                $"levelBonus={projection.LevelAttributePointsAdded} clearSpells={grant.ClearExistingSpells} " +
                $"removed={projection.RemovedSpellIds.Count} addPackets={projection.AddedSpellPackets.Count} " +
                $"persistedSpells={projection.UpdatedSpells.Spells.Count} disconnected=true " +
                "wire=7899 packetOrder=Stats,RemoveSpell*,Level,Action92,LevelStatsOrAttributes," +
                "Equipment,Hitpoints,Class,Spell*,Disconnect.")
            .ConfigureAwait(false);
    }

    private static Task LogLegacyFullAccountClassFenceAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        LegacyFullAccountClassGrant grant,
        string reason)
    {
        return context.Log.WriteLineAsync(
            "PARITY-GAP LegacyNpc AccountSecurity FullAccount class reset " +
            $"{FormatSession(context)} source={LegacyAccountSecurityFullAccountSource} " +
            $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId={optionId} " +
            $"target={grant.Name} reason={reason}; no character, spell, session, packet, or disconnect mutation was committed.");
    }

}
