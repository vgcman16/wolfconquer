using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record HealthConsumablePlan(WorldSession Updated, EquipmentMoveInputs Inputs,
        LegacyItemSaveGraph After, HealthConsumableCatalog Catalog, LegacyHealthConsumableWrite Write, byte[][] Frames);
    private sealed record HealthConsumableCatalog(ItemType Source, string[] Fields);

    private static bool TryCaptureHealthConsumableCatalog(WorldPacketHandlerContext context, uint id,
        out HealthConsumableCatalog catalog)
    {
        catalog = null!;
        if (context.WorldData is not { } data || id > int.MaxValue ||
            !data.ItemsById.TryGetValue((int)id, out var rows) || rows.Count != 1 || rows[0] is not { } source || source.Id != id) return false;
        catalog = new(source, FullAccountKnownRows(source.Fields, 4096)); return true;
    }

    private static bool HealthConsumableCatalogIsCurrent(WorldPacketHandlerContext context, HealthConsumableCatalog expected) =>
        TryCaptureHealthConsumableCatalog(context, checked((uint)expected.Source.Id), out var current) &&
        ReferenceEquals(current.Source, expected.Source) && current.Fields.SequenceEqual(expected.Fields);

    private static bool TryPrepareHealthConsumable(WorldPacketHandlerContext context, HealthConsumableOwner owner,
        out HealthConsumablePlan plan, out string reason)
    {
        plan = null!; reason = "healthConsumableInvalidState";
        var inputs = new EquipmentMoveInputs(owner.Expected);
        var stable = inputs.Stable;
        var item = stable.PlayerItems.InventoryItems.SingleOrDefault(row => row.Uid == owner.ItemUid);
        var now = DateTimeOffset.UtcNow;
        if (item is null || item.Position != 0 || item.WarehouseId != 0 || item.DepositedItems.Count != 0 ||
            stable.PlayerItems.InventoryItems.Count > LegacyInventorySlots ||
            !LegacyConsumableItemUse.IsLegacyHealthConsumable(item.ItemId) ||
            stable.Character.MinHitPoints <= 0 || HasLegacyStatusFlag(stable.Character, LegacyDeadFlag) ||
            stable.LastLegacyPotionUseUtc is { } last && now < last.AddMilliseconds(LegacyPotionCooldownMilliseconds)) return false;
        if (!TryCaptureHealthConsumableCatalog(context, item.ItemId, out var catalog) ||
            catalog.Fields.Length <= ItemType.StackSizeFieldIndex ||
            !uint.TryParse(catalog.Fields[ItemType.ItemHPFieldIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) || amount == 0 ||
            !ushort.TryParse(catalog.Fields[ItemType.StackSizeFieldIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var cap) ||
            Math.Max(1, (int)item.StackSize) > Math.Max(1, (int)cap))
        { reason = "healthConsumableMetadataUnavailableOrStackInvalid"; return false; }
        var hp = CheckedUInt32(stable.Character.MinHitPoints, nameof(stable.Character.MinHitPoints));
        var maximum = ComputeLegacyRuntimeMaxHitpoints(stable.Character, hp, stable.PlayerItems,
            stable.CombatGear ?? CombatGearSnapshot.Empty, context.WorldData, stable.AlternateEquipmentActive);
        var multiplier = HasLegacyStatusFlag(stable.Character, LegacyShieldBlockFlag) ||
            HasLegacyStatusFlag(stable.Character, LegacyHawksAmbitionFlag) ? LegacyBoostedPotionHitpointPercent : 100U;
        var result = LegacyConsumableItemUse.Calculate(item.ItemId, hp, maximum,
            CheckedUInt32(stable.Character.MinMana, nameof(stable.Character.MinMana)),
            CheckedUInt32(stable.Character.MaxMana, nameof(stable.Character.MaxMana)), amount, itemMana: 0, hitpointMultiplierPercent: multiplier);
        if (!result.ShouldConsumeItem || !result.HitpointsChanged || result.ManaChanged)
        { reason = "healthConsumableExistingResourceRuleRefused"; return false; }
        var items = ConsumeSingleLegacyInventoryItem(stable.PlayerItems, item, out var consume);
        // Stable owns detached persistence inputs. The live publication must
        // retain every unchanged actual row/child identity held by trade offers.
        var liveItem = owner.Expected.PlayerItems.InventoryItems.Single(row => row.Uid == owner.ItemUid);
        var liveItems = ConsumeSingleLegacyInventoryItem(owner.Expected.PlayerItems, liveItem, out var liveConsume);
        if (!consume.AsSpan().SequenceEqual(liveConsume) ||
            !PlayerItemSnapshotLoader.ToLegacyBytes(items).AsSpan().SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(liveItems)))
        { reason = "healthConsumableLiveProjectionChanged"; return false; }
        var updated = NormalizeItemSpellHitpoints(stable, stable with
        {
            Character = stable.Character with { MinHitPoints = result.UpdatedHitpoints },
            PlayerItems = liveItems, LastLegacyPotionUseUtc = now
        });
        var persisted = updated with { PlayerItems = items };
        if (!LegacyItemSaveGraph.TryCapture(updated.PlayerItems, out var after) ||
            !LegacyHealthConsumableWrite.TryPrepare(stable, persisted, owner.Items, item.Uid, out var write, out reason)) return false;
        var frames = new List<byte[]> { consume };
        foreach (var update in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(
            checked((uint)updated.Character.Uid), maximum, result.UpdatedHitpoints)) frames.Add(update.ToFrameBytes());
        plan = new(updated, inputs, after, catalog, write!, frames.ToArray()); reason = string.Empty; return true;
    }
}
