using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Complete owned-file operations. Native login/save activation remains separate.
internal partial class LegacyNativeRoleQuests
{
    internal enum Outcome { Committed, NotFound, Rejected, RecoveryRequired }
    internal readonly record struct Result(Outcome Status, string Reason);

    public static void Save(LegacyNativeGameClientRuntime user)
    {
        var result = TrySave(user);
        if (result.Status != Outcome.Committed)
            throw new IOException("Native quest save did not commit: " + result.Reason);
    }

    public static void Load(LegacyNativeGameClientRuntime user)
    {
        var result = TryLoad(user);
        if (result.Status is not (Outcome.Committed or Outcome.NotFound))
            throw new IOException("Native quest load did not commit: " + result.Reason);
    }

    internal static Result TrySave(LegacyNativeGameClientRuntime user) => Execute(user, load: false);
    internal static Result TryLoad(LegacyNativeGameClientRuntime user) => Execute(user, load: true);

    private static Result Execute(LegacyNativeGameClientRuntime user, bool load)
    {
        ArgumentNullException.ThrowIfNull(user);
        lock (user.SpellAdmissionSyncRoot)
        {
            var player = user.Player;
            if (player is null || !ReferenceEquals(player.Owner, user) || player.UID == 0)
                return new(Outcome.Rejected, "invalidPlayer");
            lock (player.SpellAdmissionSyncRoot)
            {
                var book = player.QuestGUI;
                if (book is null || !ReferenceEquals(book.Player, player) ||
                    !ReferenceEquals(book.PersistenceOwner, player))
                    return new(Outcome.Rejected, "invalidQuestOwner");
                lock (book.PersistenceSyncRoot)
                {
                    try
                    {
                        var location = QuestLocation.Capture(player.UID);
                        return SnapshotFileLockCoordinator.ExecuteLocked(location.Path, () =>
                            load ? LoadLocked(user, player, book, location) : SaveLocked(user, player, book, location));
                    }
                    catch (Exception exception) when (IsOrdinaryFailure(exception))
                    { return new(Outcome.Rejected, "preparation:" + exception.GetType().Name); }
                }
            }
        }
    }

    private static bool IsOrdinaryFailure(Exception exception) => exception is IOException or
        InvalidDataException or UnauthorizedAccessException or InvalidOperationException or
        ArgumentException or OverflowException or NotSupportedException or System.Security.SecurityException;
}
