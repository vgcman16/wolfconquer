namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyShopServicesGapName = "LegacyNpc-ShopServices";
    private const string LegacyShopServicesUnavailableText =
        "This legacy service is unavailable here. Your CPs, items, appearance, and storage were not changed.";

    private static readonly LegacyShopServiceGap[] LegacyShopServiceGaps =
    [
        new(
            LegacyTinterNpcId,
            "NpcHandler.Tinter",
            "38457-38527",
            "equipment color mutation requires equipped item update, appearance broadcast, and persistence",
            [3, 4, 5, 6, 7, 8, 9, 13, 14, 15, 16, 17, 18, 19]),
        new(
            LegacyShopboyNpcId,
            "NpcHandler.Shopboy",
            "38529-38591",
            "dye service requires meteor or dragonball item cost, teleport or armor-color mutation, and item persistence",
            [1, 3]),
        new(
            LegacyPharmacistNpcId,
            "NpcHandler.Pharmacist",
            "53233-53326",
            "quest reward and auto-patcher state require QuestGUI, inventory reward persistence, and experience mutation",
            [3, 6]),
        new(
            LegacyTheStorekeeperNpcId,
            "NpcHandler.TheStorekeeper",
            "53328-53419",
            "quest reward and auto-patcher state require QuestGUI, inventory reward persistence, and experience mutation",
            [4, 6]),
        new(
            LegacyArmorerNpcId,
            "NpcHandler.Armorer",
            "53420-53530",
            "quest reward and auto-patcher state require QuestGUI, inventory reward persistence, and experience mutation",
            [4, 7]),
        new(
            LegacyGarmentsDealerNpcId,
            "NpcHandler.GarmentsDealer",
            "56297-56416",
            "random garment purchase requires CP debit, random item selection, inventory creation, and persistence",
            [1, 2]),
        new(
            LegacyMountsDealerNpcId,
            "NpcHandler.MountsDealer",
            "56417-56536",
            "random mount purchase requires CP debit, random item selection, inventory creation, and persistence",
            [1, 2]),
        new(
            LegacyMarketShopNpcId,
            "NpcHandler.MarketShop",
            "56537-56817",
            "market purchase requires CP debit, item/stat/VIP mutation, inventory creation, and persistence",
            [1, 2, 3, 4, 5, 7, 8, 9, 10, 11, 21, 30, 31, 55, 111, 120, 121, 122]),
        new(
            LegacyArchveShopNpcId,
            "NpcHandler.ArchveSHop",
            "56819-56954",
            "archive shop purchase requires CP debit, item or class-resource mutation, and persistence",
            [1, 2, 3, 4, 5, 6, 7, 8]),
        new(
            LegacyAccessoriesDealerNpcId,
            "NpcHandler.AccessoriesDealer",
            "57169-57236",
            "random accessory purchase requires CP debit, random item selection, inventory creation, and persistence",
            [1]),
        new(
            LegacyBeauticianDealerNpcId,
            "NpcHandler.BeauticianDealer",
            "57237-57357",
            "hairface storage purchase requires CP debit, random storage unlock, appearance catalog checks, and persistence",
            [1, 2]),
        new(
            LegacySurgeonMiracleNpcId,
            "NpcHandler.SurgeonMiracle",
            "38636-38680",
            "size change requires dragonball item cost, body mutation, appearance broadcast, and persistence",
            [2]),
        new(
            LegacyMasterXuanNpcId,
            "NpcHandler.MasterXuan",
            "38681-38800",
            "sash upgrade requires item storage inspection, CP debit, inventory removal and creation, and persistence",
            [3, 4]),
        new(
            LegacyBarberNpcId,
            "NpcHandler.Barber",
            "36480-38172",
            "barber service is quest-aware and requires silver/VIP/item costs, hair and face mutation, and persistence",
            [],
            MatchAnyOption: true),
    ];

    private static bool TryGetLegacyShopServiceGap(
        uint npcId,
        byte optionId,
        out LegacyShopServiceGap gap)
    {
        gap = default!;
        foreach (var candidate in LegacyShopServiceGaps)
        {
            if (candidate.NpcId != npcId)
            {
                continue;
            }

            if (candidate.MatchAnyOption || ContainsOption(candidate.OptionIds, optionId))
            {
                gap = candidate;
                return true;
            }
        }

        return false;
    }

    private static async ValueTask SendLegacyShopServiceUnavailableAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        LegacyShopServiceGap gap,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                gap.SourceMethod,
                [LegacyShopServicesUnavailableText],
                [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
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
                    source: gap.SourceMethod,
                    reason: gap.Reason))
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP unavailable LegacyNpc shop-service " +
                $"{FormatSession(context)} gap={LegacyShopServicesGapName} npcId={npcId} optionId={optionId} " +
                $"source={gap.SourceMethod} sourceLines={gap.SourceLines} reason={gap.Reason}.")
            .ConfigureAwait(false);
    }

    private sealed record LegacyShopServiceGap(
        uint NpcId,
        string SourceMethod,
        string SourceLines,
        string Reason,
        byte[] OptionIds,
        bool MatchAnyOption = false);
}
