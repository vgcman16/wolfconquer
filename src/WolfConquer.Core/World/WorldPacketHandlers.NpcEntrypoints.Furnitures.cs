using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int LegacyHouseFurnitureMapId = 1_511;
    private const string LegacyFurnitureSourceMethod = "NpcHandler.Furnitures";
    private static readonly LegacyNpcDialogOption[] LegacyFurnitureBuyOptions =
    [
        new("Yes.", 1),
        new("No.", LegacyNpcClosedOption),
    ];
    private static readonly LegacyNpcDialogOption[] LegacyFurnitureNoMoneyOptions =
    [
        new("I~see.", LegacyNpcClosedOption),
    ];
    private static readonly LegacyNpcDialogOption[] LegacyFurnitureNoInventorySpaceOptions =
    [
        new("Ah~yes.", LegacyNpcClosedOption),
    ];

    private static async ValueTask<bool> TryHandleLegacyFurnitureNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.MapId != LegacyHouseFurnitureMapId)
        {
            return false;
        }

        if (!TryGetLegacyFurniture(context.WorldData, npcId, out var furniture))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer ignored LegacyNpc furniture route " +
                    $"{FormatSession(context)} npcId={npcId} option={optionId} mapId={session.Character.MapId}; " +
                    "source NpcHandler.Furnitures returns when FurnitureInformations lacks the id.")
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyFurnitureSourceMethod,
                    [$"Do you want this {furniture.Name} ? It costs only {furniture.Price} silvers."],
                    LegacyFurnitureBuyOptions,
                    avatarId: 0,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId != 1)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer ignored LegacyNpc furniture option " +
                    $"{FormatSession(context)} npcId={npcId} option={optionId} mapId={session.Character.MapId}; " +
                    "source NpcHandler.Furnitures only handles options 0 and 1.")
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyFurnitureSourceMethod,
                    ["There is not enough room in your inventory."],
                    LegacyFurnitureNoInventorySpaceOptions,
                    avatarId: 0,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (session.Character.Money < furniture.Price)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyFurnitureSourceMethod,
                    [$"You do not have {furniture.Price} silvers with you."],
                    LegacyFurnitureNoMoneyOptions,
                    avatarId: 0,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!TryReadLegacyItemType(
                context.WorldData,
                CheckedUInt32(furniture.ItemTypeId, nameof(furniture.ItemTypeId)),
                out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.ClickedNpc(
                        npcId,
                        opcode: WorldPacketOpcodes.LegacyNpcServer,
                        mapId: checked((uint)session.Character.MapId),
                        optionId: optionId,
                        interactType: (byte)LegacyNpcReplyInteractType.Dialog,
                        action: LegacyNpcServerAction.Script.ToString(),
                        source: LegacyFurnitureSourceMethod,
                        reason: "furniture purchase itemtype is not loaded"))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "PARITY-GAP skipped LegacyNpc furniture purchase " +
                    $"{FormatSession(context)} npcId={npcId} itemId={furniture.ItemTypeId} " +
                    "reason=itemtype-missing; item creation requires itemtype durability.")
                .ConfigureAwait(false);
            return true;
        }

        var purchased = CreatePurchasedItems(session.PlayerItems, itemType, count: 1);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat(purchased).ToArray(),
        };
        var updatedCharacter = session.Character with
        {
            Money = checked(session.Character.Money - furniture.Price),
        };
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updatedSession);

        await SendWalletMoneyUpdateAsync(context, updatedCharacter, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(purchased[0]).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk($"You got a {itemType.Name}!").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc furniture purchase " +
                $"{FormatSession(context)} source={LegacyFurnitureSourceMethod} npcId={npcId} " +
                $"itemId={furniture.ItemTypeId} cost={furniture.Price} money={updatedCharacter.Money}.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool TryGetLegacyFurniture(
        WorldDataCatalog? worldData,
        uint npcId,
        out Furniture furniture)
    {
        furniture = default!;
        if (npcId > int.MaxValue || worldData is null)
        {
            return false;
        }

        if (!worldData.FurnitureById.TryGetValue((int)npcId, out var candidate) || candidate is null)
        {
            return false;
        }

        furniture = candidate;
        return true;
    }
}
