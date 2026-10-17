using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const int LegacyAuraExpirationMaximumAttempts = 4;
    private const int LegacyAurasMagicDefenderStatusFlag = 0x80;

    private async ValueTask SendDueLegacyAuraExpirationsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var activeSessions = _sessions.Snapshot();
        if (activeSessions.Count == 0)
        {
            return;
        }

        var auraPlayerCount = 0;
        var magicDefenderPlayerCount = 0;
        var viewerCount = 0;
        foreach (var active in activeSessions)
        {
            if (TryCommitDueLegacyAuraExpiration(
                    active.Connection,
                    now,
                    out var auraExpiredCharacter,
                    out var removedAuraFlag))
            {
                if (removedAuraFlag)
                {
                    viewerCount += await SendLegacyAuraExpirationStatusVectorAsync(
                            active.Connection,
                            auraExpiredCharacter,
                            activeSessions,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                auraPlayerCount++;
            }

            if (TryCommitDueLegacyAurasMagicDefenderExpiration(
                    active.Connection,
                    now,
                    out var magicDefenderExpiredCharacter,
                    out var removedMagicDefenderFlag))
            {
                if (removedMagicDefenderFlag)
                {
                    viewerCount += await SendLegacyAuraExpirationStatusVectorAsync(
                            active.Connection,
                            magicDefenderExpiredCharacter,
                            activeSessions,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                magicDefenderPlayerCount++;
            }
        }

        if (auraPlayerCount > 0 || magicDefenderPlayerCount > 0)
        {
            await _log.WriteLineAsync(
                    "WorldServer expired matching legacy Auras flags " +
                    $"auraPlayerCount={auraPlayerCount} " +
                    $"magicDefenderPlayerCount={magicDefenderPlayerCount} " +
                    $"viewerCount={viewerCount}; source=ThreadPool.cs aura/generic flag expiry.")
                .ConfigureAwait(false);
        }
    }

    private bool TryCommitDueLegacyAuraExpiration(
        Networking.ConquerTcpConnection connection,
        DateTimeOffset now,
        out CharacterSnapshot expiredCharacter,
        out bool removedVisibleFlag)
    {
        expiredCharacter = default!;
        removedVisibleFlag = false;
        for (var attempt = 0; attempt < LegacyAuraExpirationMaximumAttempts; attempt++)
        {
            if (!_sessions.TryGet(connection, out var currentSession) ||
                currentSession is null ||
                !WorldPacketHandlers.TryGetLegacyActiveAuraStatusFlag(
                    currentSession.Character,
                    out var statusFlag) ||
                !TryExpireLegacyTimedStatusFlag(
                    currentSession.Character,
                    now,
                    statusFlag,
                    WorldPacketHandlers.RuntimeLegacyAuraExpiresAtUtcAttribute,
                    out var candidateCharacter,
                    out var candidateRemovedVisibleFlag,
                    WorldPacketHandlers.RuntimeLegacyAuraStatusFlagAttribute,
                    WorldPacketHandlers.RuntimeLegacyAuraSpellIdAttribute,
                    WorldPacketHandlers.RuntimeLegacyAuraSpellLevelAttribute,
                    WorldPacketHandlers.RuntimeLegacyAuraDamageAttribute,
                    WorldPacketHandlers.RuntimeLegacyAuraGDamageAttribute,
                    WorldPacketHandlers.RuntimeLegacyAuraRemoveOnDeadAttribute))
            {
                return false;
            }

            var updatedSession = currentSession with { Character = candidateCharacter };
            if (!_sessions.TryCompareAndSet(connection, currentSession, updatedSession))
            {
                continue;
            }

            if (!_sessions.TryGet(connection, out var publishedSession) ||
                !ReferenceEquals(publishedSession, updatedSession))
            {
                return false;
            }

            expiredCharacter = candidateCharacter;
            removedVisibleFlag = candidateRemovedVisibleFlag;
            return true;
        }

        return false;
    }

    private bool TryCommitDueLegacyAurasMagicDefenderExpiration(
        Networking.ConquerTcpConnection connection,
        DateTimeOffset now,
        out CharacterSnapshot expiredCharacter,
        out bool removedVisibleFlag)
    {
        expiredCharacter = default!;
        removedVisibleFlag = false;
        for (var attempt = 0; attempt < LegacyAuraExpirationMaximumAttempts; attempt++)
        {
            if (!_sessions.TryGet(connection, out var currentSession) ||
                currentSession is null ||
                !TryExpireLegacyTimedStatusFlag(
                    currentSession.Character,
                    now,
                    LegacyAurasMagicDefenderStatusFlag,
                    WorldPacketHandlers.RuntimeLegacyAurasMagicDefenderExpiresAtUtcAttribute,
                    out var candidateCharacter,
                    out var candidateRemovedVisibleFlag,
                    WorldPacketHandlers.RuntimeLegacyAurasMagicDefenderRemoveOnDeadAttribute))
            {
                return false;
            }

            var updatedSession = currentSession with { Character = candidateCharacter };
            if (!_sessions.TryCompareAndSet(connection, currentSession, updatedSession))
            {
                continue;
            }

            if (!_sessions.TryGet(connection, out var publishedSession) ||
                !ReferenceEquals(publishedSession, updatedSession))
            {
                return false;
            }

            expiredCharacter = candidateCharacter;
            removedVisibleFlag = candidateRemovedVisibleFlag;
            return true;
        }

        return false;
    }

    private static async ValueTask<int> SendLegacyAuraExpirationStatusVectorAsync(
        Networking.ConquerTcpConnection subjectConnection,
        CharacterSnapshot subject,
        IReadOnlyList<WorldActiveSession> activeSessions,
        CancellationToken cancellationToken)
    {
        var uid = CheckedUInt32(subject.Uid, nameof(subject.Uid));
        var frame = LegacyMsgUpdatePacket
            .CreateLegacyRuntimeStatusFlagVectorUpdate(
                uid,
                LegacyPlayerDeathState.CreateStatusFlagVector(
                    subject.Attributes,
                    LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength))
            .ToFrameBytes();

        await subjectConnection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        var viewerCount = 1;
        foreach (var viewer in activeSessions)
        {
            if (ReferenceEquals(viewer.Connection, subjectConnection) ||
                viewer.Session.Character.Uid == subject.Uid ||
                !CanSeePlayer(viewer.Session.Character, subject))
            {
                continue;
            }

            await viewer.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            viewerCount++;
        }

        return viewerCount;
    }
}
