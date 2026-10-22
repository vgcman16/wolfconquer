using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Managed quest-only save, not atomicity with the already-completed pickup,
    // native SQL, or an implementation of a physical native QuestGUI owner.
    private static bool TryPersistLegacyFloorPickupQuest1311(string root, long uid,
        PlayerQuestSnapshot expected, PlayerQuestSnapshot updated, out string reason)
    {
        reason = string.Empty;
        try
        {
            var path = Path.Combine(Path.GetFullPath(root), uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            var before = PlayerQuestSnapshotLoader.Serialize(expected);
            var after = PlayerQuestSnapshotLoader.Serialize(updated);
            var failure = string.Empty;
            var saved = SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
            {
                var current = SnapshotFilePersistence.ReadAllBytesIfExists(path);
                if (current is null)
                {
                    if (expected.Quests.Count != 0)
                    { failure = "questPersistenceBaselineMissing"; return false; }
                }
                else if (!current.AsSpan().SequenceEqual(before))
                { failure = "questPersistenceBaselineStale"; return false; }
                SnapshotFilePersistence.WriteAllBytesAtomically(path, after, ".player-quest-file-");
                return true;
            });
            reason = failure;
            return saved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or OverflowException or System.Security.SecurityException)
        {
            reason = "questPersistence:" + ex.GetType().Name;
            return false;
        }
    }
}
