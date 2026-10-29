using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record ManagedManaConsumablePlan(WorldSession Updated, EquipmentMoveInputs Inputs,
        LegacyItemSaveGraph After, HealthConsumableCatalog Catalog, LegacyManagedManaConsumableWrite Write, byte[][] Frames);

    private static bool TryPrepareManagedManaConsumable(WorldPacketHandlerContext context, ManagedManaConsumableOwner owner,
        out ManagedManaConsumablePlan plan, out string reason)
    {
        plan = null!; reason = "managedManaConsumableInvalidState";
        var inputs = new EquipmentMoveInputs(owner.Expected); var stable = inputs.Stable;
        var item = stable.PlayerItems.InventoryItems.SingleOrDefault(row => row.Uid == owner.ItemUid);
        var now = DateTimeOffset.UtcNow;
        if (item is null || item.Position != 0 || item.WarehouseId != 0 || item.DepositedItems.Count != 0 ||
            stable.PlayerItems.InventoryItems.Count > LegacyInventorySlots ||
            !LegacyConsumableItemUse.IsLegacyManaConsumable(item.ItemId) ||
            stable.Character.MinHitPoints <= 0 || HasLegacyStatusFlag(stable.Character, LegacyDeadFlag) ||
            stable.LastLegacyPotionUseUtc is { } last && now < last.AddMilliseconds(LegacyPotionCooldownMilliseconds)) return false;
        if (!TryCaptureHealthConsumableCatalog(context, item.ItemId, out var catalog) ||
            catalog.Fields.Length <= ItemType.StackSizeFieldIndex ||
            !uint.TryParse(catalog.Fields[ItemType.ItemMPFieldIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) || amount == 0 ||
            !ushort.TryParse(catalog.Fields[ItemType.StackSizeFieldIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var cap) ||
            Math.Max(1, (int)item.StackSize) > Math.Max(1, (int)cap))
        { reason = "managedManaConsumableMetadataUnavailableOrStackInvalid"; return false; }
        var hp = CheckedUInt32(stable.Character.MinHitPoints, nameof(stable.Character.MinHitPoints));
        var result = LegacyConsumableItemUse.Calculate(item.ItemId, hp, hp,
            CheckedUInt32(stable.Character.MinMana, nameof(stable.Character.MinMana)),
            CheckedUInt32(stable.Character.MaxMana, nameof(stable.Character.MaxMana)), itemMana: amount);
        if (!result.ShouldConsumeItem || !result.ManaChanged || result.HitpointsChanged)
        { reason = "managedManaConsumableExistingResourceRuleRefused"; return false; }
        var items = ConsumeSingleLegacyInventoryItem(stable.PlayerItems, item, out var consume);
        var liveItem = owner.Expected.PlayerItems.InventoryItems.Single(row => row.Uid == owner.ItemUid);
        var liveItems = ConsumeSingleLegacyInventoryItem(owner.Expected.PlayerItems, liveItem, out var liveConsume);
        if (!consume.AsSpan().SequenceEqual(liveConsume) ||
            !PlayerItemSnapshotLoader.ToLegacyBytes(items).AsSpan().SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(liveItems)))
        { reason = "managedManaConsumableLiveProjectionChanged"; return false; }
        // This existing pure FULL scalar normalizer updates Attributes.MinMana
        // and all persisted keys too. The HP-only normalizer would do nothing.
        var character = new BoundHitpointsInputs(stable.Character,
            stable.Character with { MinMana = result.UpdatedMana }).After;
        var updated = stable with { Character = character, PlayerItems = liveItems, LastLegacyPotionUseUtc = now };
        var persisted = updated with { PlayerItems = items };
        if (!LegacyItemSaveGraph.TryCapture(updated.PlayerItems, out var after) ||
            !LegacyManagedManaConsumableWrite.TryPrepare(stable, persisted, owner.Items, item.Uid, out var write, out reason)) return false;
        var mana = new LegacyMsgUpdatePacket(checked((uint)updated.Character.Uid),
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, result.UpdatedMana)]).ToFrameBytes();
        plan = new(updated, inputs, after, catalog, write!, [consume, mana]); reason = string.Empty; return true;
    }
}
