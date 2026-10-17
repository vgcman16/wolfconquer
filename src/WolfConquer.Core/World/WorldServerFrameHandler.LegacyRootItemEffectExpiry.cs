using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private IReadOnlyList<int> CaptureLegacyRootItemEffectExpirations(
        ConquerTcpConnection connection, WorldSession session, TimeProvider provider,
        DateTimeOffset subjectNow, out WorldSession cleaned)
    {
        var entries = new List<int>();
        var character = session.Character;
        var localNow = LegacyArrowBladesFlagRegistration.LocalTime(provider, subjectNow);
        // Fixed local order is one allowed source GetFlags order, not a dictionary-order assertion.
        foreach (var flag in new[] { LegacyTimedStigmaStatusFlag, LegacyTimedMagicShieldStatusFlag })
        {
            var visible = LegacyPlayerDeathState.HasStatusFlag(character.Attributes, flag);
            if (LegacyRootItemEffectFlagClock.IsProjected(character, flag))
            {
                if (LegacyRootItemEffectFlagClock.TryRead(_sessions, connection, character, flag, out var entry))
                {
                    if (entry.ExpiresAtLocal <= localNow) entries.Add(flag);
                }
                else if (!visible)
                    character = RetireLegacyRootItemEffectRegistration(connection, character, flag, out _);
                // A loaded/foreign/malformed projection NEVER becomes a new root clock.
                continue;
            }
            LegacyRootItemEffectFlagClock.Forget(_sessions, connection, character.Uid, flag);
            var key = RootItemEffectDeadlineKey(flag);
            if (!character.Attributes.TryGetValue(key, out var raw) ||
                !DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var expires) ||
                expires > subjectNow.ToUniversalTime()) continue;
            if (visible) entries.Add(flag);
            else character = RetireLegacyRootItemEffectRegistration(connection, character, flag, out _);
        }
        cleaned = ReferenceEquals(character, session.Character) ? session : session with { Character = character };
        return entries;
    }

    private static string RootItemEffectDeadlineKey(int flag) => flag == LegacyTimedStigmaStatusFlag
        ? WorldPacketHandlers.RuntimeLegacyStigmaExpiresAtUtcAttribute
        : WorldPacketHandlers.RuntimeLegacyMagicShieldExpiresAtUtcAttribute;

    private CharacterSnapshot RetireLegacyRootItemEffectRegistration(
        ConquerTcpConnection connection, CharacterSnapshot character, int flag, out bool removed)
    {
        // An old captured due object removes the CURRENT flag/clock even after a sameUID replacement.
        LegacyRootItemEffectFlagClock.Forget(_sessions, connection, character.Uid, flag);
        character = LegacyRootItemEffectFlagClock.ForgetProjection(character, flag);
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove(RootItemEffectDeadlineKey(flag));
        attributes.Remove(flag == LegacyTimedStigmaStatusFlag
            ? WorldPacketHandlers.RuntimeLegacyStigmaRemoveOnDeadAttribute
            : WorldPacketHandlers.RuntimeLegacyMagicShieldRemoveOnDeadAttribute);
        attributes.Remove(LegacyStatusFlagDeathRegistration.Key(flag));
        removed = RemoveLegacyStatusFlag(attributes, flag);
        return character with { Attributes = attributes };
    }

    private async ValueTask<(WorldSession? Session, int ViewerCount)> SendLegacyRootItemEffectExpiryVectorAsync(
        ConquerTcpConnection connection, long uid, WorldSession expired, CancellationToken cancellationToken)
    {
        var count = 0;
        if (!TryRefreshLegacyPhysicalStatusRuntimeSubject(connection, uid, out var current)) return (null, count);
        var frame = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
            CheckedUInt32(uid, nameof(uid)), LegacyPlayerDeathState.CreateStatusFlagVector(
                expired.Character.Attributes, LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength)).ToFrameBytes();
        await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        count++;
        if (!TryRefreshLegacyPhysicalStatusRuntimeSubject(connection, uid, out current)) return (null, count);
        foreach (var peer in _sessions.Snapshot())
        {
            if (!TryRefreshLegacyPhysicalStatusRuntimeSubject(connection, uid, out current)) return (null, count);
            if (ReferenceEquals(peer.Connection, connection) ||
                !_sessions.TryGet(peer.Connection, out var viewer) || viewer is null ||
                viewer.Character.Uid != peer.Session.Character.Uid || viewer.Character.Uid == uid ||
                !CanSeePlayer(viewer.Character, current.Character)) continue;
            await peer.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            count++;
            if (!TryRefreshLegacyPhysicalStatusRuntimeSubject(connection, uid, out current)) return (null, count);
        }
        return (current, count);
    }
}
