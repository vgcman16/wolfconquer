using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{

    private static async ValueTask HandleUnequipAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            MsgItemUsagePacket packet,
            CancellationToken cancellationToken)
    {
        if (packet.DwParam > ushort.MaxValue)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage unequip " +
                    $"{FormatSession(context)} slot={packet.DwParam}; slot is outside legacy range.")
                .ConfigureAwait(false);
            return;
        }

        var position = (ushort)packet.DwParam;
        if (position is 9 or 17)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage unequip " +
                    $"{FormatSession(context)} slot={position}; special equipment slot is legacy-blocked.")
                .ConfigureAwait(false);
            return;
        }

        var equipped = session.PlayerItems.EquipmentItems.FirstOrDefault(item => item.Position == position);
        if (equipped is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage unequip " +
                    $"{FormatSession(context)} slot={position}; equipped item was not found.")
                .ConfigureAwait(false);
            return;
        }

        var response = new MsgItemUsagePacket(
            equipped.Uid,
            equipped.Position,
            MsgItemUsageAction.Unequip,
            Timestamp: 0,
            DwParam2: 0,
            DwParam3: 0,
            DwParam4: 0);
        var moved = equipped.WithPosition(0);
        var updated = session with
        {
            PlayerItems = MoveEquippedItemToInventory(session.PlayerItems, equipped, moved),
        };
        var ownerUid = session.Character.Uid;
        var acceptedItems = updated.PlayerItems;
        if (!TryCommitLegacyEquipmentPhysicalStatusMutation(
                context, session, acceptedItems, out updated))
        {
            return;
        }

        context = context with { Session = updated };

        if (!await SendLegacyEquipmentMutationFrameAsync(
                context, updated, ownerUid, acceptedItems,
                response.ToFrameBytes(), cancellationToken).ConfigureAwait(false))
        {
            return;
        }
        if (!await SendLegacyEquipmentMutationFrameAsync(
                context, updated, ownerUid, acceptedItems,
                LegacyGameItemPacket.FromSnapshotItem(moved).ToFrameBytes(),
                cancellationToken).ConfigureAwait(false))
        {
            return;
        }
        var completed = await CompleteLegacyEquipmentPhysicalStatusMutationAsync(
                context, updated, ownerUid, acceptedItems, "unequip",
                queryUnconditionally: true, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (completed is null)
        {
            return;
        }

        updated = completed;
        context = context with { Session = updated };

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage unequip " +
                $"{FormatSession(context)} itemUid={equipped.Uid} slot={position}.")
            .ConfigureAwait(false);
        if (!TryRefreshLegacyEquipmentMutationSession(context, ownerUid, acceptedItems, ref updated))
        {
            return;
        }
    }

    private static async ValueTask HandleEquipAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            MsgItemUsagePacket packet,
            CancellationToken cancellationToken)
    {
        var item = session.PlayerItems.InventoryItems.FirstOrDefault(item => item.Uid == packet.Id);
        if (item is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage equip " +
                    $"{FormatSession(context)} itemUid={packet.Id}; inventory item was not found.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryResolveEquipPosition(session, item, packet.DwParam, out var position, out var alternate))
        {
            if (packet.DwParam == 0 && ItemType.GetLegacyEquipmentPosition(item.ItemId) == 0)
            {
                if (await TryHandleLegacyNonEquipmentItemUseAsync(context, session, item, cancellationToken)
                    .ConfigureAwait(false))
                {
                    return;
                }

                await LogParityGapAsync(
                        context,
                        LegacyParityGapDiagnostics.UnsupportedItemUse(
                            item.ItemId,
                            action: (uint)packet.Action,
                            actionName: packet.Action.ToString(),
                            itemUid: item.Uid,
                            source: "MsgItemUsage.UseItem",
                            reason: "non-equipment item effects are not modeled"))
                    .ConfigureAwait(false);
                await context.Log.WriteLineAsync(
                        "WorldServer deferred MsgItemUsage item use " +
                        $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId}; non-equipment item effects are not modeled.")
                    .ConfigureAwait(false);
            }
            else
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped MsgItemUsage equip " +
                        $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId} requestedSlot={packet.DwParam}; " +
                        "only simple normal, right-hand weapon, left-hand arrow, left-hand shield, left-hand hossu, left-hand pistol, alternate simple equipment slots, and alternate weapon slots are currently modeled.")
                    .ConfigureAwait(false);
            }

            return;
        }

        if (TryReadItemType(context.WorldData, item.ItemId, out var itemType))
        {
            if (!PassesLegacyItemGenderRequirement(session.Character, itemType))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped MsgItemUsage equip " +
                        $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId}; " +
                        $"character body {session.Character.Body} does not meet the legacy item gender requirement {itemType.RequiredGender}.")
                    .ConfigureAwait(false);
                return;
            }

            if (!PassesLegacyRebornEquipBypass(session.Character, itemType))
            {
                if (!PassesLegacyItemClassRequirement(session.Character, itemType))
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer skipped MsgItemUsage equip " +
                            $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId}; " +
                            $"character class {session.Character.Class} does not meet the legacy item class requirement {itemType.RequiredClass}.")
                        .ConfigureAwait(false);
                    return;
                }

                if (!PassesLegacyItemLevelRequirement(session.Character, itemType))
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer skipped MsgItemUsage equip " +
                            $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId}; " +
                            $"character level {session.Character.Level} does not meet the legacy item level requirement.")
                        .ConfigureAwait(false);
                    return;
                }

                if (!PassesLegacyItemStatRequirement(session.Character, itemType))
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer skipped MsgItemUsage equip " +
                            $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId}; " +
                            $"character stats strength={session.Character.Strength} agility={session.Character.Agility} do not meet the legacy item stat requirement.")
                        .ConfigureAwait(false);
                    return;
                }
            }
        }

        var displaced = session.PlayerItems.EquipmentItems.FirstOrDefault(equipped => equipped.Position == position);
        var equippedItem = item.WithPosition(position);
        var displacedItems = new List<(PlayerItemSnapshotItem Equipped, PlayerItemSnapshotItem Inventory)>();
        if (displaced is not null)
        {
            displacedItems.Add((displaced, displaced.WithPosition(0)));
        }

        var clearedLeftHand = FindLeftHandItemToClearForTwoHandEquip(session.PlayerItems, item, position, displaced);
        if (clearedLeftHand is not null)
        {
            displacedItems.Add((clearedLeftHand, clearedLeftHand.WithPosition(0)));
        }

        var updated = session with
        {
            PlayerItems = displacedItems.Count == 0
                ? MoveInventoryItemToEquipment(session.PlayerItems, item, equippedItem)
                : ReplaceEquippedItems(session.PlayerItems, item, displacedItems, equippedItem),
        };
        var ownerUid = session.Character.Uid;
        var acceptedItems = updated.PlayerItems;
        if (!TryCommitLegacyEquipmentPhysicalStatusMutation(
                context, session, acceptedItems, out updated))
        {
            return;
        }

        context = context with { Session = updated };

        if (!await SendLegacyEquipmentMutationFrameAsync(
                context, updated, ownerUid, acceptedItems,
                new MsgItemUsagePacket(
                    item.Uid,
                    DwParam: 0,
                    MsgItemUsageAction.RemoveInventory,
                    Timestamp: 0,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0).ToFrameBytes(),
                cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        foreach (var (equipped, inventory) in displacedItems)
        {
            if (!await SendLegacyEquipmentMutationFrameAsync(
                    context, updated, ownerUid, acceptedItems,
                    new MsgItemUsagePacket(
                        equipped.Uid,
                        equipped.Position,
                        MsgItemUsageAction.Unequip,
                        Timestamp: 0,
                        DwParam2: 0,
                        DwParam3: 0,
                        DwParam4: 0).ToFrameBytes(),
                    cancellationToken).ConfigureAwait(false))
            {
                return;
            }
            if (!await SendLegacyEquipmentMutationFrameAsync(
                    context, updated, ownerUid, acceptedItems,
                    LegacyGameItemPacket.FromSnapshotItem(inventory).ToFrameBytes(),
                    cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }

        if (alternate)
        {
            if (!await SendLegacyEquipmentMutationFrameAsync(
                    context, updated, ownerUid, acceptedItems,
                    new MsgItemUsagePacket(
                        equippedItem.Uid,
                        equippedItem.Position,
                        MsgItemUsageAction.Unequip,
                        Timestamp: 0,
                        DwParam2: 0,
                        DwParam3: 0,
                        DwParam4: 0).ToFrameBytes(),
                    cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }

        if (!await SendLegacyEquipmentMutationFrameAsync(
                context, updated, ownerUid, acceptedItems,
                LegacyGameItemPacket.FromSnapshotItem(equippedItem).ToFrameBytes(),
                cancellationToken).ConfigureAwait(false))
        {
            return;
        }
        if (displaced is null)
        {
            if (!await SendLegacyEquipmentMutationFrameAsync(
                    context, updated, ownerUid, acceptedItems,
                    LegacyGameItemPacket.FromSnapshotItem(
                        equippedItem,
                        mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes(),
                    cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }

        if (alternate)
        {
            // MsgItemUsuagePacket2492-2496: item/slot notification precedes
            // the selected-set query or inactive-alternate allow notification.
            if (!await SendLegacyEquipmentMutationFrameAsync(
                    context, updated, ownerUid, acceptedItems,
                    new LegacyShowEquipmentPacket(
                        WParam: equippedItem.Uid,
                        Alternante: equippedItem.Position,
                        Action: 5,
                        EquipmentSlots: default).ToFrameBytes(),
                    cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }

        var completed = await CompleteLegacyEquipmentPhysicalStatusMutationAsync(
                context, updated, ownerUid, acceptedItems, "equip",
                queryUnconditionally: !alternate, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (completed is null)
        {
            return;
        }

        updated = completed;
        context = context with { Session = updated };

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage equip " +
                $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId} slot={position} " +
                $"alternate={alternate} replacedItemUid={displaced?.Uid ?? 0} " +
                $"clearedLeftHandUid={clearedLeftHand?.Uid ?? 0}.")
            .ConfigureAwait(false);
        if (!TryRefreshLegacyEquipmentMutationSession(context, ownerUid, acceptedItems, ref updated))
        {
            return;
        }
    }

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
            ushort position,
            PlayerItemSnapshotItem? displaced)
    {
        if (position is not 4 and not 54 || displaced is not null || !ItemType.IsLegacyTwoHandWeapon(item.ItemId))
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
