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
    private readonly LegacySavedProficiencyImage? baseline, preparedImage;
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
        if (File.Exists(root) || !string.Equals(root, hit.ActorBinding.ProficienciesRoot,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) throw Invalid();
        baseline = hit.ActorBinding.SavedProficiencies;
        path = Path.Combine(root, hit.ActorBinding.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        (before, after) = SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
        {
            if (!store.IsBasicPhysicalPreparedProficiencyOwnerCurrent(frame, hit)) throw Invalid();
            var captured = ReadImage(path);
            if (!SavedOriginIsCurrentPlain() || !baseline.Matches(captured)) throw Invalid();
            RequireMatchesSnapshot(captured, Plan.Before);
            var projected = LegacyBasicPhysicalProficiencyImage.Project(captured, Plan.Calculation);
            // A known file sharing denial belongs before the HP commit.
            if (captured is not null)
            {
                using var writable = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                if (writable.Length != captured.Length) throw Invalid();
            }
            if (!store.IsBasicPhysicalPreparedProficiencyOwnerCurrent(frame, hit)) throw Invalid();
            return (captured, projected);
        });
        preparedImage = new LegacySavedProficiencyImage(after);
    }

    internal static LegacyPreparedBasicPhysicalProficiencyWrite Prepare(WorldServerFrameHandler frame,
        WorldSessionStore store, WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation hit) => new(frame, store, hit);

    internal bool IsPreparedFor(WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation actual) =>
        ReferenceEquals(hit, actual) && ReferenceEquals(Plan.Hit, actual);
    internal bool IsCommittedFor(WorldSessionStore.BasicPhysicalPublication actual) => ReferenceEquals(committed, actual);
    internal bool SavedOriginIsCurrentPlain() => !Plan.HasChanges || baseline is not null &&
        ReferenceEquals(baseline, hit.ActorBinding.SavedProficiencies);

    // Neutral has no file mutation. Changed proficiency is accepted solely
    // from the actual joint hit writer, never from an output continuation.
    internal Outcome TryCommit(WorldSessionStore.BasicPhysicalPublication publication, out string reason)
    {
        reason = "basicPhysicalProficiencyRequiresJointHit";
        if (Plan.HasChanges || !publication.IsCurrentProficiencyWritePlain(this, out var actor) ||
            !Plan.MatchesSource(actor)) return Outcome.Rejected;
        committed = publication; reason = string.Empty; return Outcome.Committed;
    }
    internal bool ExactCommittedImageIsCurrent(WorldSessionStore.BasicPhysicalPublication actual) =>
        IsCommittedFor(actual) && (!Plan.HasChanges || ReferenceEquals(preparedImage, hit.ActorBinding.SavedProficiencies) &&
            SnapshotFileLockCoordinator.ExecuteLocked(path!, () => SameImage(after, ReadImage(path!))));

    private static bool SameImage(byte[]? expected, byte[]? actual) =>
        expected is null ? actual is null : actual is not null && expected.AsSpan().SequenceEqual(actual);
    private static InvalidOperationException Invalid() => new("basicPhysicalProficiencyRequiresActualPreparedOwner");
}
