using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Complete one-file native item save. No host/autosave/SQL route is installed here.
internal static partial class LegacyNativeClientItemSave
{
    internal enum Outcome { Committed, Rejected, RecoveryRequired }
    internal readonly record struct Result(Outcome Status, string Reason);

    // Existing callers cannot silently treat a failed or unproven write as success.
    internal static void SaveClientItems(LegacyNativeGameClientRuntime client)
    {
        var result = TrySaveClientItems(client);
        if (result.Status != Outcome.Committed)
            throw new IOException("Native item save did not commit: " + result.Reason);
    }

    internal static Result TrySaveClientItems(LegacyNativeGameClientRuntime client)
    {
        ArgumentNullException.ThrowIfNull(client);
        lock (client.SpellAdmissionSyncRoot)
        {
            var player = client.Player;
            if (player is null || !ReferenceEquals(player.Owner, client) || player.UID == 0)
                return new(Outcome.Rejected, "invalidPlayer");
            lock (player.SpellAdmissionSyncRoot)
            {
                try
                {
                    var location = SaveLocation.Capture(player.UID);
                    return SnapshotFileLockCoordinator.ExecuteLocked(location.Path,
                        () => SaveLocked(client, player, location));
                }
                catch (Exception exception) when (IsOrdinaryFailure(exception))
                {
                    // Actual write attempts classify uncertain destination state inside SaveLocked.
                    return new(Outcome.Rejected, "preparation:" + exception.GetType().Name);
                }
            }
        }
    }

    private static bool IsOrdinaryFailure(Exception exception) => exception is IOException or InvalidDataException or
        UnauthorizedAccessException or InvalidOperationException or ArgumentException or
        OverflowException or NotSupportedException or System.Security.SecurityException;
}
