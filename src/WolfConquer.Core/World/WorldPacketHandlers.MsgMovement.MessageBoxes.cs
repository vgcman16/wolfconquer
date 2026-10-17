namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask SendAlienWorldLeavePromptIfNeededAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var character = session.Character;
        if (character.MapId != LegacyAlienWorldMapId ||
            LegacyGetDistance(character.X, character.Y, LegacyAlienWorldLeaveX, LegacyAlienWorldLeaveY) > 2)
        {
            return;
        }

        var updated = session with
        {
            Character = WithCharacterAttribute(
                character,
                RuntimePendingMessageBoxAttribute,
                RuntimeAlienWorldLeaveMessageBox),
        };
        context.Sessions?.Set(context.Connection, updated);

        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox(LegacyAlienWorldLeavePrompt).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent alien-world leave message box " +
                $"{FormatSession(context)} x={character.X} y={character.Y}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyMessageBoxReplyAsync(
        WorldPacketHandlerContext context,
        LegacyNpcDialogRequestPacket packet,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyNpcReply message box {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var pending = ReadAttributeString(session.Character.Attributes, RuntimePendingMessageBoxAttribute);
        if (pending.Equals(LegacyPlayerDeathState.RevivePromptPendingKey, StringComparison.Ordinal))
        {
            await HandleDeathReviveMessageBoxReplyAsync(context, session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (pending.Equals(RuntimeAlienWorldLeaveMessageBox, StringComparison.Ordinal))
        {
            await HandleAlienWorldLeaveMessageBoxReplyAsync(context, session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (pending.Equals(LegacyRuntimeMessageBoxPrompt.DragonWarInvitationKey, StringComparison.Ordinal))
        {
            await HandleLegacyDragonWarMessageBoxReplyAsync(context, session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (pending.Equals(LegacyRuntimeMessageBoxPrompt.FreezeWarInvitationKey, StringComparison.Ordinal))
        {
            await HandleLegacyFreezeWarInvitationMessageBoxReplyAsync(context, session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (IsRuntimeTopInHoursInvitationMessageBox(pending))
        {
            await HandleTopInHoursInvitationMessageBoxReplyAsync(
                    context,
                    session,
                    packet,
                    pending,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (IsRuntimeLegacyClanWarCurrentInvitationMessageBox(pending))
        {
            await HandleLegacyClanWarCurrentInvitationMessageBoxReplyAsync(
                    context,
                    session,
                    packet,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (pending.Equals(RuntimeThrillingSpookMessageBox, StringComparison.Ordinal))
        {
            await HandleThrillingSpookMessageBoxReplyAsync(context, session, packet)
                .ConfigureAwait(false);
            return;
        }

        if (pending.Equals(RuntimeLegacyDragonYuKoonDeitylandMessageBox, StringComparison.Ordinal))
        {
            await HandleLegacyDragonYuKoonMessageBoxReplyAsync(context, session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpcReply message box " +
                $"{FormatSession(context)} pending={pending}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleDeathReviveMessageBoxReplyAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyNpcDialogRequestPacket packet,
        CancellationToken cancellationToken)
    {
        var clearedSession = session with
        {
            Character = WithoutLegacyDeathRevivePromptAttributes(session.Character),
        };
        context.Sessions?.Set(context.Connection, clearedSession);

        if (!packet.IsAcceptedMessageBox)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer canceled death revive message box " +
                    $"{FormatSession(context)} option={packet.OptionId}.")
                .ConfigureAwait(false);
            return;
        }

        var reviveContext = context with { Session = clearedSession };
        await HandleReviveAsync(
                reviveContext,
                new MsgActionPacket
                {
                    ObjId = CheckedUInt32(clearedSession.Character.Uid, nameof(clearedSession.Character.Uid)),
                    Type = MsgActionType.Revive,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleAlienWorldLeaveMessageBoxReplyAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyNpcDialogRequestPacket packet,
        CancellationToken cancellationToken)
    {
        if (packet.IsAcceptedMessageBox)
        {
            var preflight = await PrepareLegacyTeleportAsync(
                    context,
                    session,
                    new LegacyTeleportRequest(
                        "MsgMovement.Movement alien-world MessageBox callback -> GameClient.Teleport",
                        LegacyTwinCityMapId),
                    cancellationToken)
                .ConfigureAwait(false);
            session = preflight.Session;
            if (!preflight.Allowed)
            {
                var deniedSession = session with
                {
                    Character = WithoutRuntimeMessageBoxAttributes(session.Character),
                };
                context.Sessions?.Set(context.Connection, deniedSession);
                return;
            }
        }

        var clearedSession = session with
        {
            Character = WithoutRuntimeMessageBoxAttributes(session.Character),
        };
        if (packet.IsAcceptedMessageBox)
        {
            var appliedSession = await ExecuteLegacyTeleportDestinationAsync(
                    context with { Session = clearedSession },
                    clearedSession,
                    new LegacyTeleportDestination(
                        LegacyTwinCityMapId,
                        LegacyTwinCityReturnX,
                        LegacyTwinCityReturnY),
                    timestamp: 0,
                    cancellationToken,
                    expectedSession: session)
                .ConfigureAwait(false);
            if (appliedSession is null)
            {
                return;
            }

            await context.Log.WriteLineAsync(
                    "WorldServer accepted alien-world leave message box " +
                    $"{FormatSession(context with { Session = appliedSession })} " +
                    $"map={LegacyTwinCityMapId} x={LegacyTwinCityReturnX} y={LegacyTwinCityReturnY}.")
                .ConfigureAwait(false);
            return;
        }

        context.Sessions?.Set(context.Connection, clearedSession);
        await context.Log.WriteLineAsync(
                "WorldServer canceled alien-world leave message box " +
                $"{FormatSession(context)} option={packet.OptionId}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyDragonWarMessageBoxReplyAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyNpcDialogRequestPacket packet,
        CancellationToken cancellationToken)
    {
        var clearedSession = session with
        {
            Character = WithoutRuntimeMessageBoxAttributes(session.Character),
        };

        if (!packet.IsAcceptedMessageBox)
        {
            context.Sessions?.Set(context.Connection, clearedSession);
            await context.Log.WriteLineAsync(
                    "WorldServer canceled legacy DragonWar invitation message box " +
                    $"{FormatSession(context)} option={packet.OptionId}.")
                .ConfigureAwait(false);
            return;
        }

        var preflight = await PrepareLegacyTeleportAsync(
                context,
                session,
                new LegacyTeleportRequest(
                    "MsgDragonWar.SendInvitation MessageBox callback -> GameClient.Teleport",
                    LegacyDragonWarRuntimeStateStore.DragonWarMapId),
                cancellationToken)
            .ConfigureAwait(false);
        clearedSession = preflight.Session with
        {
            Character = WithoutRuntimeMessageBoxAttributes(preflight.Session.Character),
        };
        if (!preflight.Allowed)
        {
            context.Sessions?.Set(context.Connection, clearedSession);
            return;
        }

        if (context.DragonWarStates is { } dragonWarStates &&
            dragonWarStates.TryJoin(clearedSession, out var joinedSession))
        {
            var dynamicId = ReadCharacterDynamicId(joinedSession.Character);
            var teleportSourceSession = CreateLegacyTeleportMutationSourceSession(
                clearedSession,
                joinedSession);
            var appliedSession = await ExecuteLegacyTeleportDestinationAsync(
                    context with { Session = teleportSourceSession },
                    teleportSourceSession,
                    new LegacyTeleportDestination(
                        joinedSession.Character.MapId,
                        joinedSession.Character.X,
                        joinedSession.Character.Y,
                        dynamicId),
                    timestamp: 0,
                    cancellationToken,
                    expectedSession: preflight.Session)
                .ConfigureAwait(false);
            if (appliedSession is null)
            {
                return;
            }

            var joinedContext = context with { Session = appliedSession };
            await context.Log.WriteLineAsync(
                    "WorldServer accepted legacy DragonWar invitation message box " +
                    $"{FormatSession(joinedContext)} map={LegacyDragonWarRuntimeStateStore.DragonWarMapId} dynamicId={dynamicId}.")
                .ConfigureAwait(false);
            return;
        }

        context.Sessions?.Set(context.Connection, clearedSession);
        await context.Log.WriteLineAsync(
                "WorldServer skipped legacy DragonWar invitation join " +
                $"{FormatSession(context)}; DragonWar state is unavailable or not accepting joins.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleTopInHoursInvitationMessageBoxReplyAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyNpcDialogRequestPacket packet,
        string pending,
        CancellationToken cancellationToken)
    {
        var sourceCharacter = session.Character;
        var hasDestination = TryReadPendingMessageBoxDestination(
            sourceCharacter.Attributes,
            out var mapId,
            out var x,
            out var y) ||
            TryReadTopInHoursPendingDestination(pending, out mapId, out x, out y);
        var clearedSession = session with
        {
            Character = WithoutRuntimeMessageBoxAttributes(sourceCharacter),
        };

        if (packet.IsAcceptedMessageBox && hasDestination)
        {
            var preflight = await PrepareLegacyTeleportAsync(
                    context,
                    session,
                    new LegacyTeleportRequest(
                        "MsgSchedules.SendInvitation TopInHours MessageBox callback -> GameClient.Teleport",
                        mapId),
                    cancellationToken)
                .ConfigureAwait(false);
            session = preflight.Session;
            sourceCharacter = session.Character;
            clearedSession = session with
            {
                Character = WithoutRuntimeMessageBoxAttributes(sourceCharacter),
            };
            if (!preflight.Allowed)
            {
                context.Sessions?.Set(context.Connection, clearedSession);
                return;
            }
        }

        if (packet.IsAcceptedMessageBox && hasDestination)
        {
            var appliedSession = await ExecuteLegacyTeleportDestinationAsync(
                    context with { Session = clearedSession },
                    clearedSession,
                    new LegacyTeleportDestination(mapId, x, y),
                    timestamp: 0,
                    cancellationToken,
                    expectedSession: session)
                .ConfigureAwait(false);
            if (appliedSession is null)
            {
                return;
            }

            await context.Log.WriteLineAsync(
                    "WorldServer accepted TopInHours invitation message box " +
                    $"{FormatSession(context with { Session = appliedSession })} map={mapId} x={x} y={y}.")
                .ConfigureAwait(false);
            return;
        }

        context.Sessions?.Set(context.Connection, clearedSession);
        if (packet.IsAcceptedMessageBox)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped TopInHours invitation teleport " +
                    $"{FormatSession(context)}; destination attributes are missing.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer canceled TopInHours invitation message box " +
                $"{FormatSession(context)} option={packet.OptionId}.")
                .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyFreezeWarInvitationMessageBoxReplyAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyNpcDialogRequestPacket packet,
        CancellationToken cancellationToken)
    {
        var sourceCharacter = session.Character;
        var hasDestination = TryReadPendingMessageBoxDestination(
            sourceCharacter.Attributes,
            out var mapId,
            out var x,
            out var y);
        var dynamicId = checked((long)(context.FreezeWarStates?.DynamicId ?? 0));
        if (TryReadInt64Attribute(
                sourceCharacter.Attributes,
                RuntimePendingMessageBoxDynamicIdAttribute,
                out var pendingDynamicId) &&
            pendingDynamicId >= 0)
        {
            dynamicId = pendingDynamicId;
        }
        var clearedSession = session with
        {
            Character = WithoutRuntimeMessageBoxAttributes(sourceCharacter),
        };

        if (packet.IsAcceptedMessageBox && hasDestination)
        {
            var preflight = await PrepareLegacyTeleportAsync(
                    context,
                    session,
                    new LegacyTeleportRequest(
                        "MsgSchedules.SendInvitation FreezeWar MessageBox callback -> GameClient.Teleport",
                        mapId,
                        dynamicId),
                    cancellationToken)
                .ConfigureAwait(false);
            session = preflight.Session;
            sourceCharacter = session.Character;
            clearedSession = session with
            {
                Character = WithoutRuntimeMessageBoxAttributes(sourceCharacter),
            };
            if (!preflight.Allowed)
            {
                context.Sessions?.Set(context.Connection, clearedSession);
                return;
            }
        }

        if (packet.IsAcceptedMessageBox && hasDestination)
        {
            var appliedSession = await ExecuteLegacyTeleportDestinationAsync(
                    context with { Session = clearedSession },
                    clearedSession,
                    new LegacyTeleportDestination(mapId, x, y, checked((ulong)dynamicId)),
                    timestamp: 0,
                    cancellationToken,
                    expectedSession: session)
                .ConfigureAwait(false);
            if (appliedSession is null)
            {
                return;
            }

            await context.Log.WriteLineAsync(
                    "WorldServer accepted legacy FreezeWar invitation message box " +
                    $"{FormatSession(context with { Session = appliedSession })} map={mapId} x={x} y={y}; " +
                    "source=MsgSchedules.SendInvitation -> MessageBox(Teleport).")
                .ConfigureAwait(false);
            return;
        }

        context.Sessions?.Set(context.Connection, clearedSession);
        if (packet.IsAcceptedMessageBox)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy FreezeWar invitation teleport " +
                    $"{FormatSession(context)}; destination attributes are missing.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer canceled legacy FreezeWar invitation message box " +
                $"{FormatSession(context)} option={packet.OptionId}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyClanWarCurrentInvitationMessageBoxReplyAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyNpcDialogRequestPacket packet,
        CancellationToken cancellationToken)
    {
        var sourceCharacter = session.Character;
        var hasDestination = TryReadPendingMessageBoxDestination(
            sourceCharacter.Attributes,
            out var mapId,
            out var x,
            out var y);
        var clearedSession = session with
        {
            Character = WithoutRuntimeMessageBoxAttributes(sourceCharacter),
        };

        if (packet.IsAcceptedMessageBox && hasDestination)
        {
            var preflight = await PrepareLegacyTeleportAsync(
                    context,
                    session,
                    new LegacyTeleportRequest(
                        "MsgClanWar.CityWar.Teleport -> GameClient.Teleport",
                        mapId),
                    cancellationToken)
                .ConfigureAwait(false);
            session = preflight.Session;
            sourceCharacter = session.Character;
            clearedSession = session with
            {
                Character = WithoutRuntimeMessageBoxAttributes(sourceCharacter),
            };
            if (!preflight.Allowed)
            {
                context.Sessions?.Set(context.Connection, clearedSession);
                return;
            }
        }

        if (packet.IsAcceptedMessageBox && hasDestination)
        {
            var appliedSession = await ExecuteLegacyTeleportDestinationAsync(
                    context with { Session = clearedSession },
                    clearedSession,
                    new LegacyTeleportDestination(mapId, x, y),
                    timestamp: 0,
                    cancellationToken,
                    expectedSession: session)
                .ConfigureAwait(false);
            if (appliedSession is null)
            {
                return;
            }

            await context.Log.WriteLineAsync(
                    "WorldServer accepted legacy ClanWar current invitation message box " +
                    $"{FormatSession(context with { Session = appliedSession })} map={mapId} x={x} y={y}.")
                .ConfigureAwait(false);
            return;
        }

        context.Sessions?.Set(context.Connection, clearedSession);
        if (packet.IsAcceptedMessageBox)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy ClanWar current invitation teleport " +
                    $"{FormatSession(context)}; destination attributes are missing.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer canceled legacy ClanWar current invitation message box " +
                $"{FormatSession(context)} option={packet.OptionId}.")
            .ConfigureAwait(false);
    }

    private static bool IsRuntimeLegacyClanWarCurrentInvitationMessageBox(string pending)
    {
        return pending.StartsWith(LegacyClanWarCurrentInvitationPendingPrefix, StringComparison.Ordinal);
    }

}
