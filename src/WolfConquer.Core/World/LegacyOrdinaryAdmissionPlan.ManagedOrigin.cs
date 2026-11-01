using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOrdinaryAdmissionPlan
{
    // Character DATA from this actual committed admission. This is not a
    // replacement loader receipt or permission for an unrelated publication.
    internal bool TryCopyCommittedUnboundCharacterOrigin(WorldServerFrameHandler issuingFrame,
        WorldSessionStore issuingStore, ConquerTcpConnection actualConnection, WorldSession admitted,
        out string path, out byte[] image)
    {
        path = roots.PathFor(LegacyTradeJournalFileKind.Character, account);
        image = [];
        if (!ReferenceEquals(frame, issuingFrame) || !ReferenceEquals(connection, actualConnection) ||
            !ReferenceEquals(storeLease?.StoreForGearOrigin, issuingStore) || native is not null ||
            !committed || !published || !ReferenceEquals(Candidate, admitted) || !IsPublishedNativeOwnerCurrent() ||
            !issuingStore.TryGet(connection, out var current) || !ReferenceEquals(current, admitted) ||
            Recovery.IsBlocked || !Recovery.IsReadyForUsers(roots.Users) ||
            uids is not null && !uids.IsCurrent(roots)) return false;
        var characterPath = path;
        var captured = CharacterSnapshotFileCoordinator.ExecuteLocked(characterPath, () =>
        {
            if (LegacyTradeJournal.HasPending(roots.Users) || !IsPublishedNativeOwnerCurrent() ||
                !CharacterSnapshotFileBaseline.TryReadBytes(characterPath, "ordinaryCommittedOrigin", out var bytes, out _) ||
                !bytes.AsSpan().SequenceEqual(characterAfter)) return null;
            return characterAfter.ToArray();
        });
        if (captured is null) return false;
        image = captured;
        return true;
    }
}
