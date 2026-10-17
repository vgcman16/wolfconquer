namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacySocialCraftingServicesUnavailableText =
        "This legacy service is unavailable here. Your character, social state, items, CPs, and event records were not changed.";

    private static readonly LegacyNpcDialogOption[] LegacySocialCraftingServicesCloseOptions =
    [
        new("I understand.", LegacyNpcClosedOption),
    ];

    public static async ValueTask<bool> TryHandleLegacySocialCraftingServicesNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        string input,
        CancellationToken cancellationToken)
    {
        if (LegacySocialCraftingServicesIsTcTeleporterNpc(npcId))
        {
            return await TryHandleLegacySocialCraftingServicesTcTeleporterAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (LegacySocialCraftingServicesIsRuneFragmentItemId(npcId))
        {
            return await TryHandleLegacySocialCraftingServicesRuneFragmentsAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return npcId switch
        {
            LegacySocialCraftingServicesJiangHuNpcId => await TryHandleLegacySocialCraftingServicesJiangHuAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacySocialCraftingServicesClanNpcId => await TryHandleLegacySocialCraftingServicesClanAsync(
                    context,
                    session,
                    optionId,
                    input,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacySocialCraftingServicesGuildCreatorNpcId => await TryHandleLegacySocialCraftingServicesGuildCreatorAsync(
                    context,
                    session,
                    optionId,
                    input,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacySocialCraftingServicesLoveStoneNpcId => await TryHandleLegacySocialCraftingServicesLoveStoneAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacySocialCraftingServicesLevelNpcId => await TryHandleLegacySocialCraftingServicesLevelAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacySocialCraftingServicesOfflineTgNpcId => await TryHandleLegacySocialCraftingServicesOfflineTgAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacySocialCraftingServicesDragonFurnaceForgingNpcId => await TryHandleLegacySocialCraftingServicesDragonFurnaceForgingAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacySocialCraftingServicesDragonFurnaceTwinNpcId => await TryHandleLegacySocialCraftingServicesDragonFurnaceTwinAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacySocialCraftingServicesLuckySmeltingListNpcId => await TryHandleLegacySocialCraftingServicesLuckySmeltingListAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacySocialCraftingServicesDragonFurnaceFusingNpcId => await TryHandleLegacySocialCraftingServicesDragonFurnaceFusingAsync(
                    context,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacySocialCraftingServicesSolarFurnaceNpcId => await TryHandleLegacySocialCraftingServicesSmeltingFurnaceAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    LegacySocialCraftingServicesSolarFurnaceSource,
                    LegacySocialCraftingServicesSolarFurnaceLines,
                    "solar",
                    cancellationToken)
                .ConfigureAwait(false),
            LegacySocialCraftingServicesLunarFurnaceNpcId => await TryHandleLegacySocialCraftingServicesSmeltingFurnaceAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    LegacySocialCraftingServicesLunarFurnaceSource,
                    LegacySocialCraftingServicesLunarFurnaceLines,
                    "lunar",
                    cancellationToken)
                .ConfigureAwait(false),
            LegacySocialCraftingServicesCloudBeastNpcId => await TryHandleLegacySocialCraftingServicesCloudBeastAsync(
                    context,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacySocialCraftingServicesGeneralOfDefenseNpcId => await TryHandleLegacySocialCraftingServicesGeneralOfDefenseAsync(
                    context,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacySocialCraftingServicesAutumnNpcId => await TryHandleLegacySocialCraftingServicesAutumnAsync(
                    context,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacySocialCraftingServicesArtisanZhuRongNpcId => await TryHandleLegacySocialCraftingServicesArtisanZhuRongAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacySocialCraftingServicesNobilityWarNpcId => await TryHandleLegacySocialCraftingServicesNobilityWarAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacySocialCraftingServicesHuntingAreaNpcId => await TryHandleLegacySocialCraftingServicesHuntingAreaAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask SendLegacySocialCraftingServicesInputDialogAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        IReadOnlyList<string> texts,
        string inputLabel,
        byte inputOptionId,
        ushort maxLength,
        ushort? avatarId,
        CancellationToken cancellationToken)
    {
        foreach (var text in texts)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForDialog(text).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForInput(inputLabel, inputOptionId, maxLength).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForClosedOption("Cancel.").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        if (avatarId.HasValue)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForAvatar(avatarId.Value).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForFinish().ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyNpc SocialCraftingServices input dialog " +
                $"{FormatSession(context)} source={sourceMethod} inputOptionId={inputOptionId} maxLength={maxLength}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacySocialCraftingServicesGapAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        string sourceMethod,
        string sourceLines,
        string reason,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                [LegacySocialCraftingServicesUnavailableText],
                LegacySocialCraftingServicesCloseOptions,
                avatarId: null,
                cancellationToken)
            .ConfigureAwait(false);
        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.ClickedNpc(
                    npcId,
                    opcode: WorldPacketOpcodes.LegacyNpcServer,
                    mapId: checked((uint)session.Character.MapId),
                    optionId: optionId,
                    interactType: (byte)LegacyNpcReplyInteractType.Dialog,
                    action: LegacyNpcServerAction.Script.ToString(),
                    source: sourceMethod,
                    reason: reason))
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc SocialCraftingServices " +
                $"{FormatSession(context)} source={sourceMethod} sourceLines={sourceLines} " +
                $"npcId={npcId} optionId={optionId} reason={reason}.")
            .ConfigureAwait(false);
    }
}
