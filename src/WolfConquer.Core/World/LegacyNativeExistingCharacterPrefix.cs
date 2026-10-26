using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// One local-file prefix only, not a complete login/normalization transaction.
internal sealed partial class LegacyNativeExistingCharacterPrefix
{
    private readonly ConquerTcpConnection connection;
    private readonly LegacyNativeGameClientRuntime client;
    private readonly LegacyNativePlayerRuntime player;
    private readonly LegacyNativePlayerRuntime.ExistingPrefixLoad playerLoad;
    private readonly LegacyNativeBeastsRuntime.PrefixLoad beasts;
    private readonly LegacyNativeHairfaceStorageRuntime.PrefixLoad hairface;
    private readonly LegacyNativeExistingPrefixBefore playerBefore;
    private readonly LegacyNativeBeastsRuntime.PrefixBefore beastsBefore;
    private readonly LegacyNativeHairfaceStorageRuntime.PrefixBefore hairfaceBefore;
    private readonly string path;
    private readonly byte[] before, after;
    private readonly CharacterSnapshot original, updated;
    private bool published;

    private LegacyNativeExistingCharacterPrefix(ConquerTcpConnection connection, string path, byte[] before, byte[] after,
        CharacterSnapshot original, CharacterSnapshot updated, LegacyNativePlayerRuntime.ExistingPrefixLoad playerLoad,
        LegacyNativeBeastsRuntime.PrefixLoad beasts, LegacyNativeHairfaceStorageRuntime.PrefixLoad hairface,
        LegacyNativeExistingPrefixBefore playerBefore, LegacyNativeBeastsRuntime.PrefixBefore beastsBefore,
        LegacyNativeHairfaceStorageRuntime.PrefixBefore hairfaceBefore)
    {
        this.connection = connection; client = connection.NativeGameClient!; player = client.Player;
        this.path = path; this.before = before; this.after = after; this.original = original; this.updated = updated;
        this.playerLoad = playerLoad; this.beasts = beasts; this.hairface = hairface;
        this.playerBefore = playerBefore; this.beastsBefore = beastsBefore; this.hairfaceBefore = hairfaceBefore;
    }

    private bool NativeIdentityCurrent() => !published && ReferenceEquals(connection.NativeGameClient, client) &&
        ReferenceEquals(client.Player, player) && ReferenceEquals(player.Owner, client) && !client.FullLoading && !client.Fake;

    private bool IsCurrent() => NativeIdentityCurrent() && playerLoad.IsCurrent() && beasts.IsCurrent() && hairface.IsCurrent() &&
        beastsBefore.IsCurrent() && hairfaceBefore.IsCurrent() && playerBefore.IsCurrent() &&
        beasts.IsOwnerCurrent() && hairface.IsOwnerCurrent() && playerLoad.IsCurrent() && NativeIdentityCurrent();

    internal bool TryCommit(out CharacterSnapshot snapshot, out string reason)
    {
        snapshot = original; reason = "normalPrefixOwnerChanged";
        var failure = reason;
        var commitAttempted = false;
        try
        {
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
            {
                var applied = SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
                {
                    if (!IsCurrent()) return false;
                    commitAttempted = true;
                    if (!CharacterSnapshotFileCoordinator.TryReplaceCapturedImage(path, before, after, out var uncertain, out failure))
                    {
                        failure = uncertain ? "normalPrefixRecoveryRequired:" + failure : "snapshotWrite:" + failure;
                        return false;
                    }
                    // Durable exact Online image is verified. Every subsequent write
                    // is a prepared plain assignment; no late rejection or metadata,
                    // snapshot, allocator, setter, save or packet callback follows.
                    playerLoad.PublishPrepared(); hairface.PublishPrepared(); beasts.PublishPrepared();
                    published = true;
                    failure = string.Empty;
                    return true;
                });
                reason = failure;
                if (applied) snapshot = updated;
                return applied;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException or
            InvalidDataException or InvalidOperationException or ArgumentException or OverflowException or IndexOutOfRangeException)
        {
            reason = (commitAttempted ? "normalPrefixRecoveryRequired:" : "normalPrefixPreparationChanged:") + error.GetType().Name;
            return false;
        }
    }
}
