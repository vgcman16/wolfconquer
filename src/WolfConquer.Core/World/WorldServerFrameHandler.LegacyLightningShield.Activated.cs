using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask SendDueLegacyLightningShieldActivatedExpirationsAsync(
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var localNow = TimeZoneInfo.ConvertTime(now,
            (_runtimeUpdateLoop.TimeProvider ?? TimeProvider.System).LocalTimeZone).DateTime;
        foreach (var candidate in _sessions.Snapshot())
        {
            var uid = candidate.Session.Character.Uid;
            if (!TryRefreshLegacyLightningShieldLifetimeSubject(candidate.Connection, uid, out var current) ||
                !LegacyLightningShieldActivationLifetime.TryRead(current.Character, out var expiresAt) ||
                localNow < expiresAt) continue;

            if (!LegacyPlayerDeathState.HasStatusFlag(current.Character.Attributes,
                    LegacyLightningShieldActivationLifetime.StatusFlag))
            {
                _sessions.Set(candidate.Connection, current with
                {
                    Character = LegacyLightningShieldActivationLifetime.Forget(current.Character),
                });
                continue; // Inert metadata is not a source registration or a visible removal.
            }

            // ThreadPool3063-3066: owner Azure reset precedes RemoveFlag/vector.
            // Leave the live flag and independent meter untouched during this await.
            var reset = new LegacyMsgUpdatePacket(CheckedUInt32(uid, nameof(uid)),
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.AzureShield, Value: 0,
                    Flag: 237, Time: 0, Damage: 0, SpellLevel: 0)]).ToFrameBytes();
            await candidate.Connection.SendAsync(reset, cancellationToken).ConfigureAwait(false);
            if (!TryRefreshLegacyLightningShieldLifetimeSubject(candidate.Connection, uid, out current) ||
                !LegacyLightningShieldActivationLifetime.TryRead(current.Character, out var currentExpiry) ||
                currentExpiry != expiresAt) continue;

            var attributes = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase);
            LegacyLightningShieldActivationLifetime.Forget(attributes);
            var removed = RemoveLegacyStatusFlag(attributes, LegacyLightningShieldActivationLifetime.StatusFlag);
            attributes.Remove(LegacyStatusFlagDeathRegistration.Key(LegacyLightningShieldActivationLifetime.StatusFlag));
            var updated = current with { Character = current.Character with { Attributes = attributes } };
            _sessions.Set(candidate.Connection, updated);
            if (removed)
                await SendLegacyLightningShieldLifetimeVectorAsync(candidate.Connection, uid, cancellationToken)
                    .ConfigureAwait(false);
        }
    }

    private bool TryRefreshLegacyLightningShieldLifetimeSubject(
        ConquerTcpConnection connection, long uid, out WorldSession session)
    {
        if (_sessions.TryGet(connection, out var current) && current is not null && current.Character.Uid == uid)
        {
            session = current;
            return true;
        }
        session = null!;
        return false;
    }

    private bool TryRefreshLegacyLightningShieldRemovedRegistration(
        ConquerTcpConnection connection, long uid, out WorldSession session) =>
        TryRefreshLegacyLightningShieldLifetimeSubject(connection, uid, out session) &&
        !LegacyPlayerDeathState.HasStatusFlag(session.Character.Attributes,
            LegacyLightningShieldActivationLifetime.StatusFlag) &&
        !session.Character.Attributes.ContainsKey(LegacyLightningShieldActivationLifetime.ExpiresAtLocalAttribute);

    private async ValueTask SendLegacyLightningShieldLifetimeVectorAsync(
        ConquerTcpConnection connection, long uid, CancellationToken cancellationToken)
    {
        if (!TryRefreshLegacyLightningShieldRemovedRegistration(connection, uid, out var current)) return;
        var frame = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
            CheckedUInt32(uid, nameof(uid)), LegacyPlayerDeathState.CreateStatusFlagVector(
                current.Character.Attributes, LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength)).ToFrameBytes();
        await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        if (!TryRefreshLegacyLightningShieldRemovedRegistration(connection, uid, out current)) return;
        foreach (var candidate in _sessions.Snapshot())
        {
            if (!TryRefreshLegacyLightningShieldRemovedRegistration(connection, uid, out current)) return;
            if (ReferenceEquals(candidate.Connection, connection) ||
                !_sessions.TryGet(candidate.Connection, out var peer) || peer is null ||
                peer.Character.Uid != candidate.Session.Character.Uid || peer.Character.Uid == uid ||
                !CanSeePlayer(current.Character, peer.Character)) continue;
            await candidate.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            if (!TryRefreshLegacyLightningShieldRemovedRegistration(connection, uid, out current)) return;
        }
    }
}
