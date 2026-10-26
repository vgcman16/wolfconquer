using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static LegacyMailboxClaimPlan MaterializeMailboxPackage(MailboxPackagePrepared prepared,
        LegacyMailboxModePacket packet, LegacyItemUidReservationReceipt receipt)
    {
        if (receipt.Count != prepared.Grants.Length || receipt.First == 0 ||
            (ulong)receipt.First + (uint)receipt.Count - 1 > uint.MaxValue)
            throw new InvalidOperationException("Package receipt cannot materialize its recipe.");
        var generated = new PlayerItemSnapshotItem[receipt.Count];
        for (var index = 0; index < generated.Length; index++)
        {
            var grant = prepared.Grants[index];
            generated[index] = new PlayerItemSnapshotItem
            {
                Uid = checked(receipt.First + (uint)index), ItemId = grant.ItemId,
                Durability = grant.Durability, MaximumDurability = grant.Durability,
                Plus = grant.Plus, Bound = grant.Bound, SocketProgress = grant.SocketProgress, StackSize = grant.Quantity,
            };
        }
        var updated = prepared.Updated;
        var all = packet.Type == LegacyMailboxModeType.ClaimAll;
        // Preserve all unselected stores and nested rows; only this new inventory
        // and selected true mailbox warehouse rows are projected.
        var items = updated.PlayerItems;
        var frames = new List<byte[]>();
        var wallet = prepared.Baseline.Owner.Expected;
        foreach (var step in prepared.Steps)
        {
            if (all) wallet = PrepareMailboxClaimMoney(wallet, step.Original, frames);
            if (step.GrantsPackage)
            {
                AppendMailboxPackageRewardFrames(prepared, step, generated, frames);
                var incoming = generated.Where((_, index) => prepared.Grants[index].MailId == step.Original.Id);
                items = items with { InventoryItems = items.InventoryItems.Concat(incoming).ToArray() };
            }
            if (all && step.Original.ItemId != 0)
            {
                if (!prepared.Baseline.Items.TryGetWarehouse(LegacyMailboxWarehouseId, step.Original.ItemId, out var incoming))
                    throw new InvalidOperationException("Prepared mailbox attachment retired.");
                var attachment = incoming.CreateInventoryItem();
                items = MoveWarehouseItemToInventory(items, incoming.Source, attachment);
                frames.Add(LegacyGameItemPacket.FromSnapshotItem(attachment).ToFrameBytes());
            }
            if (all) frames.Add(PrepareMailboxClaimRefresh(step.After, prepared.Baseline.Mail.Now,
                prepared.Baseline.Owner.Context.ProtocolProfile));
        }
        updated = updated with { PlayerItems = items };
        if (!LegacyItemSaveGraph.TryCapture(items, out var graph))
            throw new InvalidOperationException("Generated package graph is invalid or overlaps existing UIDs.");
        frames.Add(packet.ToFrameBytes());
        return new(prepared.Baseline, updated, graph, prepared.MailAfter, prepared.Selected.DistinctBy(mail => mail.Id).ToArray(),
            frames.ToArray(), true) { PackagePublication = new(prepared) };
    }

}
