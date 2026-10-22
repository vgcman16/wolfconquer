using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyFloorPickupPlan(WorldSession Updated, FloorItemRuntimeEntry Entry,
        PlayerItemSnapshotItem? Item, IReadOnlyList<byte[]> BeforeRemovalFrames, byte[] PickupTextFrame,
        bool CreditsQuest1311, LegacyFloorPickupInventoryChange? InventoryChange);

    private static bool TryPlanLegacyFloorPickup(WorldPacketHandlerContext context, WorldSession current,
        uint floorUid, out LegacyFloorPickupPlan plan, out bool ownerBypass, out string reason, out byte[]? rejection)
    {
        plan = null!;
        ownerBypass = false;
        rejection = null;
        reason = "floorStoreUnavailable";
        if (context.FloorItems is not { } floor) return false;
        if (current.Character.Uid is <= 0 or > uint.MaxValue)
        { reason = "playerUidOutsideLegacyRange"; return false; }
        var status = floor.GetPickupStatus(floorUid, (uint)current.Character.Uid, DateTimeOffset.UtcNow, out var entry);
        if (status is not (FloorItemRuntimePickupStatus.Available or FloorItemRuntimePickupStatus.OwnerLocked))
        { reason = "floorItemUnavailable:" + status; return false; }
        if (entry.Effect is not null || entry.Packet.DropType != LegacyFloorItemDropType.Visible)
        { reason = "floorEffectNotPickable"; return false; }
        if (status == FloorItemRuntimePickupStatus.OwnerLocked)
        {
            ownerBypass = AllowsLegacyProtectedFloorPickup(context, entry);
            if (!ownerBypass)
            {
                reason = "ownerLocked:ownerUid=" + entry.Packet.ItemOwnerUid;
                rejection = LegacyMsgMessagePacket.SystemTalk("You have to wait a little bit before you can pick up any items dropped from monsters killed by other players.").ToFrameBytes();
                return false;
            }
        }
        var mapId = entry.MapId == 0 ? current.Character.MapId : entry.MapId;
        var distance = LegacyGetDistance(current.Character.X, current.Character.Y, entry.Packet.X, entry.Packet.Y);
        if (current.Character.MapId != mapId || distance > LegacyFloorItemPickupRange)
        { reason = $"floorItemOutOfRange:map={mapId}:distance={distance}"; return false; }

        PlayerItemSnapshotItem? item = null;
        ItemType? itemType = null;
        LegacyFloorPickupWalletChange? wallet = null;
        LegacyFloorPickupInventoryChange? inventory = null;
        var updated = current;
        if (entry.Kind == FloorItemRuntimePickupKind.Item)
        {
            if (!HasLegacyInventorySpace(current.PlayerItems, 1))
            { reason = "inventory is full"; return false; }
            if (!TryCreateLegacyFloorPickupItem(context, current, entry, out item, out itemType))
            { reason = "itemtype data was not loaded"; return false; }
            if (item!.Uid == 0 || item.ItemId != entry.Packet.ItemId || CountLegacyNpcSaleUid(current.PlayerItems, item.Uid) != 0)
            { reason = "pickupItemIdentityInvalidOrAlreadyOwned"; return false; }
            if (!LegacyFloorPickupInventory.TryPlan(current.PlayerItems, item, itemType!, DateTime.Now,
                out inventory, out reason)) return false;
            updated = current with { PlayerItems = inventory.UpdatedItems };
            item = inventory.Item;
        }
        else
        {
            if (entry.Item is not null)
            { reason = "currencyFloorItemUnexpected"; return false; }
            if (!LegacyFloorPickupWallet.TryAdd(current.Character, entry.Kind, entry.Amount, out wallet, out reason)) return false;
            updated = current with { Character = wallet.UpdatedCharacter };
        }

        // Validate every success representation before disk/floor mutation.
        var frames = new List<byte[]>();
        if (item is not null)
        {
            AppendLegacyFloorPickupItemFrames(frames, inventory!);
        }
        else
        {
            if (wallet!.SilverTransferred > 0)
                frames.Add((LegacyMsgMessagePacket.SystemTalk(wallet.SilverTransferred.ToString(CultureInfo.InvariantCulture) +
                    " Gold has transfare to your warehouse you can't hold more than 500,000,000,000 into your inventory.")
                    with { ChatType = 2600, Color = LegacyMsgMessagePacket.YellowColor }).ToFrameBytes());
            if (wallet.CpsTransferred > 0)
                frames.Add(LegacyNpcReplyPacket.ForMessageBox(wallet.CpsTransferred.ToString(CultureInfo.InvariantCulture) +
                    " Your ConquerPoints[CPS] has Transfare To Your Bank Thanks.").ToFrameBytes());
            var type = entry.Kind == FloorItemRuntimePickupKind.Money
                ? LegacyMsgUpdateDataType.Money : LegacyMsgUpdateDataType.ConquerPoints;
            var value = entry.Kind == FloorItemRuntimePickupKind.Money ? updated.Character.Money : updated.Character.ConquerPoints;
            var update = new LegacyMsgUpdatePacket((uint)current.Character.Uid, [new(type, checked((ulong)value))]);
            frames.Add(update.ToFrameBytes());
            // Native Money setter + explicit FloorMap send. CP keeps the existing
            // managed send; native FullLoading/Poker/SQL owners remain separate.
            if (entry.Kind == FloorItemRuntimePickupKind.Money) frames.Add(update.ToFrameBytes());
        }
        var text = entry.Kind switch
        {
            FloorItemRuntimePickupKind.Money => $"You have picked up a {entry.Amount} silvers.",
            FloorItemRuntimePickupKind.ConquerPoints => $"You have picked up a {entry.Amount} ConquerPoints.",
            _ => $"You have picked up a {itemType?.Name ?? entry.Packet.ItemId.ToString(CultureInfo.InvariantCulture)}.",
        };
        var pickupText = LegacyMsgMessagePacket.SystemTalk(text).ToFrameBytes();
        _ = (entry.Packet with { DropType = LegacyFloorItemDropType.Remove }).ToFrameBytes();
        plan = new(updated, entry, item, frames, pickupText, itemType?.Id == 711_352, inventory);
        reason = string.Empty;
        return true;
    }
}
