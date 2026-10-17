using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyGodlyArtisanNpcId = 10_065;
    private const uint LegacyHouseAdminNpcId = 30_156;
    private const string LegacyGodlyArtisanSource = "NpcHandler.GodlyArtisan";
    private const string LegacyHouseAdminSource = "NpcHandler.HouseAdmin";
    private const string LegacyHouseAdminUnavailableText =
        "House management is not available yet on this server.";
    private const string LegacyGodlyArtisanMissingItemDataText =
        "I cannot repair this item yet because its item data is not loaded.";

    private static readonly LegacyNpcDialogOption[] LegacyGodlyArtisanRootOptions =
    [
        new("Repair~the~Talisman.", 20),
        new("Hat/Earring.", 1),
        new("Necklace.", 2),
        new("Coat.", 3),
        new("Weapon.", 4),
        new("Ring/Heavy~Ring/Bracelet.", 6),
        new("Boots.", 8),
        new("Riding~Crop.", 18),
        new("I~will~come~later.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyGodlyArtisanTalismanOptions =
    [
        new("Attack~Talisman.", 11),
        new("Defense~Talisman.", 12),
        new("I~will~come~later.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyHouseAdminClassSixOptions =
    [
        new("Okay,~send~me~there.", 21),
        new("Just~passing~by.", LegacyNpcClosedOption),
    ];

    private static async ValueTask<bool> TryHandleLegacyGodlyArtisanHouseAdminAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyGodlyArtisanNpcId => await TryHandleLegacyGodlyArtisanAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyHouseAdminNpcId => await TryHandleLegacyHouseAdminAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask<bool> TryHandleLegacyGodlyArtisanAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyGodlyArtisanSource,
                    ["Hello, I am skilled at repairing maximum durability. Which item do you want me to repair?"],
                    LegacyGodlyArtisanRootOptions,
                    avatarId: 63,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 20)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyGodlyArtisanSource,
                    ["Which talisman do you want me to repair?"],
                    LegacyGodlyArtisanTalismanOptions,
                    avatarId: 63,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!TryGetLegacyGodlyArtisanEquipmentPosition(optionId, out var position))
        {
            return false;
        }

        var item = session.PlayerItems.EquipmentItems.FirstOrDefault(candidate => candidate.Position == position);
        if (item is null)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyGodlyArtisanSource,
                    ["Sorry, you are not wearing this item. Please equip it first."],
                    [new("I~see.", LegacyNpcClosedOption)],
                    avatarId: 63,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (item.Durability < item.MaximumDurability)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyGodlyArtisanSource,
                    ["Sorry, please ask a shopkeeper to repair this item first. I only repair maximum durability."],
                    [new("I~see.", LegacyNpcClosedOption)],
                    avatarId: 63,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!TryReadItemType(context.WorldData, item.ItemId, out var itemType))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyGodlyArtisanSource,
                    [LegacyGodlyArtisanMissingItemDataText],
                    [new("I~see.", LegacyNpcClosedOption)],
                    avatarId: 63,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc GodlyArtisan source=NpcHandler.GodlyArtisan; " +
                    $"{FormatSession(context)} itemId={item.ItemId}; itemtype durability is not loaded.")
                .ConfigureAwait(false);
            return true;
        }

        var sourceMaximumDurability = checked((ushort)Math.Min(itemType.LegacyDurability, ushort.MaxValue));
        if (sourceMaximumDurability == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyGodlyArtisanSource,
                    [LegacyGodlyArtisanMissingItemDataText],
                    [new("I~see.", LegacyNpcClosedOption)],
                    avatarId: 63,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc GodlyArtisan source=NpcHandler.GodlyArtisan; " +
                    $"{FormatSession(context)} itemId={item.ItemId}; itemtype durability is zero.")
                .ConfigureAwait(false);
            return true;
        }

        if (item.MaximumDurability == sourceMaximumDurability)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyGodlyArtisanSource,
                    ["The maximum durability of this item is in good condition."],
                    [new("I~see.", LegacyNpcClosedOption)],
                    avatarId: 63,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var repaired = CopyLegacyGodlyArtisanItemDurability(item, sourceMaximumDurability);
        var updatedItems = ReplaceEquipmentItem(session.PlayerItems, item, repaired);
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);

        if (!string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            try
            {
                new PlayerItemSnapshotLoader(context.PlayerItemsRoot).SaveByCharacterUid(
                    updatedSession.Character.Uid,
                    updatedItems);
                await context.Log.WriteLineAsync(
                        "WorldServer persisted LegacyNpc GodlyArtisan max durability repair items " +
                        $"{FormatSession(context with { Session = updatedSession })} source={LegacyGodlyArtisanSource} " +
                        $"path={updatedSession.Character.Uid}.bin itemUid={repaired.Uid} itemId={repaired.ItemId} " +
                        $"maxDurability={item.MaximumDurability}->{sourceMaximumDurability}.")
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is ArgumentException ||
                ex is NotSupportedException)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc GodlyArtisan max durability repair item persistence " +
                        $"{FormatSession(context with { Session = updatedSession })} source={LegacyGodlyArtisanSource} " +
                        $"itemUid={repaired.Uid} itemId={repaired.ItemId} reason={ex.Message}.")
                    .ConfigureAwait(false);
            }
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc GodlyArtisan max durability repair missing PlayerItemsRoot " +
                    $"{FormatSession(context with { Session = updatedSession })} source={LegacyGodlyArtisanSource} " +
                    $"itemUid={repaired.Uid} itemId={repaired.ItemId}; repair is session-only.")
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(
                    repaired,
                    LegacyGameItemPacket.CreateRuneSpellOverride(repaired, context.WorldData?.ItemsById),
                    mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                context,
                LegacyGodlyArtisanSource,
                ["Your item durability was repaired successfully."],
                [new("Thank~you.", LegacyNpcClosedOption)],
                avatarId: 63,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc GodlyArtisan max durability repair " +
                $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId} " +
                $"maxDurability={item.MaximumDurability}->{sourceMaximumDurability}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyHouseAdminAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 20)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyHouseAdminSource,
                    [
                        "To upgrade your house to Class 6, talk to the Class 6 House Admin in the Class 6 model house. I cannot help with that here.",
                    ],
                    LegacyHouseAdminClassSixOptions,
                    avatarId: 7,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 21)
        {
            await SendLegacyNpcTeleportAsync(
                    context,
                    session,
                    LegacyHouseAdminNpcId,
                    optionId,
                    new LegacyNpcTeleportDestination(3_995, 127, 138, LegacyHouseAdminSource),
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId is 0 or 1 or 2 or 3 or 4 or 5 or 6)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyHouseAdminSource,
                    [LegacyHouseAdminUnavailableText],
                    [new("Okay.", LegacyNpcClosedOption)],
                    avatarId: 7,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc HouseAdmin source=NpcHandler.HouseAdmin; " +
                    $"{FormatSession(context)} option={optionId}; branch depends on client.MyHouse, spouse house, inventory, or furniture mutation.")
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static bool TryGetLegacyGodlyArtisanEquipmentPosition(byte optionId, out ushort position)
    {
        position = optionId switch
        {
            1 => 1,
            2 => 2,
            3 => 3,
            4 => 4,
            6 => 6,
            8 => 8,
            11 => 10,
            12 => 11,
            18 => 18,
            _ => 0,
        };
        return position != 0;
    }

    private static PlayerItemSnapshotItem CopyLegacyGodlyArtisanItemDurability(
        PlayerItemSnapshotItem item,
        ushort durability)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = item.Uid,
            ItemId = item.ItemId,
            Durability = durability,
            MaximumDurability = durability,
            Position = item.Position,
            SocketProgress = item.SocketProgress,
            SocketOne = item.SocketOne,
            SocketTwo = item.SocketTwo,
            Effect = item.Effect,
            Plus = item.Plus,
            Bless = item.Bless,
            Bound = item.Bound,
            Enchant = item.Enchant,
            Suspicious = item.Suspicious,
            Locked = item.Locked,
            PlusProgress = item.PlusProgress,
            Inscribed = item.Inscribed,
            Activate = item.Activate,
            TimeLeftInMinutes = item.TimeLeftInMinutes,
            StackSize = item.StackSize,
            WarehouseId = item.WarehouseId,
            Color = item.Color,
            UnlockTimer = item.UnlockTimer,
            ItemPoints = item.ItemPoints,
            PurificationItemId = item.PurificationItemId,
            PurificationLevel = item.PurificationLevel,
            PurificationDuration = item.PurificationDuration,
            PurificationAddedOn = item.PurificationAddedOn,
            RefineryEffectId = item.RefineryEffectId,
            RefineryEffectLevel = item.RefineryEffectLevel,
            RefineryEffectPercent = item.RefineryEffectPercent,
            RefineryEffectPercent2 = item.RefineryEffectPercent2,
            RefineryEffectDuration = item.RefineryEffectDuration,
            RefineryEffectAddedOn = item.RefineryEffectAddedOn,
            AnimaItemId = item.AnimaItemId,
            RuneExperience = item.RuneExperience,
            Attribute1 = item.Attribute1,
            Attribute2 = item.Attribute2,
            Attribute3 = item.Attribute3,
            Attribute4 = item.Attribute4,
            Attribute5 = item.Attribute5,
            Expiration = item.Expiration,
            MythSoul = item.MythSoul,
            MythSoulPoints = item.MythSoulPoints,
            PerfectionLevel = item.PerfectionLevel,
            PerfectionProgress = item.PerfectionProgress,
            OwnerUid = item.OwnerUid,
            OwnerName = item.OwnerName,
            Signature = item.Signature,
            DepositedItems = item.DepositedItems,
        };
    }
}
