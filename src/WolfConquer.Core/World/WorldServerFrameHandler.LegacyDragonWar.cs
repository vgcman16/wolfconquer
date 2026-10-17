using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const int LegacyDragonSwingStatusFlag = 0xA0;
    private const string RuntimeLegacyDragonSwingExpiresAtUtcAttribute = "LegacyDragonSwingExpiresAtUtc";
    private readonly LegacyDragonWarOpenScheduler _dragonWarOpenScheduler = new();

    private async ValueTask SendDueLegacyDragonWarRuntimeUpdatesAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await SendDueLegacyDragonWarOpenAsync(now, cancellationToken).ConfigureAwait(false);

        var activeSessions = _sessions.Snapshot();
        if (activeSessions.Count == 0)
        {
            return;
        }

        var connectionsByUid = activeSessions
            .GroupBy(active => active.Session.Character.Uid)
            .ToDictionary(
                group => group.Key,
                group => group.Select(active => active.Connection).Distinct().ToArray());
        var previousSessionsByConnection = activeSessions.ToDictionary(
            active => active.Connection,
            active => active.Session);
        var runtimeSessions = activeSessions
            .GroupBy(active => active.Session.Character.Uid)
            .Select(group => group.First().Session)
            .ToArray();
        var tick = _dragonWarStates.CreateDueRuntimeUpdates(
            now,
            runtimeSessions);
        if (tick.Updates.Count == 0 && tick.SessionUpdates.Count == 0)
        {
            return;
        }

        var mutationCount = 0;
        foreach (var sessionUpdate in tick.SessionUpdates)
        {
            if (!connectionsByUid.TryGetValue(sessionUpdate.RecipientUid, out var connections))
            {
                continue;
            }

            var teleportSources = connections
                .Select(connection => previousSessionsByConnection.TryGetValue(connection, out var previousSession)
                    ? new WorldActiveSession(connection, previousSession)
                    : null)
                .Where(active =>
                    active is not null &&
                    IsLegacyDragonWarRuntimeTeleport(
                        active.Session.Character,
                        sessionUpdate.Session.Character))
                .Cast<WorldActiveSession>()
                .ToArray();
            LegacyTeleportPreflightOutcome? teleportPreflight = null;
            if (teleportSources.Length > 0)
            {
                teleportPreflight = await PrepareLegacyRuntimeTeleportAsync(
                        teleportSources[0],
                        new LegacyTeleportRequest(
                            "MsgDragonWar runtime -> GameClient.Teleport",
                            sessionUpdate.Session.Character.MapId,
                            LegacyFreezeWarRuntimeStateStore.ReadDynamicId(sessionUpdate.Session.Character)),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!teleportPreflight.Value.Allowed)
                {
                    continue;
                }

                for (var index = 0; index < teleportSources.Length; index++)
                {
                    var source = teleportSources[index];
                    teleportSources[index] = source with
                    {
                        Session = WorldPacketHandlers.CarryLegacyTeleportEntrySideEffects(
                            teleportPreflight.Value,
                            source.Session),
                    };
                }
            }

            var appliedSessionUpdate = teleportSources.Length > 0
                ? CreateLegacyDragonWarRuntimeTeleportSessionUpdate(sessionUpdate)
                : sessionUpdate;
            if (teleportPreflight is { } appliedPreflight)
            {
                appliedSessionUpdate = appliedSessionUpdate with
                {
                    Session = WorldPacketHandlers.CarryLegacyTeleportEntrySideEffects(
                        appliedPreflight,
                        appliedSessionUpdate.Session),
                };
            }

            if (teleportSources.Length > 0)
            {
                var teleportSourcesByConnection = teleportSources.ToDictionary(
                    source => source.Connection,
                    source => source);
                var appliedConnections = new List<ConquerTcpConnection>(connections.Length);
                WorldSession? representativeSession = null;
                foreach (var connection in connections)
                {
                    var previousSession = previousSessionsByConnection.TryGetValue(
                        connection,
                        out var storedPreviousSession)
                        ? storedPreviousSession
                        : null;
                    var connectionSessionUpdate = appliedSessionUpdate;
                    if (teleportSourcesByConnection.TryGetValue(connection, out var teleportSource))
                    {
                        var destinationCharacter = appliedSessionUpdate.Session.Character;
                        var teleportedSession = await WorldPacketHandlers.ExecuteLegacyTeleportDestinationAsync(
                                CreateMonsterRuntimeContext(teleportSource),
                                teleportSource.Session,
                                new LegacyTeleportDestination(
                                    destinationCharacter.MapId,
                                    destinationCharacter.X,
                                    destinationCharacter.Y,
                                    LegacyFreezeWarRuntimeStateStore.ReadDynamicId(destinationCharacter)),
                                timestamp: 0,
                                cancellationToken)
                            .ConfigureAwait(false);
                        if (teleportedSession is null)
                        {
                            continue;
                        }

                        connectionSessionUpdate = appliedSessionUpdate with
                        {
                            Session = MergeLegacyDragonWarRuntimeTeleportSession(
                                teleportedSession,
                                appliedSessionUpdate.Session),
                        };
                        mutationCount += teleportSource.Session.Character.MapId ==
                            LegacyDragonWarRuntimeStateStore.DragonWarMapId
                            ? 4
                            : 5;
                    }

                    _sessions.Set(connection, connectionSessionUpdate.Session);
                    await LogLegacyDragonWarRewardGrantAsync(connectionSessionUpdate, previousSession)
                        .ConfigureAwait(false);
                    await PersistLegacyDragonWarPlayerItemsAsync(connectionSessionUpdate).ConfigureAwait(false);
                    appliedConnections.Add(connection);
                    representativeSession ??= connectionSessionUpdate.Session;
                }

                if (representativeSession is null)
                {
                    continue;
                }

                appliedSessionUpdate = appliedSessionUpdate with { Session = representativeSession };
                mutationCount += await SendLegacyDragonWarRuntimeStatusAfterTeleportAsync(
                        activeSessions,
                        appliedConnections,
                        appliedSessionUpdate,
                        cancellationToken)
                    .ConfigureAwait(false);

                foreach (var connection in appliedConnections)
                {
                    foreach (var ownerFrame in appliedSessionUpdate.OwnerFrameBytes)
                    {
                        await connection.SendAsync(ownerFrame, cancellationToken).ConfigureAwait(false);
                        mutationCount++;
                    }
                }

                continue;
            }

            foreach (var connection in connections)
            {
                var previousSession = previousSessionsByConnection.TryGetValue(connection, out var storedPreviousSession)
                    ? storedPreviousSession
                    : null;
                _sessions.Set(connection, appliedSessionUpdate.Session);
                await LogLegacyDragonWarRewardGrantAsync(appliedSessionUpdate, previousSession).ConfigureAwait(false);
                await PersistLegacyDragonWarPlayerItemsAsync(appliedSessionUpdate).ConfigureAwait(false);
            }

            foreach (var connection in connections)
            {
                await connection.SendAsync(appliedSessionUpdate.StatusFrameBytes, cancellationToken).ConfigureAwait(false);
                mutationCount++;
                foreach (var ownerFrame in appliedSessionUpdate.OwnerFrameBytes)
                {
                    await connection.SendAsync(ownerFrame, cancellationToken).ConfigureAwait(false);
                    mutationCount++;
                }
            }

            foreach (var viewer in activeSessions)
            {
                if (viewer.Session.Character.Uid == appliedSessionUpdate.Session.Character.Uid ||
                    !CanSeePlayer(viewer.Session.Character, appliedSessionUpdate.Session.Character))
                {
                    continue;
                }

                await viewer.Connection.SendAsync(
                        appliedSessionUpdate.StatusFrameBytes,
                        cancellationToken)
                    .ConfigureAwait(false);
                mutationCount++;
            }
        }

        var sentCount = 0;
        foreach (var update in tick.Updates)
        {
            if (update.Broadcast)
            {
                foreach (var active in activeSessions)
                {
                    await active.Connection.SendAsync(update.FrameBytes, cancellationToken).ConfigureAwait(false);
                    sentCount++;
                }

                continue;
            }

            if (!connectionsByUid.TryGetValue(update.RecipientUid, out var connections))
            {
                continue;
            }

            foreach (var connection in connections)
            {
                await connection.SendAsync(update.FrameBytes, cancellationToken).ConfigureAwait(false);
                sentCount++;
            }
        }

        if (sentCount > 0 || mutationCount > 0)
        {
            await _log.WriteLineAsync(
                    "WorldServer sent legacy DragonWar runtime updates " +
                    $"updateCount={tick.Updates.Count} sentCount={sentCount} mutationCount={mutationCount}.")
                .ConfigureAwait(false);
        }
    }

    private async ValueTask SendDueLegacyDragonWarOpenAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!_dragonWarOpenScheduler.TryConsumeDueOpen(now))
        {
            return;
        }

        var update = _dragonWarStates.Open(now);
        if (update is null)
        {
            return;
        }

        var promptedCount = await SendRuntimeUpdateToActiveSessionsAsync(
                _sessions.Snapshot(),
                update,
                cancellationToken)
            .ConfigureAwait(false);
        await _log.WriteLineAsync(
                "WorldServer automatically opened legacy DragonWar runtime " +
                $"slot={now.LocalDateTime:yyyy-MM-ddTHH:mm:ss} promptedSessions={promptedCount}.")
            .ConfigureAwait(false);
    }

    private static WorldSession MergeLegacyDragonWarRuntimeTeleportSession(
        WorldSession transitionSession,
        WorldSession mutationSession)
    {
        var transitionCharacter = transitionSession.Character;
        var attributes = new Dictionary<string, string>(
            mutationSession.Character.Attributes,
            StringComparer.OrdinalIgnoreCase);
        CopyLegacyDragonWarRuntimeTeleportAttribute(
            transitionCharacter.Attributes,
            attributes,
            "DynamicID");
        CopyLegacyDragonWarRuntimeTeleportAttribute(
            transitionCharacter.Attributes,
            attributes,
            "PDinamycID");

        return mutationSession with
        {
            Character = mutationSession.Character with
            {
                MapId = transitionCharacter.MapId,
                X = transitionCharacter.X,
                Y = transitionCharacter.Y,
                PreviousMapId = transitionCharacter.PreviousMapId,
                PreviousMapX = transitionCharacter.PreviousMapX,
                PreviousMapY = transitionCharacter.PreviousMapY,
                Attributes = attributes,
            },
        };
    }

    private static void CopyLegacyDragonWarRuntimeTeleportAttribute(
        IReadOnlyDictionary<string, string> source,
        IDictionary<string, string> destination,
        string key)
    {
        destination.Remove(key);
        if (source.TryGetValue(key, out var value))
        {
            destination[key] = value;
        }
    }

    private async ValueTask PersistLegacyDragonWarPlayerItemsAsync(
        LegacyDragonWarRuntimeSessionUpdate sessionUpdate)
    {
        if (!sessionUpdate.PlayerItemsDirty)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_snapshotLoader?.PlayerItemsRoot))
        {
            await _log.WriteLineAsync(
                    "PARITY-GAP legacy DragonWar item reward persistence missing PlayerItemsRoot " +
                    $"uid={sessionUpdate.Session.Character.Uid} " +
                    $"sourceMethod={sessionUpdate.RewardSourceMethodName ?? "unknown"}.")
                .ConfigureAwait(false);
            return;
        }

        try
        {
            new PlayerItemSnapshotLoader(_snapshotLoader.PlayerItemsRoot)
                .SaveByCharacterUid(
                    sessionUpdate.Session.Character.Uid,
                    sessionUpdate.Session.PlayerItems);
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is ArgumentException ||
            ex is NotSupportedException)
        {
            await _log.WriteLineAsync(
                    "PARITY-GAP legacy DragonWar item reward persistence failed " +
                    $"uid={sessionUpdate.Session.Character.Uid} " +
                    $"sourceMethod={sessionUpdate.RewardSourceMethodName ?? "unknown"} " +
                    $"reason={ex.GetType().Name}.")
                .ConfigureAwait(false);
        }
    }

    private async ValueTask LogLegacyDragonWarRewardGrantAsync(
        LegacyDragonWarRuntimeSessionUpdate sessionUpdate,
        WorldSession? previousSession)
    {
        if (string.IsNullOrWhiteSpace(sessionUpdate.RewardSourceMethodName))
        {
            return;
        }

        var previousConquerPoints = previousSession?.Character.ConquerPoints ?? sessionUpdate.Session.Character.ConquerPoints;
        var conquerPointDelta = sessionUpdate.Session.Character.ConquerPoints - previousConquerPoints;
        await _log.WriteLineAsync(
                "WorldServer applied legacy DragonWar reward " +
                $"sourceMethod={sessionUpdate.RewardSourceMethodName} " +
                $"uid={sessionUpdate.Session.Character.Uid.ToString(CultureInfo.InvariantCulture)} " +
                $"conquerPointsDelta={conquerPointDelta.ToString(CultureInfo.InvariantCulture)} " +
                $"playerItemsDirty={(sessionUpdate.PlayerItemsDirty ? "true" : "false")}.")
            .ConfigureAwait(false);
    }

    private async ValueTask SendDueLegacyDragonSwingExpirationsAsync(
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
                !TryExpireLegacyDragonSwing(
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
                    "WorldServer expired legacy DragonWar DragonSwing flags " +
                    $"playerCount={playerCount} viewerCount={viewerCount}.")
                .ConfigureAwait(false);
        }
    }

    private static bool TryExpireLegacyDragonSwing(
        CharacterSnapshot character,
        DateTimeOffset now,
        out CharacterSnapshot updatedCharacter,
        out bool removedVisibleFlag)
    {
        return TryExpireLegacyTimedStatusFlag(
            character,
            now,
            LegacyDragonSwingStatusFlag,
            RuntimeLegacyDragonSwingExpiresAtUtcAttribute,
            out updatedCharacter,
            out removedVisibleFlag,
            WorldPacketHandlers.RuntimeLegacyDragonSwingChanceAttribute,
            "DragonSwingChance");
    }
}
