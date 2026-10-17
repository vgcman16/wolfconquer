namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask SendDueLegacyMonsterTimedStatusFlagExpirationsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (_worldData is null)
        {
            return;
        }

        var activeSessions = _sessions.Snapshot();
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
        var monsterCount = 0;
        var viewerCount = 0;

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

                    (monsterCount, viewerCount) = await TrySendLegacyMonsterTimedStatusExpiryAsync(
                            visible,
                            now,
                            activeSessions,
                            monsterCount,
                            viewerCount,
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

                (monsterCount, viewerCount) = await TrySendLegacyMonsterTimedStatusExpiryAsync(
                        entry.Instance,
                        now,
                        activeSessions,
                        monsterCount,
                        viewerCount,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (monsterCount > 0)
        {
            await _log.WriteLineAsync(
                    "WorldServer expired legacy monster timed status flags " +
                    $"monsterCount={monsterCount} viewerCount={viewerCount}.")
                .ConfigureAwait(false);
        }
    }

    private async ValueTask<(int MonsterCount, int ViewerCount)> TrySendLegacyMonsterTimedStatusExpiryAsync(
        LegacyMonsterInstance monster,
        DateTimeOffset now,
        IReadOnlyList<WorldActiveSession> activeSessions,
        int monsterCount,
        int viewerCount,
        CancellationToken cancellationToken)
    {
        if (!_monsterStates.TryExpireTimedStatusFlags(
                monster,
                now,
                LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength,
                out var statusFlags))
        {
            return (monsterCount, viewerCount);
        }

        var frame = LegacyMsgUpdatePacket
            .CreateLegacyRuntimeStatusFlagVectorUpdate(monster.Uid, statusFlags)
            .ToFrameBytes();
        var sentToViewer = false;
        foreach (var active in activeSessions)
        {
            if (!CanSeeMonster(active.Session.Character, monster))
            {
                continue;
            }

            await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            sentToViewer = true;
            viewerCount++;
        }

        return (sentToViewer ? monsterCount + 1 : monsterCount, viewerCount);
    }
}
