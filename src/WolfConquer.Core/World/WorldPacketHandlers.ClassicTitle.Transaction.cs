using System.Net.Sockets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryCommitClassicTitle(WorldPacketHandlerContext context,
        WorldSession expected, WorldSession updated, object identity,
        CancellationToken cancellationToken, out string reason)
    {
        reason = "currentOwnerUnavailable";
        if (context.Sessions is not { } sessions || context.TradeRecovery is not { } recovery) return false;
        var failure = "sessionChanged";
        bool Persist()
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (recovery.IsBlocked) { failure = "recoveryRequired"; return false; }
            if (!IsCurrentClassicTitleOwner(context, expected, identity))
            { failure = "sessionIdentityChanged"; return false; }
            var saved = LegacyClassicTitlePersistence.TryPersist(expected, updated, out failure);
            if (!saved && failure.StartsWith(LegacyClassicTitlePersistence.RecoveryRequired, StringComparison.Ordinal))
                recovery.Block(failure);
            return saved;
        }
        var committed = sessions.TryCommitSession(context.Connection, expected, updated, Persist);
        reason = committed ? "committed" : failure;
        return committed;
    }

    private static bool IsCurrentClassicTitleOwner(WorldPacketHandlerContext context,
        WorldSession expected, object identity) => context.Sessions is { } sessions &&
        LegacyNativeClientPublication.TryCurrent(sessions, context.Connection, expected.Character.Uid,
            identity, out var current) && ReferenceEquals(current, expected);

    private static async ValueTask<bool> PublishClassicTitleAsync(WorldPacketHandlerContext context,
        WorldSession expected, object identity, IReadOnlyList<byte[]> frames,
        CancellationToken cancellationToken)
    {
        foreach (var bytes in frames)
        {
            if (!IsCurrentClassicTitleOwner(context, expected, identity)) return false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await context.Connection.SendAsync(bytes, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or OperationCanceledException)
            {
                // In Change, persistence and session publication already succeeded.
                // Never refund/reject a committed title because delivery stopped.
                await context.Log.WriteLineAsync($"WorldServer LegacyTitle delivery interrupted type={ex.GetType().Name}.")
                    .ConfigureAwait(false);
                return false;
            }
        }
        return true;
    }
}
