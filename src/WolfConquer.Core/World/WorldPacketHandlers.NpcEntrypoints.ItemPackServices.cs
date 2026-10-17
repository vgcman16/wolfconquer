using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyItemPackServicesDefaultAvatarId = 7;
    private const string LegacyItemPackServicesGapName = "LegacyNpc-ItemPackServices";
    private const string LegacyItemPackServicesUnavailableText =
        "This selection is not available yet. No items were removed or granted.";
    private const string LegacyItemPackServicesInventorySpaceText =
        "Please make one more space in your inventory.";

    private static readonly LegacyNpcDialogOption[] LegacyItemPackServicesCloseOptions =
    [
        new("I~see.", LegacyNpcClosedOption),
    ];

    public static async ValueTask<bool> TryHandleLegacyItemPackServicesNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyItemPackServicesSteed3PackNpcId => await LegacyItemPackServicesTryHandleBoundSteedPackAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    LegacyItemPackServicesSteed3PackItemId,
                    "+3",
                    "NpcHandler.Steed3Pack",
                    "21312-21363",
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesSteed1PackNpcId => await LegacyItemPackServicesTryHandleBoundSteedPackAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    LegacyItemPackServicesSteed1PackItemId,
                    "+1",
                    "NpcHandler.Steed1Pack",
                    "21365-21416",
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesBlackFridayAccesoryNpcId => await LegacyItemPackServicesTryHandleCatalogSelectorAsync(
                    context,
                    session,
                    optionId,
                    LegacyItemPackServicesBlackFridayAccessoryCatalogService,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesBlackFridayMountPackNpcId => await LegacyItemPackServicesTryHandleCatalogSelectorAsync(
                    context,
                    session,
                    optionId,
                    LegacyItemPackServicesBlackFridayMountCatalogService,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesBlackFridayGarmentPackNpcId => await LegacyItemPackServicesTryHandleCatalogSelectorAsync(
                    context,
                    session,
                    optionId,
                    LegacyItemPackServicesBlackFridayGarmentCatalogService,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesAccesoryPacket2NpcId => await LegacyItemPackServicesTryHandleCatalogSelectorAsync(
                    context,
                    session,
                    optionId,
                    LegacyItemPackServicesAccessoryPacket2CatalogService,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesAccesoryPacketNpcId => await LegacyItemPackServicesTryHandleCatalogSelectorAsync(
                    context,
                    session,
                    optionId,
                    LegacyItemPackServicesAccessoryPacketCatalogService,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesMountPacket3NpcId => await LegacyItemPackServicesTryHandleCatalogSelectorAsync(
                    context,
                    session,
                    optionId,
                    LegacyItemPackServicesMountPacket3CatalogService,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesMountPacket2NpcId => await LegacyItemPackServicesTryHandleCatalogSelectorAsync(
                    context,
                    session,
                    optionId,
                    LegacyItemPackServicesMountPacket2CatalogService,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesMountPacketNpcId => await LegacyItemPackServicesTryHandleCatalogSelectorAsync(
                    context,
                    session,
                    optionId,
                    LegacyItemPackServicesMountPacketCatalogService,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesGarmentPacket2NpcId => await LegacyItemPackServicesTryHandleCatalogSelectorAsync(
                    context,
                    session,
                    optionId,
                    LegacyItemPackServicesGarmentPacket2CatalogService,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesGarmentPacketNpcId => await LegacyItemPackServicesTryHandleCatalogSelectorAsync(
                    context,
                    session,
                    optionId,
                    LegacyItemPackServicesGarmentPacketCatalogService,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesSelectSacredRefineryPackNpcId => await LegacyItemPackServicesTryHandleSacredRefineryPackAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesIdealDesgnerNpcId => await LegacyItemPackServicesTryHandleIdealDesgnerAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesAstralPheonixNpcId => await LegacyItemPackServicesTryHandleAstralPheonixAsync(
                    context,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesSteed1NpcId => await LegacyItemPackServicesTryHandleSimpleSteedPackAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    LegacyItemPackServicesSteed1ItemId,
                    "+1",
                    "NpcHandler.Steed1",
                    "29996-30035",
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesDazzlingDiamondBoxNpcId => await LegacyItemPackServicesTryHandleDazzlingDiamondBoxAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesSteed3NpcId => await LegacyItemPackServicesTryHandleSimpleSteedPackAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    LegacyItemPackServicesSteed3ItemId,
                    "+3",
                    "NpcHandler.Steed3",
                    "30107-30147",
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesSteed6NpcId => await LegacyItemPackServicesTryHandleSimpleSteedPackAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    LegacyItemPackServicesSteed6ItemId,
                    "+6",
                    "NpcHandler.Steed6",
                    "30149-30189",
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesRareSteedPack6NpcId => await LegacyItemPackServicesTryHandleRareSteedPackAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    LegacyItemPackServicesRareSteedPack6ItemId,
                    "NpcHandler.RareSteedPack6",
                    "30191-30242",
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesNobleSteedPackNpcId => await LegacyItemPackServicesTryHandleRareSteedPackAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    LegacyItemPackServicesNobleSteedPackItemId,
                    "NpcHandler.NobleSteedPack",
                    "30245-30296",
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesSelectP7EquipmentSoulPackNpcId => await LegacyItemPackServicesTryHandleP7EquipmentSoulPackAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesSelectP7WeaponSoulPackNpcId => await LegacyItemPackServicesTryHandleP7WeaponSoulPackAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesP7SacredTreasuresEnvoyNpcId => await LegacyItemPackServicesTryHandleP7SacredTreasuresEnvoyAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyItemPackServicesSarckSoldierNpcId or
                LegacyItemPackServicesSarckSoldierEntranceNpcId or
                LegacyItemPackServicesSarckSoldierSupplyPoint1NpcId or
                LegacyItemPackServicesSarckSoldierSupplyPoint2NpcId or
                LegacyItemPackServicesSarckSoldierSupplyPoint3NpcId or
                LegacyItemPackServicesSarckSoldierSupplyPoint4NpcId or
                LegacyItemPackServicesSarckSoldierSupplyPoint5NpcId => await LegacyItemPackServicesTryHandleSarckSoldierAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask LegacyItemPackServicesSendConfirmAsync(
        WorldPacketHandlerContext context,
        string source,
        string text,
        byte yesOptionId,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                source,
                [text],
                [
                    new("Yes.", yesOptionId),
                    new("No.", LegacyNpcClosedOption),
                ],
                avatarId: null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask LegacyItemPackServicesSendMessageBoxAsync(
        WorldPacketHandlerContext context,
        string source,
        string text,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox(text).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer sent LegacyNpc message-box {FormatSession(context)} source={source}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask LegacyItemPackServicesSendUnavailableAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        string source,
        string sourceLines,
        string reason,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                source,
                [LegacyItemPackServicesUnavailableText],
                LegacyItemPackServicesCloseOptions,
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
                    action: "Script",
                    source: source,
                    reason: reason))
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc ItemPackServices " +
                $"{FormatSession(context)} gap={LegacyItemPackServicesGapName} source={source} " +
                $"sourceLines={sourceLines} npcId={npcId.ToString(CultureInfo.InvariantCulture)} " +
                $"optionId={optionId.ToString(CultureInfo.InvariantCulture)} reason={reason}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask LegacyItemPackServicesLogNoOpAsync(
        WorldPacketHandlerContext context,
        uint npcId,
        byte optionId,
        string source,
        string sourceLines)
    {
        await context.Log.WriteLineAsync(
                "WorldServer ignored LegacyNpc ItemPackServices no-op " +
                $"{FormatSession(context)} source={source} sourceLines={sourceLines} " +
                $"npcId={npcId.ToString(CultureInfo.InvariantCulture)} optionId={optionId.ToString(CultureInfo.InvariantCulture)}.")
            .ConfigureAwait(false);
    }

    private static bool LegacyItemPackServicesHasStack(
        WorldSession session,
        uint itemId,
        ushort count)
    {
        var remaining = count;
        foreach (var item in session.PlayerItems.InventoryItems)
        {
            if (item.ItemId != itemId || item.Bound != 0)
            {
                continue;
            }

            if (item.StackSize >= remaining)
            {
                return true;
            }

            remaining = checked((ushort)(remaining - item.StackSize));
        }

        return false;
    }
}
