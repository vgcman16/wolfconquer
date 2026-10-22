using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask SendLegacyCreateSocketItemResponseAsync(
            WorldPacketHandlerContext context,
            uint itemUid,
            bool success,
            CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(
                new MsgItemUsagePacket(
                    itemUid,
                    DwParam: success ? 1UL : 0UL,
                    MsgItemUsageAction.CreateSocketItem,
                    Timestamp: 0,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static IReadOnlyList<uint> ReadLegacyItemUsageArgs(PacketFrame frame)
    {
        if (!PacketReader.TryRead(frame.Frame.ToArray(), out var reader) ||
            reader.Payload.Length <= MsgItemUsagePacket.PayloadLength)
        {
            return [];
        }

        var args = new List<uint>();
        for (var offset = MsgItemUsagePacket.PayloadLength;
             offset <= reader.Payload.Length - sizeof(uint);
             offset += sizeof(uint))
        {
            args.Add(reader.ReadUInt32(offset));
        }

        return args;
    }

    private static bool TryFindLegacySocketTarget(
            PlayerItemSnapshot items,
            uint itemUid,
            out PlayerItemSnapshotItem target,
            out bool targetIsEquipment)
    {
        target = items.InventoryItems.FirstOrDefault(item => item.Uid == itemUid)!;
        if (target is not null)
        {
            targetIsEquipment = false;
            return true;
        }

        target = items.EquipmentItems.FirstOrDefault(item => item.Uid == itemUid)!;
        targetIsEquipment = target is not null;
        return target is not null;
    }

    private static bool IsLegacySocketUpdateAllowed(uint itemId)
    {
        return ItemType.GetLegacyEquipmentPosition(itemId) is not (
            7 or
            9 or
            10 or
            11 or
            12 or
            13 or
            15 or
            16 or
            17 or
            18 or
            19 or
            57 or
            59);
    }

    private static bool TryConsumeLegacySocketMaterials(
            PlayerItemSnapshot items,
            IReadOnlyList<uint> materialUids,
            uint materialItemId,
            int count,
            out PlayerItemSnapshot updatedItems,
            out IReadOnlyList<byte[]> frames)
    {
        updatedItems = items;
        frames = [];
        if (count <= 0 || materialUids.Count != count)
        {
            return false;
        }

        var remainingUids = new HashSet<uint>(materialUids);
        if (remainingUids.Count != count)
        {
            return false;
        }

        // Native VerifiedUpdateItem selects physical entries, never units from a stack.
        var selectedItems = new List<PlayerItemSnapshotItem>(count);
        foreach (var item in items.InventoryItems)
        {
            if (item.ItemId == materialItemId && remainingUids.Remove(item.Uid))
            {
                selectedItems.Add(item);
            }
        }

        if (remainingUids.Count != 0 || selectedItems.Count != count)
        {
            return false;
        }

        var inventoryItems = items.InventoryItems.ToList();
        var mutationFrames = new List<byte[]>();
        foreach (var item in selectedItems)
        {
            var index = inventoryItems.IndexOf(item);
            if (item.StackSize > 1 && item.Position < 40)
            {
                var updatedItem = item.WithStackSize(checked((ushort)(item.StackSize - 1)));
                inventoryItems[index] = updatedItem;
                mutationFrames.Add(
                    LegacyGameItemPacket
                        .FromSnapshotItem(updatedItem, mode: LegacyGameItemPacket.UpdateItemMode)
                        .ToFrameBytes());
            }
            else
            {
                mutationFrames.Add(
                    new MsgItemUsagePacket(
                        item.Uid,
                        DwParam: 0,
                        MsgItemUsageAction.RemoveInventory,
                        Timestamp: 0,
                        DwParam2: 0,
                        DwParam3: 0,
                        DwParam4: 0).ToFrameBytes());
                inventoryItems.RemoveAt(index);
            }
        }

        updatedItems = new PlayerItemSnapshot(
            inventoryItems,
            items.RelicFuseItems,
            items.MythsoulBagItems,
            items.EquipmentItems,
            items.RuneItems,
            items.WardrobeItems)
        {
            WarehouseItems = items.WarehouseItems,
        };
        frames = mutationFrames;
        return true;
    }

    private static PlayerItemSnapshot ReplaceLegacySocketTarget(
            PlayerItemSnapshot items,
            PlayerItemSnapshotItem oldItem,
            PlayerItemSnapshotItem newItem,
            bool targetIsEquipment)
    {
        return targetIsEquipment
            ? ReplaceEquipmentItem(items, oldItem, newItem)
            : ReplaceInventoryItem(items, oldItem, newItem);
    }

    private static PlayerItemSnapshotItem CreateLegacyStarDrillReward(PlayerItemSnapshot items)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = GetNextSessionItemUid(items),
            ItemId = LegacyStarDrillItemId,
            Durability = 1,
            MaximumDurability = 1,
            Position = 0,
            StackSize = 1,
        };
    }

    private static PlayerItemSnapshotItem CopyLegacyItemWithSocketOne(
            PlayerItemSnapshotItem item,
            byte socketOne)
    {
        return CopyLegacyItemWithSockets(item, socketOne, item.SocketTwo);
    }

    private static PlayerItemSnapshotItem CopyLegacyItemWithSocketTwo(
            PlayerItemSnapshotItem item,
            byte socketTwo)
    {
        return CopyLegacyItemWithSockets(item, item.SocketOne, socketTwo);
    }

    private static PlayerItemSnapshotItem CopyLegacyItemWithSockets(
            PlayerItemSnapshotItem item,
            byte socketOne,
            byte socketTwo)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = item.Uid,
            ItemId = item.ItemId,
            Durability = item.Durability,
            MaximumDurability = item.MaximumDurability,
            Position = item.Position,
            SocketProgress = item.SocketProgress,
            SocketOne = socketOne,
            SocketTwo = socketTwo,
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

    private static int ReadLegacyToughDrillSocketRoll(CharacterSnapshot character, out bool forced)
    {
        forced = TryReadInt64Attribute(character.Attributes, RuntimeLegacyToughDrillSocketRollAttribute, out var forcedRoll) &&
            forcedRoll >= 0;
        return forced
            ? checked((int)(forcedRoll % 100))
            : Random.Shared.Next(0, 100);
    }
}
