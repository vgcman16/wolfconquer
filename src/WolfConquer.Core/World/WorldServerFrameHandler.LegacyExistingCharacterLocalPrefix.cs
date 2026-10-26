using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Staged local-file source758..788 only. Normal admission/normalization and
    // Ninja saved-owner/Loading integration remain a separate unfinished join.
    private bool TryApplyLegacyExistingCharacterLocalPrefix(ConquerTcpConnection connection, CharacterSnapshot snapshot,
        out CharacterSnapshot updatedSnapshot, out string reason)
    {
        updatedSnapshot = snapshot;
        reason = string.Empty;
        if (connection.NativeGameClient is null) return true;
        if (_sessions.TryGet(connection, out _)) { reason = "normalPrefixSessionAlreadyAdmitted"; return false; }
        if (_snapshotLoader is null) { reason = "normalPrefixSnapshotLoaderMissing"; return false; }
        if (!LegacyNativeExistingCharacterPrefix.TryPrepare(connection, snapshot, _snapshotLoader.UsersRoot, out var prepared, out reason))
            return false;
        return prepared!.TryCommit(out updatedSnapshot, out reason);
    }
}
