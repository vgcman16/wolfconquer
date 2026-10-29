using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record FullRestoreConsumablePlan(WorldSession Updated, EquipmentMoveInputs Inputs,
        LegacyItemSaveGraph After, HealthConsumableCatalog Catalog, LegacyFullRestoreConsumableWrite Write, byte[][] Frames);

    private static bool TryPrepareFullRestoreConsumable(WorldPacketHandlerContext context, ManagedManaConsumableOwner owner,
        out FullRestoreConsumablePlan plan, out string reason)
    {
        plan = null!; reason = "fullRestoreConsumableInvalidState";
        var inputs = new EquipmentMoveInputs(owner.Expected); var stable = inputs.Stable;
        var item = stable.PlayerItems.InventoryItems.SingleOrDefault(row => row.Uid == owner.ItemUid);
        var now = DateTimeOffset.UtcNow;
        if (item is null || item.Position != 0 || item.WarehouseId != 0 || item.DepositedItems.Count != 0 ||
            stable.PlayerItems.InventoryItems.Count > LegacyInventorySlots ||
            !LegacyConsumableItemUse.IsLegacyFullRestoreConsumable(item.ItemId) ||
            stable.Character.MinHitPoints <= 0 || HasLegacyStatusFlag(stable.Character, LegacyDeadFlag) ||
            stable.LastLegacyPotionUseUtc is { } last && now < last.AddMilliseconds(LegacyPotionCooldownMilliseconds)) return false;
        if (!TryCaptureHealthConsumableCatalog(context, item.ItemId, out var catalog) ||
            catalog.Fields.Length <= ItemType.StackSizeFieldIndex ||
            !ushort.TryParse(catalog.Fields[ItemType.StackSizeFieldIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var cap) ||
            Math.Max(1, (int)item.StackSize) > Math.Max(1, (int)cap))
        { reason = "fullRestoreConsumableMetadataUnavailableOrStackInvalid"; return false; }
        var hp = CheckedUInt32(stable.Character.MinHitPoints, nameof(stable.Character.MinHitPoints));
        var mana = CheckedUInt32(stable.Character.MinMana, nameof(stable.Character.MinMana));
        var maximum = ComputeLegacyRuntimeMaxHitpoints(stable.Character, hp, stable.PlayerItems,
            stable.CombatGear ?? CombatGearSnapshot.Empty, context.WorldData, stable.AlternateEquipmentActive);
        var result = LegacyConsumableItemUse.Calculate(item.ItemId, hp, maximum, mana,
            CheckedUInt32(stable.Character.MaxMana, nameof(stable.Character.MaxMana)));
        if (!result.ShouldConsumeItem || !result.HitpointsChanged && !result.ManaChanged ||
            result.UpdatedHitpoints < hp || result.UpdatedMana < mana || result.UpdatedHitpoints > int.MaxValue)
        { reason = "fullRestoreConsumableExistingResourceRuleRefused"; return false; }
        var items = ConsumeSingleLegacyInventoryItem(stable.PlayerItems, item, out var consume);
        var liveItem = owner.Expected.PlayerItems.InventoryItems.Single(row => row.Uid == owner.ItemUid);
        var liveItems = ConsumeSingleLegacyInventoryItem(owner.Expected.PlayerItems, liveItem, out var liveConsume);
        if (!consume.AsSpan().SequenceEqual(liveConsume) ||
            !PlayerItemSnapshotLoader.ToLegacyBytes(items).AsSpan().SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(liveItems)))
        { reason = "fullRestoreConsumableLiveProjectionChanged"; return false; }
        var character = new BoundHitpointsInputs(stable.Character, stable.Character with
            { MinHitPoints = result.UpdatedHitpoints, MinMana = result.UpdatedMana }).After;
        var updated = stable with { Character = character, PlayerItems = liveItems, LastLegacyPotionUseUtc = now };
        var persisted = updated with { PlayerItems = items };
        if (!LegacyItemSaveGraph.TryCapture(updated.PlayerItems, out var after) ||
            !LegacyFullRestoreConsumableWrite.TryPrepare(stable, persisted, owner.Items, item.Uid, out var write, out reason)) return false;
        var frames = new List<byte[]> { consume };
        if (result.HitpointsChanged)
            foreach (var update in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(
                checked((uint)updated.Character.Uid), maximum, result.UpdatedHitpoints)) frames.Add(update.ToFrameBytes());
        if (result.ManaChanged)
            frames.Add(new LegacyMsgUpdatePacket(checked((uint)updated.Character.Uid),
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, result.UpdatedMana)]).ToFrameBytes());
        plan = new(updated, inputs, after, catalog, write!, frames.ToArray()); reason = string.Empty; return true;
    }
}
