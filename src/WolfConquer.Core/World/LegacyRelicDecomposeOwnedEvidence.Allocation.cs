using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyRelicDecomposeOwnedEvidence
{
    private LegacyItemUidReservationRequest? pending;
    private LegacyItemUidReservationReceipt? allocation;
    private LegacyRelicDecomposeProjection? projection;
    private byte[]? projectedAfter;
    private byte[][]? preparedFrames;
    private uint preparedFirst;
    private int preparedCount, attempted;
    private bool reserving, published;

    internal bool TryReserveOnce(out string reason)
    {
        reason = "relicDecomposeAllocationAlreadyAttempted";
        if (Interlocked.Exchange(ref attempted, 1) != 0) return false;
        LegacyItemUidReservationReceipt? issued = null;
        try
        {
            before.RequireRelicDecomposeOwnerHeld(context, stable);
            runtime.RequireRelicDecomposePublicationPlan(frame);
            if (!DependenciesCurrent() || runtime.RequireRelicDecomposeCapacity(frame, recipe.NewUidCount) != domain)
            { reason = "relicDecomposeInputsChangedBeforeAllocation"; return false; }
            runtime.RequireRelicDecomposeObservedMaximum(frame, this, preparation.MaximumUid);
            var digest = LegacyRelicDecomposeRecipeDigest.Compute(domain, accountId, characterUid,
                request.CopyOriginalFrame(), recipe.Selections.ToArray(), recipe.RewardDurability,
                before.CopyCharacter(), before.CopyItems(), before.CopySpells());
            pending = new(Guid.NewGuid(), LegacyItemUidRegistry.RelicDecomposeWriter,
                accountId, characterUid, digest, recipe.NewUidCount);
            reserving = true;
            LegacyItemUidRegistry.Reservation? reserved;
            LegacyItemUidReservationOutcome outcome;
            try { outcome = runtime.ReserveRelicDecomposeUids(frame, this, pending, out reserved, out issued, out reason); }
            finally { reserving = false; }
            if (outcome != LegacyItemUidReservationOutcome.Reserved || reserved is null || issued is null)
            {
                if (outcome == LegacyItemUidReservationOutcome.Reserved) runtime.FenceRelicDecomposeUid(frame);
                return false;
            }
            allocation = issued;
            if (!reserved.TryTake(out var ids) || ids.Length != recipe.NewUidCount || issued.Count != ids.Length ||
                preparedFirst != issued.First || preparedCount != ids.Length || projection is null || preparedFrames is null ||
                ids.Where((uid, index) => uid != checked(issued.First + (uint)index)).Any() ||
                issued.First <= preparation.MaximumUid || !DependenciesCurrent()) throw Invalid();
            reason = string.Empty; return true;
        }
        catch (Exception failure)
        {
            try { if (issued is not null) runtime.FenceRelicDecomposeUid(frame); }
            catch (Exception fencing) { throw new AggregateException(failure, fencing); }
            throw;
        }
    }

    // Executed by the real registry BEFORE it advances its shared counter.
    // The Store simultaneously creates the actual native prepared publication.
    internal bool TryPrepareNextUids(uint first, int count, out string reason)
    {
        reason = "relicDecomposeNextUidsInvalid";
        if (!reserving || pending is null || preparedFirst != 0 || count != recipe.NewUidCount || first == 0 ||
            !DependenciesCurrent()) return false;
        if (!recipe.TryProject(stable.PlayerItems, first, count, out var materialized, out reason) || materialized is null) return false;
        var frames = materialized.CopyFrames();
        if (frames.Length != recipe.FrameCount || frames.Any(bytes => bytes.Length < 4)) return false;
        projection = materialized; projectedAfter = materialized.CopyAfterImage();
        preparedFrames = frames.Select(bytes => bytes.ToArray()).ToArray(); preparedFirst = first; preparedCount = count;
        try { return binding.Store.TryPrepareRelicDecomposeNativeHeld(this, stable with { PlayerItems = materialized.Items }, out reason); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or InvalidDataException or OverflowException)
        { reason = "relicDecomposePreparedImageInvalid:" + error.GetType().Name; return false; }
    }
    internal byte[][] CopyPreparedFrames()
    {
        if (preparedFrames is null || preparedFirst == 0 || preparedCount != recipe.NewUidCount ||
            preparedFrames.Length != recipe.FrameCount) throw Invalid();
        return preparedFrames.Select(bytes => bytes.ToArray()).ToArray();
    }
    internal bool MatchesPreparedFrameCount(int count) => preparedFrames is not null && count == preparedFrames.Length && count == recipe.FrameCount;
    internal bool IsIssuedAllocationCurrent(LegacyOwnedAdmissionRuntime actual, WorldServerFrameHandler actualFrame,
        LegacyItemUidReservationRequest actualRequest, LegacyItemUidReservationReceipt receipt) =>
        ReferenceEquals(actual, runtime) && ReferenceEquals(actualFrame, frame) &&
        ReferenceEquals(actualRequest, pending) && ReferenceEquals(receipt, allocation) && DependenciesCurrent();
    internal bool HasCurrentAllocation() => allocation is not null && pending is not null &&
        runtime.IsRelicDecomposeAllocationCurrent(frame, this, pending, allocation);

    internal bool ConfirmPublishedHeld(WorldSession actual, LegacyOrdinarySpellBinding actualBinding, LegacyPreparedSpellFileWrite actualWriter)
    {
        if (published || allocation is null || projectedAfter is null || !ReferenceEquals(actualBinding, binding) ||
            !ReferenceEquals(actualWriter, writer) || !before.HasCommittedRelicDecompose(this) ||
            !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
            !ReferenceEquals(actual.NativeGameClient, binding.Client) || !ReferenceEquals(binding.Items.Image.FinalSnapshot, actual.PlayerItems) ||
            !binding.Items.Image.IsCurrentPlain() || !PlayerItemSnapshotLoader.ToLegacyBytes(actual.PlayerItems).AsSpan().SequenceEqual(projectedAfter)) return false;
        published = true; return true;
    }
    // Identity only. The writer separately proves the COMMITTED saved successor
    // and exact afterimages; the precommit owner predicate is inapplicable here.
    internal void RequireRegistryAndReceipt(LegacyItemUidRegistry registry, LegacyItemUidReservationReceipt receipt)
    {
        if (!ReferenceEquals(runtime.Ordinary.Registry, registry) || !ReferenceEquals(allocation, receipt) || pending is null ||
            receipt.OperationId != pending.OperationId || receipt.Count != recipe.NewUidCount) throw Invalid();
    }
    internal void RetireCommittedAllocation(LegacyPreparedSpellFileWrite actualWriter)
    {
        if (!ReferenceEquals(actualWriter, writer) || allocation is null || !before.HasCommittedRelicDecompose(this)) throw Invalid();
        try
        {
            if (!runtime.RetireRelicDecomposeUids(frame, writer, this, allocation, out var reason))
                throw new InvalidDataException("relicDecomposeRetirementRefused:" + reason);
        }
        catch (Exception failure)
        {
            Exception retained = failure;
            try { runtime.FenceRelicDecomposeUid(frame); } catch (Exception fencing) { retained = new AggregateException(failure, fencing); }
            throw new LegacyCommittedSpellPublicationException(retained);
        }
    }
    internal void FenceUnlessPublished()
    { if (allocation is not null && !published) runtime.FenceRelicDecomposeUid(frame); }
}
