using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask SendDueLegacyPhysicalStatusOverlayExpirationsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in _sessions.Snapshot())
        {
            var ownerUid = candidate.Session.Character.Uid;
            if (!TryRefreshLegacyPhysicalStatusRuntimeSubject(candidate.Connection, ownerUid, out var current) ||
                !WorldPacketHandlers.TryExpireLegacyPhysicalFrontBreak(current, now, out var expired, out var removed))
            {
                continue;
            }

            // Consume registration and flag before any await: rejection never retries this event.
            _sessions.Set(candidate.Connection, expired);
            if (!removed)
            {
                continue;
            }

            var frame = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
                    CheckedUInt32(ownerUid, nameof(ownerUid)),
                    LegacyPlayerDeathState.CreateStatusFlagVector(
                        expired.Character.Attributes, LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength))
                .ToFrameBytes();
            if (!await SendLegacyPhysicalStatusRuntimeVectorAsync(
                    candidate.Connection, ownerUid, frame, cancellationToken).ConfigureAwait(false) ||
                !TryRefreshLegacyPhysicalStatusRuntimeSubject(candidate.Connection, ownerUid, out current))
            {
                continue;
            }

            var context = CreateMonsterRuntimeContext(candidate with { Session = current });
            var query = await WorldPacketHandlers.ExecuteLegacyEquipmentQueryAsync(
                    context, current,
                    new LegacyEquipmentQueryRequest(
                        LegacyEquipmentRebuildReason.FrontBreakExpiry,
                        new LegacyEquipmentQueryPublicationScope(ownerUid)),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!query.Completed)
            {
                continue;
            }
        }
    }

    private bool TryRefreshLegacyPhysicalStatusRuntimeSubject(
        ConquerTcpConnection connection,
        long ownerUid,
        out WorldSession session)
    {
        if (_sessions.TryGet(connection, out var current) && current is not null && current.Character.Uid == ownerUid)
        {
            session = current;
            return true;
        }

        session = null!;
        return false;
    }

    private async ValueTask<bool> SendLegacyPhysicalStatusRuntimeVectorAsync(
        ConquerTcpConnection connection,
        long ownerUid,
        byte[] frame,
        CancellationToken cancellationToken)
    {
        await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        if (!TryRefreshLegacyPhysicalStatusRuntimeSubject(connection, ownerUid, out var current))
        {
            return false;
        }

        foreach (var candidate in _sessions.Snapshot())
        {
            if (!TryRefreshLegacyPhysicalStatusRuntimeSubject(connection, ownerUid, out current))
            {
                return false;
            }

            if (ReferenceEquals(candidate.Connection, connection) ||
                !_sessions.TryGet(candidate.Connection, out var viewer) || viewer is null ||
                viewer.Character.Uid != candidate.Session.Character.Uid || viewer.Character.Uid == ownerUid ||
                !CanSeePlayer(current.Character, viewer.Character))
            {
                continue;
            }

            await candidate.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            if (!TryRefreshLegacyPhysicalStatusRuntimeSubject(connection, ownerUid, out current))
            {
                return false;
            }
        }

        return true;
    }
}
