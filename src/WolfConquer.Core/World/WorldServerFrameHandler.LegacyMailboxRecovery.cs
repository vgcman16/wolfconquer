using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Called after actual account/snapshot admission and duplicate-login handling,
    // before the ordinary accepted-login prefix writes Online or refreshes owners.
    private async ValueTask<bool> TryStartLegacyMailboxRecoveryBeforePrefixAsync(ConquerTcpConnection connection,
        long accountId, CharacterSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (_mailboxPackages is not { } producer) return false;
        if (!producer.HasPendingAccountWork(accountId, out var pending, out var reason))
        {
            await _log.WriteLineAsync($"WorldServer legacy mailbox recovery admission refused: {reason}.").ConfigureAwait(false);
            return true;
        }
        if (!pending) return false;
        // The real recovery startup owns exact raw admission, cold native load,
        // PIN initialization and read-only startup packets. No normal prefix,
        // after-startup callback or runtime loop is entered on this branch.
        await TryStartMailboxDebtRecoverySessionAsync(connection, accountId, snapshot.Name, snapshot,
            connection.NativeGameClient, cancellationToken, answerLegacyLogin: true).ConfigureAwait(false);
        return true;
    }
}
