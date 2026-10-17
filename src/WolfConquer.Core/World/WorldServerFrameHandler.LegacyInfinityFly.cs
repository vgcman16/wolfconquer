using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const int LegacyInfinityStatusFlag = 0xD7;
    private const string RuntimeLegacyFlyRemoveOnDeadAttribute = "LegacyFlyRemoveOnDead";
    private const string RuntimeLegacyInfinityRemoveOnDeadAttribute = "LegacyInfinityRemoveOnDead";
    private const string RuntimeLegacyInfinitySpellDamageAttribute = "InfinitySDamage";
    private const string RuntimeLegacyInfinityMonsterDamageAttribute = "InfinityDamage";

    private async ValueTask SendDueLegacyInfinityFlyExpirationsAsync(
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
                !TryExpireLegacyInfinityFlyStatusFlags(
                    currentSession.Character,
                    now,
                    out var expiredCharacter,
                    out var removedVisibleFlag))
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
                    "WorldServer expired legacy Infinity/Fly flags " +
                    $"playerCount={playerCount} viewerCount={viewerCount}.")
                .ConfigureAwait(false);
        }
    }

    private static bool TryExpireLegacyInfinityFlyStatusFlags(
        CharacterSnapshot character,
        DateTimeOffset now,
        out CharacterSnapshot updatedCharacter,
        out bool removedVisibleFlag)
    {
        var changed = false;
        removedVisibleFlag = false;
        updatedCharacter = character;

        if (TryExpireLegacyTimedStatusFlag(
                updatedCharacter,
                now,
                LegacyFlyStatusFlag,
                WorldPacketHandlers.RuntimeLegacyFlyExpiresAtUtcAttribute,
                out updatedCharacter,
                out var removedFlyFlag,
                RuntimeLegacyFlyRemoveOnDeadAttribute))
        {
            changed = true;
            removedVisibleFlag |= removedFlyFlag;
        }

        if (TryExpireLegacyTimedStatusFlag(
                updatedCharacter,
                now,
                LegacyInfinityStatusFlag,
                WorldPacketHandlers.RuntimeLegacyInfinityExpiresAtUtcAttribute,
                out updatedCharacter,
                out var removedInfinityFlag,
                RuntimeLegacyInfinitySpellDamageAttribute,
                RuntimeLegacyInfinityMonsterDamageAttribute,
                RuntimeLegacyInfinityRemoveOnDeadAttribute))
        {
            changed = true;
            removedVisibleFlag |= removedInfinityFlag;
        }

        return changed;
    }
}
