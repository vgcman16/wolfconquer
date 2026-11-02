using System.Security.Cryptography;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyRelicFuseOwnedEvidence
{
    private int allocationAttempted;
    private LegacyItemUidReservationReceipt? allocation;
    private ushort retainedDurability;
    private uint[]? retainedWords;
    private LegacyRelicFuseRollData? retainedRoll;
    private byte[]? projectedAfterImage;
    private byte[][]? preparedFrames;
    private uint preparedUid;
    private bool published;

    // Called only by the eventual actual item-spell producer. Draws once after
    // real owner/file/catalog/capacity preflight, then reserves from the shared
    // durable global registry; there is no caller-provided UID or random source.
    internal bool TryReserveOnce(out uint uid, out string reason)
    {
        uid = 0; reason = "relicFuseAllocationAlreadyAttempted";
        if (Interlocked.Exchange(ref allocationAttempted, 1) != 0) return false;
        LegacyItemUidReservationReceipt? issued = null;
        try
        {
            before.RequireRelicFuseOwner(context, stable);
            if (!HasCurrentRuleLease() || !DependenciesCurrent() || runtime.RequireRelicFuseCapacity(frame) != domain)
            { reason = "relicFuseInputsChangedBeforeAllocation"; return false; }
            runtime.RequireRelicFuseObservedMaximum(frame, this, inputs.MaximumUid);
            if (!LegacyRelicFuseImages.TryCapture(stable.PlayerItems, addsResult: true, out _, out reason)) return false;
            if (!preparation.TryRoll(stable.PlayerItems, Random.Shared, out var roll, out reason) || roll is null) return false;
            var durability = roll.Durability;
            uint[] words = [roll.Attribute1, roll.Attribute2, roll.Attribute3, roll.Attribute4, roll.Attribute5];
            if (roll.FirstUid != inputs.First.Uid || roll.SecondUid != inputs.Second.Uid ||
                roll.ItemId != inputs.First.ItemId || roll.Bound != inputs.ResultBound || !DependenciesCurrent())
            { reason = "relicFuseInputsChangedAfterRoll"; return false; }
            before.RequireRelicFuseOwner(context, stable);
            var digest = LegacyRelicFuseRecipeDigest.Compute(domain, accountId, characterUid,
                inputs.First.Uid, inputs.Second.Uid, inputs.ResultBound, durability, words,
                before.CopyCharacter(), before.CopyItems(), before.CopySpells());
            var request = new LegacyItemUidReservationRequest(Guid.NewGuid(), LegacyItemUidRegistry.RelicFuseWriter,
                accountId, characterUid, digest, 1);
            retainedDurability = durability; retainedWords = words.ToArray(); retainedRoll = roll;
            pending = request; reserving = true;
            LegacyItemUidRegistry.Reservation? reservation;
            LegacyItemUidReservationOutcome outcome;
            try { outcome = runtime.ReserveRelicFuseUid(frame, this, request, out reservation, out issued, out reason); }
            finally { reserving = false; }
            if (outcome != LegacyItemUidReservationOutcome.Reserved || reservation is null || issued is null)
            {
                if (outcome == LegacyItemUidReservationOutcome.Reserved) runtime.FenceRelicFuseUid(frame);
                return false;
            }
            allocation = issued;
            if (!reservation.TryTake(out var ids) || ids.Length != 1 || ids[0] != issued.First ||
                ids[0] <= inputs.MaximumUid || !DependenciesCurrent()) throw Invalid();
            if (preparedUid != ids[0] || preparedFrames is null || projectedAfterImage is null) throw Invalid();
            uid = ids[0]; reason = string.Empty;
            return true;
        }
        catch
        {
            if (issued is not null) runtime.FenceRelicFuseUid(frame);
            throw;
        }
    }

    // Called only by the real registry while its gate pins the next global UID.
    // It prepares bounded DATA and frames before that UID is durably reserved;
    // this preview is not an allocation or publication authority.
    internal bool TryPrepareNextUid(uint nextUid, out string reason)
    {
        reason = "relicFuseNextUidPreparationInvalid";
        if (!reserving || !HasCurrentRuleLease() || retainedRoll is null || preparedUid != 0 || nextUid == 0)
            return false;
        try
        {
            if (!LegacyRelicFuseProjection.TryCreate(stable.PlayerItems, retainedRoll, nextUid,
                    out var projection, out reason) || projection is null) return false;
            var proposed = stable with { PlayerItems = projection.Items };
            if (!LegacyItemSaveGraph.TryCapture(proposed.PlayerItems, out var graph) || graph is null) return false;
            _ = graph.WithOwnedItemsForPersistence(proposed);
            var response = packet with { RelicID3 = nextUid };
            byte[][] frames = [LegacyGameItemPacket.FromSnapshotItem(projection.ResultItem).ToFrameBytes(), response.ToFrameBytes()];
            projectedAfterImage = projection.CopyAfterImage();
            preparedFrames = frames.Select(frame => frame.ToArray()).ToArray();
            preparedUid = nextUid; reason = string.Empty; return true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or InvalidDataException or OverflowException)
        { reason = "relicFuseNextUidPreparation:" + error.GetType().Name; return false; }
    }

    internal byte[][] CopyPreparedFrames(uint uid)
    {
        if (uid == 0 || uid != preparedUid || preparedFrames is null || !HasCurrentRuleLease()) throw Invalid();
        return preparedFrames.Select(frame => frame.ToArray()).ToArray();
    }

    internal bool IsIssuedAllocationCurrent(LegacyOwnedAdmissionRuntime actual, WorldServerFrameHandler actualFrame,
        LegacyItemUidReservationRequest request, LegacyItemUidReservationReceipt receipt) =>
        ReferenceEquals(actual, runtime) && ReferenceEquals(actualFrame, frame) &&
        ReferenceEquals(request, pending) && ReferenceEquals(receipt, allocation) && HasCurrentRuleLease() && DependenciesCurrent();
    // The projection remains inert data. A future real journal writer must
    // separately retain this exact owner and its before/after image proof.
    internal bool TryCreateRetainedProjection(out LegacyRelicFuseProjection? projection, out string reason)
    {
        projection = null; reason = "relicFuseRetainedAllocationUnavailable";
        if (retainedRoll is null || !TryReadRetainedAllocation(out var uid, out _, out _)) return false;
        if (!LegacyRelicFuseProjection.TryCreate(stable.PlayerItems, retainedRoll, uid, out projection, out reason) || projection is null) return false;
        var image = projection.CopyAfterImage();
        if (projectedAfterImage is not null && !projectedAfterImage.AsSpan().SequenceEqual(image))
        { projection = null; reason = "relicFuseProjectionChanged"; return false; }
        projectedAfterImage ??= image;
        return true;
    }
    // The real Store invokes this only after the journal writer and its own
    // native/holder publication, while the actual paired actor gates remain held.
    internal bool ConfirmPublishedHeld(WorldSession actual, LegacyOrdinarySpellBinding actualBinding,
        LegacyPreparedSpellFileWrite writer)
    {
        if (published || !HasCurrentRuleLease() || allocation is null || projectedAfterImage is null ||
            !ReferenceEquals(actualBinding, binding) || !before.IsRelicFuseWriter(writer) ||
            !before.HasCommittedRelicFuse(this) || !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) ||
            !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
            !ReferenceEquals(actual.NativeGameClient, binding.Client) ||
            !ReferenceEquals(binding.Items.Image.FinalSnapshot, actual.PlayerItems) || !binding.Items.Image.IsCurrentPlain() ||
            !PlayerItemSnapshotLoader.ToLegacyBytes(actual.PlayerItems).AsSpan().SequenceEqual(projectedAfterImage)) return false;
        published = true; return true;
    }

    internal string ItemPath => before.ItemPath;

    internal void RequireRegistryAndReceipt(LegacyItemUidRegistry registry, LegacyItemUidReservationReceipt receipt)
    {
        if (!HasCurrentRuleLease() || !ReferenceEquals(runtime.Ordinary.Registry, registry) || !ReferenceEquals(allocation, receipt) ||
            pending is null || !DependenciesCurrent()) throw Invalid();
    }

    internal void RetireCommittedAllocation(LegacyPreparedSpellFileWrite writer)
    {
        if (allocation is null || !before.HasCommittedRelicFuse(this)) throw Invalid();
        try
        {
            if (!runtime.RetireRelicFuseUid(frame, writer, this, allocation, out var reason))
                throw new InvalidDataException("relicFuseNamespaceRetirementRefused:" + reason);
        }
        catch (Exception failure)
        {
            Exception retained = failure;
            try { runtime.FenceRelicFuseUid(frame); }
            catch (Exception fencing) { retained = new AggregateException(failure, fencing); }
            throw new LegacyCommittedSpellPublicationException(retained);
        }
    }
    internal void FenceUnlessPublished()
    {
        if (allocation is not null && !published) runtime.FenceRelicFuseUid(frame);
    }
    internal bool TryCreateJournalData(out LegacyRelicFuseJournalOperation? operation)
    {
        operation = null;
        if (!TryReadRetainedAllocation(out _, out var durability, out var words) || pending is null || allocation is null)
            return false;
        operation = new(pending, allocation, inputs.First.Uid, inputs.Second.Uid,
            inputs.First.ItemId, inputs.ResultBound, durability, words);
        return true;
    }
    // Data-only read of the exact once-retained result; caller still needs the
    // future real journal/native/holder commit proof before output or success.
    internal bool TryReadRetainedAllocation(out uint uid, out ushort durability, out uint[] words)
    {
        uid = 0; durability = 0; words = [];
        if (allocation is null || pending is null || retainedWords is null || !DependenciesCurrent() ||
            !runtime.IsRelicFuseAllocationCurrent(frame, this, pending, allocation)) return false;
        uid = allocation.First; durability = retainedDurability; words = retainedWords.ToArray(); return true;
    }
}
