using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Only StartWorldSessionAsync creates this state from its actual configured
    // loader reply. It is never a general scalar saver or a native capability.
    private sealed partial class ManagedStartupPersistenceState
    {
        private readonly WorldServerFrameHandler frame;
        private readonly ConquerTcpConnection connection;
        private readonly CharacterSnapshotLoader loader;
        private readonly CharacterSnapshotLoadResult loaded;
        private readonly string path;
        private byte[] image;
        private CharacterSnapshot current;
        private LegacyNativeAttributeMapProof attributes, paid;
        private readonly PlayerQuestSnapshot quests;
        private readonly byte[] questImage;

        internal ManagedStartupPersistenceState(WorldServerFrameHandler frame, ConquerTcpConnection connection,
            long accountId, CharacterSnapshot snapshot, CharacterSnapshotLoader.ManagedOriginLoad origin)
        {
            this.frame = frame; this.connection = connection;
            loader = frame._snapshotLoader ?? throw Refused("loaderUnavailable");
            loaded = origin.Result; path = origin.Path; image = origin.Image.ToArray(); current = snapshot;
            quests = snapshot.PlayerQuests; questImage = PlayerQuestSnapshotLoader.Serialize(quests);
            attributes = LegacyNativeAttributeMapProof.CaptureRaw(snapshot.Attributes);
            paid = LegacyNativeAttributeMapProof.CaptureRaw(snapshot.PaidNobility);
            string configured = Path.Combine(loader.UsersRoot, accountId.ToString(CultureInfo.InvariantCulture) + ".ini");
            if (accountId is <= 0 or > uint.MaxValue || !loaded.Succeeded || loaded.AccountId != accountId || snapshot.Uid != accountId ||
                !ReferenceEquals(loaded.Snapshot, snapshot) || !string.Equals(configured, path, StringComparison.Ordinal) ||
                !string.Equals(loaded.Path, path, StringComparison.Ordinal) ||
                image.Length is 0 or > CharacterSnapshotFileBaseline.MaximumBytes || !MatchesImage(snapshot, image))
                throw Refused("loaderReceiptInvalid");
            lock (frame._tradeStates.SyncRoot)
                CharacterSnapshotFileCoordinator.ExecuteLocked(path, () => { RequireCurrent(snapshot); return true; });
        }

        internal CharacterSnapshotLoader.ManagedOriginLoad CompleteReceipt()
        {
            lock (frame._tradeStates.SyncRoot)
                return CharacterSnapshotFileCoordinator.ExecuteLocked(path, () =>
                {
                    RequireCurrent(current);
                    return new CharacterSnapshotLoader.ManagedOriginLoad(loaded with { Snapshot = current }, path, image.ToArray());
                });
        }

        internal CharacterSnapshot CommitClassStats(CharacterSnapshot expected, CharacterSnapshot proposed)
        {
            lock (frame._tradeStates.SyncRoot)
                return CharacterSnapshotFileCoordinator.ExecuteLocked(path, () =>
                {
                    var receipt = CommitCharacterLocked(expected, proposed);
                    try { return Accept(receipt); }
                    catch { frame._tradeRecovery.Block("managedStartupStatsCommittedPublicationFailed"); throw; }
                });
        }

        private void RequireCurrent(CharacterSnapshot expected)
        {
            if (!ReferenceEquals(current, expected) || !ReferenceEquals(frame._snapshotLoader, loader) ||
                connection.NativeGameClient is not null || frame._tradeStates.HasConnectionEnded(connection) ||
                frame._sessions.TryGet(connection, out _) || !frame._tradeRecovery.IsReadyForUsers(loader.UsersRoot) ||
                !attributes.IsCurrentPlain() || !paid.IsCurrentPlain() || !ReferenceEquals(expected.PlayerQuests, quests) ||
                !PlayerQuestSnapshotLoader.Serialize(quests).AsSpan().SequenceEqual(questImage) ||
                !CharacterSnapshotFileBaseline.TryReadBytes(path, "managedStartup", out var actual, out _) ||
                !actual.AsSpan().SequenceEqual(image)) throw Refused("beforeImageOrOwnerChanged");
        }

        private ManagedStartupCharacterReceipt CommitCharacterLocked(CharacterSnapshot expected, CharacterSnapshot proposed)
        {
            RequireCurrent(expected);
            var before = image.ToArray();
            var after = LegacyManagedCharacterProjection.ProjectAfterImage(before, proposed);
            var normalized = NormalizeCommittedSnapshot(expected, proposed, after);
            RequireCurrent(expected);
            bool saved, uncertain; string reason; Exception? failure = null;
            try { saved = CharacterSnapshotFileCoordinator.TryReplaceCapturedImage(path, before, after, out uncertain, out reason); }
            catch (Exception error) when (IsStartupIoFailure(error))
            { saved = false; uncertain = true; reason = error.GetType().Name; failure = error; }
            if (!saved)
            {
                if (uncertain) frame._tradeRecovery.Block("managedStartupCharacterOutcomeUnproven:" + reason);
                throw Refused("characterWrite:" + reason, failure);
            }
            if (!attributes.IsCurrentPlain() || !paid.IsCurrentPlain() || connection.NativeGameClient is not null ||
                frame._tradeStates.HasConnectionEnded(connection) || frame._sessions.TryGet(connection, out _) ||
                !CharacterSnapshotFileBaseline.TryReadBytes(path, "managedStartupCommitted", out var actual, out _) ||
                !actual.AsSpan().SequenceEqual(after))
            {
                frame._tradeRecovery.Block("managedStartupCharacterCommittedOwnerChanged");
                throw Refused("committedOwnerChanged");
            }
            return new ManagedStartupCharacterReceipt(this, expected, before, after, normalized);
        }

        private CharacterSnapshot Accept(ManagedStartupCharacterReceipt receipt)
        {
            if (!ReferenceEquals(receipt.Owner, this) || !ReferenceEquals(receipt.Before, current) ||
                !receipt.BeforeImage.AsSpan().SequenceEqual(image) || !attributes.IsCurrentPlain() || !paid.IsCurrentPlain() ||
                connection.NativeGameClient is not null || frame._tradeStates.HasConnectionEnded(connection) ||
                frame._sessions.TryGet(connection, out _) ||
                !CharacterSnapshotFileBaseline.TryReadBytes(path, "managedStartupReceipt", out var actual, out _) ||
                !actual.AsSpan().SequenceEqual(receipt.AfterImage))
            {
                frame._tradeRecovery.Block("managedStartupCharacterReceiptChanged");
                throw Refused("receiptChanged");
            }
            current = receipt.After; image = receipt.AfterImage.ToArray();
            attributes = LegacyNativeAttributeMapProof.CaptureRaw(current.Attributes);
            paid = LegacyNativeAttributeMapProof.CaptureRaw(current.PaidNobility);
            return current;
        }

        private sealed record ManagedStartupCharacterReceipt(ManagedStartupPersistenceState Owner,
            CharacterSnapshot Before, byte[] BeforeImage, byte[] AfterImage, CharacterSnapshot After);
        private static IOException Refused(string reason, Exception? cause = null) => new("managedStartupPersistenceRefused:" + reason, cause);
        private static bool IsStartupIoFailure(Exception error) => error is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or System.Security.SecurityException;
    }
}
