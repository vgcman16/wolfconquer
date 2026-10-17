using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const int LegacyFreezeWarStatusFlag = 0x3B;
    private const string RuntimeLegacyFreezeWarExpiresAtUtcAttribute = "LegacyFreezeWarExpiresAtUtc";

    private async ValueTask SendDueLegacyFreezeWarExpirationsAsync(
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
                !TryExpireLegacyFreezeWar(
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
                    "WorldServer expired legacy FreezeWar flags " +
                    $"playerCount={playerCount} viewerCount={viewerCount}.")
                .ConfigureAwait(false);
        }
    }

    private static bool TryExpireLegacyFreezeWar(
        CharacterSnapshot character,
        DateTimeOffset now,
        out CharacterSnapshot updatedCharacter,
        out bool removedVisibleFlag)
    {
        return TryExpireLegacyTimedStatusFlag(
            character,
            now,
            LegacyFreezeWarStatusFlag,
            RuntimeLegacyFreezeWarExpiresAtUtcAttribute,
            out updatedCharacter,
            out removedVisibleFlag);
    }

    private async ValueTask SendDueLegacyFreezeWarRuntimeUpdatesAsync(
        DateTimeOffset now,
        TimeZoneInfo localTimeZone,
        CancellationToken cancellationToken)
    {
        var update = _freezeWarStates.CreateDueOpenUpdate(now, localTimeZone);
        if (update is null)
        {
            await SendDueLegacyFreezeWarLifecycleUpdateAsync(now, cancellationToken).ConfigureAwait(false);
            return;
        }

        var activeSessions = _sessions.Snapshot();
        var sentCount = activeSessions.Count == 0
            ? 0
            : await SendRuntimeUpdateToActiveSessionsAsync(activeSessions, update, cancellationToken)
                .ConfigureAwait(false);

        await _log.WriteLineAsync(
                "WorldServer opened legacy FreezeWar runtime " +
                $"process={_freezeWarStates.Process} dynamicId={_freezeWarStates.DynamicId} " +
                $"activePlayers={activeSessions.Count} inviteCount={sentCount}; " +
                "source=MsgSchedules.CheckUp minute=55 -> CurrentTournament.Open; MsgFreezeWar.Open.")
            .ConfigureAwait(false);

        await SendDueLegacyFreezeWarLifecycleUpdateAsync(now, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask SendDueLegacyFreezeWarLifecycleUpdateAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var activeSessions = _sessions.Snapshot();
        var participantSnapshots = CreateLegacyFreezeWarParticipantSnapshots(activeSessions);
        var lifecycleUpdate = _freezeWarStates.CreateDueLifecycleUpdate(now, participantSnapshots);
        if (lifecycleUpdate is null)
        {
            return;
        }

        var broadcastCount = 0;
        foreach (var active in SelectLegacyFreezeWarUpdateRecipients(activeSessions, lifecycleUpdate))
        {
            foreach (var frame in lifecycleUpdate.FrameBytes)
            {
                await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                broadcastCount++;
            }
        }

        var unfrozenCount = 0;
        var roundFrozenCount = 0;
        var roundResetCount = 0;
        var finishedCount = 0;
        var rewardedCount = 0;
        if (lifecycleUpdate.Kind == LegacyFreezeWarRuntimeUpdateKind.Started &&
            lifecycleUpdate.ParticipantUids.Count > 0)
        {
            var participantUids = lifecycleUpdate.ParticipantUids.ToHashSet();
            foreach (var active in activeSessions)
            {
                if (!_sessions.TryGet(active.Connection, out var currentSession) ||
                    currentSession is null ||
                    !participantUids.Contains(currentSession.Character.Uid))
                {
                    continue;
                }

                var updatedCharacter = LegacyFreezeWarRuntimeStateStore.MarkAliveAndUnfreeze(currentSession.Character);
                var updatedSession = currentSession with { Character = updatedCharacter };
                _sessions.Set(active.Connection, updatedSession);

                var statusFrame = LegacyMsgUpdatePacket
                    .CreateLegacyRuntimeStatusFlagVectorUpdate(
                        CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
                        LegacyPlayerDeathState.CreateStatusFlagVector(
                            updatedCharacter.Attributes,
                            LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength))
                    .ToFrameBytes();
                await active.Connection.SendAsync(statusFrame, cancellationToken).ConfigureAwait(false);
                unfrozenCount++;
            }
        }
        else if (lifecycleUpdate.Kind == LegacyFreezeWarRuntimeUpdateKind.RoundWon &&
            lifecycleUpdate.Participants.Count > 0)
        {
            var participantUids = lifecycleUpdate.Participants
                .Select(participant => participant.Uid)
                .ToHashSet();
            foreach (var active in activeSessions)
            {
                if (!_sessions.TryGet(active.Connection, out var currentSession) ||
                    currentSession is null ||
                    !participantUids.Contains(currentSession.Character.Uid))
                {
                    continue;
                }

                var updatedCharacter = LegacyFreezeWarRuntimeStateStore.MarkRoundFrozen(currentSession.Character);
                var updatedSession = currentSession with { Character = updatedCharacter };
                _sessions.Set(active.Connection, updatedSession);

                await SendLegacyFreezeWarStatusUpdateAsync(active, updatedCharacter, cancellationToken)
                    .ConfigureAwait(false);
                roundFrozenCount++;
            }
        }
        else if (lifecycleUpdate.Kind == LegacyFreezeWarRuntimeUpdateKind.RoundReset &&
            lifecycleUpdate.Participants.Count > 0)
        {
            var participantUids = lifecycleUpdate.Participants
                .Select(participant => participant.Uid)
                .ToHashSet();
            var dynamicId = _freezeWarStates.DynamicId;
            foreach (var active in activeSessions)
            {
                if (!_sessions.TryGet(active.Connection, out var currentSession) ||
                    currentSession is null ||
                    !participantUids.Contains(currentSession.Character.Uid))
                {
                    continue;
                }

                var preflight = await PrepareLegacyRuntimeTeleportAsync(
                        new WorldActiveSession(active.Connection, currentSession),
                        new LegacyTeleportRequest(
                            "MsgFreezeWar.CheckUp round reset -> Team.TeleportPlayers -> GameClient.Teleport",
                            LegacyFreezeWarRuntimeStateStore.FreezeWarMapId,
                            dynamicId),
                        cancellationToken)
                    .ConfigureAwait(false);
                currentSession = preflight.Session;
                var teleportAllowed = preflight.Allowed;
                var updatedCharacter = LegacyFreezeWarRuntimeStateStore.MarkRoundReset(
                    currentSession.Character,
                    dynamicId);
                if (!teleportAllowed)
                {
                    var attributes = new Dictionary<string, string>(
                        currentSession.Character.Attributes,
                        StringComparer.OrdinalIgnoreCase);
                    attributes.Remove("LegacyFreezeWarExpiresAtUtc");
                    RemoveLegacyStatusFlag(
                        attributes,
                        LegacyFreezeWarRuntimeStateStore.FreezeStatusFlag);
                    updatedCharacter = currentSession.Character with { Attributes = attributes };
                }

                var updatedSession = currentSession with { Character = updatedCharacter };
                if (!teleportAllowed)
                {
                    _sessions.Set(active.Connection, updatedSession);
                    await SendLegacyFreezeWarStatusUpdateAsync(active, updatedCharacter, cancellationToken)
                        .ConfigureAwait(false);
                    roundResetCount++;
                    continue;
                }

                var teleportSourceSession = CreateLegacyFreezeWarRuntimeTeleportSourceSession(
                    currentSession,
                    updatedSession);
                _sessions.Set(active.Connection, teleportSourceSession);
                await SendLegacyFreezeWarStatusUpdateAsync(
                        active,
                        teleportSourceSession.Character,
                        cancellationToken)
                    .ConfigureAwait(false);

                var teleportedSession = await WorldPacketHandlers.ExecuteLegacyTeleportDestinationAsync(
                        CreateMonsterRuntimeContext(
                            new WorldActiveSession(active.Connection, teleportSourceSession)),
                        teleportSourceSession,
                        new LegacyTeleportDestination(
                            updatedCharacter.MapId,
                            updatedCharacter.X,
                            updatedCharacter.Y,
                            dynamicId),
                        timestamp: 0,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (teleportedSession is null)
                {
                    roundResetCount++;
                    continue;
                }

                roundResetCount++;
            }
        }
        else if (lifecycleUpdate.Kind == LegacyFreezeWarRuntimeUpdateKind.Ended &&
            lifecycleUpdate.Participants.Count > 0)
        {
            var participantsByUid = lifecycleUpdate.Participants.ToDictionary(
                participant => participant.Uid,
                participant => participant.Team);
            foreach (var active in activeSessions)
            {
                if (!_sessions.TryGet(active.Connection, out var currentSession) ||
                    currentSession is null ||
                    !participantsByUid.TryGetValue(currentSession.Character.Uid, out var team))
                {
                    continue;
                }

                var isWinner = team == lifecycleUpdate.WinnerTeam;
                var preflight = await PrepareLegacyRuntimeTeleportAsync(
                        new WorldActiveSession(active.Connection, currentSession),
                        new LegacyTeleportRequest(
                            "MsgFreezeWar.FinishTournament -> GameClient.Teleport",
                            LegacyFreezeWarRuntimeStateStore.FinishMapId),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!preflight.Allowed)
                {
                    continue;
                }

                currentSession = preflight.Session;
                var teleportedSession = await WorldPacketHandlers.ExecuteLegacyTeleportDestinationAsync(
                        CreateMonsterRuntimeContext(
                            new WorldActiveSession(active.Connection, currentSession)),
                        currentSession,
                        new LegacyTeleportDestination(
                            LegacyFreezeWarRuntimeStateStore.FinishMapId,
                            LegacyFreezeWarRuntimeStateStore.FinishX,
                            LegacyFreezeWarRuntimeStateStore.FinishY),
                        timestamp: 0,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (teleportedSession is null)
                {
                    continue;
                }

                var updatedSession = ApplyLegacyFreezeWarRuntimeFinishMutation(
                    teleportedSession,
                    isWinner);
                var updatedCharacter = updatedSession.Character;
                _sessions.Set(active.Connection, updatedSession);

                await SendLegacyFreezeWarStatusUpdateAsync(active, updatedCharacter, cancellationToken)
                    .ConfigureAwait(false);

                if (isWinner)
                {
                    var uid = CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid));
                    await active.Connection.SendAsync(
                            new LegacyMsgUpdatePacket(
                                uid,
                                [new LegacyMsgUpdateEntry(
                                    LegacyMsgUpdateDataType.ConquerPoints,
                                    checked((ulong)updatedCharacter.ConquerPoints))])
                                .ToFrameBytes(),
                            cancellationToken)
                        .ConfigureAwait(false);
                    await active.Connection.SendAsync(
                            LegacyProtoHeroInfoPacket.FromSnapshot(updatedCharacter).ToFrameBytes(),
                            cancellationToken)
                        .ConfigureAwait(false);
                    rewardedCount++;
                }

                var dialog = isWinner
                    ? "Your team won and received 1200 ConquerPoints."
                    : "Your team lose : ( .Better luck next time.";
                await active.Connection.SendAsync(
                        LegacyMsgMessagePacket.BoxDialog(dialog).ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
                finishedCount++;
            }
        }

        await _log.WriteLineAsync(
                "WorldServer ticked legacy FreezeWar lifecycle " +
                $"kind={lifecycleUpdate.Kind} process={_freezeWarStates.Process} " +
                $"broadcastCount={broadcastCount} participantCount={lifecycleUpdate.ParticipantUids.Count} " +
                $"unfrozenCount={unfrozenCount} roundFrozenCount={roundFrozenCount} " +
                $"roundResetCount={roundResetCount} finishedCount={finishedCount} " +
                $"rewardedCount={rewardedCount}; " +
                "source=MsgFreezeWar.CheckUp.")
            .ConfigureAwait(false);
    }

    private static IReadOnlyList<WorldActiveSession> SelectLegacyFreezeWarUpdateRecipients(
        IReadOnlyList<WorldActiveSession> activeSessions,
        LegacyFreezeWarRuntimeUpdate lifecycleUpdate)
    {
        if (lifecycleUpdate.Kind is not (LegacyFreezeWarRuntimeUpdateKind.ScoreHud
            or LegacyFreezeWarRuntimeUpdateKind.RoundWon
            or LegacyFreezeWarRuntimeUpdateKind.RoundReset))
        {
            return activeSessions;
        }

        if (lifecycleUpdate.ParticipantUids.Count == 0)
        {
            return [];
        }

        var participantUids = lifecycleUpdate.ParticipantUids.ToHashSet();
        return activeSessions
            .Where(active => participantUids.Contains(active.Session.Character.Uid))
            .ToArray();
    }

    private static IReadOnlyList<LegacyFreezeWarRuntimeParticipantSnapshot> CreateLegacyFreezeWarParticipantSnapshots(
        IReadOnlyList<WorldActiveSession> activeSessions)
    {
        if (activeSessions.Count == 0)
        {
            return [];
        }

        var snapshots = new List<LegacyFreezeWarRuntimeParticipantSnapshot>();
        foreach (var active in activeSessions)
        {
            var character = active.Session.Character;
            var team = LegacyFreezeWarRuntimeStateStore.ReadFreezeWarTeam(character);
            if (team == LegacyFreezeWarTeamType.None)
            {
                continue;
            }

            snapshots.Add(new LegacyFreezeWarRuntimeParticipantSnapshot(
                character.Uid,
                team,
                character.MapId,
                LegacyFreezeWarRuntimeStateStore.ReadDynamicId(character),
                LegacyFreezeWarRuntimeStateStore.HasFreezeStatusFlag(character)));
        }

        return snapshots;
    }

    private static WorldSession CreateLegacyFreezeWarRuntimeTeleportSourceSession(
        WorldSession sourceSession,
        WorldSession mutationSession)
    {
        var attributes = new Dictionary<string, string>(
            mutationSession.Character.Attributes,
            StringComparer.OrdinalIgnoreCase);
        attributes.Remove("DynamicID");
        if (sourceSession.Character.Attributes.TryGetValue("DynamicID", out var dynamicId))
        {
            attributes["DynamicID"] = dynamicId;
        }

        return mutationSession with
        {
            Character = mutationSession.Character with
            {
                MapId = sourceSession.Character.MapId,
                X = sourceSession.Character.X,
                Y = sourceSession.Character.Y,
                PreviousMapId = sourceSession.Character.PreviousMapId,
                PreviousMapX = sourceSession.Character.PreviousMapX,
                PreviousMapY = sourceSession.Character.PreviousMapY,
                Attributes = attributes,
            },
        };
    }

    private static WorldSession ApplyLegacyFreezeWarRuntimeFinishMutation(
        WorldSession transitionSession,
        bool isWinner)
    {
        var transitionCharacter = transitionSession.Character;
        var mutationCharacter = LegacyFreezeWarRuntimeStateStore.MarkFinishedAndReturnToTwinCity(
            transitionCharacter,
            isWinner);
        return transitionSession with
        {
            Character = mutationCharacter with
            {
                MapId = transitionCharacter.MapId,
                X = transitionCharacter.X,
                Y = transitionCharacter.Y,
                PreviousMapId = transitionCharacter.PreviousMapId,
                PreviousMapX = transitionCharacter.PreviousMapX,
                PreviousMapY = transitionCharacter.PreviousMapY,
            },
        };
    }

    private static async ValueTask SendLegacyFreezeWarStatusUpdateAsync(
        WorldActiveSession active,
        CharacterSnapshot character,
        CancellationToken cancellationToken)
    {
        await active.Connection.SendAsync(
                LegacyMsgUpdatePacket
                    .CreateLegacyRuntimeStatusFlagVectorUpdate(
                        CheckedUInt32(character.Uid, nameof(character.Uid)),
                        LegacyPlayerDeathState.CreateStatusFlagVector(
                            character.Attributes,
                            LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength))
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
