using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask SendDueLegacyCanAttackFlashingExpirationsAsync(CancellationToken cancellationToken)
    {
        var provider = _runtimeUpdateLoop.TimeProvider ?? TimeProvider.System;
        foreach (var candidate in _sessions.Snapshot())
        {
            var uid = candidate.Session.Character.Uid;
            if (!TryRefreshLegacyLightningShieldLifetimeSubject(candidate.Connection, uid, out var current)) continue;
            var localNow = LegacyCanAttackFlashingRegistration.LocalTime(provider, provider.GetUtcNow());
            if (!LegacyCanAttackFlashingRegistration.TryRead(current, out var captured) ||
                localNow < captured.ExpiresAtLocal) continue;
            if (!TryRefreshLegacyLightningShieldLifetimeSubject(candidate.Connection, uid, out current)) continue;
            // Expiry belongs to the captured source entry, but RemoveFlag acts on the current key.
            var attributes = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase);
            if (!RemoveLegacyStatusFlag(attributes, LegacyCanAttackFlashingRegistration.StatusFlag)) continue;
            attributes.Remove(LegacyStatusFlagDeathRegistration.Key(LegacyCanAttackFlashingRegistration.StatusFlag));
            current = current with { Character = current.Character with { Attributes = attributes } };
            _sessions.Set(candidate.Connection, current);
            await SendLegacyCanAttackFlashingExpiryVectorAsync(
                    candidate.Connection, uid, current, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask SendLegacyCanAttackFlashingExpiryVectorAsync(
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
