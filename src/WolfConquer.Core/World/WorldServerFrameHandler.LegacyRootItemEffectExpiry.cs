using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private sealed record RootItemEffectDue(object Identity, WorldSession Initial,
        LegacyNativeAttributeMapProof Map, LegacyRootItemEffectFlagClock.ExpiryObservation[] Entries, DateTimeOffset SubjectNow);

    private RootItemEffectDue? CaptureLegacyRootItemEffectExpirations(
        RootExpiryTick tick, ConquerTcpConnection connection, WorldSession session)
    {
        if (!RootExpiryTickIsCurrent(tick, out var tickData)) return null;
        var provider = tickData.Provider;
        var now = provider.GetUtcNow();
        RootItemEffectDue? result = null;
        _sessions.WithCurrentNativeClientIdentity(connection, session.Character.Uid, identity =>
        {
            if (!RootExpiryTickIsCurrent(tick, out _) ||
                !_sessions.TryGet(connection, out var actual) || !ReferenceEquals(actual, session)) return;
            var map = LegacyNativeAttributeMapProof.CaptureRaw(session.Character.Attributes);
            var entries = new List<LegacyRootItemEffectFlagClock.ExpiryObservation>();
            foreach (var flag in new[] { LegacyTimedStigmaStatusFlag, LegacyTimedMagicShieldStatusFlag })
                if (LegacyRootItemEffectFlagClock.CaptureExpiry(_sessions, connection, session, identity, flag, now, provider) is { } entry)
                    entries.Add(entry);
            if (map.IsCurrentPlain() && RootExpiryTickIsCurrent(tick, out _)) result = new(identity, session, map, entries.ToArray(), now);
        });
        return result;
    }

    internal static bool TryProjectRootItemEffectExpiry(CharacterSnapshot character,
        LegacyRootItemEffectFlagClock.ExpiryObservation observation, out CharacterSnapshot projected, out bool removed)
    {
        projected = character; removed = false;
        if (observation.Flag is not 8 and not 9) return false;
        try
        {
            _ = LegacyWindWalkerStatusProjection.Read(character); // Validate both real vector aliases before any change.
            var attributes = LegacyCharacterAttributeMap.Copy(character.Attributes);
            LegacyCharacterAttributeMap.Remove(attributes, "LegacyRootItemEffectFlag." + observation.Flag + ".NativeOwned");
            LegacyCharacterAttributeMap.Remove(attributes, observation.Flag == LegacyTimedStigmaStatusFlag
                ? WorldPacketHandlers.RuntimeLegacyStigmaExpiresAtUtcAttribute
                : WorldPacketHandlers.RuntimeLegacyMagicShieldExpiresAtUtcAttribute);
            LegacyCharacterAttributeMap.Remove(attributes, observation.Flag == LegacyTimedStigmaStatusFlag
                ? WorldPacketHandlers.RuntimeLegacyStigmaRemoveOnDeadAttribute
                : WorldPacketHandlers.RuntimeLegacyMagicShieldRemoveOnDeadAttribute);
            LegacyCharacterAttributeMap.Remove(attributes, LegacyStatusFlagDeathRegistration.Key(observation.Flag));
            if (observation.Visible && !RemoveLegacyXpStatusFlag(attributes, observation.Flag)) return false;
            removed = observation.Visible;
            projected = character with { Attributes = attributes };
            return true;
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
        { return false; }
    }

    private bool RootEffectHolderCurrent(ConquerTcpConnection connection, long uid, object identity, WorldSession expected) =>
        LegacyNativeClientPublication.TryCurrent(_sessions, connection, uid, identity, out var current) && ReferenceEquals(current, expected);

    internal static bool IsRootExpiryRecipientVisible(ConquerTcpConnection subjectConnection, WorldSession subject,
        ConquerTcpConnection recipientConnection, WorldSession recipient) =>
        ReferenceEquals(subjectConnection, recipientConnection)
            ? ReferenceEquals(subject, recipient)
            : subject.Character.Uid != recipient.Character.Uid && CanSeePlayer(recipient.Character, subject.Character);

    private async ValueTask<(bool Current, int ViewerCount)> SendLegacyRootItemEffectExpiryVectorAsync(
        ConquerTcpConnection connection, long uid, object identity, WorldSession expired,
        WorldSessionStore.RootExpiryOutputReceipt output, CancellationToken cancellationToken)
    {
        var count = 0;
        if (!_sessions.TryGet(connection, out var current) ||
            !_sessions.TryIssueRootExpiryWrite(output, new WorldActiveSession(connection, current), cancellationToken, out var own) || own is null ||
            !await connection.SendOwnedRootExpiryAsync(_sessions, own, cancellationToken).ConfigureAwait(false)) return (false, count);
        count++;
        foreach (var peer in _sessions.Snapshot())
        {
            if (ReferenceEquals(peer.Connection, connection) ||
                !_sessions.TryIssueRootExpiryWrite(output, peer, cancellationToken, out var permit) || permit is null) continue;
            if (await peer.Connection.SendOwnedRootExpiryAsync(_sessions, permit, cancellationToken).ConfigureAwait(false)) count++;
        }
        return (RootEffectHolderCurrent(connection, uid, identity, expired), count);
    }
}
