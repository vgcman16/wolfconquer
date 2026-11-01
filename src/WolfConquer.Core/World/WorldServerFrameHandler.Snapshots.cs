using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask HandleSnapshotRequestAsync(
        ConquerTcpConnection connection,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        if (!InternalWorldSnapshotRequestPacket.TryParse(frame, out var request))
        {
            await SendResponseAsync(
                    connection,
                    WorldSnapshotWireStatus.InvalidPacket,
                    accountId: 0,
                    snapshot: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await _log.WriteLineAsync("WorldServer rejected malformed internal snapshot request.").ConfigureAwait(false);
            return;
        }

        await SendSnapshotForAccountAsync(connection, request.AccountId, cancellationToken).ConfigureAwait(false);
    }

    private readonly record struct SnapshotReply(CharacterSnapshot? Snapshot, WorldSnapshotWireStatus Status,
        CharacterSnapshotLoader.ManagedOriginLoad? ManagedOrigin = null);

    private async ValueTask<SnapshotReply> SendSnapshotForAccountAsync(
        ConquerTcpConnection connection,
        long accountId,
        CancellationToken cancellationToken, bool captureManagedOrigin = false)
    {
        if (_snapshotLoader is null)
        {
            await SendResponseAsync(
                    connection,
                    WorldSnapshotWireStatus.SnapshotsUnavailable,
                    accountId,
                    snapshot: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await _log.WriteLineAsync(
                    $"WorldServer snapshot request for {accountId} received but no Users root is configured.")
                .ConfigureAwait(false);
            return new(null, WorldSnapshotWireStatus.SnapshotsUnavailable);
        }

        CharacterSnapshotLoader.ManagedOriginLoad? managedOrigin = null;
        CharacterSnapshotLoadResult loadResult;
        try
        {
            string path = Path.Combine(_snapshotLoader.UsersRoot, accountId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".ini");
            if (captureManagedOrigin && connection.NativeGameClient is null && File.Exists(path))
            {
                managedOrigin = _snapshotLoader.LoadManagedOrigin(accountId);
                loadResult = managedOrigin.Result;
            }
            else loadResult = _snapshotLoader.TryLoadByAccountId(accountId);
        }
        catch (InvalidDataException error)
        {
            if (error.Message.StartsWith("managedOriginExpiryOutcomeUnproven:", StringComparison.Ordinal))
                _tradeRecovery.Block("managedOriginExpiryOutcomeUnproven");
            await SendResponseAsync(connection, WorldSnapshotWireStatus.SnapshotError, accountId, null, cancellationToken)
                .ConfigureAwait(false);
            await _log.WriteLineAsync($"WorldServer managed origin refused: {error.Message}.").ConfigureAwait(false);
            return new(null, WorldSnapshotWireStatus.SnapshotError);
        }
        if (loadResult.Status is CharacterSnapshotLoadStatus.SnapshotReadFailed or
            CharacterSnapshotLoadStatus.SnapshotReadDenied or
            CharacterSnapshotLoadStatus.SnapshotParseFailed or
            CharacterSnapshotLoadStatus.UidMismatch)
        {
            await SendResponseAsync(
                    connection,
                    WorldSnapshotWireStatus.SnapshotError,
                    accountId,
                    snapshot: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await _log.WriteLineAsync(
                    FormatSnapshotLoadFailure("WorldServer snapshot error", loadResult))
                .ConfigureAwait(false);
            return new(null, WorldSnapshotWireStatus.SnapshotError);
        }

        if (loadResult.Status == CharacterSnapshotLoadStatus.MissingSnapshot)
        {
            await SendResponseAsync(
                    connection,
                    WorldSnapshotWireStatus.MissingSnapshot,
                    accountId,
                    snapshot: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await _log.WriteLineAsync(
                    FormatSnapshotLoadFailure("WorldServer snapshot missing", loadResult))
                .ConfigureAwait(false);
            return new(null, WorldSnapshotWireStatus.MissingSnapshot);
        }

        if (captureManagedOrigin && connection.NativeGameClient is null && managedOrigin is null)
        {
            await SendResponseAsync(connection, WorldSnapshotWireStatus.SnapshotError, accountId, null, cancellationToken)
                .ConfigureAwait(false);
            await _log.WriteLineAsync("WorldServer managed origin unavailable after snapshot load.").ConfigureAwait(false);
            return new(null, WorldSnapshotWireStatus.SnapshotError);
        }

        var snapshot = loadResult.Snapshot;
        if (snapshot is null)
        {
            throw new InvalidOperationException("Successful character snapshot load returned no snapshot.");
        }

        await SendResponseAsync(
                connection,
                WorldSnapshotWireStatus.Success,
                accountId,
                snapshot,
                cancellationToken)
            .ConfigureAwait(false);
        await _log.WriteLineAsync(
                $"WorldServer snapshot Success for {accountId} uid={snapshot.Uid} map={snapshot.MapId}.")
            .ConfigureAwait(false);
        return new(snapshot, WorldSnapshotWireStatus.Success, managedOrigin);
    }
}
