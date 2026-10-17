using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> ExecuteLegacyPhysicalSpellsSobNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        SobNpc sobNpc,
        CancellationToken cancellationToken)
    {
        if (context.SobNpcStates is not { } sobNpcStates)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        SobNpcRuntimeStateResource,
                        "physical-spells-sob-magic",
                        "PhysicalSpells SOB magic target was found, but SOB runtime state is not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", sobNpc.MapId),
                            LegacyParityGapDiagnostics.Fact("opcode", WorldPacketOpcodes.MsgAttack),
                            LegacyParityGapDiagnostics.Fact("sobNpcKind", sobNpc.Kind),
                            LegacyParityGapDiagnostics.Fact("sort", (int)spell.Sort),
                            LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                            LegacyParityGapDiagnostics.Fact("targetKind", "sob-npc"),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("sort", (int)spell.Sort),
                            LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                            LegacyParityGapDiagnostics.Fact("targetKind", "sob-npc"),
                        ]))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy PhysicalSpells SOB magic " +
                    $"{FormatSession(context)} spellId={spell.Id}; SOB runtime state is not loaded.")
                .ConfigureAwait(false);
            return session;
        }

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var results = new List<SobNpcDamageResult>();
        var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(sobNpc, context.WorldData?.StaticStatues);
        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index < LegacyPhysicalSpellsHitCount; index++)
        {
            var damage = CalculateLegacySobNpcPhysicalSpellDamage(
                session,
                spell,
                context.WorldData,
                damageMultiplier: 5);
            var result = sobNpcStates.ApplyDamage(sobNpc, damage, now, deathBehavior);
            results.Add(result);
            if (result.AlreadyDead || result.DamageApplied == 0)
            {
                break;
            }

            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(sobNpc.Id, nameof(sobNpc.Id)),
                result.DamageApplied,
                Hit: true,
                MsgAttackEffect.None));

            if (result.Killed)
            {
                break;
            }
        }

        await SendLegacyPhysicalSpellsAnimationAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                animationTargets,
                spellEffect: 0,
                cancellationToken)
            .ConfigureAwait(false);

        var killedResult = results.FirstOrDefault(result => result.Killed);
        if (killedResult.Killed && LegacySobNpcEventRules.IsTrainingSob(sobNpc))
        {
            await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                    context,
                    session,
                    sobNpc,
                    sobNpcStates.GetVisible(sobNpc),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (!LegacySobNpcEventRules.IsTrainingSob(sobNpc))
        {
            var lastResult = results.LastOrDefault(result => !result.AlreadyDead);
            if (!lastResult.Equals(default(SobNpcDamageResult)))
            {
                await LogLegacySobNpcEventSideEffectGapAsync(context, sobNpc, lastResult, spell)
                    .ConfigureAwait(false);
            }
        }

        var totalExperienceDamage = results.Aggregate(
            0UL,
            (total, result) => total + CalculateLegacySobNpcExperienceDamage(sobNpc, result));
        var proficiencyExperienceDamage = results.Aggregate(
            0UL,
            (total, result) => total + result.DamageApplied);
        var updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                session,
                playerSpell,
                spell,
                totalExperienceDamage,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                updatedSession,
                ClampLegacyProficiencyRawExperience(proficiencyExperienceDamage),
                cancellationToken,
                activity: "SOB magic weapon proficiency experience")
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy PhysicalSpells SOB magic " +
                $"{FormatSession(context)} spellId={spell.Id} targetUid={sobNpc.Id} " +
                $"targets={animationTargets.Count} totalDamage={totalExperienceDamage}.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<bool> HandleLegacyPlayerSobNpcMagicAttackAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (!TryFindVisibleSobNpcByUid(context, session.Character, query.OpponentUid, out var primaryTarget))
        {
            return false;
        }

        if (context.SobNpcStates is not { } sobNpcStates)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        SobNpcRuntimeStateResource,
                        "player-sob-magic",
                        "visible SOB magic target was found, but SOB runtime state is not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", primaryTarget.MapId),
                            LegacyParityGapDiagnostics.Fact("opcode", WorldPacketOpcodes.MsgAttack),
                            LegacyParityGapDiagnostics.Fact("sobNpcKind", primaryTarget.Kind),
                            LegacyParityGapDiagnostics.Fact("sort", (int)spell.Sort),
                            LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                            LegacyParityGapDiagnostics.Fact("targetKind", "sob-npc"),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("sort", (int)spell.Sort),
                            LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                            LegacyParityGapDiagnostics.Fact("targetKind", "sob-npc"),
                        ]))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy player-SOB magic " +
                    $"{FormatSession(context)} spellId={spell.Id}; SOB runtime state is not loaded.")
                .ConfigureAwait(false);
            return true;
        }

        if (!LegacyMagicRuntime.IsSupportedVisibleMonsterAttack(spell))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.UnsupportedMagic(
                        spell.Id,
                        sort: (int)spell.Sort,
                        reason: LegacyMagicSortCapabilities.FormatDiagnostic(spell.Sort, spell.Id)))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy player-SOB magic " +
                    $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort}; unsupported SOB magic sort.")
                .ConfigureAwait(false);
            return true;
        }

        if (!PassesLegacyPlayerSobNpcMagicPreResourceRangeGate(
                session,
                primaryTarget,
                query,
                spell,
                out var failureDiagnostic))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy player-SOB magic " +
                    $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort}; {failureDiagnostic}.")
                .ConfigureAwait(false);
            return true;
        }

        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context,
                session,
                spell,
                cancellationToken).ConfigureAwait(false);
        if (resourceSession is null)
        {
            return true;
        }

        if (!TrySelectLegacySobNpcMagicTargets(
                context,
                resourceSession,
                primaryTarget,
                query,
                spell,
                out var targets,
                out failureDiagnostic))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy player-SOB magic " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} sort={(int)spell.Sort}; {failureDiagnostic}.")
                .ConfigureAwait(false);
            return true;
        }

        var statusFields = CalculateLegacyRuntimeStatusFields(resourceSession, context.WorldData);
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var killedTrainingTargets = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var eventSideEffectTargets = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            LegacySobNpcDeathBehavior DeathBehavior)>();
        var totalExperienceDamage = 0UL;
        var proficiencyExperienceDamage = 0UL;
        var now = DateTimeOffset.UtcNow;
        foreach (var target in targets)
        {
            var damage = LegacySobNpcMagicDamageCalculator.CalculateDamage(
                statusFields[LegacyPlayerStatusPacket.MagicAttackIndex],
                statusFields[LegacyPlayerStatusPacket.MagicPercentIndex],
                statusFields[LegacyPlayerStatusPacket.MagicDamageIncreaseIndex],
                spell);
            var baseDamage = damage;
            damage = ApplyLegacyCombatGearWildPhoenixAttackEffect(
                resourceSession,
                spell,
                damage,
                LegacyWildPhoenixDamageTargetKind.MonsterOrSobNpc);
            if (damage != baseDamage)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer applied legacy WildPhoenix attacker damage bonus " +
                        $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} " +
                        $"targetKind=SobNpc baseDamage={baseDamage} boostedDamage={damage}; " +
                        "source=MyCombatGear.AttackEffect.")
                    .ConfigureAwait(false);
            }

            var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc, context.WorldData?.StaticStatues);
            var receiveGearOutcome = await TrySendLegacySobNpcReceiveCombatGearUpdateAsync(
                    context with { Session = resourceSession },
                    resourceSession,
                    target.SobNpc,
                    cancellationToken)
                .ConfigureAwait(false);
            if (receiveGearOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return true;
            }

            resourceSession = receiveGearOutcome.Session;
            context = context with { Session = resourceSession };
            (resourceSession, _) = await TrySendLegacySobNpcReceiveNinjaSageModeAsync(
                    context with { Session = resourceSession },
                    resourceSession,
                    context.WorldData,
                    cancellationToken)
                .ConfigureAwait(false);
            resourceSession = WithLegacySobNpcReceiveHitAttributes(context, resourceSession, target.SobNpc);
            damage = ApplyLegacySobNpcReceiveDamageModifiers(resourceSession, damage);
            var result = sobNpcStates.ApplyDamage(
                target.SobNpc,
                damage,
                now,
                deathBehavior);
            if (result.DamageApplied == 0)
            {
                continue;
            }

            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id)),
                result.DamageApplied,
                Hit: true,
                MsgAttackEffect.None));
            totalExperienceDamage += CalculateLegacySobNpcExperienceDamage(target.SobNpc, result);
            proficiencyExperienceDamage += result.DamageApplied;

            if (result.Killed && LegacySobNpcEventRules.IsTrainingSob(target.SobNpc))
            {
                killedTrainingTargets.Add((target.SobNpc, result));
            }
            else if (!LegacySobNpcEventRules.IsTrainingSob(target.SobNpc))
            {
                eventSideEffectTargets.Add((target.SobNpc, result, deathBehavior));
            }
        }

        if (animationTargets.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy player-SOB magic " +
                    $"{FormatSession(context)} spellId={spell.Id}; selected SOB NPCs had no applicable damage.")
                .ConfigureAwait(false);
            return true;
        }

        var animationX = query.X != 0 || query.Y != 0
            ? query.X
            : CheckedUInt16(primaryTarget.X, nameof(primaryTarget.X));
        var animationY = query.X != 0 || query.Y != 0
            ? query.Y
            : CheckedUInt16(primaryTarget.Y, nameof(primaryTarget.Y));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid)),
            animationX,
            animationY,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, resourceSession, animation, cancellationToken)
            .ConfigureAwait(false);

        foreach (var killed in killedTrainingTargets)
        {
            await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                    context,
                    resourceSession,
                    killed.SobNpc,
                    sobNpcStates.GetVisible(killed.SobNpc),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var eventTarget in eventSideEffectTargets)
        {
            resourceSession = await ApplyLegacySobNpcEventSideEffectsAsync(
                    context,
                    resourceSession,
                    eventTarget.SobNpc,
                    eventTarget.Result,
                    eventTarget.DeathBehavior,
                    spell,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                resourceSession,
                playerSpell,
                spell,
                totalExperienceDamage,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                updatedSession,
                ClampLegacyProficiencyRawExperience(proficiencyExperienceDamage),
                cancellationToken,
                activity: "player-SOB magic weapon proficiency experience")
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy player-SOB magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} targets={animationTargets.Count} totalDamage={totalExperienceDamage}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<WorldSession> ApplyLegacySobNpcEventSideEffectsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult result,
        LegacySobNpcDeathBehavior deathBehavior,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        var updatedSession = await ApplyLegacySobNpcBoundedEventSideEffectsAsync(
                context,
                session,
                sobNpc,
                result,
                deathBehavior,
                cancellationToken)
            .ConfigureAwait(false);

        await LogLegacySobNpcEventSideEffectGapAsync(
                context with { Session = updatedSession },
                sobNpc,
                result,
                spell)
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> ApplyLegacySobNpcBoundedEventSideEffectsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult result,
        LegacySobNpcDeathBehavior deathBehavior,
        CancellationToken cancellationToken)
    {
        if (context.SobNpcStates is not { } sobNpcStates)
        {
            return session;
        }

        if (TryApplyCaptureTheFlagBaseScore(
                session,
                sobNpc,
                result.DamageApplied,
                out var scoredSession,
                out var scoreGuildId,
                out var scoreGuildName,
                out var score))
        {
            session = scoredSession;
            context.Sessions?.Set(context.Connection, session);
            await context.Log.WriteLineAsync(
                    "WorldServer updated legacy CaptureTheFlag base score " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"guildId={scoreGuildId} guildName={scoreGuildName} damage={result.DamageApplied} score={score}; " +
                    "source MsgCaptureTheFlag.Basse.UpdateScore modeled.")
                .ConfigureAwait(false);
        }

        if (!result.Killed &&
            TryApplyLegacySuperGuildWarPoleScore(
                session,
                sobNpc,
                result.DamageApplied,
                out var superGuildWarScoredSession,
                out var superGuildWarScore))
        {
            session = superGuildWarScoredSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacySuperGuildWarScorePanelFramesAsync(context, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer updated legacy SuperGuildWar pole score " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"guildId={superGuildWarScore.GuildId} guildName={superGuildWarScore.GuildName} " +
                    $"damage={result.DamageApplied} score={superGuildWarScore.Score}; " +
                    "source MsgSuperGuildWar.UpdateScore modeled.")
                .ConfigureAwait(false);
        }

        if (!result.Killed &&
            TryApplyLegacyWarOfPlayersPoleScore(
                session,
                sobNpc,
                result.DamageApplied,
                out var warOfPlayersScoredSession,
                out var warOfPlayersScore))
        {
            session = warOfPlayersScoredSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyWarOfPlayersScorePanelFramesAsync(context, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer updated legacy WarOfPlayers pole score " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"playerUid={warOfPlayersScore.PlayerUid} playerName={warOfPlayersScore.PlayerName} " +
                    $"damage={result.DamageApplied} score={warOfPlayersScore.Score}; " +
                    "source MsgWarOfPlayers.UpdateScore modeled.")
                .ConfigureAwait(false);
        }

        if (!result.Killed &&
            TryApplyLegacyGuild6PoleWarPoleScore(
                session,
                sobNpc,
                result.DamageApplied,
                out var guild6PoleWarScoredSession,
                out var guild6PoleWarScore))
        {
            session = guild6PoleWarScoredSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyGuild6PoleWarScorePanelFramesAsync(
                    context,
                    guild6PoleWarScore.ScheduleName,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer updated legacy Guild6PoleWar pole score " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"schedule={guild6PoleWarScore.ScheduleName} guildId={guild6PoleWarScore.GuildId} " +
                    $"guildName={guild6PoleWarScore.GuildName} damage={result.DamageApplied} " +
                    $"score={guild6PoleWarScore.Score}; source MsgGuild6PoleWar.UpdateScore modeled.")
                .ConfigureAwait(false);
        }

        if (!result.Killed &&
            TryApplyLegacyGuildWarScorePoleScore(
                session,
                sobNpc,
                result.DamageApplied,
                out var guildWarScoreScoredSession,
                out var guildWarScore))
        {
            session = guildWarScoreScoredSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyGuildWarScoreScorePanelFramesAsync(context, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer updated legacy GuildWarScore/TopWarScore pole score " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"guildId={guildWarScore.GuildId} guildName={guildWarScore.GuildName} " +
                    $"damage={result.DamageApplied} score={guildWarScore.Score}; " +
                    "source MsgTopWarScore.UpdateScore modeled.")
                .ConfigureAwait(false);
        }

        if (!result.Killed &&
            TryApplyLegacyEmperorWarPoleScore(
                session,
                sobNpc,
                result.DamageApplied,
                out var emperorWarScoredSession,
                out var emperorWarScore))
        {
            session = emperorWarScoredSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyEmperorWarScorePanelFramesAsync(context, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer updated legacy EmperorWar pole score " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"playerUid={emperorWarScore.PlayerUid} playerName={emperorWarScore.PlayerName} " +
                    $"damage={result.DamageApplied} score={emperorWarScore.Score}; " +
                    "source MsgEmperorWar.UpdateScore modeled.")
                .ConfigureAwait(false);
        }

        if (!result.Killed &&
            TryApplyLegacyTopPolePoleScore(
                session,
                sobNpc,
                result.DamageApplied,
                out var topPoleScoredSession,
                out var topPoleScore))
        {
            session = topPoleScoredSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyTopPoleScorePanelFramesAsync(context, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer updated legacy TopPole pole score " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"playerUid={topPoleScore.PlayerUid} playerName={topPoleScore.PlayerName} " +
                    $"damage={result.DamageApplied} score={topPoleScore.Score}; " +
                    "source MsgTopPole.UpdateScore modeled.")
                .ConfigureAwait(false);
        }

        if (!result.Killed &&
            TryApplyLegacyWarFightersPoleScore(
                session,
                sobNpc,
                result.DamageApplied,
                out var warFightersScoredSession,
                out var warFightersScore))
        {
            session = warFightersScoredSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyWarFightersScorePanelFramesAsync(context, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer updated legacy WarFighters pole score " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"playerUid={warFightersScore.PlayerUid} playerName={warFightersScore.PlayerName} " +
                    $"damage={result.DamageApplied} score={warFightersScore.Score}; " +
                    "source MsgWarFighters.UpdateScore modeled.")
                .ConfigureAwait(false);
        }

        if (!result.Killed &&
            TryApplyLegacyTrepilWarPoleScore(
                session,
                sobNpc,
                result.DamageApplied,
                out var trepilWarScoredSession,
                out var trepilWarScore))
        {
            session = trepilWarScoredSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyTrepilWarScorePanelFramesAsync(context, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer updated legacy TrepilWar pole score " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"guildId={trepilWarScore.GuildId} guildName={trepilWarScore.GuildName} " +
                    $"damage={result.DamageApplied} score={trepilWarScore.Score}; " +
                    "source MsgTrepilWar.UpdateScore modeled.")
                .ConfigureAwait(false);
        }

        if (!result.Killed &&
            TryApplyLegacyNobilityPoleScore(
                session,
                sobNpc,
                result.DamageApplied,
                out var nobilityPoleScoredSession,
                out var nobilityPoleScore))
        {
            session = nobilityPoleScoredSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyNobilityPoleScorePanelFramesAsync(
                    context,
                    nobilityPoleScore.ScheduleName,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer updated legacy NobilityPole score " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"schedule={nobilityPoleScore.ScheduleName} displayRank={nobilityPoleScore.DisplayRank} " +
                    $"playerUid={nobilityPoleScore.PlayerUid} playerName={nobilityPoleScore.PlayerName} " +
                    $"damage={result.DamageApplied} score={nobilityPoleScore.Score}; " +
                    "source MsgNobilityPole.UpdateScore modeled.")
                .ConfigureAwait(false);
        }

        if (!result.Killed &&
            TryApplyLegacyWarriorGodPoleScore(
                session,
                sobNpc,
                result.DamageApplied,
                out var warriorGodScoredSession,
                out var warriorGodScore))
        {
            session = warriorGodScoredSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyWarriorGodScorePanelFramesAsync(context, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer updated legacy WarriorGod pole score " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"playerUid={warriorGodScore.PlayerUid} playerName={warriorGodScore.PlayerName} " +
                    $"damage={result.DamageApplied} score={warriorGodScore.Score}; " +
                    "source MsgWarriorGod.UpdateScore modeled.")
                .ConfigureAwait(false);
        }

        if (!result.Killed &&
            TryApplyLegacyClassPolaWarPoleScore(
                session,
                sobNpc,
                result.DamageApplied,
                out var classPolaWarScoredSession,
                out var classPolaWarScore))
        {
            session = classPolaWarScoredSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyClassPolaWarScorePanelFramesAsync(context, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer updated legacy ClassPolaWar pole score " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"playerUid={classPolaWarScore.PlayerUid} playerName={classPolaWarScore.PlayerName} " +
                    $"damage={result.DamageApplied} score={classPolaWarScore.Score}; " +
                    "source MsgClassPolaWar.UpdateScore modeled.")
                .ConfigureAwait(false);
        }

        if (!result.Killed &&
            TryApplyLegacyArcherPoleScore(
                session,
                sobNpc,
                result.DamageApplied,
                out var archerPoleScoredSession,
                out var archerPoleScore))
        {
            session = archerPoleScoredSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyArcherPoleScorePanelFramesAsync(context, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer updated legacy ArcherMaster pole score " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"playerUid={archerPoleScore.PlayerUid} playerName={archerPoleScore.PlayerName} " +
                    $"damage={result.DamageApplied} score={archerPoleScore.Score}; " +
                    "source MsgArcherMaster.UpdateScore modeled.")
                .ConfigureAwait(false);
        }

        if (!result.Killed &&
            TryApplyLegacyTrojanPoleScore(
                session,
                sobNpc,
                result.DamageApplied,
                out var trojanPoleScoredSession,
                out var trojanPoleScore))
        {
            session = trojanPoleScoredSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyTrojanPoleScorePanelFramesAsync(context, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer updated legacy TrojanMaster pole score " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"playerUid={trojanPoleScore.PlayerUid} playerName={trojanPoleScore.PlayerName} " +
                    $"damage={result.DamageApplied} score={trojanPoleScore.Score}; " +
                    "source MsgTrojanMaster.UpdateScore modeled.")
                .ConfigureAwait(false);
        }

        if (!result.Killed &&
            TryApplyLegacyNinjaMasterPoleScore(
                session,
                sobNpc,
                result.DamageApplied,
                out var ninjaMasterScoredSession,
                out var ninjaMasterScore))
        {
            session = ninjaMasterScoredSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyNinjaMasterScorePanelFramesAsync(context, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer updated legacy NinjaMaster pole score " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"playerUid={ninjaMasterScore.PlayerUid} playerName={ninjaMasterScore.PlayerName} " +
                    $"damage={result.DamageApplied} score={ninjaMasterScore.Score}; " +
                    "source MsgNinjaMaster.UpdateScore modeled.")
                .ConfigureAwait(false);
        }

        if (!result.Killed &&
            (await TryApplyLegacyRemainingClassPoleWarPoleScoreAsync(
                    context,
                    session,
                    sobNpc,
                    result.DamageApplied,
                    cancellationToken)
                .ConfigureAwait(false)) is { } remainingClassPoleScoredSession)
        {
            session = remainingClassPoleScoredSession;
        }

        if (!result.Killed &&
            TryApplyLegacyClanWarScorePoleScore(
                session,
                sobNpc,
                result.DamageApplied,
                out var clanWarScoreScoredSession,
                out var clanWarScore))
        {
            session = clanWarScoreScoredSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyClanWarScoreScorePanelFramesAsync(context, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer updated legacy ClanWarScore/MsgClanW pole score " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"clanId={clanWarScore.ClanId} clanName={clanWarScore.ClanName} " +
                    $"damage={result.DamageApplied} score={clanWarScore.Score}; " +
                    "source MsgClanScore.UpdateScore modeled.")
                .ConfigureAwait(false);
        }

        if (!result.Killed &&
            TryApplyLegacyClanWarCurrentPoleScore(
                session,
                sobNpc,
                result.DamageApplied,
                out var clanWarScoredSession,
                out var clanWarCurrentScore))
        {
            session = clanWarScoredSession;
            context.Sessions?.Set(context.Connection, session);
            await context.Log.WriteLineAsync(
                    "WorldServer updated legacy ClanWar current-war pole score " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"city={clanWarCurrentScore.City} clanId={clanWarCurrentScore.ClanId} " +
                    $"clanName={clanWarCurrentScore.ClanName} damage={result.DamageApplied} " +
                    $"score={clanWarCurrentScore.Score}; source MsgClanWar.CityWar.UpdateScore modeled.")
                .ConfigureAwait(false);
        }

        if (!result.Killed &&
            TryApplyLegacyEliteGuildWarPoleScore(
                session,
                sobNpc,
                result.DamageApplied,
                out var eliteGuildWarScoredSession,
                out var eliteGuildWarScore))
        {
            session = eliteGuildWarScoredSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyEliteGuildWarScorePanelFramesAsync(context, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer updated legacy EliteGuildWar pole score " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"guildId={eliteGuildWarScore.GuildId} guildName={eliteGuildWarScore.GuildName} " +
                    $"damage={result.DamageApplied} score={eliteGuildWarScore.Score}; " +
                    "source MsgEliteGuildWar.UpdateScore modeled.")
                .ConfigureAwait(false);
        }

        if (!result.Killed &&
            TryApplyLegacyUnionWarPoleScore(
                session,
                sobNpc,
                result.DamageApplied,
                out var unionWarScoredSession,
                out var unionWarScore))
        {
            session = unionWarScoredSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyUnionWarScorePanelFramesAsync(context, session, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer updated legacy UnionWar pole score " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"unionId={unionWarScore.UnionId} unionName={unionWarScore.UnionName} " +
                    $"damage={result.DamageApplied} score={unionWarScore.Score}; " +
                    "source MsgUnionWar.UpdateScore modeled.")
                .ConfigureAwait(false);
        }

        if (!result.Killed &&
            TryApplyLegacyChampionsOfWarrPoleScore(
                session,
                sobNpc,
                result.DamageApplied,
                out var championsOfWarrScoredSession,
                out var championsOfWarrScore))
        {
            session = championsOfWarrScoredSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyChampionsOfWarrScorePanelFramesAsync(context, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer updated legacy ChampionsOfWarr pole score " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"guildId={championsOfWarrScore.GuildId} guildName={championsOfWarrScore.GuildName} " +
                    $"damage={result.DamageApplied} score={championsOfWarrScore.Score}; " +
                    "source MsgChampionsOfWarr.UpdateScore modeled.")
                .ConfigureAwait(false);
        }

        if (result.Killed &&
            deathBehavior == LegacySobNpcDeathBehavior.GateDemolition)
        {
            await SendLegacyGuildWarGateDemolitionSobNpcUpdateAsync(
                    context,
                    session,
                    sobNpc,
                    sobNpcStates.GetVisible(sobNpc),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (result.Killed &&
            TryApplyLegacyClanWarCurrentPoleFinishRound(
                session,
                sobNpc,
                result,
                out var clanWarFinishedSession,
                out var clanWarFinishRound))
        {
            session = clanWarFinishedSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyClanWarCurrentFinishRoundScorePanelFramesAsync(
                    context,
                    clanWarFinishRound,
                    cancellationToken)
                .ConfigureAwait(false);
            sobNpcStates.ResetHitpointsToMax(sobNpc, clanWarFinishRound.ResetPoleName);
            var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
            await SendFrameToVisiblePlayersAsync(
                    context,
                    session,
                    LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer finished legacy ClanWar current-war pole round " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"city={clanWarFinishRound.City} winnerClanId={clanWarFinishRound.Winner.ClanId} " +
                    $"winnerClanName={clanWarFinishRound.Winner.ClanName} " +
                    $"score={clanWarFinishRound.Winner.Score} resetHitpoints={visibleSobNpc.Life}; " +
                    "source MsgClanWar.CityWar.FinishRound modeled.")
                .ConfigureAwait(false);
        }
        else if (result.Killed &&
            TryApplyLegacyGuildWarScorePoleFinishRound(
                session,
                sobNpc,
                result,
                out var guildWarScoreFinishedSession,
                out var guildWarScoreFinishRound))
        {
            session = guildWarScoreFinishedSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyGuildWarScoreScorePanelFramesAsync(
                    context,
                    guildWarScoreFinishRound.ScorePanelFrames,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyGuildWarScoreFinishRoundCenterMessageAsync(
                    context,
                    guildWarScoreFinishRound,
                    cancellationToken)
                .ConfigureAwait(false);
            sobNpcStates.ResetHitpointsToMax(sobNpc, guildWarScoreFinishRound.ResetPoleName);
            var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
            await SendFrameToVisiblePlayersAsync(
                    context,
                    session,
                    LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer finished legacy GuildWarScore/TopWarScore pole round " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"roundOwnerGuildId={guildWarScoreFinishRound.Winner.GuildId} " +
                    $"roundOwnerGuildName={guildWarScoreFinishRound.Winner.GuildName} " +
                    $"score={guildWarScoreFinishRound.Winner.Score} resetHitpoints={visibleSobNpc.Life}; " +
                    "source MsgTopWarScore.UpdateScore modeled; source MsgTopWarScore.FinishRound modeled.")
                .ConfigureAwait(false);
        }
        else if (result.Killed &&
            TryApplyLegacyClanWarScorePoleFinishRound(
                session,
                sobNpc,
                result,
                out var clanWarScoreFinishedSession,
                out var clanWarScoreFinishRound))
        {
            session = clanWarScoreFinishedSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyClanWarScoreScorePanelFramesAsync(
                    context,
                    clanWarScoreFinishRound.ScorePanelFrames,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyClanWarScoreFinishRoundCenterMessageAsync(
                    context,
                    clanWarScoreFinishRound,
                    cancellationToken)
                .ConfigureAwait(false);
            sobNpcStates.ResetHitpointsToMax(sobNpc, clanWarScoreFinishRound.ResetPoleName);
            var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
            await SendFrameToVisiblePlayersAsync(
                    context,
                    session,
                    LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer finished legacy ClanWarScore/MsgClanW pole round " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"roundOwnerClanId={clanWarScoreFinishRound.Winner.ClanId} " +
                    $"roundOwnerClanName={clanWarScoreFinishRound.Winner.ClanName} " +
                    $"score={clanWarScoreFinishRound.Winner.Score} resetHitpoints={visibleSobNpc.Life}; " +
                    "source MsgClanScore.UpdateScore modeled; source MsgClanScore.FinishRound modeled.")
                .ConfigureAwait(false);
        }
        else if (result.Killed &&
            TryApplyLegacyUnionWarPoleFinishRound(
                session,
                sobNpc,
                result,
                out var unionWarFinishedSession,
                out var unionWarFinishRound))
        {
            session = unionWarFinishedSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyUnionWarScorePanelFramesAsync(
                    context,
                    unionWarFinishRound.ScorePanelFrames,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyUnionWarFinishRoundMessagesAsync(
                    context,
                    unionWarFinishRound,
                    cancellationToken)
                .ConfigureAwait(false);
            sobNpcStates.ResetHitpointsToMax(sobNpc, unionWarFinishRound.ResetPoleName);
            var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
            await SendFrameToVisiblePlayersAsync(
                    context,
                    session,
                    LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer finished legacy UnionWar pole round " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"winnerUnionId={unionWarFinishRound.Winner.UnionId} " +
                    $"winnerUnionName={unionWarFinishRound.Winner.UnionName} " +
                    $"score={unionWarFinishRound.Winner.Score} resetHitpoints={visibleSobNpc.Life}; " +
                    "source MsgUnionWar.UpdateScore modeled; source MsgUnionWar.FinishRound modeled.")
                .ConfigureAwait(false);
        }
        else if (result.Killed &&
            TryApplyLegacyChampionsOfWarrPoleFinishRound(
                session,
                sobNpc,
                result,
                out var championsOfWarrFinishedSession,
                out var championsOfWarrFinishRound))
        {
            session = championsOfWarrFinishedSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyChampionsOfWarrScorePanelFramesAsync(
                    context,
                    championsOfWarrFinishRound.ScorePanelFrames,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyChampionsOfWarrFinishRoundCenterMessageAsync(
                    context,
                    championsOfWarrFinishRound,
                    cancellationToken)
                .ConfigureAwait(false);
            sobNpcStates.ResetHitpointsToMax(sobNpc, championsOfWarrFinishRound.ResetPoleName);
            var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
            await SendFrameToVisiblePlayersAsync(
                    context,
                    session,
                    LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer finished legacy ChampionsOfWarr pole round " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"roundOwnerGuildId={championsOfWarrFinishRound.Winner.GuildId} " +
                    $"roundOwnerGuildName={championsOfWarrFinishRound.Winner.GuildName} " +
                    $"score={championsOfWarrFinishRound.Winner.Score} resetHitpoints={visibleSobNpc.Life}; " +
                    "source MsgChampionsOfWarr.UpdateScore modeled; source MsgChampionsOfWarr.FinishRound modeled.")
                .ConfigureAwait(false);
        }
        else if (result.Killed &&
            TryApplyLegacySuperGuildWarPoleFinishRound(
                session,
                sobNpc,
                result,
                out var superGuildWarFinishedSession,
                out var superGuildWarFinishRound))
        {
            session = superGuildWarFinishedSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacySuperGuildWarScorePanelFramesAsync(
                    context,
                    superGuildWarFinishRound.ScorePanelFrames,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacySuperGuildWarFinishRoundMessagesAsync(
                    context,
                    superGuildWarFinishRound,
                    cancellationToken)
                .ConfigureAwait(false);
            sobNpcStates.ResetHitpointsToMax(sobNpc, superGuildWarFinishRound.ResetPoleName);
            var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
            await SendFrameToVisiblePlayersAsync(
                    context,
                    session,
                    LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer finished legacy SuperGuildWar pole round " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"winnerGuildId={superGuildWarFinishRound.Winner.GuildId} " +
                    $"winnerGuildName={superGuildWarFinishRound.Winner.GuildName} " +
                    $"score={superGuildWarFinishRound.Winner.Score} resetHitpoints={visibleSobNpc.Life}; " +
                    "source MsgSuperGuildWar.UpdateScore modeled; source MsgSuperGuildWar.FinishRound modeled.")
                .ConfigureAwait(false);
        }
        else if (result.Killed &&
            TryApplyLegacyGuild6PoleWarPoleFinishRound(
                session,
                sobNpc,
                result,
                out var guild6PoleWarFinishedSession,
                out var guild6PoleWarFinishRound))
        {
            session = guild6PoleWarFinishedSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyGuild6PoleWarScorePanelFramesAsync(
                    context,
                    guild6PoleWarFinishRound.ScorePanelFrames,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyGuild6PoleWarFinishRoundCenterMessageAsync(
                    context,
                    guild6PoleWarFinishRound,
                    cancellationToken)
                .ConfigureAwait(false);
            sobNpcStates.ResetHitpointsToMax(sobNpc, guild6PoleWarFinishRound.ResetPoleName);
            var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
            await SendFrameToVisiblePlayersAsync(
                    context,
                    session,
                    LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer finished legacy Guild6PoleWar pole round " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"schedule={guild6PoleWarFinishRound.ScheduleName} " +
                    $"roundOwnerGuildId={guild6PoleWarFinishRound.Winner.GuildId} " +
                    $"roundOwnerGuildName={guild6PoleWarFinishRound.Winner.GuildName} " +
                    $"score={guild6PoleWarFinishRound.Winner.Score} resetHitpoints={visibleSobNpc.Life}; " +
                    "source MsgGuild6PoleWar.UpdateScore modeled; source MsgGuild6PoleWar.FinishRound modeled.")
                .ConfigureAwait(false);
        }
        else if (result.Killed &&
            TryApplyLegacyWarOfPlayersPoleFinishRound(
                session,
                sobNpc,
                result,
                out var warOfPlayersFinishedSession,
                out var warOfPlayersFinishRound))
        {
            session = warOfPlayersFinishedSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyWarOfPlayersScorePanelFramesAsync(
                    context,
                    warOfPlayersFinishRound.ScorePanelFrames,
                    cancellationToken)
                .ConfigureAwait(false);
            sobNpcStates.ResetHitpointsToMax(sobNpc, warOfPlayersFinishRound.ResetPoleName);
            var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
            await SendFrameToVisiblePlayersAsync(
                    context,
                    session,
                    LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer finished legacy WarOfPlayers pole round " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"roundOwnerPlayerUid={warOfPlayersFinishRound.Winner.PlayerUid} " +
                    $"roundOwnerName={warOfPlayersFinishRound.Winner.PlayerName} " +
                    $"score={warOfPlayersFinishRound.Winner.Score} resetHitpoints={visibleSobNpc.Life}; " +
                    "source MsgWarOfPlayers.UpdateScore modeled; source MsgWarOfPlayers.FinishRound modeled.")
                .ConfigureAwait(false);
        }
        else if (result.Killed &&
            TryApplyLegacyEmperorWarPoleFinishRound(
                session,
                sobNpc,
                result,
                out var emperorWarFinishedSession,
                out var emperorWarFinishRound))
        {
            session = emperorWarFinishedSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyEmperorWarScorePanelFramesAsync(
                    context,
                    emperorWarFinishRound.ScorePanelFrames,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyEmperorWarFinishRoundCenterMessageAsync(
                    context,
                    emperorWarFinishRound,
                    cancellationToken)
                .ConfigureAwait(false);
            sobNpcStates.ResetHitpointsToMax(sobNpc, emperorWarFinishRound.ResetPoleName);
            var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
            await SendFrameToVisiblePlayersAsync(
                    context,
                    session,
                    LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer finished legacy EmperorWar pole round " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"roundOwnerPlayerUid={emperorWarFinishRound.Winner.PlayerUid} " +
                    $"roundOwnerName={emperorWarFinishRound.Winner.PlayerName} " +
                    $"score={emperorWarFinishRound.Winner.Score} resetHitpoints={visibleSobNpc.Life}; " +
                    "source MsgEmperorWar.UpdateScore modeled; source MsgEmperorWar.FinishRound modeled.")
                .ConfigureAwait(false);
        }
        else if (result.Killed &&
            TryApplyLegacyTopPolePoleFinishRound(
                session,
                sobNpc,
                result,
                out var topPoleFinishedSession,
                out var topPoleFinishRound))
        {
            session = topPoleFinishedSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyTopPoleScorePanelFramesAsync(
                    context,
                    topPoleFinishRound.ScorePanelFrames,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyTopPoleFinishRoundCenterMessageAsync(
                    context,
                    topPoleFinishRound,
                    cancellationToken)
                .ConfigureAwait(false);
            sobNpcStates.ResetHitpointsToMax(sobNpc, topPoleFinishRound.ResetPoleName);
            var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
            await SendFrameToVisiblePlayersAsync(
                    context,
                    session,
                    LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer finished legacy TopPole pole round " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"roundOwnerPlayerUid={topPoleFinishRound.Winner.PlayerUid} " +
                    $"roundOwnerName={topPoleFinishRound.Winner.PlayerName} " +
                    $"score={topPoleFinishRound.Winner.Score} resetHitpoints={visibleSobNpc.Life}; " +
                    "source MsgTopPole.UpdateScore modeled; source MsgTopPole.FinishRound modeled.")
                .ConfigureAwait(false);
        }
        else if (result.Killed &&
            TryApplyLegacyWarFightersPoleFinishRound(
                session,
                sobNpc,
                result,
                out var warFightersFinishedSession,
                out var warFightersFinishRound))
        {
            session = warFightersFinishedSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyWarFightersScorePanelFramesAsync(
                    context,
                    warFightersFinishRound.ScorePanelFrames,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyWarFightersFinishRoundCenterMessageAsync(
                    context,
                    warFightersFinishRound,
                    cancellationToken)
                .ConfigureAwait(false);
            sobNpcStates.ResetHitpointsToMax(sobNpc, warFightersFinishRound.ResetPoleName);
            var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
            await SendFrameToVisiblePlayersAsync(
                    context,
                    session,
                    LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer finished legacy WarFighters pole round " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"roundOwnerPlayerUid={warFightersFinishRound.Winner.PlayerUid} " +
                    $"roundOwnerName={warFightersFinishRound.Winner.PlayerName} " +
                    $"score={warFightersFinishRound.Winner.Score} resetHitpoints={visibleSobNpc.Life}; " +
                    "source MsgWarFighters.UpdateScore modeled; source MsgWarFighters.FinishRound modeled.")
                .ConfigureAwait(false);
        }
        else if (result.Killed &&
            TryApplyLegacyTrepilWarPoleFinishRound(
                session,
                sobNpc,
                result,
                out var trepilWarFinishedSession,
                out var trepilWarFinishRound))
        {
            session = trepilWarFinishedSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyTrepilWarScorePanelFramesAsync(
                    context,
                    trepilWarFinishRound.ScorePanelFrames,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyTrepilWarFinishRoundCenterMessageAsync(
                    context,
                    trepilWarFinishRound,
                    cancellationToken)
                .ConfigureAwait(false);
            sobNpcStates.ResetHitpointsToMax(sobNpc, trepilWarFinishRound.ResetPoleName);
            var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
            await SendFrameToVisiblePlayersAsync(
                    context,
                    session,
                    LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer finished legacy TrepilWar pole round " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"roundOwnerGuildId={trepilWarFinishRound.Winner.GuildId} " +
                    $"roundOwnerGuildName={trepilWarFinishRound.Winner.GuildName} " +
                    $"score={trepilWarFinishRound.Winner.Score} resetHitpoints={visibleSobNpc.Life}; " +
                    "source MsgTrepilWar.UpdateScore modeled; source MsgTrepilWar.FinishRound modeled.")
                .ConfigureAwait(false);
        }
        else if (result.Killed &&
            TryApplyLegacyNobilityPoleFinishRound(
                session,
                sobNpc,
                result,
                out var nobilityPoleFinishedSession,
                out var nobilityPoleFinishRound))
        {
            session = nobilityPoleFinishedSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyNobilityPoleScorePanelFramesAsync(
                    context,
                    nobilityPoleFinishRound.ScorePanelFrames,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyNobilityPoleFinishRoundCenterMessageAsync(
                    context,
                    nobilityPoleFinishRound,
                    cancellationToken)
                .ConfigureAwait(false);
            sobNpcStates.ResetHitpointsToMax(sobNpc, nobilityPoleFinishRound.ResetPoleName);
            var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
            await SendFrameToVisiblePlayersAsync(
                    context,
                    session,
                    LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer finished legacy NobilityPole round " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"schedule={nobilityPoleFinishRound.ScheduleName} displayRank={nobilityPoleFinishRound.DisplayRank} " +
                    $"roundOwnerPlayerUid={nobilityPoleFinishRound.Winner.PlayerUid} " +
                    $"roundOwnerName={nobilityPoleFinishRound.Winner.PlayerName} " +
                    $"score={nobilityPoleFinishRound.Winner.Score} resetHitpoints={visibleSobNpc.Life}; " +
                    "source MsgNobilityPole.UpdateScore modeled; source MsgNobilityPole.FinishRound modeled.")
                .ConfigureAwait(false);
        }
        else if (result.Killed &&
            TryApplyLegacyWarriorGodPoleFinishRound(
                session,
                sobNpc,
                result,
                out var warriorGodFinishedSession,
                out var warriorGodFinishRound))
        {
            session = warriorGodFinishedSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyWarriorGodScorePanelFramesAsync(
                    context,
                    warriorGodFinishRound.ScorePanelFrames,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyWarriorGodFinishRoundCenterMessageAsync(
                    context,
                    warriorGodFinishRound,
                    cancellationToken)
                .ConfigureAwait(false);
            sobNpcStates.ResetHitpointsToMax(sobNpc, warriorGodFinishRound.ResetPoleName);
            var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
            await SendFrameToVisiblePlayersAsync(
                    context,
                    session,
                    LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer finished legacy WarriorGod pole round " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"roundOwnerPlayerUid={warriorGodFinishRound.Winner.PlayerUid} " +
                    $"roundOwnerName={warriorGodFinishRound.Winner.PlayerName} " +
                    $"score={warriorGodFinishRound.Winner.Score} resetHitpoints={visibleSobNpc.Life}; " +
                    "source MsgWarriorGod.UpdateScore modeled; source MsgWarriorGod.FinishRound modeled.")
                .ConfigureAwait(false);
        }
        else if (result.Killed &&
            TryApplyLegacyClassPolaWarPoleFinishRound(
                session,
                sobNpc,
                result,
                out var classPolaWarFinishedSession,
                out var classPolaWarFinishRound))
        {
            session = classPolaWarFinishedSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyClassPolaWarScorePanelFramesAsync(
                    context,
                    classPolaWarFinishRound.ScorePanelFrames,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyClassPolaWarFinishRoundCenterMessageAsync(
                    context,
                    classPolaWarFinishRound,
                    cancellationToken)
                .ConfigureAwait(false);
            sobNpcStates.ResetHitpointsToMax(sobNpc, classPolaWarFinishRound.ResetPoleName);
            var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
            await SendFrameToVisiblePlayersAsync(
                    context,
                    session,
                    LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer finished legacy ClassPolaWar pole round " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"roundOwnerPlayerUid={classPolaWarFinishRound.Winner.PlayerUid} " +
                    $"roundOwnerName={classPolaWarFinishRound.Winner.PlayerName} " +
                    $"score={classPolaWarFinishRound.Winner.Score} resetHitpoints={visibleSobNpc.Life}; " +
                    "source MsgClassPolaWar.UpdateScore modeled; source MsgClassPolaWar.FinishRound modeled.")
                .ConfigureAwait(false);
        }
        else if (result.Killed &&
            TryApplyLegacyArcherPoleFinishRound(
                session,
                sobNpc,
                result,
                out var archerPoleFinishedSession,
                out var archerPoleFinishRound))
        {
            session = archerPoleFinishedSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyArcherPoleScorePanelFramesAsync(
                    context,
                    archerPoleFinishRound.ScorePanelFrames,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyArcherPoleFinishRoundCenterMessageAsync(
                    context,
                    archerPoleFinishRound,
                    cancellationToken)
                .ConfigureAwait(false);
            sobNpcStates.ResetHitpointsToMax(sobNpc, archerPoleFinishRound.ResetPoleName);
            var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
            await SendFrameToVisiblePlayersAsync(
                    context,
                    session,
                    LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer finished legacy ArcherMaster pole round " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"roundOwnerPlayerUid={archerPoleFinishRound.Winner.PlayerUid} " +
                    $"roundOwnerName={archerPoleFinishRound.Winner.PlayerName} " +
                    $"score={archerPoleFinishRound.Winner.Score} resetHitpoints={visibleSobNpc.Life}; " +
                    "source MsgArcherMaster.UpdateScore modeled; source MsgArcherMaster.FinishRound modeled.")
                .ConfigureAwait(false);
        }
        else if (result.Killed &&
            TryApplyLegacyTrojanPoleFinishRound(
                session,
                sobNpc,
                result,
                out var trojanPoleFinishedSession,
                out var trojanPoleFinishRound))
        {
            session = trojanPoleFinishedSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyTrojanPoleScorePanelFramesAsync(
                    context,
                    trojanPoleFinishRound.ScorePanelFrames,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyTrojanPoleFinishRoundCenterMessageAsync(
                    context,
                    trojanPoleFinishRound,
                    cancellationToken)
                .ConfigureAwait(false);
            sobNpcStates.ResetHitpointsToMax(sobNpc, trojanPoleFinishRound.ResetPoleName);
            var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
            await SendFrameToVisiblePlayersAsync(
                    context,
                    session,
                    LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer finished legacy TrojanMaster pole round " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"roundOwnerPlayerUid={trojanPoleFinishRound.Winner.PlayerUid} " +
                    $"roundOwnerName={trojanPoleFinishRound.Winner.PlayerName} " +
                    $"score={trojanPoleFinishRound.Winner.Score} resetHitpoints={visibleSobNpc.Life}; " +
                    "source MsgTrojanMaster.UpdateScore modeled; source MsgTrojanMaster.FinishRound modeled.")
                .ConfigureAwait(false);
        }
        else if (result.Killed &&
            TryApplyLegacyNinjaMasterPoleFinishRound(
                session,
                sobNpc,
                result,
                out var ninjaMasterFinishedSession,
                out var ninjaMasterFinishRound))
        {
            session = ninjaMasterFinishedSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyNinjaMasterScorePanelFramesAsync(
                    context,
                    ninjaMasterFinishRound.ScorePanelFrames,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyNinjaMasterFinishRoundCenterMessageAsync(
                    context,
                    ninjaMasterFinishRound,
                    cancellationToken)
                .ConfigureAwait(false);
            sobNpcStates.ResetHitpointsToMax(sobNpc, ninjaMasterFinishRound.ResetPoleName);
            var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
            await SendFrameToVisiblePlayersAsync(
                    context,
                    session,
                    LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer finished legacy NinjaMaster pole round " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"roundOwnerPlayerUid={ninjaMasterFinishRound.Winner.PlayerUid} " +
                    $"roundOwnerName={ninjaMasterFinishRound.Winner.PlayerName} " +
                    $"score={ninjaMasterFinishRound.Winner.Score} resetHitpoints={visibleSobNpc.Life}; " +
                    "source MsgNinjaMaster.UpdateScore modeled; source MsgNinjaMaster.FinishRound modeled.")
                .ConfigureAwait(false);
        }
        else if (result.Killed &&
            (await TryApplyLegacyRemainingClassPoleWarPoleFinishRoundAsync(
                    context,
                    session,
                    sobNpcStates,
                    sobNpc,
                    result,
                    cancellationToken)
                .ConfigureAwait(false)) is { } remainingClassPoleFinishedSession)
        {
            session = remainingClassPoleFinishedSession;
        }
        else if (result.Killed &&
            TryApplyLegacyEliteGuildWarPoleFinishRound(
                session,
                sobNpc,
                result,
                out var eliteGuildWarFinishedSession,
                out var eliteGuildWarFinishRound))
        {
            session = eliteGuildWarFinishedSession;
            context.Sessions?.Set(context.Connection, session);
            await SendLegacyEliteGuildWarScorePanelFramesAsync(
                    context,
                    eliteGuildWarFinishRound.ScorePanelFrames,
                    cancellationToken)
                .ConfigureAwait(false);
            sobNpcStates.ResetHitpointsToMax(sobNpc, eliteGuildWarFinishRound.ResetPoleName);
            var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
            await SendFrameToVisiblePlayersAsync(
                    context,
                    session,
                    LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer finished legacy EliteGuildWar pole round " +
                    $"{FormatSession(context with { Session = session })} sobNpcUid={sobNpc.Id} " +
                    $"roundOwnerGuildId={eliteGuildWarFinishRound.Winner.GuildId} " +
                    $"roundOwnerGuildName={eliteGuildWarFinishRound.Winner.GuildName} " +
                    $"score={eliteGuildWarFinishRound.Winner.Score} resetHitpoints={visibleSobNpc.Life}; " +
                    "source MsgEliteGuildWar.UpdateScore modeled; source MsgEliteGuildWar.FinishRound modeled.")
                .ConfigureAwait(false);
        }
        else if (result.Killed &&
            LegacySobNpcEventRules.TryGetGuildWarOwnershipChangeDwParam(sobNpc, out var ownershipDwParam) &&
            TryGetLegacyGuildWarCaptureGuild(session, out var captureGuildId, out var captureGuildName))
        {
            sobNpcStates.ResetHitpointsToMax(sobNpc, captureGuildName, captureGuildId);
            await SendLegacyGuildWarCapturePillarSobNpcUpdateAsync(
                    context,
                    session,
                    sobNpc,
                    sobNpcStates.GetVisible(sobNpc),
                    captureGuildId,
                    ownershipDwParam,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (result.Killed &&
            TryApplyLegacyCaptureTheFlagBaseCapture(
                session,
                sobNpc,
                out var updatedSession,
                out var capturerGuildId,
                out var capturerGuildName))
        {
            sobNpcStates.ResetHitpointsToMax(
                sobNpc,
                capturerGuildName,
                capturerGuildId <= uint.MaxValue ? (uint)capturerGuildId : null);
            context.Sessions?.Set(context.Connection, updatedSession);
            var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
            await SendFrameToVisiblePlayersAsync(
                    context,
                    updatedSession,
                    LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            var occupiedBase = new LegacyCaptureTheFlagUpdatePacket(
                LegacyCaptureTheFlagUpdateMode.OccupiedBase,
                checked((uint)(sobNpc.Id % 100)),
                Parameter2: 0,
                Count: 1);
            await SendFrameToVisiblePlayersAsync(
                    context,
                    updatedSession,
                    occupiedBase.ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer sent legacy CaptureTheFlag base capture " +
                    $"{FormatSession(context with { Session = updatedSession })} sobNpcUid={sobNpc.Id} " +
                    $"guildId={capturerGuildId} guildName={capturerGuildName} occupiedBase={sobNpc.Id % 100}; " +
                    "source MsgCaptureTheFlag.UpdateFlagScore killed base branch.")
                .ConfigureAwait(false);
            session = updatedSession;
        }

        return session;
    }

    private static bool PassesLegacyPlayerSobNpcMagicPreResourceRangeGate(
        WorldSession session,
        SobNpc primaryTarget,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (spell.Sort is not (LegacyMagicSort.Attack or LegacyMagicSort.DirectAttack))
        {
            return true;
        }

        var distance = LegacyGetDistance(
            session.Character.X,
            session.Character.Y,
            primaryTarget.X,
            primaryTarget.Y);
        if (distance <= spell.Range)
        {
            return true;
        }

        failureDiagnostic = $"sobNpcUid={query.OpponentUid} distance={distance} range={spell.Range}";
        return false;
    }

    private static async ValueTask SendLegacyTrainingSobNpcDeathUpdatesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        SobNpc sobNpc,
        SobNpc visibleSobNpc,
        CancellationToken cancellationToken)
    {
        var sobNpcUid = CheckedUInt32(sobNpc.Id, nameof(sobNpc.Id));
        var death = new MsgAttackPacket(new MsgAttackQuery(
            Uid: CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            OpponentUid: sobNpcUid,
            X: CheckedUInt16(sobNpc.X, nameof(sobNpc.X)),
            Y: CheckedUInt16(sobNpc.Y, nameof(sobNpc.Y)),
            AttackType: MsgAttackType.Death,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0));
        await SendFrameToVisiblePlayersAsync(context, session, death.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        var hitpointUpdate = new LegacyMsgUpdatePacket(
            sobNpcUid,
            [new LegacyMsgUpdateEntry(
                LegacyMsgUpdateDataType.Hitpoints,
                CheckedUInt32(visibleSobNpc.Life, nameof(visibleSobNpc.Life)))]);
        await SendFrameToVisiblePlayersAsync(context, session, hitpointUpdate.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        var maxHitpointUpdate = new LegacyMsgUpdatePacket(
            sobNpcUid,
            [new LegacyMsgUpdateEntry(
                LegacyMsgUpdateDataType.MaxHitpoints,
                CheckedUInt32(visibleSobNpc.MaxLife, nameof(visibleSobNpc.MaxLife)))]);
        await SendFrameToVisiblePlayersAsync(context, session, maxHitpointUpdate.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyGuildWarGateDemolitionSobNpcUpdateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        SobNpc sobNpc,
        SobNpc visibleSobNpc,
        CancellationToken cancellationToken)
    {
        if (TryGetLegacyGuildWarGateDestroyedDwParam(sobNpc, out var destroyedDwParam) &&
            TryGetLegacyGuildWarSobOwnerGuildId(context, sobNpc, out var syndicateId) &&
            context.GuildWarStates?.CanBroadcastMsgNewSynWarDestroyed(syndicateId) == true &&
            context.SobNpcStates?.TryMarkGuildWarGateDestroyed(sobNpc) == true)
        {
            var destroyed = LegacyNewSynWarPacket.Destroyed(destroyedDwParam, syndicateId);
            await SendFrameToVisiblePlayersAsync(context, session, destroyed.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);

            await context.Log.WriteLineAsync(
                    "WorldServer sent LegacyNewSynWar GuildWar gate destroyed " +
                    $"{FormatSession(context)} sobNpcUid={sobNpc.Id} map={sobNpc.MapId} " +
                    $"guildId={syndicateId} dwParam={destroyedDwParam}.")
                .ConfigureAwait(false);
        }

        var spawn = LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc);
        await SendFrameToVisiblePlayersAsync(context, session, spawn.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer sent LegacySobNpcSpawn GuildWar gate demolition " +
                $"{FormatSession(context)} sobNpcUid={sobNpc.Id} map={sobNpc.MapId} " +
                $"mesh={visibleSobNpc.Lookface} hitpoints={visibleSobNpc.Life} maxHitpoints={visibleSobNpc.MaxLife}.")
            .ConfigureAwait(false);
    }

    private static bool TryGetLegacyGuildWarGateDestroyedDwParam(SobNpc sobNpc, out uint dwParam)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        if (sobNpc.MapId == LegacySobNpcEventRules.GuildWarMapId &&
            sobNpc.Id == LegacySobNpcEventRules.GuildWarLeftGateUid)
        {
            dwParam = LegacyNewSynWarPacket.LeftGateDestroyedDwParam;
            return true;
        }

        if (sobNpc.MapId == LegacySobNpcEventRules.GuildWarMapId &&
            sobNpc.Id == LegacySobNpcEventRules.GuildWarRightGateUid)
        {
            dwParam = LegacyNewSynWarPacket.RightGateDestroyedDwParam;
            return true;
        }

        dwParam = 0;
        return false;
    }

    private static async ValueTask SendLegacyGuildWarCapturePillarSobNpcUpdateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        SobNpc sobNpc,
        SobNpc visibleSobNpc,
        uint captureGuildId,
        uint ownershipDwParam,
        CancellationToken cancellationToken)
    {
        var ownerGuildId = captureGuildId;
        if (context.SobNpcStates?.TryGetOwnerGuildId(sobNpc, out var storedOwnerGuildId) == true)
        {
            ownerGuildId = storedOwnerGuildId;
        }

        var spawn = LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc);
        await SendFrameToVisiblePlayersAsync(context, session, spawn.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        if (context.GuildWarStates?.CanBroadcastMsgNewSynWarOwnershipChanged(ownerGuildId) == true)
        {
            var ownership = LegacyNewSynWarPacket.OwnershipChanged(ownershipDwParam, ownerGuildId);
            await SendFrameToVisiblePlayersAsync(context, session, ownership.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);

            await context.Log.WriteLineAsync(
                    "WorldServer sent LegacyNewSynWar GuildWar pillar ownership " +
                    $"{FormatSession(context)} sobNpcUid={sobNpc.Id} map={sobNpc.MapId} " +
                    $"guildId={ownerGuildId} dwParam={ownershipDwParam}.")
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer sent LegacySobNpcSpawn GuildWar pillar capture " +
                $"{FormatSession(context)} sobNpcUid={sobNpc.Id} map={sobNpc.MapId} " +
                $"guildId={captureGuildId} dwParam={ownershipDwParam} name={visibleSobNpc.Name} " +
                $"hitpoints={visibleSobNpc.Life} maxHitpoints={visibleSobNpc.MaxLife}.")
            .ConfigureAwait(false);
    }

    private static bool TryGetLegacyGuildWarSobOwnerGuildId(
        WorldPacketHandlerContext context,
        SobNpc sobNpc,
        out uint guildId)
    {
        guildId = 0;
        return context.SobNpcStates?.TryGetOwnerGuildId(sobNpc, out guildId) == true;
    }

    private static bool TryGetLegacyGuildWarCaptureGuild(
        WorldSession session,
        out uint guildId,
        out string guildName)
    {
        guildId = 0;
        guildName = string.Empty;
        if (session.Character.MapId != LegacySobNpcEventRules.GuildWarMapId)
        {
            return false;
        }

        var rawGuildId = ReadAttributeUInt64(session.Character.Attributes, "GuildID", "GuildId");
        if (rawGuildId == 0 || rawGuildId > uint.MaxValue)
        {
            return false;
        }

        guildId = (uint)rawGuildId;
        if (TryGetLegacyGuildInfo(session, guildId, out var guild) &&
            !string.IsNullOrWhiteSpace(guild.Name))
        {
            guildName = guild.Name.Trim();
            return true;
        }

        guildName = ReadAttributeString(session.Character.Attributes, "GuildName").Trim();
        return !string.IsNullOrEmpty(guildName);
    }

    private static ulong CalculateLegacySobNpcExperienceDamage(
        SobNpc sobNpc,
        SobNpcDamageResult result)
    {
        if (result.DamageApplied == 0)
        {
            return 0;
        }

        if (result.Killed)
        {
            return LegacySobNpcEventRules.IsTrainingSob(sobNpc)
                ? result.DamageApplied / 10
                : 0;
        }

        return sobNpc.MapId is 1_038 or 1_039
            ? result.DamageApplied / 1_000
            : 0;
    }

    private static async ValueTask LogLegacySobNpcEventSideEffectGapAsync(
        WorldPacketHandlerContext context,
        SobNpc sobNpc,
        SobNpcDamageResult result,
        LegacyMagicRuntimeSpell spell)
    {
        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.SobNpcSideEffects(
                    spell.Id,
                    sort: (int)spell.Sort,
                    mapId: sobNpc.MapId,
                    sobNpcKind: sobNpc.Kind,
                    reason:
                        "GuildWar/CTF/SuperGuildWar/WarOfPlayers/Guild6PoleWar/GuildWarScore/TopWarScore/EmperorWar/TopPole/WarFighters/TrepilWar/NobilityPole/WarriorGod/ClassPolaWar/ArcherMaster/TrojanMaster/NinjaMaster/ClanWarScore/ClanWar current-war/EliteGuildWar/UnionWar/ChampionsOfWarr scores and bounded score panels/FinishRound resets modeled; deferred: " +
                        "rewards, remaining event-specific winner/pole resets, persistence, full event state"))
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy player-SOB event damage with deferred side effects " +
                $"{FormatSession(context)} sobNpcUid={sobNpc.Id} map={sobNpc.MapId} kind={sobNpc.Kind} " +
                $"spellId={spell.Id} damage={result.DamageApplied} remainingHitpoints={result.RemainingHitpoints} " +
                $"killed={result.Killed}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask LogLegacyDirectSobNpcEventSideEffectGapAsync(
        WorldPacketHandlerContext context,
        SobNpc sobNpc,
        SobNpcDamageResult result,
        MsgAttackType attackType)
    {
        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.DirectSobNpcSideEffects(
                    attackType,
                    mapId: sobNpc.MapId,
                    sobNpcKind: sobNpc.Kind,
                    reason:
                        "GuildWar/CTF/SuperGuildWar/WarOfPlayers/Guild6PoleWar/GuildWarScore/TopWarScore/EmperorWar/TopPole/WarFighters/TrepilWar/NobilityPole/WarriorGod/ClassPolaWar/ArcherMaster/TrojanMaster/NinjaMaster/ClanWarScore/ClanWar current-war/EliteGuildWar/UnionWar/ChampionsOfWarr scores and bounded score panels/FinishRound resets modeled; deferred: " +
                        "rewards, remaining event-specific winner/pole resets, persistence, full event state"))
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy direct SOB NPC event damage with bounded side effects " +
                $"{FormatSession(context)} sobNpcUid={sobNpc.Id} map={sobNpc.MapId} kind={sobNpc.Kind} " +
                $"attackType={attackType} damage={result.DamageApplied} " +
                $"remainingHitpoints={result.RemainingHitpoints} killed={result.Killed}.")
            .ConfigureAwait(false);
    }
}
