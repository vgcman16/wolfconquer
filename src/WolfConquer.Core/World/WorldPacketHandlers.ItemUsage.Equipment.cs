using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{

    private static WorldSession ClampLegacyEquipmentRuntimeHitpoints(
            WorldSession session,
            WorldDataCatalog? worldData)
    {
        if (worldData is null)
        {
            return session;
        }

        var currentHitpoints = CheckedUInt32(session.Character.MinHitPoints, nameof(session.Character.MinHitPoints));
        var maxHitpoints = ComputeLegacyRuntimeMaxHitpoints(
            session.Character,
            currentHitpoints,
            session.PlayerItems,
            session.CombatGear ?? CombatGearSnapshot.Empty,
            worldData,
            session.AlternateEquipmentActive);
        var clampedHitpoints = Math.Min(currentHitpoints, maxHitpoints);
        if (clampedHitpoints == currentHitpoints)
        {
            return session;
        }

        return session with
        {
            Character = session.Character with { MinHitPoints = clampedHitpoints },
        };
    }

    private static async ValueTask SendLegacyEquipmentRuntimeRefreshAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            CancellationToken cancellationToken)
    {
        if (context.WorldData is not { } worldData)
        {
            return;
        }

        var uid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var currentHitpoints = CheckedUInt32(session.Character.MinHitPoints, nameof(session.Character.MinHitPoints));
        var maxHitpoints = ComputeLegacyRuntimeMaxHitpoints(
            session.Character,
            currentHitpoints,
            session.PlayerItems,
            session.CombatGear ?? CombatGearSnapshot.Empty,
            worldData,
            session.AlternateEquipmentActive);

        foreach (var update in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(
            uid,
            maxHitpoints,
            Math.Min(currentHitpoints, maxHitpoints)))
        {
            await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                CreateNativeMagicCachedPlayerStatusResponse(context, session).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyUserTotalRefineLevPacket.FromEquipmentPerfection(uid, session.PlayerItems).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent MsgItemUsage equipment stat refresh " +
                $"{FormatSession(context with { Session = session })} maxHitpoints={maxHitpoints} hitpoints={currentHitpoints}.")
            .ConfigureAwait(false);
    }

    private static PlayerItemSnapshot MoveEquippedItemToInventory(
            PlayerItemSnapshot items,
            PlayerItemSnapshotItem equipped,
            PlayerItemSnapshotItem moved)
    {
        return new PlayerItemSnapshot(
            items.InventoryItems.Concat([moved]).ToArray(),
            items.RelicFuseItems,
            items.MythsoulBagItems,
            items.EquipmentItems.Where(item => item.Uid != equipped.Uid).ToArray(),
            items.RuneItems,
            items.WardrobeItems)
        {
            WarehouseItems = items.WarehouseItems,
        };
    }

    private static PlayerItemSnapshot ReplaceInventoryItem(
            PlayerItemSnapshot items,
            PlayerItemSnapshotItem oldItem,
            PlayerItemSnapshotItem newItem)
    {
        return new PlayerItemSnapshot(
            items.InventoryItems.Select(item => item.Uid == oldItem.Uid ? newItem : item).ToArray(),
            items.RelicFuseItems,
            items.MythsoulBagItems,
            items.EquipmentItems,
            items.RuneItems,
            items.WardrobeItems)
        {
            WarehouseItems = items.WarehouseItems,
        };
    }

    private static PlayerItemSnapshot ReplaceEquipmentItem(
            PlayerItemSnapshot items,
            PlayerItemSnapshotItem oldItem,
            PlayerItemSnapshotItem newItem)
    {
        return new PlayerItemSnapshot(
            items.InventoryItems,
            items.RelicFuseItems,
            items.MythsoulBagItems,
            items.EquipmentItems
                .Select(item => item.Uid == oldItem.Uid ? newItem : item)
                .OrderBy(item => item.Position)
                .ThenBy(item => item.Uid)
                .ToArray(),
            items.RuneItems,
            items.WardrobeItems)
        {
            WarehouseItems = items.WarehouseItems,
        };
    }

    private static PlayerItemSnapshot MoveInventoryItemToEquipment(
            PlayerItemSnapshot items,
            PlayerItemSnapshotItem inventoryItem,
            PlayerItemSnapshotItem equippedItem)
    {
        return new PlayerItemSnapshot(
            items.InventoryItems.Where(item => item.Uid != inventoryItem.Uid).ToArray(),
            items.RelicFuseItems,
            items.MythsoulBagItems,
            items.EquipmentItems.Concat([equippedItem]).OrderBy(item => item.Position).ThenBy(item => item.Uid).ToArray(),
            items.RuneItems,
            items.WardrobeItems)
        {
            WarehouseItems = items.WarehouseItems,
        };
    }

    private static PlayerItemSnapshot ReplaceEquippedItems(
            PlayerItemSnapshot items,
            PlayerItemSnapshotItem inventoryItem,
            IReadOnlyList<(PlayerItemSnapshotItem Equipped, PlayerItemSnapshotItem Inventory)> displacedItems,
            PlayerItemSnapshotItem equippedItem)
    {
        var displacedUids = displacedItems
            .Select(item => item.Equipped.Uid)
            .ToHashSet();

        return new PlayerItemSnapshot(
            items.InventoryItems
                .Where(item => item.Uid != inventoryItem.Uid)
                .Concat(displacedItems.Select(item => item.Inventory))
                .ToArray(),
            items.RelicFuseItems,
            items.MythsoulBagItems,
            items.EquipmentItems
                .Where(item => !displacedUids.Contains(item.Uid))
                .Concat([equippedItem])
                .OrderBy(item => item.Position)
                .ThenBy(item => item.Uid)
                .ToArray(),
            items.RuneItems,
            items.WardrobeItems)
        {
            WarehouseItems = items.WarehouseItems,
        };
    }

    private static PlayerItemSnapshotItem? FindLeftHandItemToClearForTwoHandEquip(
            PlayerItemSnapshot items,
            PlayerItemSnapshotItem item,
            ushort position)
    {
        if (position is not 4 and not 54 || !ItemType.IsLegacyTwoHandWeapon(item.ItemId))
        {
            return null;
        }

        var leftHandPosition = position == 54 ? 55 : 5;
        var leftHand = items.EquipmentItems.FirstOrDefault(equipped => equipped.Position == leftHandPosition);
        return leftHand;
    }

    private static bool TryResolveEquipPosition(
            WorldSession session,
            PlayerItemSnapshotItem item,
            ulong requestedPosition,
            out ushort position,
            out bool alternate)
    {
        position = 0;
        alternate = false;
        if (requestedPosition > ushort.MaxValue)
        {
            return false;
        }

        var legacyPosition = ItemType.GetLegacyEquipmentPosition(item.ItemId);
        var effectiveRequestedPosition = requestedPosition == 0 ? legacyPosition : requestedPosition;
        if (effectiveRequestedPosition >= 50)
        {
            return TryResolveAlternateEquipPosition(
                session,
                item,
                legacyPosition,
                effectiveRequestedPosition,
                out position,
                out alternate);
        }

        if (ItemType.IsLegacyKnife(item.ItemId) && !IsLegacyArcherClass(session.Character.Class))
        {
            return false;
        }

        if (ItemType.IsLegacyHookScythe(item.ItemId) && !CanLegacyClassEquipHookScythe(session))
        {
            return false;
        }

        if (ItemType.IsLegacyPistol(item.ItemId))
        {
            if (effectiveRequestedPosition != 5 || !CanEquipLegacyPistol(session))
            {
                return false;
            }

            position = 5;
            return true;
        }

        if (legacyPosition == 4)
        {
            if (effectiveRequestedPosition == 4)
            {
                if (!CanEquipLegacyRightHandItem(session, item))
                {
                    return false;
                }

                position = legacyPosition;
                return true;
            }

            if (effectiveRequestedPosition != 5 || !CanEquipRightHandWeaponInLeftHand(session, item))
            {
                return false;
            }

            position = 5;
            return true;
        }

        if (legacyPosition == 5)
        {
            if (effectiveRequestedPosition != 5 || !CanEquipLegacyLeftHandItem(session, item))
            {
                return false;
            }

            position = 5;
            return true;
        }

        if (!IsSimpleNormalEquipPosition(legacyPosition))
        {
            return false;
        }

        position = legacyPosition;
        return true;
    }

    private static bool IsSimpleNormalEquipPosition(ushort position)
    {
        return position is 1 or 2 or 3 or 6 or 7 or 8 or 10 or 11 or 12 or 13 or 18 or 19;
    }

    private static bool IsSimpleAlternateEquipPosition(ushort position)
    {
        return position is 1 or 2 or 3 or 6 or 7 or 8 or 13;
    }

    private static bool TryResolveAlternateEquipPosition(
            WorldSession session,
            PlayerItemSnapshotItem item,
            ushort legacyPosition,
            ulong requestedPosition,
            out ushort position,
            out bool alternate)
    {
        position = 0;
        alternate = false;

        var requestedBasePosition = (ushort)(requestedPosition - 50);
        if (ItemType.IsLegacyKnife(item.ItemId) && !IsLegacyArcherClass(session.Character.Class))
        {
            return false;
        }

        if (ItemType.IsLegacyHookScythe(item.ItemId) && !CanLegacyClassEquipHookScythe(session))
        {
            return false;
        }

        if (ItemType.IsLegacyPistol(item.ItemId))
        {
            if (requestedBasePosition != 5 || !CanEquipLegacyPistol(session, alternate: true))
            {
                return false;
            }

            position = 55;
            alternate = true;
            return true;
        }

        if (legacyPosition == 4)
        {
            if (requestedBasePosition == 4)
            {
                if (!CanEquipLegacyRightHandItem(session, item, alternate: true))
                {
                    return false;
                }

                position = 54;
                alternate = true;
                return true;
            }

            if (requestedBasePosition != 5 || !CanEquipRightHandWeaponInLeftHand(session, item, alternate: true))
            {
                return false;
            }

            position = 55;
            alternate = true;
            return true;
        }

        if (legacyPosition == 5)
        {
            if (requestedBasePosition != 5 || !CanEquipLegacyLeftHandItem(session, item, alternate: true))
            {
                return false;
            }

            position = 55;
            alternate = true;
            return true;
        }

        if (!IsSimpleAlternateEquipPosition(legacyPosition))
        {
            return false;
        }

        position = checked((ushort)(legacyPosition + 50));
        alternate = true;
        return true;
    }

}
