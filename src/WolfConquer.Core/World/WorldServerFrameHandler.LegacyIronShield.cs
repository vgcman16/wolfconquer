using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const int LegacyIronShieldStatusFlag = 0xD9;
    private const string RuntimeLegacyIronShieldRemoveOnDeadAttribute = "LegacyIronShieldRemoveOnDead";
    private const string RuntimeLegacyIronShieldDamageAttribute = "IronShieldDamage";

    private async ValueTask SendDueLegacyIronShieldExpirationsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var activeSessions = _sessions.Snapshot();
        if (activeSessions.Count == 0)
        {
            return;
        }

        var playerCount = 0;
        var viewerCount = 0;
        foreach (var active in activeSessions)
        {
            if (!_sessions.TryGet(active.Connection, out var currentSession) ||
                currentSession is null ||
                !TryExpireLegacyTimedStatusFlag(
                    currentSession.Character,
                    now,
                    LegacyIronShieldStatusFlag,
                    WorldPacketHandlers.RuntimeLegacyIronShieldExpiresAtUtcAttribute,
                    out var expiredCharacter,
                    out var removedVisibleFlag,
                    RuntimeLegacyIronShieldDamageAttribute,
                    RuntimeLegacyIronShieldRemoveOnDeadAttribute))
            {
                continue;
            }

            var updatedSession = currentSession with { Character = expiredCharacter };
            _sessions.Set(active.Connection, updatedSession);
            if (!removedVisibleFlag)
            {
                continue;
            }

            var uid = CheckedUInt32(expiredCharacter.Uid, nameof(expiredCharacter.Uid));
            var statusFrame = LegacyMsgUpdatePacket
                .CreateLegacyRuntimeStatusFlagVectorUpdate(
                    uid,
                    LegacyPlayerDeathState.CreateStatusFlagVector(
                        expiredCharacter.Attributes,
                        LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength))
                .ToFrameBytes();

            await active.Connection.SendAsync(statusFrame, cancellationToken).ConfigureAwait(false);
            viewerCount++;

            foreach (var viewer in activeSessions)
            {
                if (ReferenceEquals(viewer.Connection, active.Connection) ||
                    viewer.Session.Character.Uid == expiredCharacter.Uid ||
                    !CanSeePlayer(viewer.Session.Character, expiredCharacter))
                {
                    continue;
                }

                await viewer.Connection.SendAsync(statusFrame, cancellationToken).ConfigureAwait(false);
                viewerCount++;
            }

            playerCount++;
        }

        if (playerCount > 0)
        {
            await _log.WriteLineAsync(
                    "WorldServer expired legacy IronShield flags " +
                    $"playerCount={playerCount} viewerCount={viewerCount}.")
                .ConfigureAwait(false);
        }
    }
}
