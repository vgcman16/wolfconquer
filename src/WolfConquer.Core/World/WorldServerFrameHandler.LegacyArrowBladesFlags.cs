using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask SendDueLegacyArrowBladesFlagExpirationsAsync(CancellationToken cancellationToken)
    {
        var provider = _runtimeUpdateLoop.TimeProvider ?? TimeProvider.System;
        foreach (var candidate in _sessions.Snapshot())
        {
            var uid = candidate.Session.Character.Uid;
            if (!TryRefreshLegacyLightningShieldLifetimeSubject(candidate.Connection, uid, out var current)) continue;
            // BuffersCallback: one DateTime.Now and one GetFlags snapshot per original client.
            var localNow = LegacyArrowBladesFlagRegistration.LocalTime(provider, provider.GetUtcNow());
            var entries = new List<LegacyArrowBladesFlagRegistration>();
            foreach (var flag in new[] { LegacyArrowBladesFlagRegistration.FireFlag, LegacyArrowBladesFlagRegistration.WaterFlag, LegacyArrowBladesFlagRegistration.StarFlowFlag, LegacyArrowBladesFlagRegistration.StarFlowTriggerFlag })
                if (LegacyArrowBladesFlagRegistration.TryRead(current.Character, flag, out var entry))
                    entries.Add(entry);
            foreach (var entry in entries)
            {
                if (localNow < entry.ExpiresAtLocal) continue;
                if (!TryRefreshLegacyLightningShieldLifetimeSubject(candidate.Connection, uid, out current)) break;
                // Source acts on the captured old entry but RemoveFlag targets the current integer key.
                // A same-owner rearm after capture does not cancel an already-entered source expiry.
                var attributes = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase);
                var removed = RemoveLegacyStatusFlag(attributes, entry.Flag);
                attributes.Remove(LegacyStatusFlagDeathRegistration.Key(entry.Flag));
                current = current with { Character = current.Character with { Attributes = attributes } };
                _sessions.Set(candidate.Connection, current);
                if (removed)
                    await SendLegacyArrowBladesExpiryVectorAsync(
                            candidate.Connection, uid, current, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask SendLegacyArrowBladesExpiryVectorAsync(
        ConquerTcpConnection connection, long uid, WorldSession removed, CancellationToken cancellationToken)
    {
        if (!TryRefreshLegacyLightningShieldLifetimeSubject(connection, uid, out var current)) return;
        var frame = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
            CheckedUInt32(uid, nameof(uid)), LegacyPlayerDeathState.CreateStatusFlagVector(
                removed.Character.Attributes, LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength)).ToFrameBytes();
        await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        if (!TryRefreshLegacyLightningShieldLifetimeSubject(connection, uid, out current)) return;
        foreach (var candidate in _sessions.Snapshot())
        {
            if (!TryRefreshLegacyLightningShieldLifetimeSubject(connection, uid, out current)) return;
            if (ReferenceEquals(candidate.Connection, connection) ||
                !_sessions.TryGet(candidate.Connection, out var peer) || peer is null ||
                peer.Character.Uid != candidate.Session.Character.Uid || peer.Character.Uid == uid ||
                !CanSeePlayer(current.Character, peer.Character)) continue;
            await candidate.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            if (!TryRefreshLegacyLightningShieldLifetimeSubject(connection, uid, out current)) return;
        }
    }
}
