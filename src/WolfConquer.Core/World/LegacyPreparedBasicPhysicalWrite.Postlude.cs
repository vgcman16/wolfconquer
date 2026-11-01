using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedBasicPhysicalWrite
{
    // A plain private writer pointer and full file observation. The actual
    // Store receipt supplies current publication authority around this read.
    internal bool MatchesActualCommittedAfterImages() => SnapshotFileLockCoordinator.ExecuteLocked(Paths, () =>
        ReferenceEquals(committed, Operation) && spells.HasCommittedPhysicalDeathAnchor(this) &&
        !LegacyTradeJournal.HasPending(Roots.Users) && ImagesMatch(after: true));
}
