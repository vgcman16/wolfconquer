using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const int LegacyRuntimeSessionMergeAttemptLimit = 4;

    private void StartLegacyRuntimeUpdateLoop(
        ConquerTcpConnection connection,
        CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems,
        PlayerSpellSnapshot playerSpells,
        CancellationToken cancellationToken)
    {
        if (!_runtimeUpdateLoop.Enabled)
        {
            return;
        }

        _ = SendLegacyPlayerRuntimeUpdatesAsync(
            connection,
            snapshot,
            playerItems,
            playerSpells,
            cancellationToken);
        TryStartLegacyGlobalRuntimeUpdateLoop(cancellationToken);
    }

    private void TryStartLegacyGlobalRuntimeUpdateLoop(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _legacyRuntimeGlobalLoopStarted, 1, 0) != 0)
        {
            return;
        }

        _ = SendLegacyGlobalRuntimeUpdatesAsync(cancellationToken);
    }

    private async Task SendLegacyPlayerRuntimeUpdatesAsync(
        ConquerTcpConnection connection,
        CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems,
        PlayerSpellSnapshot playerSpells,
        CancellationToken cancellationToken)
    {
        try
        {
            var timeProvider = _runtimeUpdateLoop.TimeProvider ?? TimeProvider.System;
            var ticker = new LegacyRuntimeUpdateTicker(
                snapshot,
                timeProvider.GetUtcNow(),
                _clientCompatibility.EnableNewerClientOverwhelmStartupEffect &&
                    HasLegacyOverwhelmRune(playerItems, _worldData),
                _clientCompatibility.EnableNewerClientRevelatorStartupEffect &&
                    HasLegacyRevelatorStartupEffect(snapshot, playerSpells));

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_runtimeUpdateLoop.TickInterval, timeProvider, cancellationToken)
                    .ConfigureAwait(false);

                using var operation = await _worldOperations.EnterSharedAsync(cancellationToken).ConfigureAwait(false);
                if (_tradeRecovery.IsBlocked) continue;
                TryStartLegacyGlobalRuntimeUpdateLoop(cancellationToken);
                var now = timeProvider.GetUtcNow();
                _sessions.TryGet(connection, out var currentSession);
                await ProcessDueLegacyVoteClaimRuntimeTickAsync(
                        connection,
                        currentSession,
                        now,
                        timeProvider,
                        cancellationToken)
                    .ConfigureAwait(false);
                _sessions.TryGet(connection, out currentSession);
                foreach (var update in ticker.CreateDueUpdates(now, currentSession?.Character))
                {
                    await connection.SendAsync(update.FrameBytes, cancellationToken)
                        .ConfigureAwait(false);

                    if (update.RuntimeStamina is { } stamina)
                    {
                        ApplyLegacyRuntimeStaminaUpdate(connection, stamina);
                    }

                    if (update.RuntimeXpCount is not null || update.RuntimeXpListActive is not null)
                    {
                        ApplyLegacyRuntimeXpStateUpdate(
                            connection,
                            update.RuntimeXpCount,
                            update.RuntimeXpListActive);
                    }

                    if (update.MessageBoxPrompt is { } prompt)
                    {
                        await SendRuntimeMessageBoxPromptAsync(connection, prompt, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                await SendLegacyThunderStrikerUndyingImprintingRuntimeTickAsync(
                        connection,
                        playerItems,
                        playerSpells,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            await LogRuntimeLoopExceptionAsync(
                    "legacy-player-runtime-update-loop",
                    "WorldServerFrameHandler.RuntimeTicks.Player",
                    ex,
                    $"uid={snapshot.Uid}")
                .ConfigureAwait(false);
        }
    }

    private async Task SendLegacyGlobalRuntimeUpdatesAsync(CancellationToken cancellationToken)
    {
        var timeProvider = _runtimeUpdateLoop.TimeProvider ?? TimeProvider.System;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_runtimeUpdateLoop.TickInterval, timeProvider, cancellationToken)
                    .ConfigureAwait(false);

                using var operation = await _worldOperations.EnterSharedAsync(cancellationToken).ConfigureAwait(false);
                if (_tradeRecovery.IsBlocked) continue;
                var now = timeProvider.GetUtcNow();
                RemoveExpiredLegacyQualifierMatches(now);
                await TickLegacyGuildWarRuntimeAsync(now, timeProvider.LocalTimeZone).ConfigureAwait(false);
                await SendDueLegacyClanWarCurrentScorePanelFramesAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDuePaidNobilityExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDuePlayerGhostTransitionsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueGuardMonsterActionsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueReviverMonsterRevivesAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDuePlayerDeathPromptsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDuePlayerBlackSpotExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyEagleEyeCountdownExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyRideExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyTransformExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyScarOfEarthRuntimeAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyMonsterTimedStatusFlagExpirationsAsync(now, cancellationToken)
                    .ConfigureAwait(false);
                await SendDueMonsterBlackSpotInactivePacketsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyShurikenVortexAttacksAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyXpSkillExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyManiacDanceAttacksAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyShieldBlockExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyInvisibilityAttachStatusExpirationsAsync(now, cancellationToken)
                    .ConfigureAwait(false);
                await SendDueLegacyCombatGearAttachStatusExpirationsAsync(now, cancellationToken)
                    .ConfigureAwait(false);
                await SendDueLegacyArrowAndFlashingFlagExpirationsAsync(cancellationToken).ConfigureAwait(false);
                await SendDueLegacyHookMoonExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyStigmaMagicShieldExpirationsAsync(now, cancellationToken)
                    .ConfigureAwait(false);
                await SendDueLegacyCleanSweepExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyAxeShadowCelestialDanceExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyBloodTideExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyPlayerReceiveXpRuneExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyMirrorOfSinXpListExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyKillingFlashXpListExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyDivineGuardExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyKineticSparkExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyPathOfShadowExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyFreezeWarExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyFreezeWarRuntimeUpdatesAsync(now, timeProvider.LocalTimeZone, cancellationToken)
                    .ConfigureAwait(false);
                await SendDueLegacyFootballBallExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyDragonWarRuntimeUpdatesAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyIntensifyFocusUpdatesAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyDragonSwingExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyAutoHuntAttachStatusExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyCastPrayExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyInfiniteMistExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyInfinityFlyExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacySacrificeExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyRiseOfTaoismExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyIronGuardExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyIronShieldExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyFineRainExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyPhysicalStatusOverlayExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyAuraExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyMagicDefenderExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyCrackStarExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyBackfireExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyDefensiveStanceExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyLightningShieldExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyImmortalForceExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyPoisonStarExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyFrostArrowsExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacySoulShackleExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyRevengeTailExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyVenomExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyPoisonedExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueMonsterDeathRemovalsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueMonsterRespawnsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyBossFrightenedExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueLegacyDustSigilStunnExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueMonsterAttacksAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueFloorSpellEffectsAsync(now, cancellationToken).ConfigureAwait(false);
                await SendDueFloorItemExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            await LogRuntimeLoopExceptionAsync(
                    "legacy-global-runtime-update-loop",
                    "WorldServerFrameHandler.RuntimeTicks.Global",
                    ex)
                .ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _legacyRuntimeGlobalLoopStarted, 0);
        }
    }

    private async ValueTask LogRuntimeLoopExceptionAsync(
        string surface,
        string sourceSeam,
        Exception exception,
        string? detail = null)
    {
        var diagnostic = LegacyParityGapDiagnostics.RuntimeException(
            surface,
            exceptionType: exception.GetType().Name,
            sourceSeam: sourceSeam,
            nextSeam: "inspect-runtime-loop-exception",
            reason: "runtime loop exception was caught without terminating the live server; inspect the error log and decompiled source runtime tick path for parity triage");
        await _log.WriteLineAsync(diagnostic.FormatLogMessage()).ConfigureAwait(false);
        await _log.WriteLineAsync(
                "PARITY-BUG world runtime loop exception " +
                $"{(string.IsNullOrWhiteSpace(detail) ? string.Empty : detail + " ")}" +
                $"surface={surface} sourceSeam={sourceSeam} " +
                $"exception={exception.GetType().Name}: {exception.Message}")
            .ConfigureAwait(false);
        Console.Error.WriteLine(
            "PARITY-BUG world runtime loop exception " +
            $"{DateTimeOffset.UtcNow:O} " +
            $"{(string.IsNullOrWhiteSpace(detail) ? string.Empty : detail + " ")}" +
            $"surface={surface} sourceSeam={sourceSeam}{Environment.NewLine}{exception}");
    }

    private void ApplyLegacyRuntimeStaminaUpdate(
        ConquerTcpConnection connection,
        ushort stamina)
    {
        if (!_sessions.TryGet(connection, out var session) ||
            session is null)
        {
            return;
        }

        TryMergeLegacyRuntimeStaminaUpdate(_sessions, connection, session, stamina);
    }

    internal static bool TryMergeLegacyRuntimeStaminaUpdate(
        WorldSessionStore sessions,
        ConquerTcpConnection connection,
        WorldSession initialSession,
        ushort stamina)
    {
        return TryMergeLegacyRuntimeSessionUpdate(
            sessions,
            connection,
            initialSession,
            character =>
            {
                var attributes = new Dictionary<string, string>(
                    character.Attributes,
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["Stamina"] = stamina.ToString(CultureInfo.InvariantCulture),
                };
                return character with { Attributes = attributes };
            });
    }

    private void ApplyLegacyRuntimeXpStateUpdate(
        ConquerTcpConnection connection,
        ulong? xpCount,
        bool? xpListActive)
    {
        if (!_sessions.TryGet(connection, out var session) ||
            session is null)
        {
            return;
        }

        TryMergeLegacyRuntimeXpStateUpdate(
            _sessions,
            connection,
            session,
            xpCount,
            xpListActive);
    }

    internal static bool TryMergeLegacyRuntimeXpStateUpdate(
        WorldSessionStore sessions,
        ConquerTcpConnection connection,
        WorldSession initialSession,
        ulong? xpCount,
        bool? xpListActive)
    {
        return TryMergeLegacyRuntimeSessionUpdate(
            sessions,
            connection,
            initialSession,
            character =>
            {
                var attributes = new Dictionary<string, string>(
                    character.Attributes,
                    StringComparer.OrdinalIgnoreCase);
                if (xpCount is { } count)
                {
                    attributes["XPCount"] = count.ToString(CultureInfo.InvariantCulture);
                }

                if (xpListActive is { } active)
                {
                    var key = attributes.ContainsKey("BitVector") && !attributes.ContainsKey("StatusFlags")
                        ? "BitVector"
                        : "StatusFlags";
                    var flags = LegacyPlayerDeathState.CreateStatusFlagVector(
                        attributes,
                        LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength);
                    var wordIndex = LegacyRuntimeUpdateTicker.XpListFlag / 32;
                    var bitIndex = LegacyRuntimeUpdateTicker.XpListFlag % 32;
                    if (active)
                    {
                        LegacyStatusFlagDeathRegistration.PrepareAdd(attributes, LegacyRuntimeUpdateTicker.XpListFlag);
                        flags[wordIndex] |= 1UL << bitIndex;
                    }
                    else
                    {
                        attributes.Remove(LegacyStatusFlagDeathRegistration.Key(LegacyRuntimeUpdateTicker.XpListFlag));
                        flags[wordIndex] &= ~(1UL << bitIndex);
                    }

                    attributes[key] = string.Join('/', flags) + "/";
                }

                return character with { Attributes = attributes };
            });
    }

    private static bool TryMergeLegacyRuntimeSessionUpdate(
        WorldSessionStore sessions,
        ConquerTcpConnection connection,
        WorldSession initialSession,
        Func<CharacterSnapshot, CharacterSnapshot> mergeCharacter)
    {
        var expectedSession = initialSession;
        for (var attempt = 0; attempt < LegacyRuntimeSessionMergeAttemptLimit; attempt++)
        {
            var updatedSession = expectedSession with
            {
                Character = mergeCharacter(expectedSession.Character),
            };
            if (sessions.TryCompareAndSet(connection, expectedSession, updatedSession))
            {
                return true;
            }

            if (!sessions.TryGet(connection, out var latestSession) || latestSession is null)
            {
                return false;
            }

            expectedSession = latestSession;
        }

        return false;
    }

    private async ValueTask TickLegacyGuildWarRuntimeAsync(
        DateTimeOffset now,
        TimeZoneInfo localTimeZone)
    {
        var transition = _guildWarRuntime.Tick(now, localTimeZone);
        if (transition == LegacyGuildWarRuntimeTransition.None)
        {
            return;
        }

        if (transition == LegacyGuildWarRuntimeTransition.Started)
        {
            var resetGateCount = LegacyGuildWarGateRuntimeReset.ResetGateOwners(
                _worldData?.GuildWarInfo,
                _worldData?.SobNpcs,
                _sobNpcStates,
                _snapshotLoader?.UsersRoot,
                _worldData?.SyndicateFormationTypes);
            if (resetGateCount != 0)
            {
                await _log.WriteLineAsync(
                        "WorldServer reset legacy GuildWar gate owner state " +
                        $"gates={resetGateCount} ownerGuildId={_worldData?.GuildWarInfo.Info?.Id ?? 0}.")
                    .ConfigureAwait(false);
            }
        }

        await _log.WriteLineAsync(
                $"WorldServer legacy GuildWar runtime state {transition.ToString().ToLowerInvariant()} at {now:O}.")
            .ConfigureAwait(false);
    }

    private async ValueTask SendDuePaidNobilityExpirationsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!await _paidNobilityExpiryLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await SendDuePaidNobilityExpirationsLockedAsync(now, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _paidNobilityExpiryLock.Release();
        }
    }

    private async ValueTask SendDuePaidNobilityExpirationsLockedAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var activeSessions = _sessions.Snapshot();
        if (activeSessions.Count == 0)
        {
            return;
        }

        var usersRoot = _snapshotLoader?.UsersRoot;
        var beforeIcons = new Dictionary<ulong, LegacyNobilityIconPacket>();
        foreach (var active in activeSessions)
        {
            var uid = CheckedUInt64(active.Session.Character.Uid, nameof(active.Session.Character.Uid));
            beforeIcons[uid] = LegacyNobilityIconPacket.FromSnapshot(
                active.Session.Character,
                usersRoot,
                _sessions);
        }

        var expired = new Dictionary<ulong, (ConquerTcpConnection Connection, WorldSession Session, byte BaseRank)>();
        foreach (var active in activeSessions)
        {
            if (!_sessions.TryGet(active.Connection, out var currentSession) ||
                currentSession is null ||
                !TryReadExpiredLegacyPaidNobility(
                    currentSession.Character,
                    now,
                    out var restoredDonation))
            {
                continue;
            }

            var uid = CheckedUInt64(currentSession.Character.Uid, nameof(currentSession.Character.Uid));
            var baseRank = LegacyNobilityRanking.ComputeRank(
                restoredDonation,
                LegacyNobilityRanking.DefaultPosition);
            var restoredCharacter = WithExpiredLegacyPaidNobilityState(
                currentSession.Character,
                restoredDonation,
                baseRank,
                LegacyNobilityRanking.DefaultPosition);
            var restoredSession = currentSession with { Character = restoredCharacter };

            _sessions.Set(active.Connection, restoredSession);
            expired[uid] = (active.Connection, restoredSession, baseRank);
        }

        if (expired.Count == 0)
        {
            return;
        }

        var latestSessions = _sessions.Snapshot();
        var sentIcons = 0;
        var persisted = 0;
        var skippedPersistence = 0;
        var promotions = new List<LegacyNobilityState>();

        foreach (var active in latestSessions)
        {
            if (!_sessions.TryGet(active.Connection, out var currentSession) ||
                currentSession is null ||
                TryIsActiveLegacyPaidNobility(currentSession.Character.PaidNobility, out var paidActive) &&
                paidActive)
            {
                continue;
            }

            var state = LegacyNobilityRanking.ResolveForSnapshot(currentSession.Character, usersRoot, _sessions);
            var uid = state.Uid;
            var isExpiredPlayer = expired.TryGetValue(uid, out var expiredState);
            var updatedCharacter = isExpiredPlayer
                ? WithExpiredLegacyPaidNobilityState(currentSession.Character, state.Donation, state.Rank, state.Position)
                : WithLegacyNobilityStateAttributes(currentSession.Character, state);
            var updatedSession = currentSession with { Character = updatedCharacter };
            _sessions.Set(active.Connection, updatedSession);

            if (isExpiredPlayer)
            {
                if (TryPersistExpiredLegacyPaidNobility(
                        updatedSession.UsersRoot,
                        updatedSession.AccountId,
                        updatedCharacter,
                        out _,
                        out _))
                {
                    persisted++;
                }
                else
                {
                    skippedPersistence++;
                }

                if (state.Position != LegacyNobilityRanking.DefaultPosition &&
                    state.Rank > expiredState.BaseRank)
                {
                    promotions.Add(state);
                }
            }

            var afterIcon = LegacyNobilityIconPacket.FromSnapshot(updatedCharacter, usersRoot, _sessions);
            if (beforeIcons.TryGetValue(uid, out var beforeIcon) &&
                beforeIcon.Donation == afterIcon.Donation &&
                beforeIcon.Rank == afterIcon.Rank &&
                beforeIcon.Position == afterIcon.Position)
            {
                continue;
            }

            await active.Connection.SendAsync(afterIcon.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
            sentIcons++;
        }

        foreach (var promotion in promotions)
        {
            await BroadcastLegacyPaidNobilityPromotionAsync(promotion, cancellationToken)
                .ConfigureAwait(false);
        }

        await _log.WriteLineAsync(
                "WorldServer applied LegacyPaidNobility online expiry " +
                $"count={expired.Count} iconCount={sentIcons} persisted={persisted} " +
                $"skippedPersistence={skippedPersistence}.")
            .ConfigureAwait(false);
    }

    private ValueTask SendDuePlayerGhostTransitionsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return SendDuePlayerGhostTransitionPublicationsAsync(now, cancellationToken);
    }

    private async ValueTask SendDuePlayerBlackSpotExpirationsAsync(
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
                !IsLegacyPlayerBlackSpotExpired(currentSession.Character, now))
            {
                continue;
            }

            var expiredCharacter = WithoutLegacyPlayerBlackSpot(currentSession.Character);
            var updatedSession = currentSession with { Character = expiredCharacter };
            _sessions.Set(active.Connection, updatedSession);

            var uid = CheckedUInt32(expiredCharacter.Uid, nameof(expiredCharacter.Uid));
            var inactiveFrame = LegacyBlackspotPacket.CreateInactive(uid).ToFrameBytes();

            await active.Connection.SendAsync(inactiveFrame, cancellationToken).ConfigureAwait(false);
            viewerCount++;

            foreach (var viewer in activeSessions)
            {
                if (ReferenceEquals(viewer.Connection, active.Connection) ||
                    viewer.Session.Character.Uid == expiredCharacter.Uid ||
                    !CanSeePlayer(viewer.Session.Character, expiredCharacter))
                {
                    continue;
                }

                await viewer.Connection.SendAsync(inactiveFrame, cancellationToken).ConfigureAwait(false);
                viewerCount++;
            }

            playerCount++;
        }

        if (playerCount > 0)
        {
            await _log.WriteLineAsync(
                    $"WorldServer expired legacy player BlackSpot playerCount={playerCount} viewerCount={viewerCount}.")
                .ConfigureAwait(false);
        }
    }

    private async ValueTask SendDuePlayerDeathPromptsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var activeSessions = _sessions.Snapshot();
        if (activeSessions.Count == 0)
        {
            return;
        }

        var prompted = 0;
        var cleared = 0;
        foreach (var active in activeSessions)
        {
            if (!_sessions.TryGet(active.Connection, out var currentSession) ||
                currentSession is null)
            {
                continue;
            }

            var promptVisible = IsLegacyDeathRevivePromptVisible(currentSession.Character);
            var readiness = LegacyPlayerDeathState.EvaluateDeathPromptReadiness(
                currentSession.Character,
                now,
                promptVisible);
            if (!readiness.ShouldEmitPacket)
            {
                continue;
            }

            if (readiness.ShouldEmitRevivePrompt)
            {
                var promptedCharacter = WithLegacyDeathRevivePrompt(currentSession.Character);
                _sessions.Set(active.Connection, currentSession with { Character = promptedCharacter });
                await active.Connection.SendAsync(
                        LegacyNpcReplyPacket.ForMessageBox(LegacyPlayerDeathState.RevivePromptText).ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
                prompted++;
                continue;
            }

            if (readiness.ShouldClearRevivePrompt)
            {
                var clearedCharacter = WithoutLegacyDeathRevivePrompt(currentSession.Character);
                _sessions.Set(active.Connection, currentSession with { Character = clearedCharacter });
                cleared++;
            }
        }

        if (prompted > 0 || cleared > 0)
        {
            await _log.WriteLineAsync(
                    "WorldServer processed player death revive prompts " +
                    $"prompted={prompted} cleared={cleared}.")
                .ConfigureAwait(false);
        }
    }
}
