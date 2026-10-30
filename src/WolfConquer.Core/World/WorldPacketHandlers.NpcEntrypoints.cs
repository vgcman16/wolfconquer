using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    [PacketHandler(WorldPacketOpcodes.LegacyNpcServer)]
    private static async ValueTask HandleLegacyNpcServerAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyNpcServerRequestPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyNpcServer packet {FormatSession(context)} {FormatFrameDiagnostic(frame)}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpcServer " +
                $"{FormatSession(context)} npcId={packet.NpcId} option={packet.OptionId} " +
                $"type={packet.InteractType} action={packet.Action} inputLength={packet.Input.Length}.")
            .ConfigureAwait(false);

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyNpcServer {FormatSession(context)} npcId={packet.NpcId}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action != LegacyNpcServerAction.Script &&
            !IsLegacyCursorScriptNpc(packet))
        {
            if (await HandleLegacyNpcServerActionAsync(context, session, packet).ConfigureAwait(false))
            {
                return;
            }

            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.ClickedNpc(
                        packet.NpcId,
                        opcode: WorldPacketOpcodes.LegacyNpcServer,
                        mapId: checked((uint)session.Character.MapId),
                        optionId: packet.OptionId,
                        interactType: packet.InteractType,
                        action: packet.Action.ToString(),
                        source: "LegacyNpcServer",
                        reason: "source action does not map to a modeled branch and no live fallback is available"))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "PARITY-GAP unmodeled LegacyNpcServer " +
                    $"{FormatSession(context)} npcId={packet.NpcId} action={packet.Action}; " +
                    "source action does not map to a modeled branch and no live fallback is available.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.OptionId == LegacyNpcClosedOption)
        {
            if (IsLegacyNobilityDonorNpc(packet.NpcId))
            {
                await TryHandleLegacyNobilityDonorAsync(
                        context,
                        session,
                        packet.OptionId,
                        packet.Input,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            await context.Log.WriteLineAsync(
                    $"WorldServer ignored LegacyNpcServer closed option {FormatSession(context)} npcId={packet.NpcId}.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.NpcId == LegacyBankCpsNpcId && IsBankCpsMutationOption(packet.OptionId))
        {
            await TryHandleLegacyBankCpsMutationAsync(context, session, packet.OptionId, packet.Input,
                requireActiveNpc: false, cancellationToken).ConfigureAwait(false);
            return;
        }

        var updatedSession = session with { ActiveNpcId = packet.NpcId };
        if (context.Sessions is { } sessions)
            sessions.Set(context.Connection, updatedSession, out updatedSession);

        if (!CanUseLegacyNpcServerClick(context, session.Character, packet.NpcId, out var npcLookup))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.ClickedNpc(
                        packet.NpcId,
                        opcode: WorldPacketOpcodes.LegacyNpcServer,
                        mapId: checked((uint)session.Character.MapId),
                        optionId: packet.OptionId,
                        interactType: packet.InteractType,
                        action: packet.Action.ToString(),
                        source: GetLegacyNpcScriptSource(packet.NpcId),
                        reason: "clicked NPC is not currently visible"))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "PARITY-GAP skipped LegacyNpcServer " +
                    $"{FormatSession(context)} npcId={packet.NpcId} {npcLookup}; " +
                    "clicked NPC is not currently visible.")
                .ConfigureAwait(false);
            return;
        }

        if (await TryHandleLegacyNpcActionWithSpellRefusalAsync(
                context,
                updatedSession,
                packet.NpcId,
                packet.OptionId,
                packet.InteractType,
                packet.Input,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return;
        }

        var scriptResolution = LegacyNpcScriptRuntime.Resolve(packet);
        if (!scriptResolution.IsModeled)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.MissingNpcScript(
                        scriptResolution,
                        mapId: CheckedUInt32(session.Character.MapId, nameof(session.Character.MapId))))
                .ConfigureAwait(false);
        }

        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.ClickedNpc(
                    packet.NpcId,
                    opcode: WorldPacketOpcodes.LegacyNpcServer,
                    mapId: checked((uint)session.Character.MapId),
                    optionId: packet.OptionId,
                    interactType: packet.InteractType,
                    action: packet.Action.ToString(),
                    source: GetLegacyNpcScriptSource(packet.NpcId),
                    reason: FormatLegacyNpcScriptDiagnostic(packet.NpcId, packet.OptionId)))
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox(LegacyNpcClickFallbackText).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyNpcServer fallback dialog " +
                $"{FormatSession(context)} npcId={packet.NpcId} option={packet.OptionId} " +
                $"{npcLookup} {FormatLegacyNpcScriptDiagnostic(packet.NpcId, packet.OptionId)}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> HandleLegacyNpcServerActionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyNpcServerRequestPacket packet)
    {
        if (packet.OptionId == LegacyNpcClosedOption)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer ignored LegacyNpcServer closed option {FormatSession(context)} npcId={packet.NpcId} action={packet.Action}.")
                .ConfigureAwait(false);
            return true;
        }

        if (packet.Action is not LegacyNpcServerAction.Statue and
            not LegacyNpcServerAction.PlaceFurniture and
            not LegacyNpcServerAction.Cursor)
        {
            return false;
        }

        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.ClickedNpc(
                    packet.NpcId,
                    opcode: WorldPacketOpcodes.LegacyNpcServer,
                    mapId: checked((uint)session.Character.MapId),
                    optionId: packet.OptionId,
                    interactType: packet.InteractType,
                    action: packet.Action.ToString(),
                    source: "LegacyNpcServer.Action",
                    reason: "source-backed NPC action is treated as a no-op in this parity lane"))
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP source-backed no-op LegacyNpcServer " +
                $"{FormatSession(context)} npcId={packet.NpcId} action={packet.Action} option={packet.OptionId} " +
                $"mapId={session.Character.MapId}.")
            .ConfigureAwait(false);

        return true;
    }

    [PacketHandler(WorldPacketOpcodes.LegacyNpcReply)]
    private static async ValueTask HandleLegacyNpcReplyAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyNpcDialogRequestPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyNpcReply packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpcReply " +
                $"{FormatSession(context)} npcId={packet.NpcId} option={packet.OptionId} " +
                $"type={packet.InteractType} action={packet.Action}.")
            .ConfigureAwait(false);

        if (!packet.IsMessageBox)
        {
            if (await TryHandleLegacyNpcReplyActionAsync(context, packet, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            var diagnosticNpcId = packet.NpcId;
            if (diagnosticNpcId == 0 && context.Session?.ActiveNpcId is { } activeNpcId)
            {
                diagnosticNpcId = activeNpcId;
            }

            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.ClickedNpc(
                        diagnosticNpcId,
                        opcode: WorldPacketOpcodes.LegacyNpcReply,
                        mapId: context.Session is { } session ? checked((uint)session.Character.MapId) : null,
                        optionId: packet.OptionId,
                        interactType: packet.InteractType,
                        action: packet.Action.ToString(),
                        source: GetLegacyNpcScriptSource(diagnosticNpcId),
                        reason: "normal NPC script/action handling is not modeled"))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "PARITY-GAP unmodeled LegacyNpcReply " +
                    $"{FormatSession(context)} npcId={packet.NpcId} resolvedNpcId={diagnosticNpcId} mesh={packet.Mesh} " +
                    $"option={packet.OptionId} type={packet.InteractType} action={packet.Action} " +
                    $"inputLength={packet.Input.Length} {FormatLegacyNpcLookup(context, diagnosticNpcId)} " +
                    $"{FormatLegacyNpcScriptDiagnostic(diagnosticNpcId, packet.OptionId)}; " +
                    "normal NPC script/action handling is not modeled.")
                .ConfigureAwait(false);
            return;
        }

        await HandleLegacyMessageBoxReplyAsync(context, packet, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyNpcReplyActionAsync(
        WorldPacketHandlerContext context,
        LegacyNpcDialogRequestPacket packet,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            return false;
        }

        var npcId = packet.NpcId;
        if (npcId == 0 && session.ActiveNpcId is { } activeNpcId)
        {
            npcId = activeNpcId;
        }

        if (npcId == 0)
        {
            return false;
        }

        if (npcId == LegacyBankCpsNpcId && IsBankCpsMutationOption(packet.OptionId) && packet.IsScriptReply)
            return await TryHandleLegacyBankCpsMutationAsync(context, session, packet.OptionId, packet.Input,
                requireActiveNpc: packet.NpcId == 0, cancellationToken).ConfigureAwait(false);

        if (packet.OptionId == LegacyNpcClosedOption)
        {
            if (IsLegacyNobilityDonorNpc(npcId))
            {
                return await TryHandleLegacyNobilityDonorAsync(
                        context,
                        session,
                        packet.OptionId,
                        packet.Input,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await context.Log.WriteLineAsync(
                    $"WorldServer ignored LegacyNpcReply closed option {FormatSession(context)} npcId={npcId}.")
                .ConfigureAwait(false);
            return true;
        }

        if (packet.OptionId == 0)
        {
            if (IsLegacySourceIgnoredZeroOptionNpcReply(npcId))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer ignored source-backed LegacyNpcReply zero option " +
                        $"{FormatSession(context)} npcId={npcId} type={packet.InteractType} " +
                        $"source={GetLegacyNpcScriptSource(npcId)}; " +
                        "legacy Procesor.NpcServerRequest dispatches only option != byte.MaxValue && option != 0.")
                    .ConfigureAwait(false);
                return true;
            }

            return false;
        }

        if (IsLegacyDeitylandSupportItemNpc(npcId))
        {
            return await TryHandleLegacyDeitylandSupportItemNpcAsync(
                    context,
                    session,
                    npcId,
                    packet.OptionId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (IsLegacyItemPackNpc(npcId))
        {
            return await TryHandleLegacyItemPackNpcAsync(
                    context,
                    session,
                    npcId,
                    packet.OptionId,
                    packet.Input,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (IsLegacyDemonBoxNpc(npcId))
        {
            return await TryHandleLegacyDemonBoxesAsync(
                    context,
                    session,
                    npcId,
                    packet.OptionId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!CanUseLegacyNpcServerClick(context, session.Character, npcId, out var npcLookup))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.ClickedNpc(
                        npcId,
                        opcode: WorldPacketOpcodes.LegacyNpcReply,
                        mapId: checked((uint)session.Character.MapId),
                        optionId: packet.OptionId,
                        interactType: packet.InteractType,
                        action: packet.Action.ToString(),
                        source: GetLegacyNpcScriptSource(npcId),
                        reason: "clicked NPC is not currently visible"))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "PARITY-GAP skipped LegacyNpcReply action " +
                    $"{FormatSession(context)} npcId={npcId} {npcLookup}; " +
                    "clicked NPC is not currently visible.")
                .ConfigureAwait(false);
            return true;
        }

        return await TryHandleLegacyNpcActionAsync(
                context,
                session,
                npcId,
                packet.OptionId,
                packet.InteractType,
                packet.Input,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsLegacySourceIgnoredZeroOptionNpcReply(uint npcId)
    {
        return IsLegacyNobilityDonorNpc(npcId) || npcId == LegacyAccountManagerNpcId;
    }

    private static async ValueTask<bool> TryHandleLegacyNpcActionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        byte interactType,
        string input,
        CancellationToken cancellationToken)
    {
        if (IsLegacyNobilityDonorNpc(npcId))
        {
            return await TryHandleLegacyNobilityDonorAsync(context, session, optionId, input, cancellationToken)
                .ConfigureAwait(false);
        }

        if (npcId == LegacyAccountManagerNpcId)
        {
            return await TryHandleLegacyAccountManagerAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (await TryHandleLegacyOnlinePointsAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyFurnitureNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (IsLegacyWarehouseNpc(npcId))
        {
            if (npcId == 8 && optionId == 0)
            {
                await SendLegacyWarehouseDialogAsync(context, npcId, cancellationToken).ConfigureAwait(false);
                return true;
            }

            if (optionId is 0 or 1)
            {
                await SendLegacyWarehouseOpenActionAsync(context, session, npcId, optionId, cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }
        }

        if (npcId == LegacyTeleportallNpcId)
        {
            return await TryHandleLegacyTeleportallAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (npcId == LegacyGreatMerchantNpcId)
        {
            if (optionId == 0)
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.GreatMerchant",
                        [LegacyGreatMerchantDialogText1, LegacyGreatMerchantDialogText2],
                        LegacyGreatMerchantOptions,
                        LegacyGreatMerchantAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            if (optionId == 20)
            {
                await SendLegacyNpcOpenGuiActionAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        "NpcHandler.GreatMerchant",
                        useMapIdField: false,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }
        }

        if (npcId == LegacyIronTongNpcId)
        {
            if (optionId == 0)
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.IronTong",
                        LegacyIronTongDialogTexts,
                        LegacyIronTongOptions,
                        LegacyIronTongAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            if (optionId == 1)
            {
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        new LegacyNpcTeleportDestination(10_137, 95, 409, "NpcHandler.IronTong"),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }
        }

        if (await TryHandleLegacyExchangeShopGoodsNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyLotteryBoxesAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyHauKanAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyPointsNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyMountRetailerAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacySteedRaceAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyClientWindowNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyGodlyArtisanHouseAdminAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyHourlyEventsNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacySpringFortuneNpcAsync(
                context,
                session,
                npcId,
                optionId,
                interactType,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyEventUtilityNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyAdminUtilityNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyDragonIslandNavigationAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyDeitylandNavigationAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyTowerOfMysteryAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyGuildGateNavigationAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyGuildConductorsAndSpaceMarksAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyArthurVipQuestAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyWhiteDragonAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyKingDoomCrystalsAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyAgatePrizeCenterAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyPoleWarEventNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyClanWarEventNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyPlayerWarEventNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyClassPoleWarNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyClassPkWarNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyUnionEliteWarEventNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyFlameWarEventNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyMiscWarEventNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacySuperGuildWarGateNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyMissedWarEventNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyEventGuideNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyVisualSettingsNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyUtilityNpcAsync(context, session, npcId, optionId, input, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyProgressionServicesNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyRewardServicesNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyTopHoursNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacySubclassMastersNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyPromotionMastersNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyShopServicesNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyLabyrinthNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyCompetitionManagersNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyBankingServicesNpcAsync(context, session, npcId, optionId, input, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyAccountSecurityNpcAsync(context, session, npcId, optionId, input, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyItemPackServicesNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyRealmTransferServicesNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyDisCityBattlefieldServicesNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyJailGuildServicesNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacySocialCraftingServicesNpcAsync(context, session, npcId, optionId, input, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyCupCasinoServicesNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyQuestEventObjectNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (npcId == LegacyTwinCityConductressNpcId)
        {
            return await TryHandleLegacyTwinCityConductressAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (await TryHandleLegacyRegionalConductressAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyLevelHuntExitAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyArchiveSkillsNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacySkillEraserNpcAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (LegacyOpenGuiNpcIds.Contains(npcId) && optionId is 0 or 1)
        {
            var sourceMethod = LegacyNpcScriptNames.TryGetValue(npcId, out var scriptName)
                ? $"NpcHandler.{scriptName}"
                : "NpcHandler.OpenGuiNpc";
            await SendLegacyNpcOpenGuiActionAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    sourceMethod,
                    useMapIdField: npcId is 1 or 4,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (npcId == LegacyArchiveJoinNpcId && optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.joinArchaveMap",
                    [LegacyArchiveJoinDialogText],
                    LegacyArchiveJoinOptions,
                    LegacyTeleportallAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (npcId == LegacyArchiveExitNpcId && optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.outArchaveMap",
                    [LegacyArchiveExitDialogText],
                    LegacyArchiveExitOptions,
                    LegacyTeleportallAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (TryGetLegacyArchiveTeleportDestination(npcId, optionId, out var archiveDestination))
        {
            await SendLegacyNpcTeleportAsync(context, session, npcId, optionId, archiveDestination, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (npcId == LegacyJobCenterNpcId && optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.joinJopCentr",
                    [LegacyJobCenterDialogText],
                    LegacyJobCenterOptions,
                    LegacyTeleportallAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (npcId == LegacyJobCenterNpcId && optionId == 1)
        {
            if (TryGetLegacyJobCenterTeleportDestination(session.Character.Class, out var jobDestination))
            {
                await SendLegacyNpcTeleportAsync(context, session, npcId, optionId, jobDestination, cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            await context.Log.WriteLineAsync(
                    "PARITY-GAP skipped LegacyNpc job-center teleport " +
                    $"{FormatSession(context)} npcId={npcId} option={optionId} " +
                    $"class={session.Character.Class} {FormatLegacyNpcScriptDiagnostic(npcId, optionId)}; " +
                    "legacy source has no matching class range.")
                .ConfigureAwait(false);
            return true;
        }

        return await TryHandleLegacyNpcScriptRuntimeAsync(
                context,
                session,
                npcId,
                optionId,
                interactType,
                input,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyNpcScriptRuntimeAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        byte interactType,
        string input,
        CancellationToken cancellationToken)
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(npcId, optionId, interactType, input));

        if (!resolution.IsModeled)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.MissingNpcScript(
                        resolution,
                        mapId: CheckedUInt32(session.Character.MapId, nameof(session.Character.MapId))))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc runtime script " +
                    $"{FormatSession(context)} npcId={npcId} option={optionId} " +
                    $"reason={resolution.Reason} nextSeam={resolution.NextSeam}.")
                .ConfigureAwait(false);
            return false;
        }

        if (resolution.Dialog is { } dialog)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    resolution.SourceMethodName ?? "NpcHandler.Unknown",
                    dialog.Lines,
                    dialog.Options.Select(option => new LegacyNpcDialogOption(option.Text, option.OptionId)).ToArray(),
                    dialog.AvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (resolution.Result is not { } result)
        {
            return false;
        }

        if (result.Kind == LegacyNpcScriptOptionResultKind.Teleport &&
            result.MapId.HasValue &&
            result.X.HasValue &&
            result.Y.HasValue)
        {
            await SendLegacyNpcTeleportAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    new LegacyNpcTeleportDestination(
                        result.MapId.Value,
                        result.X.Value,
                        result.Y.Value,
                        resolution.SourceMethodName ?? "NpcHandler.Unknown"),
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (result.Kind == LegacyNpcScriptOptionResultKind.Shop && result.ShopId == LegacyNpcShopGuiId)
        {
            await SendLegacyNpcOpenGuiActionAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    resolution.SourceMethodName ?? "NpcHandler.Unknown",
                    useMapIdField: npcId is 1 or 4,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (result.Kind == LegacyNpcScriptOptionResultKind.NoOp)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer ignored LegacyNpc runtime no-op " +
                    $"{FormatSession(context)} npcId={npcId} option={optionId} " +
                    $"source={resolution.SourceMethodName ?? "NpcHandler.Unknown"}.")
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

}
