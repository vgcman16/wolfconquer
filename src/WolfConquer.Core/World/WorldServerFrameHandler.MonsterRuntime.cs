using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const uint LegacyGenericAggroGuardSettings = 0xA4;
    private const uint LegacyGenericAggroReviverSettings = 0x62;
    private const uint LegacyGenericAggroLottusSettings = 0x800;

    internal WorldPacketHandlerContext CreateMonsterRuntimeContext(WorldActiveSession active)
    {
        ArgumentNullException.ThrowIfNull(active);

        return new WorldPacketHandlerContext(
            active.Connection,
            _log,
            active.Session,
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
            ProtocolProfile: _protocolProfile,
            TeleportTerrain: _teleportTerrain,
            TimeProvider: _runtimeUpdateLoop.TimeProvider ?? TimeProvider.System,
            LegacyClock: _runtimeUpdateLoop.LegacyClock ?? LegacySignedMillisecondClock.System,
            TournamentWatcherHolders: _tournamentWatcherHolders)
        {
            Trades = _tradeStates,
            TradeRecovery = _tradeRecovery,
        };
    }

    private async ValueTask SendDueFloorSpellEffectsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var activeSessions = _sessions.Snapshot();
        if (activeSessions.Count == 0)
        {
            return;
        }

        var active = activeSessions[0];
        var context = CreateMonsterRuntimeContext(active) with
        {
            PlayerProficienciesRoot = _snapshotLoader?.PlayerProficienciesRoot,
        };
        await WorldPacketHandlers.SendDueLegacyTideTrapFloorSpellsAsync(context, now, cancellationToken)
            .ConfigureAwait(false);
        await WorldPacketHandlers.SendDueLegacyLayTrapFloorSpellsAsync(context, now, cancellationToken)
            .ConfigureAwait(false);
        await WorldPacketHandlers.SendDueLegacyLayTrapSourceCases12550And12570FloorSpellsAsync(context, now, cancellationToken)
            .ConfigureAwait(false);
        await WorldPacketHandlers.SendDueLegacyThundercloudFloorSpellsAsync(context, now, cancellationToken)
            .ConfigureAwait(false);
        await WorldPacketHandlers.SendDueLegacyRageOfWarFloorSpellsAsync(context, now, cancellationToken)
            .ConfigureAwait(false);
        await WorldPacketHandlers.SendDueLegacyCombatGearFloorSpellsAsync(context, now, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask SendDueFloorItemExpirationsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var expired = _floorItems.ConsumeExpired(now);
        if (expired.Count == 0)
        {
            return;
        }

        var activeSessions = _sessions.Snapshot();
        if (activeSessions.Count == 0)
        {
            return;
        }

        var itemCount = 0;
        var viewerCount = 0;
        foreach (var entry in expired)
        {
            if (!entry.DynamicId.HasValue && entry.MapId == 0)
            {
                continue;
            }

            var packet = entry.Packet with { DropType = LegacyFloorItemDropType.Remove };
            var frame = packet.ToFrameBytes();
            var sentToViewer = false;
            foreach (var active in activeSessions)
            {
                if (entry.DynamicId.HasValue
                    ? !LegacyFloorItemVisibility.CanBroadcast(active.Session.Character, entry)
                    : !CanSeeFloorItem(active.Session.Character, entry.MapId, packet.X, packet.Y))
                {
                    continue;
                }

                await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                sentToViewer = true;
                viewerCount++;
            }

            if (sentToViewer)
            {
                itemCount++;
            }
        }

        if (itemCount > 0)
        {
            await _log.WriteLineAsync(
                    $"WorldServer sent floor item expire remove itemCount={itemCount} viewerCount={viewerCount}.")
                .ConfigureAwait(false);
        }
    }

    private IReadOnlyList<LegacyMonsterInstance> GetActiveMonsterLifecycleInstances(
        IReadOnlyList<WorldActiveSession> activeSessions)
    {
        if (_worldData is null)
        {
            return [];
        }

        var scopes = new HashSet<(int MapId, ulong DynamicId)>();
        var visited = new HashSet<(int MapId, ulong DynamicId, uint Uid)>();
        var instances = new List<LegacyMonsterInstance>();
        foreach (var active in activeSessions)
        {
            var character = active.Session.Character;
            if (character.MapId is < int.MinValue or > int.MaxValue)
            {
                continue;
            }

            var mapId = checked((int)character.MapId);
            var dynamicId = (ulong)ReadCharacterDynamicId(character);
            if (!scopes.Add((mapId, dynamicId)))
            {
                continue;
            }

            if (dynamicId == 0 && _worldData.MonsterSpawnsByMapId.TryGetValue(mapId, out var spawns))
            {
                foreach (var entry in LegacyStaticMonsterMaterializer.Enumerate(_worldData, spawns))
                {
                    if (visited.Add((entry.Instance.MapId, entry.Instance.DynamicId, entry.Instance.Uid)))
                    {
                        instances.Add(entry.Instance);
                    }
                }
            }

            foreach (var instance in _monsterStates.GetDynamicMonsterLifecycleInstances(mapId, dynamicId))
            {
                if (visited.Add((instance.MapId, instance.DynamicId, instance.Uid)))
                {
                    instances.Add(instance);
                }
            }
        }

        return instances;
    }

    private async ValueTask SendDueMonsterDeathRemovalsAsync(
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

        var monsterCount = 0;
        var viewerCount = 0;
        foreach (var instance in GetActiveMonsterLifecycleInstances(activeSessions))
        {
            if (!_monsterStates.TryConsumeDeathRemovalInstance(instance, now, out var removed))
            {
                continue;
            }

            var frame = new MsgActionPacket
            {
                ObjId = removed.Uid,
                Type = MsgActionType.RemoveEntity,
            }.ToFrameBytes();

            var sentToViewer = false;
            foreach (var active in activeSessions)
            {
                if (!CanSeeMonster(active.Session.Character, removed))
                {
                    continue;
                }

                await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                sentToViewer = true;
                viewerCount++;
            }

            if (sentToViewer)
            {
                monsterCount++;
            }
        }

        if (monsterCount > 0)
        {
            await _log.WriteLineAsync(
                    $"WorldServer sent monster death remove monsterCount={monsterCount} viewerCount={viewerCount}.")
                .ConfigureAwait(false);
        }
    }

    private async ValueTask SendDueMonsterBlackSpotInactivePacketsAsync(
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

        var monsterCount = 0;
        var viewerCount = 0;
        foreach (var instance in GetActiveMonsterLifecycleInstances(activeSessions))
        {
            if (!_monsterStates.TryConsumeBlackSpotInactiveInstance(instance, now, out var inactive))
            {
                continue;
            }

            var frame = LegacyBlackspotPacket.CreateInactive(inactive.Uid).ToFrameBytes();
            var sentToViewer = false;
            foreach (var active in activeSessions)
            {
                if (!CanSeeMonster(active.Session.Character, inactive))
                {
                    continue;
                }

                await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                sentToViewer = true;
                viewerCount++;
            }

            if (sentToViewer)
            {
                monsterCount++;
            }
        }

        if (monsterCount > 0)
        {
            await _log.WriteLineAsync(
                    "WorldServer sent monster BlackSpot inactive " +
                    $"monsterCount={monsterCount} viewerCount={viewerCount}.")
                .ConfigureAwait(false);
        }
    }

    private async ValueTask SendDueMonsterRespawnsAsync(
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

        var monsterCount = 0;
        var viewerCount = 0;
        foreach (var instance in GetActiveMonsterLifecycleInstances(activeSessions))
        {
            if (!_monsterStates.TryConsumeRespawnedInstance(instance, now, out var respawned))
            {
                continue;
            }

            var frames = CreateLegacyMonsterRespawnFrameBytes(respawned, _clientCompatibility);
            var sentToViewer = false;
            foreach (var active in activeSessions)
            {
                if (!CanSeeMonster(active.Session.Character, respawned))
                {
                    continue;
                }

                foreach (var frame in frames)
                {
                    await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                }

                sentToViewer = true;
                viewerCount++;
            }

            if (sentToViewer)
            {
                monsterCount++;
            }
        }

        if (monsterCount > 0)
        {
            await _log.WriteLineAsync(
                    $"WorldServer sent monster respawn monsterCount={monsterCount} viewerCount={viewerCount}.")
                .ConfigureAwait(false);
        }
    }

    private async ValueTask SendDueMonsterAttacksAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (_clientCompatibility.DisableLegacyProactiveMonsterAttacks)
        {
            return;
        }

        if (_worldData is not { } worldData)
        {
            return;
        }

        var activeSessions = _sessions.Snapshot();
        if (activeSessions.Count == 0)
        {
            return;
        }

        var activeScopes = new HashSet<(int MapId, uint DynamicId)>();
        foreach (var active in activeSessions)
        {
            var mapId = active.Session.Character.MapId;
            if (mapId is >= int.MinValue and <= int.MaxValue)
            {
                activeScopes.Add((checked((int)mapId), ReadCharacterDynamicId(active.Session.Character)));
            }
        }

        var attackCount = 0;
        var movementCount = 0;
        foreach (var scope in activeScopes)
        {
            var visibleMonsters = GetVisibleProactiveMonsterEntries(
                worldData,
                scope.MapId,
                scope.DynamicId,
                now);
            if (visibleMonsters.Count == 0)
            {
                continue;
            }

            var occupiedMonsterTiles = CreateVisibleProactiveMonsterTileCounts(
                worldData,
                scope.MapId,
                scope.DynamicId,
                visibleMonsters,
                now);
            foreach (var entry in visibleMonsters)
            {
                var monsterFamily = entry.MonsterFamily;
                if (IsLegacyGenericAggroExcludedMonsterFamily(monsterFamily) ||
                    IsLegacyGenericAggroExcludedBySourceSettings(monsterFamily))
                {
                    continue;
                }

                var attackRange = LegacyMonsterFamilyRuntimeRules.ResolveAttackRange(monsterFamily);
                if (attackRange == 0)
                {
                    continue;
                }

                var viewRange = ReadMonsterAttributeUInt32(monsterFamily.Attributes, "view_range", "ViewRange");
                if (viewRange == 0)
                {
                    viewRange = checked((uint)LegacyMonsterViewThreshold);
                }

                var visible = entry.Instance;
                var attackInterval = GetLegacyMonsterAttackInterval(monsterFamily);
                var effectiveScanRange = ResolveLegacyMonsterTargetScanRange(visible, attackRange, viewRange);
                _monsterStates.TryGetTargetLockState(visible, now, out var targetLock);
                var hasTarget = TryFindMonsterTargetLockCandidate(
                    activeSessions,
                    visible,
                    effectiveScanRange,
                    targetLock.LastTargetUid,
                    out var target,
                    out var targetSnapshot);
                var targetDecision = _monsterStates.AdvanceTargetLock(
                    visible,
                    hasTarget ? targetSnapshot : null,
                    LegacyMonsterTargetLockLifecycle.Alive,
                    CheckedInt32(attackRange, nameof(attackRange)),
                    CheckedInt32(effectiveScanRange, nameof(effectiveScanRange)),
                    now,
                    attackInterval);

                if (targetDecision.AttackDue && hasTarget)
                {
                    var context = CreateMonsterRuntimeContext(target);
                    await WorldPacketHandlers.TrySendLegacyMonsterRetaliationAsync(
                            context,
                            target.Session,
                            visible,
                            monsterFamily,
                            cancellationToken)
                        .ConfigureAwait(false);
                    attackCount++;
                    continue;
                }

                if (targetDecision.Action != LegacyMonsterTargetLockAction.RetainTarget ||
                    !hasTarget ||
                    !_monsterStates.TryMoveTowardTarget(
                        visible,
                        CheckedUInt16(target.Session.Character.X, nameof(target.Session.Character.X)),
                        CheckedUInt16(target.Session.Character.Y, nameof(target.Session.Character.Y)),
                        now,
                        GetLegacyMonsterMoveInterval(monsterFamily),
                        out var moved,
                        out var direction,
                        CreateLegacyMonsterMovementValidator(visible, occupiedMonsterTiles)))
                {
                    continue;
                }

                RemoveLegacyMonsterTileOccupancy(occupiedMonsterTiles, visible.X, visible.Y);
                AddLegacyMonsterTileOccupancy(occupiedMonsterTiles, moved.X, moved.Y);
                await SendMonsterMovementToVisibleAsync(
                        activeSessions,
                        visible,
                        moved,
                        direction,
                        now,
                        _monsterStates,
                        _clientCompatibility,
                        cancellationToken)
                    .ConfigureAwait(false);
                movementCount++;
            }
        }

        if (attackCount > 0)
        {
            await _log.WriteLineAsync($"WorldServer sent proactive monster attacks monsterCount={attackCount}.")
                .ConfigureAwait(false);
        }

        if (movementCount > 0)
        {
            await _log.WriteLineAsync($"WorldServer sent proactive monster movement monsterCount={movementCount}.")
                .ConfigureAwait(false);
        }
    }

    private IReadOnlyList<LegacyProactiveMonsterRuntimeEntry> GetVisibleProactiveMonsterEntries(
        WorldDataCatalog worldData,
        int mapId,
        uint dynamicId,
        DateTimeOffset now)
    {
        var visited = new HashSet<uint>();
        var entries = new List<LegacyProactiveMonsterRuntimeEntry>();
        if (dynamicId == 0 && worldData.MonsterSpawnsByMapId.TryGetValue(mapId, out var spawns))
        {
            foreach (var entry in LegacyStaticMonsterMaterializer.Enumerate(worldData, spawns))
            {
                if (!visited.Add(entry.Instance.Uid) ||
                    !_monsterStates.TryGetVisibleInstance(entry.Instance, now, out var visible))
                {
                    continue;
                }

                entries.Add(new LegacyProactiveMonsterRuntimeEntry(
                    visible,
                    entry.MonsterFamily,
                    IsRegisteredDynamic: false));
            }
        }

        foreach (var entry in _monsterStates.GetVisibleDynamicMonsters(mapId, dynamicId, now))
        {
            if (!visited.Add(entry.Instance.Uid))
            {
                continue;
            }

            entries.Add(new LegacyProactiveMonsterRuntimeEntry(
                entry.Instance,
                entry.MonsterFamily,
                IsRegisteredDynamic: true));
        }

        return entries;
    }

    private Dictionary<(ushort X, ushort Y), int> CreateVisibleProactiveMonsterTileCounts(
        WorldDataCatalog worldData,
        int mapId,
        uint dynamicId,
        IReadOnlyList<LegacyProactiveMonsterRuntimeEntry> visibleMonsters,
        DateTimeOffset now)
    {
        var occupiedMonsterTiles = dynamicId == 0 &&
            worldData.MonsterSpawnsByMapId.TryGetValue(mapId, out var spawns)
                ? CreateVisibleMonsterTileCounts(mapId, spawns, now)
                : new Dictionary<(ushort X, ushort Y), int>();

        foreach (var entry in visibleMonsters)
        {
            if (entry.IsRegisteredDynamic)
            {
                AddLegacyMonsterTileOccupancy(
                    occupiedMonsterTiles,
                    entry.Instance.X,
                    entry.Instance.Y);
            }
        }

        return occupiedMonsterTiles;
    }

    private static bool IsLegacyGenericAggroExcludedBySourceSettings(Monster monsterFamily)
    {
        var settings = ReadMonsterAttributeUInt32(monsterFamily.Attributes, "settings", "Settings");
        return (settings & LegacyGenericAggroGuardSettings) == LegacyGenericAggroGuardSettings ||
            (settings & LegacyGenericAggroReviverSettings) == LegacyGenericAggroReviverSettings ||
            (settings & LegacyGenericAggroLottusSettings) == LegacyGenericAggroLottusSettings;
    }

    private bool TryFindMonsterTargetLockCandidate(
        IReadOnlyList<WorldActiveSession> activeSessions,
        LegacyMonsterInstance monster,
        long scanRange,
        uint? preferredTargetUid,
        out WorldActiveSession target,
        out LegacyMonsterTargetSnapshot snapshot)
    {
        if (preferredTargetUid is { } uid)
        {
            foreach (var active in activeSessions)
            {
                if (active.Session.Character.Uid == uid &&
                    TryCreateLegacyMonsterTargetSnapshot(active, monster, scanRange, out target, out snapshot))
                {
                    return true;
                }
            }
        }

        foreach (var active in activeSessions)
        {
            if (TryCreateLegacyMonsterTargetSnapshot(active, monster, scanRange, out target, out snapshot))
            {
                return true;
            }
        }

        target = default!;
        snapshot = default;
        return false;
    }

    private bool TryCreateLegacyMonsterTargetSnapshot(
        WorldActiveSession active,
        LegacyMonsterInstance monster,
        long scanRange,
        out WorldActiveSession target,
        out LegacyMonsterTargetSnapshot snapshot)
    {
        if (!_sessions.TryGet(active.Connection, out var currentSession) ||
            currentSession is null ||
            currentSession.Character.MapId != monster.MapId ||
            ReadCharacterDynamicId(currentSession.Character) != monster.DynamicId)
        {
            target = default!;
            snapshot = default;
            return false;
        }

        var distance = LegacyGetDistance(
            currentSession.Character.X,
            currentSession.Character.Y,
            monster.X,
            monster.Y);
        var alive = currentSession.Character.MinHitPoints > 0 &&
            !LegacyPlayerDeathState.HasStatusFlag(currentSession.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag) &&
            !LegacyPlayerDeathState.HasStatusFlag(currentSession.Character.Attributes, LegacyPlayerDeathState.GhostStatusFlag);
        var flying = LegacyPlayerDeathState.HasStatusFlag(currentSession.Character.Attributes, LegacyFlyStatusFlag);
        snapshot = new LegacyMonsterTargetSnapshot(
            CheckedUInt32(currentSession.Character.Uid, nameof(currentSession.Character.Uid)),
            CheckedInt32(distance, nameof(distance)),
            alive,
            flying,
            SocketAlive: true);

        if (!alive || flying || distance > scanRange)
        {
            target = default!;
            return false;
        }

        target = new WorldActiveSession(active.Connection, currentSession);
        return true;
    }

    private readonly record struct LegacyProactiveMonsterRuntimeEntry(
        LegacyMonsterInstance Instance,
        Monster MonsterFamily,
        bool IsRegisteredDynamic);
}
