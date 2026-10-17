using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    [PacketHandler(WorldPacketOpcodes.MsgMovement)]
    private static async ValueTask HandleMsgMovementAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!MsgMovementPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed MsgMovement packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgMovement " +
                $"{FormatSession(context)} uid={packet.Uid} direction={packet.Direction} " +
                $"running={packet.Running} timestamp={packet.Timestamp} map={packet.MapId}.")
            .ConfigureAwait(false);

        await HandleMovementStateAsync(context, packet, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask HandleSetLocationAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped MsgAction set-location {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var character = session.Character;
        var response = packet with
        {
            DwParam = ResolveLegacyTeleportClientMapId(character.MapId),
            Facing = ReadAttributeUInt16(character.Attributes, "Angle"),
            PositionX = CheckedUInt32(character.X, nameof(character.X)),
            PositionY = CheckedUInt32(character.Y, nameof(character.Y)),
        };

        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await SendVisibleRouletteTablesAsync(context, character, cancellationToken).ConfigureAwait(false);
        await SendVisiblePlayersAsync(context, session, cancellationToken).ConfigureAwait(false);
        await SendVisibleStaticNpcHeadObjectsAsync(context, character, cancellationToken).ConfigureAwait(false);
        await SendVisibleMonstersAsync(context, character, cancellationToken).ConfigureAwait(false);
        await SendVisibleFloorItemsAsync(context, character, cancellationToken).ConfigureAwait(false);
        await SendVisiblePokerTablesAsync(context, character, cancellationToken).ConfigureAwait(false);
        await SendVisibleSobNpcTailObjectsAsync(context, character, cancellationToken).ConfigureAwait(false);

        var mapStatus = LegacyMapStatusPacket.FromMap(character.MapId, context.WorldData?.MapsById);
        await context.Connection.SendAsync(mapStatus.ToFrameBytes(), cancellationToken).ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyMapStatus " +
                $"{FormatSession(context)} map={mapStatus.Id} baseMap={mapStatus.BaseId} status={mapStatus.Status}.")
            .ConfigureAwait(false);

        if (character.MapId == 3846)
        {
            await context.Connection.SendAsync(
                    new MsgActionPacket
                    {
                        ObjId = CheckedUInt32(character.Uid, nameof(character.Uid)),
                        Type = MsgActionType.LegacyColor,
                        DwParam = 16_755_370U,
                        PositionX = CheckedUInt32(character.X, nameof(character.X)),
                        PositionY = CheckedUInt32(character.Y, nameof(character.Y)),
                    }.ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask HandleReviveAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped MsgAction revive {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var currentHitpoints = session.Character.MinHitPoints <= 0
            ? 1U
            : CheckedUInt32(session.Character.MinHitPoints, nameof(session.Character.MinHitPoints));
        var maxHitpoints = ComputeLegacyRuntimeMaxHitpoints(
            session.Character,
            currentHitpoints,
            session.PlayerItems,
            session.CombatGear ?? CombatGearSnapshot.Empty,
            context.WorldData,
            session.AlternateEquipmentActive);
        var revive = LegacyPlayerDeathState.TryRevive(
            session.Character,
            DateTimeOffset.UtcNow,
            new LegacyPlayerReviveOptions
            {
                MaximumHitpoints = maxHitpoints,
                MaximumMana = session.Character.MaxMana,
            });
        if (!revive.Allowed)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgAction revive " +
                    $"{FormatSession(context)} objId={packet.ObjId} " +
                    $"hitpoints={session.Character.MinHitPoints} reviveAllowedAt={revive.State.ReviveAllowedAt}.")
                .ConfigureAwait(false);
            return;
        }

        var revivedCharacter = revive.Character;

        var updated = session with { Character = revivedCharacter };
        context.Sessions?.Set(context.Connection, updated);
        var updatedContext = context with { Session = updated };
        var uid = CheckedUInt32(revivedCharacter.Uid, nameof(revivedCharacter.Uid));

        foreach (var update in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(
            uid,
            maxHitpoints,
            maxHitpoints))
        {
            await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(uid, 100).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
                    uid,
                    CreateLegacyRuntimeStatusFlagVector(revivedCharacter)).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyRuntimeMeshUpdate(uid, BuildLegacyCharacterMesh(revivedCharacter)).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMapStatusPacket.FromMap(revivedCharacter.MapId, context.WorldData?.MapsById).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendPlayerSpawnToVisiblePeersAsync(context, updated, cancellationToken).ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgAction revive " +
                $"{FormatSession(updatedContext)} objId={packet.ObjId} maxHitpoints={maxHitpoints}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleMovementStateAsync(
        WorldPacketHandlerContext context,
        MsgMovementPacket packet,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped MsgMovement state handling {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryCalculateMovementPosition(session.Character, packet, out var x, out var y, out var angle))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgMovement state handling " +
                    $"{FormatSession(context)} direction={packet.Direction} running={packet.Running}; movement target is outside legacy coordinate range.")
                .ConfigureAwait(false);
            return;
        }

        var movementSideEffects = LegacyMovementSideEffects.ApplyMsgMovement(session.Character);
        var movementCharacter = movementSideEffects.Character;
        var steedVigor = packet.Running == MsgMovementMode.Steed
            ? DecrementSteedMovementVigor(movementCharacter)
            : (uint?)null;
        var movementAttributes = steedVigor is { } vigor
            ? WithCharacterAttribute(movementCharacter, "Vigor", vigor).Attributes
            : WithCharacterAttribute(movementCharacter, "Angle", angle).Attributes;
        var removedFocusedFlag = HasLegacyStatusFlag(movementCharacter, LegacyFocusedFlag);
        var removedMagicDefenderFlag = HasLegacyStatusFlag(movementCharacter, LegacyMagicDefenderFlag);
        var removedActivePick = HasActivePick(movementCharacter);
        var cancelledArmedFocus = TryCancelLegacyIntensifyArm(
            movementAttributes,
            out movementAttributes);
        var removedMovementFlags = TryRemoveLegacyMovementStatusFlags(movementAttributes, out var updatedMovementAttributes);
        var character = movementCharacter with
        {
            X = x,
            Y = y,
            Attributes = updatedMovementAttributes,
        };

        if (packet.Running != MsgMovementMode.Steed &&
            TryGetMovementInteractionPartner(context, session, out var partner) &&
            partner.Session.Character.X == session.Character.X &&
            partner.Session.Character.Y == session.Character.Y)
        {
            var partnerCharacter = partner.Session.Character with
            {
                X = character.X,
                Y = character.Y,
                Attributes = WithCharacterAttribute(partner.Session.Character, "Angle", angle).Attributes,
            };
            var interactionUpdated = session with { Character = character };
            var interactionUpdatedPartner = partner.Session with { Character = partnerCharacter };

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
            await HandleInteractionMovementAsync(
                    context,
                    session,
                    partner,
                    character,
                    packet,
                    angle,
                    removedMovementFlags,
                    movementSideEffects.ClearedAway,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendVisibleStaticNpcObjectsAsync(
                    context with { Session = interactionUpdated },
                    character,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendVisibleStaticNpcObjectsAsync(
                    context with
                    {
                        Connection = partner.Connection,
                        Session = interactionUpdatedPartner,
                    },
                    partnerCharacter,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (removedActivePick)
        {
            character = WithoutCharacterAttribute(character, "ActivePick");
        }

        var pickedCaptureTheFlag = TryApplyCaptureTheFlagPickup(
            character,
            out character,
            out var captureTheFlagPickup);
        var pickedNewGuildWarItems = TryApplyNewGuildWarItemPickups(
            character,
            out character,
            out var newGuildWarPickups);
        var pickedRaceItem = TryApplySteedRaceItemPickup(
            character,
            out character,
            out var raceItemPickup);
        var updated = session with { Character = character };
        if (context.Sessions is { } sessionStore &&
            !sessionStore.TryCompareAndSet(context.Connection, session, updated))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped stale MsgMovement publication " +
                    $"{FormatSession(context)} x={x} y={y} angle={angle}; " +
                    "the context session is no longer current.")
                .ConfigureAwait(false);
            return;
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

        if (movementSideEffects.ClearedAway)
        {
            await SendPlayerSpawnToVisiblePeersAsync(context, updated, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer cleared MsgMovement Away " +
                    $"{FormatSession(context)}; legacy MsgMovement.Movement clears Player.Away and refreshes visible role spawn.")
                .ConfigureAwait(false);
        }

        var response = packet with
        {
            Uid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            MapId = CheckedUInt32(session.Character.MapId, nameof(session.Character.MapId)),
        };
        var responseFrame = response.ToFrameBytes();
        await context.Connection.SendAsync(responseFrame, cancellationToken).ConfigureAwait(false);

        var visiblePeerCount = 0;
        var playerEnterViewCount = 0;
        var playerLeaveViewCount = 0;
        if (context.Sessions is { } sessions)
        {
            foreach (var active in sessions.Snapshot())
            {
                if (ReferenceEquals(active.Connection, context.Connection) ||
                    active.Session.Character.Uid == character.Uid)
                {
                    continue;
                }

                var wasVisible = CanSeePlayer(active.Session.Character, session.Character);
                var isVisible = CanSeePlayer(active.Session.Character, character);
                if (!isVisible)
                {
                    if (wasVisible)
                    {
                        await SendReciprocalPlayerRemoveAsync(
                                context,
                                session,
                                active,
                                cancellationToken)
                            .ConfigureAwait(false);
                        playerLeaveViewCount++;
                    }

                    continue;
                }

                await active.Connection.SendAsync(responseFrame, cancellationToken).ConfigureAwait(false);
                visiblePeerCount++;

                if (!wasVisible)
                {
                    await SendReciprocalPlayerSpawnAsync(
                            context,
                            updated,
                            active,
                            cancellationToken)
                        .ConfigureAwait(false);
                    playerEnterViewCount++;
                }
            }
        }

        await SendVisibleStaticNpcHeadObjectsAsync(context, character, cancellationToken)
            .ConfigureAwait(false);
        await SendVisibleMonstersAsync(context, character, cancellationToken, session.Character)
            .ConfigureAwait(false);
        await SendVisibleFloorItemsAsync(context, character, cancellationToken, session.Character)
            .ConfigureAwait(false);
        await SendVisiblePokerTablesAsync(context, character, cancellationToken)
            .ConfigureAwait(false);
        await SendVisibleSobNpcTailObjectsAsync(context, character, cancellationToken)
            .ConfigureAwait(false);

        if (steedVigor is { } updatedVigor)
        {
            await context.Connection.SendAsync(new LegacyServerInfoPacket(updatedVigor).ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer sent MsgMovement steed vigor " +
                    $"{FormatSession(context)} vigor={updatedVigor}.")
                .ConfigureAwait(false);
        }

        if (pickedCaptureTheFlag)
        {
            await SendCaptureTheFlagPickupAsync(
                    context,
                    updated,
                    captureTheFlagPickup!,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (pickedNewGuildWarItems)
        {
            await SendNewGuildWarItemPickupsAsync(
                    context,
                    updated,
                    newGuildWarPickups,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (pickedRaceItem)
        {
            await SendSteedRaceItemPickupAsync(
                    context,
                    updated,
                    raceItemPickup!,
                    cancellationToken)
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
                updated,
                cancellationToken)
            .ConfigureAwait(false);

        if (removedMovementFlags)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer cleared MsgMovement status flags " +
                    $"{FormatSession(context)} flags=0x15/0x1E/0x1F/0x80.")
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer updated MsgMovement " +
                $"{FormatSession(context)} x={x} y={y} angle={angle}.")
            .ConfigureAwait(false);

        if (visiblePeerCount > 0 || playerLeaveViewCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer sent MsgMovement visibility " +
                    $"{FormatSession(context)} map={character.MapId} playerCount={visiblePeerCount} " +
                $"playerEnterCount={playerEnterViewCount} playerLeaveCount={playerLeaveViewCount}.")
            .ConfigureAwait(false);
        }
    }

}
