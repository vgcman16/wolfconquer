using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask SendDuePlayerGhostTransitionPublicationsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var playerCount = 0;
        var viewerCount = 0;
        foreach (var active in _sessions.Snapshot())
        {
            var ownerUid = active.Session.Character.Uid;
            if (!TryRefreshLegacyGhostSubject(active.Connection, ownerUid, out var current))
                continue;

            var ghost = LegacyPlayerDeathState.TryEnterGhost(current.Character, now);
            if (!ghost.Applied)
                continue;

            // Native AddFlag completes its owner/view vector before TransformationID is assigned.
            _sessions.Set(active.Connection, current with { Character = ghost.Character });
            playerCount++;
            var uid = CheckedUInt32(ownerUid, nameof(ownerUid));
            var statusFrame = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
                uid, LegacyPlayerDeathState.CreateStatusFlagVector(
                    ghost.Attributes, LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength)).ToFrameBytes();
            viewerCount += await SendLegacyGhostVisibleFrameAsync(
                active.Connection, ownerUid, statusFrame, cancellationToken).ConfigureAwait(false);
            if (!TryRefreshLegacyGhostSubject(active.Connection, ownerUid, out current))
                continue;

            // Read current Body/Face only after the entered AddFlag publication has completed.
            var transformed = LegacyPlayerDeathState.ApplyEnteredGhostTransformation(
                current.Character, out var transformationId);
            _sessions.Set(active.Connection, current with { Character = transformed });
            var mesh = unchecked((uint)(transformationId * 10_000_000 +
                transformed.Face * 10_000 + transformed.Body));
            var meshFrame = LegacyMsgUpdatePacket.CreateLegacyRuntimeMeshUpdate(uid, mesh).ToFrameBytes();
            await SendLegacyGhostVisibleFrameAsync(
                active.Connection, ownerUid, meshFrame, cancellationToken).ConfigureAwait(false);
            if (!TryRefreshLegacyGhostSubject(active.Connection, ownerUid, out current))
                continue;

            // Native MapStatus follows the Mesh setter's owner/view send and uses current map state.
            var mapFrame = LegacyMapStatusPacket.FromMap(current.Character.MapId, _worldData?.MapsById)
                .ToFrameBytes();
            await active.Connection.SendAsync(mapFrame, cancellationToken).ConfigureAwait(false);
        }

        if (playerCount > 0)
        {
            await _log.WriteLineAsync(
                    $"WorldServer sent player ghost transition playerCount={playerCount} viewerCount={viewerCount}.")
                .ConfigureAwait(false);
        }
    }

    private bool TryRefreshLegacyGhostSubject(
        ConquerTcpConnection connection, long ownerUid, out WorldSession session)
    {
        if (_sessions.TryGet(connection, out var current) && current is not null &&
            current.Character.Uid == ownerUid)
        {
            session = current;
            return true;
        }

        session = null!;
        return false;
    }

    private async ValueTask<int> SendLegacyGhostVisibleFrameAsync(
        ConquerTcpConnection connection, long ownerUid, byte[] frame, CancellationToken cancellationToken)
    {
        if (!TryRefreshLegacyGhostSubject(connection, ownerUid, out _))
            return 0;

        await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        var recipients = 1;
        if (!TryRefreshLegacyGhostSubject(connection, ownerUid, out var current))
            return recipients;

        foreach (var candidate in _sessions.Snapshot())
        {
            if (!TryRefreshLegacyGhostSubject(connection, ownerUid, out current))
                return recipients;

            if (ReferenceEquals(candidate.Connection, connection) ||
                !_sessions.TryGet(candidate.Connection, out var viewer) || viewer is null ||
                viewer.Character.Uid != candidate.Session.Character.Uid || viewer.Character.Uid == ownerUid ||
                !CanSeePlayer(viewer.Character, current.Character))
                continue;

            await candidate.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            recipients++;
            if (!TryRefreshLegacyGhostSubject(connection, ownerUid, out current))
                return recipients;
        }

        return recipients;
    }
}
