using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record ItemRepairCatalog(ItemType Source, string[] Fields);

    private sealed record ItemRepairPlan(
        WorldSession Updated,
        EquipmentMoveInputs Inputs,
        LegacyItemSaveGraph After,
        LegacyItemRepairPersistence Write,
        ItemRepairCatalog? Catalog,
        byte[][] Frames,
        uint TargetUid,
        LegacyItemRepairPayment Payment,
        long Cost);

    private static bool TryPrepareItemRepair(
        WorldPacketHandlerContext context,
        ItemRepairOwner owner,
        uint targetUid,
        out ItemRepairPlan plan,
        out string reason)
    {
        plan = null!;
        reason = "itemRepairInvalidTarget";
        var inputs = new EquipmentMoveInputs(owner.Expected);
        var stable = inputs.Stable;
        var equipment = stable.PlayerItems.EquipmentItems.FirstOrDefault(item => item.Uid == targetUid);
        var target = equipment ?? stable.PlayerItems.InventoryItems.FirstOrDefault(item => item.Uid == targetUid);
        if (target is null || target.MaximumDurability == 0 || target.Durability >= target.MaximumDurability)
            return false;
        var repaired = target.WithDurability(target.MaximumDurability);
        ItemRepairCatalog? catalog = null;
        var cost = 0L;
        PlayerItemSnapshot paidItems;
        IReadOnlyList<byte[]> costFrames;
        LegacyItemRepairPayment payment;
        var character = stable.Character;
        if (target.Durability == 0)
        {
            payment = LegacyItemRepairPayment.Meteors;
            cost = LegacyZeroDurabilityRepairMeteorCost;
            reason = "itemRepairRequiresFiveNonDestructiveMeteors";
            if (!TryConsumeLegacyMeteorRepairCostForTarget(stable.PlayerItems,
                LegacyZeroDurabilityRepairMeteorCost, targetUid, out paidItems, out costFrames))
                return false;
        }
        else
        {
            payment = LegacyItemRepairPayment.Money;
            reason = "itemRepairGoldWorthUnavailable";
            if (!TryCaptureItemRepairCatalog(context, target.ItemId, out catalog, out var goldWorth))
                return false;
            cost = CalculateLegacyRepairCost(target.ItemId, goldWorth, target.Durability, target.MaximumDurability);
            reason = "itemRepairInsufficientMoney";
            if (character.Money < cost)
                return false;
            var moneyAfter = checked(character.Money - cost);
            var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
            if (attributes.ContainsKey("Money"))
                attributes["Money"] = moneyAfter.ToString(CultureInfo.InvariantCulture);
            character = character with { Money = moneyAfter, Attributes = attributes };
            paidItems = stable.PlayerItems;
            var money = new LegacyMsgUpdatePacket(checked((uint)character.Uid),
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Money, checked((ulong)character.Money))]).ToFrameBytes();
            // Retain the genuine successful two-Money sequence. Both complete
            // buffers are prepared before the real writer is entered.
            costFrames = [money, (byte[])money.Clone()];
        }
        var repairedItems = equipment is not null
            ? ReplaceEquipmentItem(paidItems, target, repaired)
            : ReplaceInventoryItem(paidItems, target, repaired);
        var updated = stable with { Character = character, PlayerItems = repairedItems };
        reason = "itemRepairCompleteImageUnavailable";
        if (!LegacyItemSaveGraph.TryCapture(updated.PlayerItems, out var after))
            return false;
        var frames = new byte[costFrames.Count + 1][];
        for (var index = 0; index < costFrames.Count; index++)
            frames[index] = costFrames[index];
        frames[^1] = LegacyGameItemPacket.FromSnapshotItem(repaired,
            mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes();
        if (!LegacyItemRepairPersistence.TryPrepare(stable, updated, owner.ItemsRoot, targetUid,
            payment, out var write, out reason) || write is null)
            return false;
        plan = new(updated, inputs, after, write, catalog, frames, targetUid, payment, cost);
        reason = string.Empty;
        return true;
    }

    private static bool TryCaptureItemRepairCatalog(
        WorldPacketHandlerContext context,
        uint itemId,
        out ItemRepairCatalog captured,
        out uint goldWorth)
    {
        captured = null!;
        goldWorth = 0;
        if (context.WorldData is not { } data || itemId > int.MaxValue ||
            !data.ItemsById.TryGetValue((int)itemId, out var rows) || rows.Count != 1 ||
            rows[0] is not { } source || source.Id != itemId)
            return false;
        var fields = FullAccountKnownRows(source.Fields, 4096);
        if (fields.Length <= ItemType.GoldWorthFieldIndex ||
            !uint.TryParse(fields[ItemType.GoldWorthFieldIndex], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out goldWorth))
            return false;
        captured = new(source, fields);
        return true;
    }

    private static bool ItemRepairCatalogIsCurrent(WorldPacketHandlerContext context, ItemRepairCatalog? expected) =>
        expected is null || TryCaptureItemRepairCatalog(context, checked((uint)expected.Source.Id), out var current, out _) &&
            ReferenceEquals(current.Source, expected.Source) && current.Fields.SequenceEqual(expected.Fields);
}
