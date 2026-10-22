using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleChangeMapAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped MsgAction ChangeMap {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var character = session.Character;
        if (context.WorldData is not { } worldData ||
            character.MapId is < int.MinValue or > int.MaxValue ||
            !worldData.PortalsBySourceMapId.TryGetValue((int)character.MapId, out var portals))
        {
            await ApplyLegacyChangeMapFallbackAsync(
                    context,
                    session,
                    packet,
                    "missing portal data",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var portal = portals
            .OrderBy(portal => LegacyGetDistance(character.X, character.Y, portal.SourceX, portal.SourceY))
            .FirstOrDefault(portal => LegacyGetDistance(character.X, character.Y, portal.SourceX, portal.SourceY) < 7);
        if (portal is null)
        {
            await ApplyLegacyChangeMapFallbackAsync(
                    context,
                    session,
                    packet,
                    $"no nearby portal portalCount={portals.Count}",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var acknowledgement = packet with
        {
            ObjId = CheckedUInt32(character.Uid, nameof(character.Uid)),
        };
        var preflight = await PrepareLegacyTeleportAsync(
                context,
                session,
                new LegacyTeleportRequest(
                    "MsgDataPacket.ChangeMap [Data(85)] -> GameClient.Teleport",
                    portal.TargetMapId),
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflight.Allowed)
        {
            await context.Connection.SendAsync(
                    acknowledgement.ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        session = preflight.Session;
        character = session.Character;

        var updatedSession = await ExecuteLegacyTeleportDestinationAsync(
                context with { Session = session },
                session,
                new LegacyTeleportDestination(
                    portal.TargetMapId,
                    portal.TargetX,
                    portal.TargetY),
                packet.Timestamp,
                cancellationToken)
            .ConfigureAwait(false);
        if (updatedSession is null)
        {
            return;
        }

        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(
                acknowledgement.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgAction ChangeMap " +
                $"{FormatSession(updatedContext)} fromMap={character.MapId} sourceX={portal.SourceX} sourceY={portal.SourceY} " +
                $"targetMap={portal.TargetMapId} targetX={portal.TargetX} targetY={portal.TargetY}; " +
                "source=MsgDataPacket.ChangeMap [Data(85)] -> GameClient.Teleport.")
            .ConfigureAwait(false);
    }

    private static async ValueTask ApplyLegacyChangeMapFallbackAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgActionPacket packet,
        string reason,
        CancellationToken cancellationToken)
    {
        var preflight = await PrepareLegacyTeleportAsync(
                context,
                session,
                new LegacyTeleportRequest(
                    "MsgDataPacket.ChangeMap [Data(85)] -> GameClient.Teleport Twin City fallback",
                    1_002),
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflight.Allowed)
        {
            return;
        }

        session = preflight.Session;

        var character = session.Character;
        var updatedSession = await ExecuteLegacyTeleportDestinationAsync(
                context with { Session = session },
                session,
                new LegacyTeleportDestination(1_002, 410, 354),
                packet.Timestamp,
                cancellationToken)
            .ConfigureAwait(false);
        if (updatedSession is null)
        {
            return;
        }

        var updatedContext = context with { Session = updatedSession };

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgAction ChangeMap fallback " +
                $"{FormatSession(updatedContext)} fromMap={character.MapId} x={character.X} y={character.Y} " +
                $"targetMap=1002 targetX=410 targetY=354 reason={reason}; " +
                "source=MsgDataPacket.ChangeMap [Data(85)] -> GameClient.Teleport Twin City when no portal match is found.")
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession?> SendLegacyTeleportTransitionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint timestamp,
        CancellationToken cancellationToken,
        long? sourceMapId = null)
    {
        var transitionSourceMapId = sourceMapId ?? session.Character.PreviousMapId;
        var cleanedSession = await TryApplyLegacyTeleportStatusCleanupAsync(
                context,
                session,
                transitionSourceMapId,
                cancellationToken)
            .ConfigureAwait(false);
        if (cleanedSession is null)
        {
            return null;
        }

        session = cleanedSession;
        context = context with { Session = cleanedSession };
        var character = session.Character;
        var uid = CheckedUInt32(character.Uid, nameof(character.Uid));
        var mapId = CheckedUInt32(character.MapId, nameof(character.MapId));
        var clientMapId = ResolveLegacyTeleportClientMapId(character.MapId);
        var x = CheckedUInt32(character.X, nameof(character.X));
        var y = CheckedUInt32(character.Y, nameof(character.Y));

        await SendLegacyTeleportFrameAsync(context, context.Connection,
                new MsgActionPacket
                {
                    ObjId = uid,
                    Timestamp = timestamp,
                    Type = MsgActionType.Teleport,
                    DwParam = clientMapId,
                    DwParam3 = clientMapId,
                    PositionX = x,
                    PositionY = y,
                }.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        if (transitionSourceMapId != 700)
        {
            await SendLegacyTeleportFrameAsync(context, context.Connection,
                    new MsgActionPacket
                    {
                        ObjId = uid,
                        Timestamp = timestamp,
                        Type = MsgActionType.LegacyMerchantStartup,
                        DwParam = 2,
                        DwParam3 = mapId,
                        PositionX = x,
                        PositionY = y,
                    }.ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await SendLegacyTeleportFrameAsync(context, context.Connection,
                new MsgActionPacket
                {
                    ObjId = uid,
                    Timestamp = timestamp,
                    Type = MsgActionType.StopVending,
                    DwParam = mapId,
                    DwParam3 = mapId,
                    PositionX = x,
                    PositionY = y,
                }.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyTeleportFrameAsync(context, context.Connection,
                new MsgActionPacket
                {
                    ObjId = uid,
                    Timestamp = timestamp,
                    Type = MsgActionType.LegacyColor,
                    DwParam = ResolveLegacyTeleportMapColor(character.MapId, context.WorldData?.MapsById),
                    PositionX = x,
                    PositionY = y,
                }.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var mapStatus = LegacyMapStatusPacket.FromMap(character.MapId, context.WorldData?.MapsById);
        await SendLegacyTeleportFrameAsync(context, context.Connection, mapStatus.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await SendVisibleRouletteTablesAsync(context, character, cancellationToken).ConfigureAwait(false);
        await SendVisiblePlayersAsync(context, session, cancellationToken).ConfigureAwait(false);
        await SendVisibleStaticNpcHeadObjectsAsync(context, character, cancellationToken).ConfigureAwait(false);
        await SendVisibleMonstersAsync(context, character, cancellationToken).ConfigureAwait(false);
        await SendVisibleFloorItemsAsync(context, character, cancellationToken).ConfigureAwait(false);
        await SendVisiblePokerTablesAsync(context, character, cancellationToken).ConfigureAwait(false);
        await SendVisibleSobNpcTailObjectsAsync(context, character, cancellationToken).ConfigureAwait(false);

        var destinationCleanedSession = await ApplyLegacyTeleportDestinationStatusCleanupAsync(
                context,
                session,
                cancellationToken)
            .ConfigureAwait(false);
        if (destinationCleanedSession is null)
        {
            return null;
        }

        session = destinationCleanedSession;
        context = context with { Session = session };

        await SendLegacyTeleportEnvironmentAsync(
                context,
                session.Character,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyTeleportGuildWarEquipmentRefreshAsync(
                context,
                session,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer sent legacy GameClient.Teleport transition " +
                $"{FormatSession(context)} sourceMap={transitionSourceMapId} " +
                $"map={character.MapId} clientMap={clientMapId} " +
                $"x={character.X} y={character.Y} mapStatus={mapStatus.Id}/{mapStatus.BaseId} color={ResolveLegacyTeleportMapColor(character.MapId, context.WorldData?.MapsById)}.")
            .ConfigureAwait(false);
        return session;
    }

    private static uint ResolveLegacyTeleportClientMapId(long mapId)
    {
        if (mapId is >= int.MinValue and <= int.MaxValue)
        {
            var alias = LegacyMapAliasResolver.Resolve((int)mapId);
            if (alias.IsAlias)
            {
                return checked((uint)alias.BaseMapId);
            }
        }

        return CheckedUInt32(mapId, nameof(mapId));
    }

    private static uint ResolveLegacyTeleportMapColor(
        long mapId,
        IReadOnlyDictionary<int, MapMetadata>? mapsById)
    {
        if (mapId == 1780)
        {
            return 3_289_650U;
        }

        if (mapId == 3846)
        {
            return 16_755_370U;
        }

        if (mapId is 10088 or 44455 or 44456)
        {
            return 14_535_867U;
        }

        if (mapId is >= 3830 and <= 3832 &&
            mapId <= int.MaxValue &&
            mapId >= int.MinValue)
        {
            var alias = LegacyMapAliasResolver.Resolve((int)mapId);
            if (mapsById?.TryGetValue(alias.BaseMapId, out var map) == true)
            {
                return CheckedUInt32(map.Color, nameof(map.Color));
            }
        }

        return 0;
    }

    private static async ValueTask HandleJumpActionAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped MsgAction Jump {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryGetJumpDestination(packet, out var x, out var y))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP MsgAction Jump missing destination " +
                    $"{FormatSession(context)} targetX={packet.TargetPositionX} targetY={packet.TargetPositionY} " +
                    $"x={packet.PositionX} y={packet.PositionY}.")
                .ConfigureAwait(false);
            return;
        }

        var movementSideEffects = LegacyMovementSideEffects.ApplyMsgDataPlayerJump(session.Character);
        var character = movementSideEffects.Character;
        if ((character.MinHitPoints <= 0 || HasLegacyStatusFlag(character, LegacyDeadFlag)) &&
            HasLegacyStatusFlag(character, LegacyNeptuneCurseFlag))
        {
            var neptuneRemovedFocusedFlag = HasLegacyStatusFlag(character, LegacyFocusedFlag);
            var neptuneCancelledArmedFocus = TryCancelLegacyIntensifyArm(
                character.Attributes,
                out var neptuneAttributes);
            if (neptuneCancelledArmedFocus)
            {
                character = character with { Attributes = neptuneAttributes };
            }

            if (neptuneRemovedFocusedFlag)
            {
                character = WithoutLegacyStatusFlag(character, LegacyFocusedFlag);
            }

            var deadPosition = LegacyPlayerDeathState.ResolveDeadPosition(character);
            var pulledBack = character with
            {
                X = deadPosition.ResolvedX,
                Y = deadPosition.ResolvedY,
            };
            var neptuneUpdatedSession = session with { Character = pulledBack };
            context.Sessions?.Set(context.Connection, neptuneUpdatedSession);

            if (movementSideEffects.ClearedAway)
            {
                await SendPlayerSpawnToVisiblePeersAsync(context, neptuneUpdatedSession, cancellationToken)
                    .ConfigureAwait(false);
                await context.Log.WriteLineAsync(
                        "WorldServer cleared MsgAction Jump Away " +
                        $"{FormatSession(context)}; legacy MsgDataPacket.PlayerJump clears Player.Away before NeptuneCurse dead-position pullback.")
                    .ConfigureAwait(false);
            }

            await SendMovementFocusedCancelIfNeededAsync(
                    context,
                    neptuneUpdatedSession,
                    neptuneRemovedFocusedFlag || neptuneCancelledArmedFocus,
                    cancellationToken)
                .ConfigureAwait(false);

            var uid = CheckedUInt32(pulledBack.Uid, nameof(pulledBack.Uid));
            var interaction = new MsgAttackPacket(new MsgAttackQuery(
                Uid: 0,
                OpponentUid: uid,
                X: deadPosition.InteractionX,
                Y: deadPosition.InteractionY,
                AttackType: MsgAttackType.Death,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0));
            await SendInteractionToVisibleAsync(context, neptuneUpdatedSession, interaction, cancellationToken)
                .ConfigureAwait(false);

            await context.Log.WriteLineAsync(
                    "WorldServer handled MsgAction Jump NeptuneCurse dead-position pullback " +
                    $"{FormatSession(context)} recordedX={deadPosition.RecordedX} recordedY={deadPosition.RecordedY} " +
                    $"resolvedX={deadPosition.ResolvedX} resolvedY={deadPosition.ResolvedY} usesFallback={deadPosition.UsesFallback}; " +
                    "source=MsgDataPacket.PlayerJump [Data(137)] dead NeptuneCurse branch.")
                .ConfigureAwait(false);
            return;
        }

        if (Math.Abs(x - character.X) >= 17 || Math.Abs(y - character.Y) >= 17)
        {
            await SendLegacySetLocationAsync(context, context.Connection, character, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer rejected MsgAction Jump " +
                    $"{FormatSession(context)} fromX={character.X} fromY={character.Y} targetX={x} targetY={y}; " +
                    "legacy jump delta must be less than 17 on both axes.")
                .ConfigureAwait(false);
            return;
        }

        var legacyJumpDistance = LegacyGetDistance(character.X, character.Y, x, y);
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["Angle"] = packet.Facing.ToString(CultureInfo.InvariantCulture),
        };
        var removedActivePick = HasActivePick(character);
        var removedFocusedFlag = HasLegacyStatusFlag(character, LegacyFocusedFlag);
        var removedMagicDefenderFlag = HasLegacyStatusFlag(character, LegacyMagicDefenderFlag);
        var steedVigor = HasLegacyStatusFlag(character, LegacyRideFlag)
            ? DecrementJumpRideVigor(character, legacyJumpDistance)
            : (uint?)null;
        if (steedVigor is { } vigor)
        {
            attributes["Vigor"] = vigor.ToString(CultureInfo.InvariantCulture);
        }

        var cancelledArmedFocus = TryCancelLegacyIntensifyArm(attributes, out var jumpAttributes);
        var removedMovementFlags = TryRemoveLegacyMovementStatusFlags(jumpAttributes, out var updatedMovementAttributes);
        var jumpedCharacter = character with
        {
            X = x,
            Y = y,
            Attributes = updatedMovementAttributes,
        };
        if (removedActivePick)
        {
            jumpedCharacter = WithoutCharacterAttribute(jumpedCharacter, "ActivePick");
        }

        WorldActiveSession interactionPartner = default!;
        var hasInteractionJump =
            TryGetMovementInteractionPartner(context, session, out interactionPartner) &&
            interactionPartner.Session.Character.X == character.X &&
            interactionPartner.Session.Character.Y == character.Y;

        var updatedSession = session with { Character = jumpedCharacter };
        WorldSession? updatedPartnerSession = null;
        ConquerTcpConnection? updatedPartnerConnection = null;
        if (hasInteractionJump && context.Sessions is { } interactionSessions)
        {
            var partnerCharacter = interactionPartner.Session.Character with
            {
                X = x,
                Y = y,
                Attributes = WithCharacterAttribute(
                    interactionPartner.Session.Character,
                    "Angle",
                    packet.Facing).Attributes,
            };
            updatedPartnerSession = interactionPartner.Session with { Character = partnerCharacter };
            updatedPartnerConnection = interactionPartner.Connection;
            interactionSessions.Set(context.Connection, updatedSession);
            interactionSessions.Set(updatedPartnerConnection, updatedPartnerSession);
        }
        else
        {
            context.Sessions?.Set(context.Connection, updatedSession);
        }

        if (movementSideEffects.ClearedAway)
        {
            await SendPlayerSpawnToVisiblePeersAsync(context, updatedSession, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer cleared MsgAction Jump Away " +
                    $"{FormatSession(context)}; legacy MsgDataPacket.PlayerJump clears Player.Away and refreshes visible role spawn.")
                .ConfigureAwait(false);
        }

        await SendMovementAzureShieldRemovalIfNeededAsync(
                context,
                session,
                removedMagicDefenderFlag,
                cancellationToken)
            .ConfigureAwait(false);
        await SendMovementFocusedCancelIfNeededAsync(
                context,
                session,
                removedFocusedFlag || cancelledArmedFocus,
                cancellationToken)
            .ConfigureAwait(false);

        var visiblePeerCount = 0;
        if (hasInteractionJump)
        {
            var interactionJump = LegacyInterActionWalkPacket.ForJump(
                CheckedUInt32(character.Uid, nameof(character.Uid)),
                CheckedUInt32(interactionPartner.Session.Character.Uid, nameof(interactionPartner.Session.Character.Uid)),
                CheckedUInt16(x, nameof(x)),
                CheckedUInt16(y, nameof(y)));
            var frame = interactionJump.ToFrameBytes();
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);

            if (context.Sessions is { } sessions)
            {
                foreach (var active in sessions.Snapshot())
                {
                    if (ReferenceEquals(active.Connection, context.Connection) ||
                        active.Session.Character.Uid == jumpedCharacter.Uid ||
                        !CanSeePlayer(active.Session.Character, jumpedCharacter))
                    {
                        continue;
                    }

                    await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                    visiblePeerCount++;
                }
            }

            await context.Log.WriteLineAsync(
                    "WorldServer sent MsgAction interaction jump " +
                    $"{FormatSession(context)} opponentUid={interactionJump.OpponentUid} x={x} y={y} angle={packet.Facing} playerCount={visiblePeerCount}.")
                .ConfigureAwait(false);
        }
        else
        {
            var response = packet with
            {
                ObjId = CheckedUInt32(character.Uid, nameof(character.Uid)),
                PositionX = checked((uint)x),
                PositionY = checked((uint)y),
                TargetPositionX = checked((uint)x),
                TargetPositionY = checked((uint)y),
                MapId = CheckedUInt32(character.MapId, nameof(character.MapId)),
            };
            var frame = response.ToFrameBytes();
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);

            if (context.Sessions is { } sessions)
            {
                foreach (var active in sessions.Snapshot())
                {
                    if (ReferenceEquals(active.Connection, context.Connection) ||
                        active.Session.Character.Uid == jumpedCharacter.Uid ||
                        !CanSeePlayer(active.Session.Character, jumpedCharacter))
                    {
                        continue;
                    }

                    await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                    visiblePeerCount++;
                }
            }
        }

        await SendVisibleStaticNpcHeadObjectsAsync(
                context with { Session = updatedSession },
                jumpedCharacter,
                cancellationToken)
            .ConfigureAwait(false);
        if (updatedPartnerSession is { } partnerSession &&
            updatedPartnerConnection is { } partnerConnection)
        {
            await SendVisibleStaticNpcHeadObjectsAsync(
                    context with
                    {
                        Connection = partnerConnection,
                        Session = partnerSession,
                    },
                    partnerSession.Character,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        await SendVisibleMonstersAsync(context, jumpedCharacter, cancellationToken, character)
            .ConfigureAwait(false);
        await SendVisibleFloorItemsAsync(context, jumpedCharacter, cancellationToken, character)
            .ConfigureAwait(false);
        await SendVisiblePokerTablesAsync(context, jumpedCharacter, cancellationToken)
            .ConfigureAwait(false);
        await SendVisibleSobNpcTailObjectsAsync(
                context with { Session = updatedSession },
                jumpedCharacter,
                cancellationToken)
            .ConfigureAwait(false);
        if (updatedPartnerSession is { } partnerTailSession &&
            updatedPartnerConnection is { } partnerTailConnection)
        {
            await SendVisibleSobNpcTailObjectsAsync(
                    context with
                    {
                        Connection = partnerTailConnection,
                        Session = partnerTailSession,
                    },
                    partnerTailSession.Character,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (steedVigor is { } updatedVigor)
        {
            await context.Connection.SendAsync(new LegacyServerInfoPacket(updatedVigor).ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer sent MsgAction Jump ride vigor " +
                    $"{FormatSession(context)} distance={legacyJumpDistance} vigor={updatedVigor}.")
                .ConfigureAwait(false);
        }

        await SendMovementActivePickRemovalIfNeededAsync(
                context,
                session,
                removedActivePick,
                cancellationToken)
            .ConfigureAwait(false);
        await SendAlienWorldLeavePromptIfNeededAsync(
                context,
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);

        if (removedMovementFlags)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer cleared MsgAction Jump status flags " +
                    $"{FormatSession(context)} flags=0x15/0x1E/0x1F/0x80.")
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgAction Jump " +
                $"{FormatSession(context)} x={x} y={y} angle={packet.Facing} playerCount={visiblePeerCount}.")
            .ConfigureAwait(false);
    }
}
