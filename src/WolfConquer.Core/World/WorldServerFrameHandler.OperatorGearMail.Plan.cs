using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private sealed record OperatorMailPlan(OperatorMailRequest Request, OperatorMailOwner Owner,
        OperatorMailBaseline Before, WorldSession Candidate, LegacyItemSaveGraph AfterGraph,
        byte[] ItemsAfter, LegacyMailboxStore.DeliveryProjection MailProjection, byte[] MailAfter,
        uint[] MailIds, uint[] ItemUids, byte[][] Frames)
    { internal WorldSession? Final { get; set; } }

    private LegacyOperatorGearMailResult PrepareOperatorMailPlan(LegacyOwnedAdmissionRuntime runtime,
        OperatorMailRequest request, OperatorMailOwner owner, CancellationToken cancellationToken, out OperatorMailPlan? plan)
    {
        plan = null;
        OperatorMailPlan? prepared = null;
        var reason = "operatorMailPreparationRefused";
        var status = LegacyOperatorGearMailStatus.Rejected;
        SnapshotFileLockCoordinator.ExecuteLocked(owner.Paths, () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            request.NativeCatalog.RequireCurrent();
            if (request.Catalogs.Any(catalog => !catalog.IsCurrent()) ||
                !TryReadOperatorMailBaseline(runtime, owner, out var before, out reason) ||
                !before!.Mail.TryAllocateMailIds(request.Request.Items.Count, out var mailIds) ||
                !before.Graph.IsCurrent(owner.Expected.PlayerItems) ||
                !OperatorMailOwnerCurrent(runtime, owner, owner.Expected) ||
                !request.NativeCatalog.IsCurrentPlain()) return false;
            var reservation = new LegacyItemUidReservationRequest(request.Request.OperationId,
                LegacyOwnedAdmissionRuntime.OperatorGearMailWriter, request.Request.AccountId,
                request.Request.CharacterUid, request.Digest, request.Request.Items.Count);
            var reserved = runtime.ReserveOperatorGearMail(this, request.Request.ExpectedDomain, reservation,
                out var token, out _, out reason);
            if (reserved == LegacyItemUidReservationOutcome.AlreadyRecorded)
            {
                status = LegacyOperatorGearMailStatus.PreviouslySubmittedOutcomeUnproven;
                reason = "operatorMailPreviouslySubmittedOutcomeUnproven:noSecondMaterialization";
                return false;
            }
            if (reserved != LegacyItemUidReservationOutcome.Reserved)
            {
                if (reason.StartsWith(LegacyItemUidRegistry.RecoveryRequired, StringComparison.Ordinal))
                {
                    status = LegacyOperatorGearMailStatus.RecoveryRequired;
                    FenceOperatorMail("operatorMailReservationOutcomeUnproven:" + reason);
                }
                return false;
            }
            if (token is null || !token.TryTake(out var uids))
            { reason = "operatorMailReservationCapabilityUnavailable"; return false; }
            // A failure from this point burns the durable operation/UIDs. Neither
            // reusing the Guid nor changing the payload can obtain another token.
            var items = new PlayerItemSnapshotItem[uids.Length];
            var mails = new LegacyMailboxEntry[uids.Length];
            var frames = new byte[uids.Length + 1][];
            var now = DateTime.Now;
            for (var i = 0; i < uids.Length; i++)
            {
                var row = request.Request.Items[i];
                items[i] = new PlayerItemSnapshotItem
                {
                    Uid = uids[i], ItemId = row.ItemId, StackSize = row.StackSize,
                    Durability = request.Catalogs[i].Durability, MaximumDurability = request.Catalogs[i].Durability,
                    Plus = row.Plus, Bound = row.Bound ? (byte)1 : (byte)0,
                    SocketOne = row.FirstSocket ? byte.MaxValue : (byte)0,
                    SocketTwo = row.SecondSocket ? byte.MaxValue : (byte)0, Position = 0, WarehouseId = 1000,
                };
                mails[i] = new LegacyMailboxEntry
                {
                    Id = mailIds[i], ItemId = uids[i], Title = request.Request.Title,
                    SenderName = request.Request.SenderName, Content = request.Request.Content,
                    // The legacy saved-mail contract uses positive wall-clock
                    // ticks. Local DateTime.ToBinary sets a negative kind bit.
                    Timer = now.AddDays(7).Ticks,
                };
                frames[i] = LegacyMailListPacket.NewMail(LegacyMailListPacket.FromMailboxEntry(mails[i], now))
                    .ToFrameBytes(_protocolProfile);
            }
            frames[^1] = LegacyMailNotifyPacket.Startup(true).ToFrameBytes();
            var candidate = owner.Expected with { PlayerItems = owner.Expected.PlayerItems with
                { WarehouseItems = owner.Expected.PlayerItems.WarehouseItems.Concat(items).ToArray() } };
            if (!LegacyItemSaveGraph.TryCapture(candidate.PlayerItems, out var afterGraph) ||
                !before.Mail.TryPrepare(mails, out var projection, out reason)) return false;
            var image = PlayerItemSnapshotLoader.ToLegacyBytes(afterGraph.WithOwnedItemsForPersistence(candidate).PlayerItems);
            var mailImage = projection.CopyAfterImage();
            if (request.Catalogs.Any(catalog => !catalog.IsCurrent()) || !before.Graph.IsCurrent(owner.Expected.PlayerItems) ||
                !afterGraph.IsCurrent(candidate.PlayerItems) || !before.Mail.IsCurrent() ||
                !OperatorMailOwnerCurrent(runtime, owner, owner.Expected)) return false;
            prepared = new(request, owner, before, candidate, afterGraph, image, projection, mailImage, mailIds, uids, frames);
            reason = string.Empty;
            return true;
        });
        plan = prepared;
        return OperatorMailResult(request.Request.OperationId, status, reason);
    }
}
