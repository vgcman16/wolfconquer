using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    [PacketHandler(WorldPacketOpcodes.MsgAction)]
    private static async ValueTask HandleMsgActionAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!MsgActionPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed MsgAction packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgAction " +
                $"{FormatSession(context)} type={packet.Type} dwParam={packet.DwParam} " +
                $"x={packet.PositionX} y={packet.PositionY} from {context.Connection.RemoteEndPoint}.")
            .ConfigureAwait(false);

        if (packet.Type == MsgActionType.SetLocation)
        {
            await HandleSetLocationAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.Hotkeys)
        {
            await HandleHotkeysActionAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.ConfirmAssociates)
        {
            await HandleConfirmAssociatesAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.ConfirmProficiencies)
        {
            await HandleConfirmProficienciesAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.ConfirmSpells)
        {
            await HandleConfirmSpellsAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.ChangeDirection)
        {
            await HandleChangeDirectionActionAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.ChangeStance)
        {
            await HandleChangeStanceAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.Revive)
        {
            await HandleReviveAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.SetPkMode)
        {
            await HandleSetPkModeActionAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.ConfirmGuild)
        {
            await HandleConfirmGuildAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.TeamSearchForMember)
        {
            await HandleTeamSearchForMemberActionAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.StartVendor)
        {
            await HandleStartVendorActionAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.StopVending)
        {
            await HandleStopVendingActionAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.EndFly)
        {
            await HandleEndFlyActionAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.CompleteLogin)
        {
            await HandleCompleteLoginAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.AllowAnimation)
        {
            await HandleAllowAnimationActionAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.ChangeLookface)
        {
            await HandleChangeLookfaceAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.QuerySpawn)
        {
            await HandleQuerySpawnActionAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.ViewEquipment)
        {
            await HandleViewEquipmentActionAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.ChangeMap)
        {
            await HandleChangeMapAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.RequestEntity)
        {
            await HandleRequestEntityAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.Jump)
        {
            await HandleJumpActionAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.UpdateInventorySash)
        {
            await HandleUpdateInventorySashAsync(context, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.QueryEquipment)
        {
            await HandleQueryEquipmentActionAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.PetAttack)
        {
            await HandleLegacyPetAttackActionAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.LegacyUnregistered476)
        {
            await HandleLegacyUnregistered476ActionAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.Away)
        {
            await HandleAwayActionAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (packet.Type == MsgActionType.Ghost)
        {
            await HandleGhostActionAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (TryGetRegisteredMsgActionGap(packet.Type, out var legacyHandler, out var gapReason))
        {
            await LogRegisteredMsgActionGapAsync(
                    context,
                    packet,
                    legacyHandler,
                    gapReason)
                .ConfigureAwait(false);
            return;
        }

        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.UnhandledGameplayClickAction(
                    WorldPacketOpcodes.MsgAction,
                    actionType: (ushort)packet.Type,
                    actionSubtype: packet.DwParam != 0 ? packet.DwParam : null,
                    mapId: packet.MapId != 0
                        ? packet.MapId
                        : context.Session is { } activeSession
                            ? CheckedUInt32(activeSession.Character.MapId, nameof(activeSession.Character.MapId))
                            : null,
                    x: packet.PositionX != 0 ? packet.PositionX : null,
                    y: packet.PositionY != 0 ? packet.PositionY : null,
                    targetUid: packet.TargetUid != 0
                        ? packet.TargetUid
                        : packet.ObjId != 0 ? packet.ObjId : null,
                    sourceSeam: "WorldPacketHandlers.MsgAction.Fallback",
                    nextSeam: "msgaction-runtime",
                    reason: "unhandled MsgAction packet is not modeled; capture client click/action for parity triage"))
            .ConfigureAwait(false);
        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.MovementAction(
                    packet.Type.ToString(),
                    actionType: (ushort)packet.Type,
                    reason: "action state is not modeled"))
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP unmodeled MsgAction " +
                $"{FormatSession(context)} type={packet.Type} typeId={(ushort)packet.Type} " +
                $"objId={packet.ObjId} targetUid={packet.TargetUid} dwParam={packet.DwParam} " +
                $"dwParam2={packet.DwParam2} dwParam3={packet.DwParam3} npcId={packet.NpcId} " +
                $"map={packet.MapId} x={packet.PositionX} y={packet.PositionY} strings={packet.Strings.Count}; " +
                "action state is not modeled.")
            .ConfigureAwait(false);
    }

    private static async ValueTask LogRegisteredMsgActionGapAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        string legacyHandler,
        string reason)
    {
        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.RegisteredMsgAction(
                    packet.Type.ToString(),
                    (ushort)packet.Type,
                    legacyHandler,
                    reason))
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP registered MsgAction " +
                $"{FormatSession(context)} type={packet.Type} typeId={(ushort)packet.Type} " +
                $"legacyHandler={legacyHandler} objId={packet.ObjId} targetUid={packet.TargetUid} " +
                $"dwParam={packet.DwParam} dwParam2={packet.DwParam2} dwParam3={packet.DwParam3} " +
                $"npcId={packet.NpcId} map={packet.MapId} x={packet.PositionX} y={packet.PositionY} " +
                $"strings={packet.Strings.Count}; {reason}.")
            .ConfigureAwait(false);
    }

    private static bool TryGetRegisteredMsgActionGap(
        MsgActionType actionType,
        out string legacyHandler,
        out string reason)
    {
        _ = actionType;
        legacyHandler = string.Empty;
        reason = string.Empty;
        return false;
    }

    private static async ValueTask HandleRequestEntityAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgAction RequestEntity; no active world session.")
                .ConfigureAwait(false);
            return;
        }

        var requestedUid = packet.DwParam;
        if (requestedUid == 0)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP MsgAction RequestEntity missing requested uid " +
                    $"{FormatSession(context)} targetUid={packet.TargetUid}.")
                .ConfigureAwait(false);
            return;
        }

        if (TryGetVisibleSessionByUid(context, session, requestedUid, out var activeSession))
        {
            await context.Connection.SendAsync(
                    CreatePlayerSpawnPacket(activeSession.Session, context.WorldData).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);

            await context.Log.WriteLineAsync(
                    "WorldServer handled MsgAction RequestEntity player " +
                    $"{FormatSession(context)} requestedUid={requestedUid}.")
                .ConfigureAwait(false);
            return;
        }

        if (TryFindVisibleMonsterInstanceByUid(context, session.Character, requestedUid, out var monsterInstance))
        {
            var statusFlags = context.MonsterStates?.CreateStatusFlagVector(
                monsterInstance,
                DateTimeOffset.UtcNow,
                LegacyMonsterSpawnPacket.StatusFlagVectorLength);
            await context.Connection.SendAsync(
                    monsterInstance.ToSpawnPacket(
                            clientCompatibility: context.ClientCompatibility,
                            statusFlags: statusFlags)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);

            foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(monsterInstance))
            {
                await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
            }

            await context.Log.WriteLineAsync(
                    "WorldServer handled MsgAction RequestEntity monster " +
                    $"{FormatSession(context)} requestedUid={requestedUid} " +
                    $"monster={monsterInstance.Name} x={monsterInstance.X} y={monsterInstance.Y}.")
                .ConfigureAwait(false);
            return;
        }

        if (TryFindVisibleStaticNpcByUid(context, session.Character, requestedUid, out var npc))
        {
            await context.Connection.SendAsync(
                    LegacyNpcSpawnPacket.FromNpc(npc).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);

            await context.Log.WriteLineAsync(
                    "WorldServer handled MsgAction RequestEntity static NPC " +
                    $"{FormatSession(context)} requestedUid={requestedUid} " +
                    $"map={npc.MapId} x={npc.X} y={npc.Y}.")
                .ConfigureAwait(false);
            return;
        }

        if (TryFindVisibleFurnitureNpcByUid(context, session.Character, requestedUid, out var furnitureNpc))
        {
            await context.Connection.SendAsync(
                    furnitureNpc.ToNpcSpawnPacket().ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);

            await context.Log.WriteLineAsync(
                    "WorldServer handled MsgAction RequestEntity furniture NPC " +
                    $"{FormatSession(context)} requestedUid={requestedUid} " +
                    $"map={furnitureNpc.MapId} x={furnitureNpc.X} y={furnitureNpc.Y}.")
                .ConfigureAwait(false);
            return;
        }

        if (TryFindVisibleChristmasNpcByUid(context, session.Character, requestedUid, out var christmasNpc))
        {
            await context.Connection.SendAsync(
                    christmasNpc.ToNpcSpawnPacket().ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);

            await context.Log.WriteLineAsync(
                    "WorldServer handled MsgAction RequestEntity Christmas NPC " +
                    $"{FormatSession(context)} requestedUid={requestedUid} " +
                    $"map={christmasNpc.MapId} x={christmasNpc.X} y={christmasNpc.Y} " +
                    $"source={christmasNpc.SourceFile}:{christmasNpc.SourceLine}.")
                .ConfigureAwait(false);
            return;
        }

        if (TryFindVisibleSobNpcByUid(context, session.Character, requestedUid, out var sobNpc))
        {
            var visibleSobNpc = context.SobNpcStates?.GetVisible(sobNpc) ?? sobNpc;
            await context.Connection.SendAsync(
                    LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);

            await context.Log.WriteLineAsync(
                    "WorldServer handled MsgAction RequestEntity SOB NPC " +
                    $"{FormatSession(context)} requestedUid={requestedUid} " +
                    $"map={visibleSobNpc.MapId} x={visibleSobNpc.X} y={visibleSobNpc.Y}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "PARITY-GAP MsgAction RequestEntity missing visible entity " +
                $"{FormatSession(context)} requestedUid={requestedUid} targetUid={packet.TargetUid}.")
            .ConfigureAwait(false);
    }

}
