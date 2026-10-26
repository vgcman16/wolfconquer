using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static LegacyMailboxClaimPlan MaterializeMailboxPackage(MailboxPackagePrepared prepared,
        LegacyMailboxModePacket packet, LegacyItemUidReservationReceipt? receipt)
    {
        var stock = prepared.Stacks.Materialize(receipt);
        var updated = prepared.Updated;
        var all = packet.Type == LegacyMailboxModeType.ClaimAll;
        var frames = new List<byte[]>();
        var wallet = prepared.Baseline.Owner.Expected;
        foreach (var step in prepared.Steps)
        {
            if (all) wallet = PrepareMailboxClaimMoney(wallet, step.Original, frames);
            if (step.GrantsPackage)
            {
                AppendMailboxPackageRewardFrames(prepared, step, stock, frames);
            }
            if (all && step.Original.ItemId != 0)
            {
                var attachment = prepared.Stacks.CopyAttachmentForMove(step.Original.ItemId);
                frames.Add(LegacyGameItemPacket.FromSnapshotItem(attachment).ToFrameBytes());
            }
            if (all) frames.Add(PrepareMailboxClaimRefresh(step.After, prepared.Baseline.Mail.Now,
                prepared.Baseline.Owner.Context.ProtocolProfile));
        }
        updated = updated with { PlayerItems = stock.Items };
        if (!LegacyItemSaveGraph.TryCapture(stock.Items, out var graph))
            throw new InvalidOperationException("Generated package graph is invalid or overlaps existing UIDs.");
        frames.Add(packet.ToFrameBytes());
        return new(prepared.Baseline, updated, graph, prepared.MailAfter, prepared.Selected.DistinctBy(mail => mail.Id).ToArray(),
            frames.ToArray(), true) { PackagePublication = new(prepared) };
    }

}
