using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask SendDueLegacyScarOfEarthRuntimeAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var activeSessions = _sessions.Snapshot();
        foreach (var active in activeSessions)
        {
            if (!_sessions.TryGet(active.Connection, out var currentSession) || currentSession is null)
            {
                continue;
            }

            var current = active with { Session = currentSession };
            await WorldPacketHandlers.TrySendDueLegacyPlayerScarOfEarthTickAsync(
                    CreateMonsterRuntimeContext(current),
                    now,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (_worldData is null)
        {
            return;
        }

        activeSessions = _sessions.Snapshot();
        if (activeSessions.Count == 0)
        {
            return;
        }

        var activeScopes = activeSessions
            .Where(active => active.Session.Character.MapId is >= int.MinValue and <= int.MaxValue)
            .Select(active => (
                MapId: (int)active.Session.Character.MapId,
                DynamicId: ReadCharacterDynamicId(active.Session.Character)))
            .Distinct()
            .ToArray();
        var visited = new HashSet<(int MapId, uint DynamicId, uint Uid)>();

        foreach (var scope in activeScopes)
        {
            if (_worldData.MonsterSpawnsByMapId.TryGetValue(scope.MapId, out var spawns))
            {
                foreach (var entry in LegacyStaticMonsterMaterializer.Enumerate(_worldData, spawns))
                {
                    if (entry.Instance.DynamicId != scope.DynamicId ||
                        !visited.Add((entry.Instance.MapId, entry.Instance.DynamicId, entry.Instance.Uid)) ||
                        !_monsterStates.TryGetVisibleInstance(entry.Instance, now, out var visible))
                    {
                        continue;
                    }

                    await ProcessLegacyMonsterScarOfEarthAsync(
                            visible,
                            entry.MonsterFamily,
                            now,
                            activeSessions,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            foreach (var entry in _monsterStates.GetVisibleDynamicMonsters(
                         scope.MapId,
                         scope.DynamicId,
                         now))
            {
                if (!visited.Add((entry.Instance.MapId, entry.Instance.DynamicId, entry.Instance.Uid)))
                {
                    continue;
                }

                await ProcessLegacyMonsterScarOfEarthAsync(
                        entry.Instance,
                        entry.MonsterFamily,
                        now,
                        activeSessions,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private async ValueTask ProcessLegacyMonsterScarOfEarthAsync(
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        DateTimeOffset now,
        IReadOnlyList<WorldActiveSession> activeSessions,
        CancellationToken cancellationToken)
    {
        var tick = _monsterStates.ConsumeScarOfEarthTick(
            monster,
            WorldPacketHandlers.LegacyScarOfEarthFlag,
            now,
            LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength);
        if (tick.Expired)
        {
            var frame = LegacyMsgUpdatePacket
                .CreateLegacyRuntimeStatusFlagVectorUpdate(monster.Uid, tick.StatusFlags)
                .ToFrameBytes();
            foreach (var viewer in activeSessions)
            {
                if (CanSeeMonster(viewer.Session.Character, monster))
                {
                    await viewer.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        if (!tick.Due || tick.State is not { } scar)
        {
            return;
        }

        if (!TryGetLegacyScarOfEarthCaster(activeSessions, scar.CasterUid, out var caster))
        {
            await _log.WriteLineAsync(
                    "WorldServer skipped due legacy Scar of Earth monster tick " +
                    $"monsterUid={monster.Uid} casterUid={scar.CasterUid}; " +
                    "original caster is not active, so cadence advanced without an offline attacker model.")
                .ConfigureAwait(false);
            return;
        }

        await WorldPacketHandlers.TrySendDueLegacyMonsterScarOfEarthTickAsync(
                CreateMonsterRuntimeContext(caster),
                monster,
                monsterFamily,
                scar,
                now,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private bool TryGetLegacyScarOfEarthCaster(
        IReadOnlyList<WorldActiveSession> activeSessions,
        uint casterUid,
        out WorldActiveSession caster)
    {
        foreach (var candidate in activeSessions)
        {
            if (candidate.Session.Character.Uid != casterUid ||
                !_sessions.TryGet(candidate.Connection, out var currentSession) ||
                currentSession is null)
            {
                continue;
            }

            caster = candidate with { Session = currentSession };
            return true;
        }

        caster = default!;
        return false;
    }
}
