using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const int LegacyRiseOfTaoismStatusFlag = 0xE3;
    private const string RuntimeLegacyRiseOfTaoismRemoveOnDeadAttribute = "LegacyRiseOfTaoismRemoveOnDead";
    private const string RuntimeLegacyRiseOfTaoismExtraMagicDamageAttribute = "RiseofTaoismExtraMDamage";

    private async ValueTask SendDueLegacyRiseOfTaoismExpirationsAsync(
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var playerCount = 0;
        var viewerCount = 0;
        foreach (var candidate in _sessions.Snapshot())
        {
            var uid = candidate.Session.Character.Uid;
            if (!LegacyNativeClientPublication.TryCaptureIdentity(
                    _sessions, candidate.Connection, uid, out var identity))
                continue;
            var expired = false;
            var removed = false;
            if (!LegacyNativeClientPublication.TryUpdate(_sessions, candidate.Connection, uid, identity,
                owner =>
                {
                    if (!TryExpireLegacyTimedStatusFlag(owner.Character, now,
                            LegacyRiseOfTaoismStatusFlag,
                            WorldPacketHandlers.RuntimeLegacyRiseOfTaoismExpiresAtUtcAttribute,
                            out var character, out var visible,
                            RuntimeLegacyRiseOfTaoismRemoveOnDeadAttribute))
                        return owner;
                    expired = true;
                    removed = visible;
                    return owner with { Character = character };
                }, out var current) || !expired || !removed)
                continue;

            var frame = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
                CheckedUInt32(uid, nameof(uid)), LegacyPlayerDeathState.CreateStatusFlagVector(
                    current.Character.Attributes, LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength))
                .ToFrameBytes();
            await candidate.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            viewerCount++;
            if (!LegacyNativeClientPublication.TryCurrent(
                    _sessions, candidate.Connection, uid, identity, out current))
                continue;

            // Capture the view AFTER the owner's remove-vector await, retaining original peer identities.
            var viewers = new List<(WolfConquer.Core.Networking.ConquerTcpConnection Connection,
                long Uid, object Identity)>();
            foreach (var peer in _sessions.Snapshot())
            {
                var peerUid = peer.Session.Character.Uid;
                if (ReferenceEquals(peer.Connection, candidate.Connection) || peerUid == uid ||
                    !LegacyNativeClientPublication.TryCaptureIdentity(
                        _sessions, peer.Connection, peerUid, out var peerIdentity) ||
                    !LegacyNativeClientPublication.TryCurrent(
                        _sessions, peer.Connection, peerUid, peerIdentity, out var freshPeer) ||
                    !CanSeePlayer(freshPeer.Character, current.Character))
                    continue;
                viewers.Add((peer.Connection, peerUid, peerIdentity));
            }
            foreach (var viewer in viewers)
            {
                if (!LegacyNativeClientPublication.TryCurrent(
                        _sessions, candidate.Connection, uid, identity, out current))
                    break;
                if (!LegacyNativeClientPublication.TryCurrent(
                        _sessions, viewer.Connection, viewer.Uid, viewer.Identity, out var peer) ||
                    !CanSeePlayer(peer.Character, current.Character))
                    continue;
                await viewer.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                viewerCount++;
            }
            if (!LegacyNativeClientPublication.TryCurrent(
                    _sessions, candidate.Connection, uid, identity, out current))
                continue;

            // Native ThreadPool3007-3011 owes this query AFTER RemoveFlag's complete vector fanout.
            // Reentrant real re-acquisition is CURRENT input to this owed query, not an expiry re-gate.
            var context = CreateMonsterRuntimeContext(candidate with { Session = current });
            var scope = new LegacyEquipmentQueryPublicationScope(uid) { OwnerNativeClientIdentity = identity };
            var query = await WorldPacketHandlers.ExecuteLegacyEquipmentQueryAsync(context, current,
                new LegacyEquipmentQueryRequest(LegacyEquipmentRebuildReason.RiseOfTaoismChange, scope),
                cancellationToken).ConfigureAwait(false);
            if (query.Completed) playerCount++;
        }

        if (playerCount > 0)
            await _log.WriteLineAsync("WorldServer expired legacy RiseofTaoism flags " +
                $"playerCount={playerCount} viewerCount={viewerCount}.").ConfigureAwait(false);
    }
}
