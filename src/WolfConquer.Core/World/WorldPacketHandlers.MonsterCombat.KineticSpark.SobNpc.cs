using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private readonly record struct LegacyKineticSparkSobNpcHit(
        SobNpc SobNpc,
        uint RequestedDamage,
        SobNpcDamageResult Result,
        LegacySobNpcDeathBehavior DeathBehavior,
        bool Primary);

    private static async ValueTask<bool> TryHandleLegacyKineticSparkPassiveSobNpcAttackAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (!IsLegacyKineticSparkPassiveSobNpcProcAttackType(packet.Query.AttackType) ||
            context.WorldData is not { } worldData ||
            !HasLegacyStatusFlag(session.Character, LegacyKineticSparkFlag) ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                session.PlayerSpells,
                worldData,
                LegacyKineticSparkSpellId,
                out var playerSpell,
                out var spell) ||
            spell.Sort != LegacyMagicSort.KineticSpark ||
            !TryFindVisibleSobNpcByUid(
                context,
                session.Character,
                packet.Query.OpponentUid,
                out var primary))
        {
            return false;
        }

        if (context.SobNpcStates is not { } sobNpcStates)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        SobNpcRuntimeStateResource,
                        "kinetic-spark-passive-sob-npc",
                        "KineticSpark passive SOB target was visible, but SOB runtime state is not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", primary.MapId),
                            LegacyParityGapDiagnostics.Fact("opcode", WorldPacketOpcodes.MsgAttack),
                            LegacyParityGapDiagnostics.Fact("sobNpcKind", primary.Kind),
                            LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                            LegacyParityGapDiagnostics.Fact("targetKind", "sob-npc"),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                            LegacyParityGapDiagnostics.Fact("targetKind", "sob-npc"),
                        ]))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy KineticSpark passive SOB NPC proc " +
                    $"{FormatSession(context)} spellId={spell.Id}; SOB runtime state is not loaded.")
                .ConfigureAwait(false);
            return true;
        }

        if (!CanLegacySobNpcMagicDamageTarget(context, session.Character, primary, out var failureDiagnostic))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer rejected legacy KineticSpark passive SOB NPC target " +
                    $"{FormatSession(context)} spellId={spell.Id} targetUid={primary.Id}; " +
                    $"source=KineticSpark.cs AttackSpell case 11590 SOB branch CanAttackNpc.Verified; {failureDiagnostic}.")
                .ConfigureAwait(false);
            return true;
        }

        if (!CheckLegacyKineticSparkPassiveRateRoll(session.Character))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var targetBudget = GetLegacyKineticSparkTargetBudget(session, worldData);
        var hits = new List<LegacyKineticSparkSobNpcHit>();
        var animationTargets = new List<LegacySpellAnimationTarget>();

        targetBudget--;
        if (targetBudget > 0)
        {
            var hitSession = await AddLegacyKineticSparkSobNpcHitAsync(
                    context,
                    sobNpcStates,
                    session,
                    worldData,
                    spell,
                    primary,
                    now,
                    remainingBudget: null,
                    primary: true,
                    useMagicDamage: false,
                    hits,
                    animationTargets,
                    cancellationToken)
                .ConfigureAwait(false);
            if (hitSession is null)
            {
                return true;
            }

            session = hitSession;
            context = context with { Session = session };
        }

        foreach (var candidate in EnumerateLegacyKineticSparkVisibleSobNpcChainCandidates(
                     context,
                     session.Character,
                     primary,
                     spell))
        {
            targetBudget--;
            if (targetBudget <= 0)
            {
                break;
            }

            var hitSession = await AddLegacyKineticSparkSobNpcHitAsync(
                    context,
                    sobNpcStates,
                    session,
                    worldData,
                    spell,
                    candidate.SobNpc,
                    now,
                    targetBudget,
                    primary: false,
                    useMagicDamage: true,
                    hits,
                    animationTargets,
                    cancellationToken)
                .ConfigureAwait(false);
            if (hitSession is null)
            {
                return true;
            }

            session = hitSession;
            context = context with { Session = session };
        }

        if (animationTargets.Count != 0)
        {
            var animation = LegacySpellAnimationPacket.ForPlayerMagic(
                CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
                CheckedUInt16(primary.X, nameof(primary.X)),
                CheckedUInt16(primary.Y, nameof(primary.Y)),
                spell.Id,
                (byte)Math.Min(playerSpell.Level, byte.MaxValue),
                playerSpell.UseSpellSoul,
                animationTargets);
            await SendPlayerSpellAnimationToVisibleAsync(context, session, animation, cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var hit in hits)
        {
            if (hit.Result.Killed && LegacySobNpcEventRules.IsTrainingSob(hit.SobNpc))
            {
                await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                        context,
                        session,
                        hit.SobNpc,
                        sobNpcStates.GetVisible(hit.SobNpc),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (!hit.Result.AlreadyDead && !LegacySobNpcEventRules.IsTrainingSob(hit.SobNpc))
            {
                session = await ApplyLegacySobNpcEventSideEffectsAsync(
                        context with { Session = session },
                        session,
                        hit.SobNpc,
                        hit.Result,
                        hit.DeathBehavior,
                        spell,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var progressDamage = hits.Aggregate(
            0UL,
            (total, hit) => total + CalculateLegacySobNpcExperienceDamage(hit.SobNpc, hit.Result));
        var updatedSession = await ApplyLegacyKineticSparkSobNpcIncreaseExperienceAsync(
                context,
                session,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                updatedSession,
                ClampLegacyProficiencyRawExperience(progressDamage),
                cancellationToken,
                activity: "KineticSpark passive SOB NPC weapon proficiency experience")
            .ConfigureAwait(false);

        var totalRequestedDamage = hits
            .Where(hit => !hit.Result.AlreadyDead)
            .Aggregate(0UL, (total, hit) => total + hit.RequestedDamage);
        var totalAppliedDamage = hits
            .Where(hit => !hit.Result.AlreadyDead)
            .Aggregate(0UL, (total, hit) => total + hit.Result.DamageApplied);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy KineticSpark passive SOB NPC proc " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} " +
                $"spellLevel={playerSpell.Level} soulLevel={playerSpell.UseSpellSoul} primarySobNpcUid={primary.Id} " +
                $"targets={animationTargets.Count} progressDamage={progressDamage} " +
                $"totalRequestedDamage={totalRequestedDamage} totalAppliedDamage={totalAppliedDamage}; " +
                "source=KineticSpark.cs AttackSpell case 11590 SOB branch.")
            .ConfigureAwait(false);
        return true;
    }

    private static IEnumerable<LegacyPlayerSobNpcMagicTarget> EnumerateLegacyKineticSparkVisibleSobNpcChainCandidates(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        SobNpc primary,
        LegacyMagicRuntimeSpell spell)
    {
        if (character.MapId is < int.MinValue or > int.MaxValue ||
            context.WorldData is not { } worldData ||
            !worldData.SobNpcsByMapId.TryGetValue((int)character.MapId, out var sobNpcs))
        {
            yield break;
        }

        foreach (var sobNpc in sobNpcs)
        {
            if (sobNpc.Id == primary.Id ||
                !CanSeeSobNpc(character, sobNpc) ||
                !CanLegacySobNpcMagicDamageTarget(context, character, sobNpc, out _) ||
                LegacyGetDistance(primary.X, primary.Y, sobNpc.X, sobNpc.Y) >= spell.Range / 2)
            {
                continue;
            }

            yield return new LegacyPlayerSobNpcMagicTarget(sobNpc);
        }
    }

    private static async ValueTask<WorldSession?> AddLegacyKineticSparkSobNpcHitAsync(
        WorldPacketHandlerContext context,
        SobNpcRuntimeStateStore sobNpcStates,
        WorldSession session,
        WorldDataCatalog worldData,
        LegacyMagicRuntimeSpell spell,
        SobNpc sobNpc,
        DateTimeOffset now,
        int? remainingBudget,
        bool primary,
        bool useMagicDamage,
        List<LegacyKineticSparkSobNpcHit> hits,
        List<LegacySpellAnimationTarget> animationTargets,
        CancellationToken cancellationToken)
    {
        var receiveGearOutcome = await TrySendLegacySobNpcReceiveCombatGearUpdateAsync(
                context with { Session = session },
                session,
                sobNpc,
                cancellationToken)
            .ConfigureAwait(false);
        if (receiveGearOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
        {
            return null;
        }

        session = receiveGearOutcome.Session;
        context = context with { Session = session };
        (session, _) = await TrySendLegacySobNpcReceiveNinjaSageModeAsync(
                context with { Session = session },
                session,
                worldData,
                cancellationToken)
            .ConfigureAwait(false);
        session = WithLegacySobNpcReceiveHitAttributes(context, session, sobNpc);
        var damage = useMagicDamage
            ? CalculateLegacyKineticSparkSobNpcMagicDamage(session, worldData, spell)
            : CalculateLegacySobNpcPhysicalSpellDamage(session, spell, worldData, damageMultiplier: 1);
        damage = CalculateLegacyKineticSparkSobNpcSoulDamage(damage, session);
        if (remainingBudget is { } budget)
        {
            damage = ScaleLegacyKineticSparkChainedDamage(damage, budget);
        }

        damage = ApplyLegacySobNpcReceiveDamageModifiers(session, damage);
        var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(sobNpc, worldData.StaticStatues);
        var result = sobNpcStates.ApplyDamage(sobNpc, damage, now, deathBehavior);
        hits.Add(new LegacyKineticSparkSobNpcHit(sobNpc, damage, result, deathBehavior, primary));
        if (!result.AlreadyDead && damage != 0)
        {
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(sobNpc.Id, nameof(sobNpc.Id)),
                damage,
                Hit: true,
                MsgAttackEffect.None));
        }

        return session;
    }

    private static uint CalculateLegacyKineticSparkSobNpcMagicDamage(
        WorldSession session,
        WorldDataCatalog worldData,
        LegacyMagicRuntimeSpell spell)
    {
        var fields = CalculateLegacyRuntimeStatusFields(session, worldData);
        return LegacySobNpcMagicDamageCalculator.CalculateDamage(
            fields[LegacyPlayerStatusPacket.MagicAttackIndex],
            fields[LegacyPlayerStatusPacket.MagicPercentIndex],
            fields[LegacyPlayerStatusPacket.MagicDamageIncreaseIndex],
            spell);
    }

    private static uint CalculateLegacyKineticSparkSobNpcSoulDamage(uint damage, WorldSession session)
    {
        _ = session;
        return damage;
    }

    private static async ValueTask<WorldSession> ApplyLegacyKineticSparkSobNpcIncreaseExperienceAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        ulong progressDamage,
        CancellationToken cancellationToken)
    {
        if (progressDamage == 0)
        {
            return session;
        }

        var boundedProgress = progressDamage > uint.MaxValue ? uint.MaxValue : (uint)progressDamage;
        var experienceResult = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateLegacyExperienceGainInputs(context, session, boundedProgress));
        var updatedSession = ApplyLegacyExperienceSideEffects(context, context.Connection, session, experienceResult);
        if (experienceResult.AppliedExperienceGain == 0)
        {
            if (experienceResult.OblivionExperienceGain != 0 ||
                experienceResult.HuntingBlessingGain != 0)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer applied legacy KineticSpark passive SOB NPC experience side effects " +
                        $"{FormatSession(context with { Session = updatedSession })} progressDamage={boundedProgress} " +
                        $"applied=0 oblivion={experienceResult.OblivionExperienceGain} " +
                        $"huntingBlessing={experienceResult.HuntingBlessingGain} suppression={experienceResult.SuppressionReason}; " +
                        "source=KineticSpark.cs AttackSpell case 11590 IncreaseExperience.Up.")
                    .ConfigureAwait(false);
            }

            return updatedSession;
        }

        updatedSession = await ApplyLegacyFinalExperienceGainAsync(
                context,
                context.Connection,
                updatedSession,
                experienceResult.AppliedExperienceGain,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent legacy KineticSpark passive SOB NPC experience " +
                $"{FormatSession(context with { Session = updatedSession })} progressDamage={boundedProgress} " +
                $"gain={experienceResult.AppliedExperienceGain} oblivion={experienceResult.OblivionExperienceGain} " +
                $"huntingBlessing={experienceResult.HuntingBlessingGain} experience={updatedSession.Character.Experience} " +
                $"level={updatedSession.Character.Level}; source=KineticSpark.cs AttackSpell case 11590 IncreaseExperience.Up.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool IsLegacyKineticSparkPassiveSobNpcProcAttackType(MsgAttackType attackType)
    {
        return attackType == MsgAttackType.Archer;
    }
}
