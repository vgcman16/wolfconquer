using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedBasicPhysicalProficiencyWrite
{
    internal enum Outcome { Rejected, Committed, RecoveryRequired }
    private readonly WorldSessionStore store;
    private readonly WorldServerFrameHandler frame;
    private readonly WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation hit;
    private readonly string? path;
    private readonly byte[]? before, after;
    private WorldSessionStore.BasicPhysicalPublication? committed;
    internal WorldPacketHandlers.OwnedBasicPhysicalProficiencyPlan Plan { get; }

    private LegacyPreparedBasicPhysicalProficiencyWrite(WorldServerFrameHandler frame, WorldSessionStore store,
        WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation hit)
    {
        this.frame = frame; this.store = store; this.hit = hit;
        if (!store.IsBasicPhysicalPreparedProficiencyOwnerCurrent(frame, hit)) throw Invalid();
        Plan = new(hit);
        if (!Plan.HasChanges) return; // Derived neutral phase, no unrelated file requirement.
        string root = frame.RequireBasicPhysicalProficiencyRoot(store, hit.ActorBinding);
        if (!Directory.Exists(root)) throw Invalid();
        path = Path.Combine(root, hit.ActorBinding.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        (before, after) = SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
        {
            if (!store.IsBasicPhysicalPreparedProficiencyOwnerCurrent(frame, hit)) throw Invalid();
            var captured = ReadImage(path);
            RequireMatchesSnapshot(captured, Plan.Before);
            var projected = ProjectImage(captured, Plan.After);
            // A known file sharing denial belongs before the HP commit.
            if (captured is not null)
            {
                using var writable = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                if (writable.Length != captured.Length) throw Invalid();
            }
            if (!store.IsBasicPhysicalPreparedProficiencyOwnerCurrent(frame, hit)) throw Invalid();
            return (captured, projected);
        });
    }

    internal static LegacyPreparedBasicPhysicalProficiencyWrite Prepare(WorldServerFrameHandler frame,
        WorldSessionStore store, WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation hit) => new(frame, store, hit);

    internal bool IsPreparedFor(WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation actual) =>
        ReferenceEquals(hit, actual) && ReferenceEquals(Plan.Hit, actual);
    internal bool IsCommittedFor(WorldSessionStore.BasicPhysicalPublication actual) => ReferenceEquals(committed, actual);

    internal Outcome TryCommit(WorldSessionStore.BasicPhysicalPublication publication, out string reason)
    {
        reason = "basicPhysicalProficiencyOwnerChanged";
        if (!publication.IsCurrentProficiencyWritePlain(this, out var actor) || !Plan.MatchesSource(actor)) return Outcome.Rejected;
        if (!Plan.HasChanges) { committed = publication; reason = string.Empty; return Outcome.Committed; }
        var failure = reason;
        try
        {
            var outcome = SnapshotFileLockCoordinator.ExecuteLocked(path!, () =>
            {
                if (!publication.IsCurrentProficiencyWritePlain(this, out var current) || !Plan.MatchesSource(current) ||
                    !SameImage(before, ReadImage(path!))) return Outcome.Rejected;
                bool saved, uncertain;
                if (before is null)
                    saved = CharacterSnapshotFileCoordinator.TryCreateCapturedImage(path!, after!, out uncertain, out failure);
                else
                    saved = CharacterSnapshotFileCoordinator.TryReplaceCapturedImage(path!, before, after!, out uncertain, out failure);
                if (!saved) return uncertain ? Outcome.RecoveryRequired : Outcome.Rejected;
                if (!SameImage(after, ReadImage(path!))) return Outcome.RecoveryRequired;
                committed = publication;
                return Outcome.Committed;
            });
            reason = failure; return outcome;
        }
        catch (Exception error) when (IsFileFailure(error))
        {
            reason = "basicPhysicalProficiencyWrite:" + error.GetType().Name;
            try
            {
                return SnapshotFileLockCoordinator.ExecuteLocked(path!, () =>
                    SameImage(before, ReadImage(path!)) ? Outcome.Rejected : Outcome.RecoveryRequired);
            }
            catch (Exception observation) when (IsFileFailure(observation))
            { reason += ";observation:" + observation.GetType().Name; return Outcome.RecoveryRequired; }
        }
    }

    internal bool ExactCommittedImageIsCurrent(WorldSessionStore.BasicPhysicalPublication actual) =>
        IsCommittedFor(actual) && (!Plan.HasChanges || SnapshotFileLockCoordinator.ExecuteLocked(path!, () => SameImage(after, ReadImage(path!))));

    private static bool SameImage(byte[]? expected, byte[]? actual) =>
        expected is null ? actual is null : actual is not null && expected.AsSpan().SequenceEqual(actual);
    private static InvalidOperationException Invalid() => new("basicPhysicalProficiencyRequiresActualPreparedOwner");
    private static bool IsFileFailure(Exception error) => error is IOException or UnauthorizedAccessException or
        ArgumentException or InvalidOperationException or NotSupportedException or System.Security.SecurityException;
}
