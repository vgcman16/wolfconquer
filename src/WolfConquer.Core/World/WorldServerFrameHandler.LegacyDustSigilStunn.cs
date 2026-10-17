namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask SendDueLegacyDustSigilStunnExpirationsAsync(
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
            if (!TryCommitDueLegacyControlStatusExpiration(
                    active,
                    now,
                    LegacyBossStatusEffect.DizzyFlag,
                    LegacyControlStatusLifetime.DizzyExpiresAtUtcAttribute,
                    out var expiredCharacter,
                    out var removedVisibleFlag,
                    LegacyControlStatusLifetime.DizzyRemoveOnDeadAttribute))
            {
                continue;
            }

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

            await active.Connection.SendAsync(
                    new LegacyGameUpdatePacket(
                            uid,
                            LegacyGameUpdateDataType.Stun,
                            Active: false,
                            ShownAmount: 0,
                            Time: 0,
                            Amount: 0)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            playerCount++;
        }

        if (playerCount > 0)
        {
            await _log.WriteLineAsync(
                    "WorldServer expired legacy DustSigilStunn flags " +
                    $"playerCount={playerCount} viewerCount={viewerCount} " +
                    $"dataType={(uint)LegacyGameUpdateDataType.Stun}.")
                .ConfigureAwait(false);
        }
    }
}
