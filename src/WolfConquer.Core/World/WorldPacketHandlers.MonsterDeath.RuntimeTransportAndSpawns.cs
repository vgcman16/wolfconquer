using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> SendLegacyMonsterDeathTeleportAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        long mapId,
        long x,
        long y,
        string sourceBranch,
        CancellationToken cancellationToken)
    {
        var preflight = await PrepareLegacyTeleportAsync(
                context,
                session,
                new LegacyTeleportRequest(
                    $"MonsterRole monster-death branch ({sourceBranch}) -> GameClient.Teleport",
                    mapId),
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflight.Allowed)
        {
            return preflight.Session;
        }

        session = preflight.Session;
        var updatedSession = await ExecuteLegacyTeleportDestinationAsync(
                context with { Session = session },
                session,
                new LegacyTeleportDestination(mapId, x, y),
                timestamp: 0,
                cancellationToken)
            .ConfigureAwait(false);
        if (updatedSession is null)
        {
            return session;
        }

        await context.Log.WriteLineAsync(
                "WorldServer sent legacy monster death teleport " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"fromMap={monster.MapId} map={mapId} x={x} y={y} branch=\"{sourceBranch}\"; " +
                "source=MonsterRole.cs killer.Teleport -> GameClient.Teleport.")
            .ConfigureAwait(false);

        return updatedSession;
    }

    private static async ValueTask<WorldSession> SendLegacyMonsterDeathDynamicTeleportAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        long mapId,
        long x,
        long y,
        long dynamicId,
        string sourceBranch,
        CancellationToken cancellationToken)
    {
        var preflight = await PrepareLegacyTeleportAsync(
                context,
                session,
                new LegacyTeleportRequest(
                    $"MonsterRole monster-death dynamic branch ({sourceBranch}) -> GameClient.Teleport",
                    mapId,
                    dynamicId),
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflight.Allowed)
        {
            return preflight.Session;
        }

        session = preflight.Session;
        var updatedSession = await ExecuteLegacyTeleportDestinationAsync(
                context with { Session = session },
                session,
                new LegacyTeleportDestination(mapId, x, y, checked((ulong)dynamicId)),
                timestamp: 0,
                cancellationToken)
            .ConfigureAwait(false);
        if (updatedSession is null)
        {
            return session;
        }

        await context.Log.WriteLineAsync(
                "WorldServer sent legacy monster death dynamic teleport " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"fromMap={monster.MapId} map={mapId} x={x} y={y} dynamicId={dynamicId} branch=\"{sourceBranch}\"; " +
                "source=MonsterRole.cs killer.Teleport -> GameClient.Teleport.")
            .ConfigureAwait(false);

        return updatedSession;
    }

    private static async ValueTask<bool> TrySendLegacyMonsterDeathDynamicSpawnAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance sourceMonster,
        Monster sourceMonsterFamily,
        int spawnedMonsterId,
        int mapId,
        ushort x,
        ushort y,
        long dynamicId,
        string sourceBranch,
        CancellationToken cancellationToken,
        bool removeOnDead = false)
    {
        if (context.WorldData is not { } worldData ||
            !worldData.MonstersById.TryGetValue(spawnedMonsterId, out var spawnedFamily))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "monster.txt",
                        "legacy-dynamic-monster-spawn",
                        reason: "source Map.AddMapMonster creates a follow-up monster, but the target monster family row is not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", mapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", sourceMonster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", sourceMonsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("spawnedMonsterId", spawnedMonsterId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("spawnedMonsterId", spawnedMonsterId),
                        ]))
                .ConfigureAwait(false);
            return false;
        }

        if (context.MonsterStates is not { } monsterStates)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "MonsterRuntimeStateStore",
                        "legacy-dynamic-monster-spawn",
                        reason: "source Map.AddMapMonster creates a killable runtime monster, but no modern monster runtime store is available",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", mapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", sourceMonster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", sourceMonsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("spawnedMonsterId", spawnedMonsterId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("spawnedMonsterId", spawnedMonsterId),
                        ]))
                .ConfigureAwait(false);
            return false;
        }

        var uid = monsterStates.NextLegacyDynamicMonsterUid();
        var maxLife = ReadAttributeUInt32(spawnedFamily.Attributes, "life");
        var instance = new LegacyMonsterInstance(
            uid,
            mapId,
            x,
            y,
            ReadAttributeUInt32(spawnedFamily.Attributes, "lookface"),
            CheckedByte(checked((int)ReadAttributeUInt32(spawnedFamily.Attributes, "level")), "level"),
            maxLife,
            maxLife,
            CheckedUInt32(spawnedFamily.Id, nameof(spawnedFamily.Id)),
            spawnedFamily.Name,
            ReadAttributeUInt32(spawnedFamily.Attributes, "Boss") > 0,
            GeneratorId: CheckedInt32(uid, nameof(uid)),
            SpawnIndex: 0,
            RestSeconds: 0,
            DynamicId: CheckedUInt32(dynamicId, nameof(dynamicId)))
        {
            RemoveOnDead = removeOnDead,
        };
        instance = monsterStates.RegisterDynamicMonster(instance, spawnedFamily);

        await SendLegacyInstanceInfoMonsterAsync(context, instance, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent legacy monster death dynamic spawn " +
                $"{FormatSession(context)} sourceMonsterUid={sourceMonster.Uid} sourceFamilyId={sourceMonsterFamily.Id} " +
                $"monsterUid={instance.Uid} monsterId={instance.MonsterId} map={instance.MapId} x={instance.X} y={instance.Y} " +
                $"dynamicId={dynamicId} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        _ = session;
        return true;
    }

    private static bool HasVisibleLegacyDynamicMonster(
        MonsterRuntimeStateStore? monsterStates,
        int mapId,
        ulong dynamicId,
        uint monsterId)
    {
        if (monsterStates is null)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var entry in monsterStates.GetVisibleDynamicMonsters(mapId, dynamicId, now))
        {
            if (entry.Instance.MonsterId == monsterId)
            {
                return true;
            }
        }

        return false;
    }
}
