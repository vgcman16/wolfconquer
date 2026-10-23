using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask SendDueGuardMonsterActionsAsync(
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

        var activeMapIds = new HashSet<int>();
        foreach (var active in activeSessions)
        {
            var mapId = active.Session.Character.MapId;
            if (mapId is >= int.MinValue and <= int.MaxValue)
            {
                activeMapIds.Add((int)mapId);
            }
        }

        var playerAttackCount = 0;
        var monsterAttackCount = 0;
        var returnMovementCount = 0;
        foreach (var mapId in activeMapIds)
        {
            if (!_worldData.MonsterSpawnsByMapId.TryGetValue(mapId, out var spawns))
            {
                continue;
            }

            var occupiedMonsterTiles = CreateVisibleMonsterTileCounts(mapId, spawns, now);
            foreach (var entry in LegacyStaticMonsterMaterializer.Enumerate(_worldData, spawns))
            {
                var monsterFamily = entry.MonsterFamily;
                if (!IsLegacyGuardMonsterFamily(monsterFamily))
                {
                    continue;
                }

                var attackRange = LegacyMonsterFamilyRuntimeRules.ResolveAttackRange(monsterFamily);
                if (attackRange == 0)
                {
                    continue;
                }

                if (!_monsterStates.TryGetVisibleInstance(entry.Instance, now, out var visible))
                {
                    continue;
                }

                if (!CanAnyActiveSessionSeeMonster(activeSessions, visible))
                {
                    continue;
                }

                if (!IsLegacyMonsterTileUniquelyOccupied(occupiedMonsterTiles, visible))
                {
                    continue;
                }

                if (_monsterStates.TryMoveTowardTarget(
                        visible,
                        entry.Instance.X,
                        entry.Instance.Y,
                        now,
                        TimeSpan.FromMilliseconds(LegacyGuardReturnMoveMilliseconds),
                        out var returnedGuard,
                        out var returnDirection,
                        CreateLegacyMonsterMovementValidator(visible, occupiedMonsterTiles),
                        allowFallback: false))
                {
                    RemoveLegacyMonsterTileOccupancy(occupiedMonsterTiles, visible.X, visible.Y);
                    AddLegacyMonsterTileOccupancy(occupiedMonsterTiles, returnedGuard.X, returnedGuard.Y);
                    await SendMonsterMovementToVisibleAsync(
                            activeSessions,
                            visible,
                            returnedGuard,
                            returnDirection,
                            now,
                            _monsterStates,
                            _clientCompatibility,
                            cancellationToken)
                        .ConfigureAwait(false);
                    visible = returnedGuard;
                    returnMovementCount++;
                }

                if (TryFindLegacyGuardFlashingPlayerTarget(
                        activeSessions,
                        visible,
                        attackRange,
                        out var target) &&
                    _monsterStates.TryMarkAttackDue(
                        visible,
                        CheckedUInt32(target.Session.Character.Uid, nameof(target.Session.Character.Uid)),
                        now,
                        GetLegacyMonsterAttackInterval(monsterFamily)))
                {
                    var context = new WorldPacketHandlerContext(
                        target.Connection,
                        _log,
                        target.Session,
                        _arenaStatistics,
                        _arenaStates,
                        _worldData,
                        _sessions,
                        _teamArenaStatistics,
                        _teamArenaStates,
                        _teamStates,
                        _monsterStates,
                        _sobNpcStates,
                        _floorItems,
                        _terrain,
                        _guildWarStates,
                        _snapshotLoader?.PlayerSpellsRoot,
                        _snapshotLoader?.PlayerItemsRoot,
                        _mailboxes,
                        ClientCompatibility: _clientCompatibility,
                        TeleportTerrain: _teleportTerrain);
                    await WorldPacketHandlers.TrySendLegacyMonsterRetaliationAsync(
                            context,
                            target.Session,
                            visible,
                            monsterFamily,
                            cancellationToken)
                        .ConfigureAwait(false);
                    playerAttackCount++;
                }

                if (TryFindLegacyGuardMonsterProtectionTarget(
                        activeSessions,
                        spawns,
                        occupiedMonsterTiles,
                        visible,
                        attackRange,
                        now,
                        out var protectedTarget,
                        out var protectedTargetFamily) &&
                    _monsterStates.TryMarkAttackDue(
                        visible,
                        protectedTarget.Uid,
                        now,
                        GetLegacyMonsterAttackInterval(monsterFamily)) &&
                    await TrySendLegacyGuardMonsterProtectionAttackAsync(
                            activeSessions,
                            visible,
                            monsterFamily,
                            protectedTarget,
                            protectedTargetFamily,
                            now,
                            cancellationToken)
                        .ConfigureAwait(false))
                {
                    monsterAttackCount++;
                }
            }
        }

        if (playerAttackCount > 0)
        {
            await _log.WriteLineAsync($"WorldServer sent legacy guard attacks guardCount={playerAttackCount}.")
                .ConfigureAwait(false);
        }

        if (monsterAttackCount > 0)
        {
            await _log.WriteLineAsync(
                    $"WorldServer sent legacy guard monster-protection attacks monsterCount={monsterAttackCount}.")
                .ConfigureAwait(false);
        }

        if (returnMovementCount > 0)
        {
            await _log.WriteLineAsync(
                    $"WorldServer sent legacy guard return movement guardCount={returnMovementCount}.")
                .ConfigureAwait(false);
        }
    }

    private bool TryFindLegacyGuardMonsterProtectionTarget(
        IReadOnlyList<WorldActiveSession> activeSessions,
        IReadOnlyList<MonsterSpawn> spawns,
        IReadOnlyDictionary<(ushort X, ushort Y), int> occupiedMonsterTiles,
        LegacyMonsterInstance guard,
        uint attackRange,
        DateTimeOffset now,
        out LegacyMonsterInstance target,
        out Monster targetFamily)
    {
        if (_worldData is null)
        {
            target = default!;
            targetFamily = default!;
            return false;
        }

        foreach (var entry in LegacyStaticMonsterMaterializer.Enumerate(_worldData, spawns))
        {
            var monsterFamily = entry.MonsterFamily;
            if (IsLegacyGuardMonsterFamily(monsterFamily) ||
                IsLegacyReviverMonsterFamily(monsterFamily))
            {
                continue;
            }

            if (entry.Instance.Uid == guard.Uid ||
                !_monsterStates.TryGetVisibleInstance(entry.Instance, now, out var visible) ||
                visible.MapId != guard.MapId ||
                visible.DynamicId != guard.DynamicId ||
                !IsLegacyMonsterTileUniquelyOccupied(occupiedMonsterTiles, visible) ||
                !CanAnyActiveSessionSeeBothMonsters(activeSessions, guard, visible) ||
                LegacyGetDistance(visible.X, visible.Y, guard.X, guard.Y) >= attackRange)
            {
                continue;
            }

            target = visible;
            targetFamily = monsterFamily;
            return true;
        }

        target = default!;
        targetFamily = default!;
        return false;
    }

    private static bool IsLegacyMonsterTileUniquelyOccupied(
        IReadOnlyDictionary<(ushort X, ushort Y), int> occupiedMonsterTiles,
        LegacyMonsterInstance monster)
    {
        return occupiedMonsterTiles.TryGetValue((monster.X, monster.Y), out var count) &&
            count == 1;
    }

    private bool TryFindLegacyGuardFlashingPlayerTarget(
        IReadOnlyList<WorldActiveSession> activeSessions,
        LegacyMonsterInstance guard,
        uint attackRange,
        out WorldActiveSession target)
    {
        foreach (var active in activeSessions)
        {
            if (!_sessions.TryGet(active.Connection, out var currentSession) ||
                currentSession is null ||
                currentSession.Character.MapId != guard.MapId ||
                ReadCharacterDynamicId(currentSession.Character) != guard.DynamicId ||
                IsLegacyCharacterDead(currentSession.Character) ||
                LegacyPlayerDeathState.HasStatusFlag(currentSession.Character.Attributes, LegacyFlyStatusFlag) ||
                !LegacyPlayerDeathState.HasStatusFlag(currentSession.Character.Attributes, LegacyFlashingNameStatusFlag) ||
                !CanSeeMonster(currentSession.Character, guard))
            {
                continue;
            }

            var distance = LegacyGetDistance(
                currentSession.Character.X,
                currentSession.Character.Y,
                guard.X,
                guard.Y);
            if (distance >= attackRange)
            {
                continue;
            }

            target = new WorldActiveSession(active.Connection, currentSession);
            return true;
        }

        target = default!;
        return false;
    }

    private async ValueTask SendDueReviverMonsterRevivesAsync(
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

        var revivedCount = 0;
        var spellViewerCount = 0;
        foreach (var active in activeSessions)
        {
            // Skip-only prefilter; authoritative eligibility is read again after entry.
            if (!_sessions.TryGet(active.Connection, out var candidate) ||
                candidate is null ||
                !IsLegacyCharacterDead(candidate.Character))
            {
                continue;
            }

            using var publication = await EnterLegacyRuntimePublicationAsync(active.Connection, cancellationToken)
                .ConfigureAwait(false);

            if (!_sessions.TryGet(active.Connection, out var currentSession) ||
                currentSession is null ||
                !IsLegacyCharacterDead(currentSession.Character) ||
                !TryFindVisibleLegacyReviverFor(currentSession.Character, now, out var reviver))
            {
                continue;
            }

            var updatedSession = await ApplyLegacyReviverReviveAsync(
                    active.Connection,
                    currentSession,
                    now,
                    cancellationToken)
                .ConfigureAwait(false);
            if (updatedSession is null)
            {
                continue;
            }

            spellViewerCount += await SendLegacyReviverSpellAnimationAsync(
                    activeSessions,
                    updatedSession,
                    reviver,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyReviverPlayerSpawnToVisiblePeersAsync(
                    activeSessions,
                    active.Connection,
                    updatedSession,
                    cancellationToken)
                .ConfigureAwait(false);
            revivedCount++;
        }

        if (revivedCount > 0)
        {
            await _log.WriteLineAsync(
                    "WorldServer sent legacy Reviver revive " +
                    $"playerCount={revivedCount} spellViewerCount={spellViewerCount}.")
                .ConfigureAwait(false);
        }
    }

    private async ValueTask SendLegacyReviverPlayerSpawnToVisiblePeersAsync(
        IReadOnlyList<WorldActiveSession> activeSessions,
        ConquerTcpConnection revivedConnection,
        WorldSession revivedSession,
        CancellationToken cancellationToken)
    {
        var currentHitpoints = revivedSession.Character.MinHitPoints <= 0
            ? 1U
            : CheckedUInt32(revivedSession.Character.MinHitPoints, nameof(revivedSession.Character.MinHitPoints));
        var maxHitpoints = ComputeLegacyStartupMaxHitpoints(
            revivedSession.Character,
            currentHitpoints,
            revivedSession.PlayerItems,
            revivedSession.CombatGear ?? CombatGearSnapshot.Empty,
            _worldData);
        var spawn = LegacyPlayerSpawnPacket.FromSnapshot(
                revivedSession.Character,
                revivedSession.PlayerItems,
                alternateEquipmentActive: revivedSession.AlternateEquipmentActive,
                maxLife: maxHitpoints)
            .ToFrameBytes();

        foreach (var active in activeSessions)
        {
            if (ReferenceEquals(active.Connection, revivedConnection) ||
                active.Session.Character.Uid == revivedSession.Character.Uid ||
                !CanSeePlayer(active.Session.Character, revivedSession.Character))
            {
                continue;
            }

            await active.Connection.SendAsync(spawn, cancellationToken).ConfigureAwait(false);
        }
    }

    private bool TryFindVisibleLegacyReviverFor(
        CharacterSnapshot character,
        DateTimeOffset now,
        out LegacyMonsterInstance reviver)
    {
        reviver = default!;
        if (_worldData is null ||
            character.MapId is < int.MinValue or > int.MaxValue ||
            !_worldData.MonsterSpawnsByMapId.TryGetValue((int)character.MapId, out var spawns))
        {
            return false;
        }

        var dynamicId = ReadCharacterDynamicId(character);
        foreach (var entry in LegacyStaticMonsterMaterializer.Enumerate(_worldData, spawns))
        {
            if (!IsLegacyReviverMonsterFamily(entry.MonsterFamily))
            {
                continue;
            }

            if (!_monsterStates.TryGetVisibleInstance(entry.Instance, now, out var visible) ||
                visible.DynamicId != dynamicId ||
                LegacyGetDistance(character.X, character.Y, visible.X, visible.Y) >= 13)
            {
                continue;
            }

            reviver = visible;
            return true;
        }

        return false;
    }
}
