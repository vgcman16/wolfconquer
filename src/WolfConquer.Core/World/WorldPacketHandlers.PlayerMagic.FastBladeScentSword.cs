using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyFastBladeSpellId = 1_045;
    private const ushort LegacyScentSwordSpellId = 1_046;
    private const byte LegacyFastBladeScentSwordLineWidth = 2;
    private const uint LegacyFastBladeScentSwordPlayerPhysicalPercent = 110;
    private const int LegacyFastBladeScentSwordMagicLevelField = 8;
    private const int LegacyFastBladeScentSwordMagicDamageField = 10;

    private static async ValueTask<bool> HandleLegacyPlayerFastBladeScentSwordManualLineMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.ManualLine ||
            spell.Id is not (LegacyFastBladeSpellId or LegacyScentSwordSpellId))
        {
            return false;
        }

        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context,
                session,
                spell,
                cancellationToken)
            .ConfigureAwait(false);
        if (resourceSession is null)
        {
            return true;
        }

        var updatedSession = resourceSession;
        var monsterTargets = context.MonsterStates is not null
            ? SelectLegacyFastBladeScentSwordMonsterTargets(context, updatedSession, query, spell)
            : [];
        var playerTargets = SelectLegacyFastBladeScentSwordPlayerTargets(context, updatedSession, query, spell);
        var sobNpcTargets = context.SobNpcStates is not null
            ? SelectLegacyFastBladeScentSwordSobNpcTargets(context, updatedSession, query, spell)
            : [];
        var rawSpellDamage = ReadLegacyFastBladeScentSwordRawDamage(context.WorldData, playerSpell, spell);

        updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                updatedSession,
                playerTargets,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var monsterResults = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result, uint RuntimeDamage, uint Progress)>();
        var playerResults = new List<LegacyPlayerMagicDamageResult>();
        var sobNpcResults = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var killedTrainingSobNpcTargets = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var eventSobNpcSideEffectTargets = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            LegacySobNpcDeathBehavior DeathBehavior)>();
        var now = DateTimeOffset.UtcNow;

        if (context.MonsterStates is { } monsterStates)
        {
            foreach (var target in monsterTargets)
            {
                var damage = CalculateLegacyFastBladeScentSwordMonsterDamage(
                    updatedSession,
                    context.WorldData,
                    target.Monster,
                    target.MonsterFamily,
                    spell,
                    rawSpellDamage);
                damage = CalculateLegacyFastBladeScentSwordSoulDamage(damage, playerSpell.UseSpellSoul);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    target.Monster,
                    damage,
                    now,
                    updatedSession);
                var progress = result.AlreadyDead ? 0 : damage;
                monsterResults.Add((target, result, damage, progress));
                if (result.AlreadyDead)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    target.Monster.Uid,
                    LegacyCombatDamageCalculator.AddLegacySaturating(result.RequestedDamage, result.RequestedDamage),
                    Hit: result.RequestedDamage > 0,
                    MsgAttackEffect.None));
            }
        }

        foreach (var target in playerTargets)
        {
            await SendLegacyFastBladeScentSwordStaminaUpdateAsync(
                    context,
                    updatedSession,
                    cancellationToken)
                .ConfigureAwait(false);

            var damage = CalculateLegacyFastBladeScentSwordPlayerDamage(
                updatedSession,
                target.ActiveSession.Session,
                context.WorldData);
            damage = CalculateLegacyFastBladeScentSwordSoulDamage(damage, playerSpell.UseSpellSoul);
            damage = MulDivToUInt32(damage, 15, 21);
            var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                    context,
                    target.ActiveSession,
                    updatedSession.Character,
                    damage,
                    cancellationToken)
                .ConfigureAwait(false);
            if (receiveOutcome.AttackerSession is { } receiveAttacker)
            {
                updatedSession = receiveAttacker;
                context = context with { Session = updatedSession };
            }

            if (receiveOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return true;
            }

            if (receiveOutcome.Result is not { } result)
            {
                continue;
            }
            if (result.DamageApplied == 0)
            {
                continue;
            }

            playerResults.Add(result);
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                result.DamageApplied,
                Hit: true,
                MsgAttackEffect.None));
        }

        var sobNpcProgress = 0UL;
        if (context.SobNpcStates is { } sobNpcStates)
        {
            foreach (var target in sobNpcTargets)
            {
                var damage = CalculateLegacyFastBladeScentSwordSobNpcDamage(
                    updatedSession,
                    context.WorldData,
                    spell,
                    rawSpellDamage);
                damage = CalculateLegacyFastBladeScentSwordSoulDamage(damage, playerSpell.UseSpellSoul);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc);
                var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                sobNpcResults.Add((target.SobNpc, result));
                if (!result.AlreadyDead)
                {
                    sobNpcProgress += result.DamageApplied;
                }
                if (result.AlreadyDead || result.DamageApplied == 0)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id)),
                    result.DamageApplied,
                    Hit: true,
                    MsgAttackEffect.None));

                if (result.Killed && LegacySobNpcEventRules.IsTrainingSob(target.SobNpc))
                {
                    killedTrainingSobNpcTargets.Add((target.SobNpc, result));
                }
                else if (!LegacySobNpcEventRules.IsTrainingSob(target.SobNpc))
                {
                    eventSobNpcSideEffectTargets.Add((target.SobNpc, result, deathBehavior));
                }
            }
        }

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendLegacyFastBladeScentSwordAnimationAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        LegacyPlayerMonsterMagicTarget? firstRetaliationTarget = null;
        foreach (var (target, result, runtimeDamage, _) in monsterResults)
        {
            if (result.AlreadyDead)
            {
                continue;
            }

            if (!result.Killed && LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(target.Monster))
            {
                var damagedMonster = target.Monster with { Hitpoints = result.RemainingHitpoints };
                foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
                {
                    await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                }
            }

            if (result.Killed)
            {
                var death = new MsgAttackPacket(new MsgAttackQuery(
                    Uid: CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
                    OpponentUid: target.Monster.Uid,
                    X: target.Monster.X,
                    Y: target.Monster.Y,
                    AttackType: MsgAttackType.Death,
                    Damage: 0,
                    KilledMonster: 1,
                    ResponseDamage: 0,
                    Effect: MsgAttackEffect.None,
                    Unknown: 0));
                await SendMonsterCombatFrameToVisibleAsync(context, updatedSession, target.Monster, death, cancellationToken)
                    .ConfigureAwait(false);
                await TrySendLegacyMonsterDeathDropAsync(
                        context,
                        updatedSession,
                        target.Monster,
                        target.MonsterFamily,
                        cancellationToken)
                    .ConfigureAwait(false);
                updatedSession = await TryApplyLegacyBossDeathRewardsAsync(
                        context,
                        updatedSession,
                        target.Monster,
                        target.MonsterFamily,
                        cancellationToken)
                    .ConfigureAwait(false);
                await TryApplyLegacyMonsterTeamExperienceAsync(
                        context,
                        updatedSession,
                        target.Monster,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                firstRetaliationTarget ??= target;
            }

            _ = runtimeDamage;
        }

        if (firstRetaliationTarget is { } retaliationTarget)
        {
            await TrySendLegacyMonsterRetaliationAsync(
                    context,
                    updatedSession,
                    retaliationTarget.Monster,
                    retaliationTarget.MonsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var result in playerResults)
        {
            await SendLegacyPlayerMagicDamageUpdatesAsync(
                    context,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (context.SobNpcStates is { } currentSobNpcStates)
        {
            foreach (var killed in killedTrainingSobNpcTargets)
            {
                await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                        context,
                        updatedSession,
                        killed.SobNpc,
                        currentSobNpcStates.GetVisible(killed.SobNpc),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            foreach (var (sobNpc, result, _) in eventSobNpcSideEffectTargets)
            {
                await LogLegacySobNpcEventSideEffectGapAsync(context, sobNpc, result, spell)
                    .ConfigureAwait(false);
            }
        }

        var monsterProgress = monsterResults.Aggregate(0UL, (total, result) => total + result.Progress);
        var progressDamage = monsterProgress + sobNpcProgress;
        updatedSession = await ApplyLegacyFastBladeScentSwordIncreaseExperienceAsync(
                context,
                updatedSession,
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
                activity: "FastBlade/ScentSword ManualLine magic weapon proficiency experience")
            .ConfigureAwait(false);

        var totalAppliedDamage =
            monsterResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
            playerResults.Aggregate(0UL, (total, result) => total + result.DamageApplied) +
            sobNpcResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy FastBlade/ScentSword ManualLine magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetX={query.X} targetY={query.Y} range={spell.Range} width={LegacyFastBladeScentSwordLineWidth} " +
                $"targets={animationTargets.Count} monsterTargets={monsterResults.Count} playerTargets={playerResults.Count} " +
                $"sobNpcTargets={sobNpcResults.Count} monsterProgress={monsterProgress} sobNpcProgress={sobNpcProgress} " +
                $"totalAppliedDamage={totalAppliedDamage}; source=Line.cs case 1045/1046.")
            .ConfigureAwait(false);
        return true;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyFastBladeScentSwordMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (IsLegacyGuardMonsterFamily(candidate.Monster, candidate.MonsterFamily) ||
                LegacyGetDistance(session.Character.X, session.Character.Y, candidate.Monster.X, candidate.Monster.Y) >= spell.Range ||
                !IsLegacyFastBladeScentSwordInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.Monster.X,
                    candidate.Monster.Y,
                    spell.Range,
                    LegacyFastBladeScentSwordLineWidth) ||
                !CanLegacyPlayerAttackMonster(session.Character, candidate.Monster, candidate.MonsterFamily, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyFastBladeScentSwordPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (LegacyGetDistance(session.Character.X, session.Character.Y, character.X, character.Y) >= spell.Range ||
                !IsLegacyFastBladeScentSwordInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    character.X,
                    character.Y,
                    spell.Range,
                    LegacyFastBladeScentSwordLineWidth) ||
                !CanLegacyPlayerMagicDamageTarget(context, session, candidate.ActiveSession.Session, spell, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyFastBladeScentSwordSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (LegacyGetDistance(session.Character.X, session.Character.Y, candidate.SobNpc.X, candidate.SobNpc.Y) >= spell.Range ||
                !IsLegacyFastBladeScentSwordInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y,
                    spell.Range,
                    LegacyFastBladeScentSwordLineWidth) ||
                !CanLegacySobNpcMagicDamageTarget(context, session.Character, candidate.SobNpc, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static bool IsLegacyFastBladeScentSwordInLine(
        long casterX,
        long casterY,
        long originX,
        long originY,
        long targetX,
        long targetY,
        byte range,
        byte width)
    {
        var directionX = originX - casterX;
        var directionY = originY - casterY;
        if (directionX == 0 && directionY == 0)
        {
            return LegacyGetDistance(casterX, casterY, targetX, targetY) <= width;
        }

        return IsLegacyInLine(
            casterX,
            casterY,
            originX,
            originY,
            targetX,
            targetY,
            range,
            width);
    }

    private static uint CalculateLegacyFastBladeScentSwordMonsterDamage(
        WorldSession session,
        WorldDataCatalog? worldData,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        LegacyMagicRuntimeSpell spell,
        uint rawSpellDamage)
    {
        if (LegacyMonsterDamageRules.TakesOnePlayerDamage(monster, monsterFamily))
        {
            return LegacyMonsterDamageRules.ApplyLegacyPhysicalSpellNonLethalClamp(
                spell.Id,
                monster.Hitpoints,
                damage: 1);
        }

        var fields = CalculateLegacyRuntimeStatusFields(session, worldData);
        var damage = GetLegacyDeterministicAttack(
            fields[LegacyPlayerStatusPacket.MaxAttackIndex],
            fields[LegacyPlayerStatusPacket.MinAttackIndex]);
        damage = ApplyLegacyPlayerMonsterAjustAttack(damage, fields, session);
        damage = ApplyLegacyPlayerMonsterAjustMaxAttack(damage, session.Character);

        var defense = ReadMonsterDefense(monsterFamily);
        damage = damage <= defense ? 0 : damage - defense;
        damage = ApplyLegacyPlayerMonsterPhysicalStatusMultipliers(
            damage,
            session.Character,
            monster,
            physicalSpellDamagePercent: Math.Max(10U, rawSpellDamage),
            monsterDamageBonusPercent: CalculateLegacyPlayerMonsterPhysicalDamageBonusPercent(
                session,
                worldData,
                monster));
        damage = Math.Max(
            damage,
            CalculateLegacyMinimumUserMonsterDamage(
                session.Character,
                session.PlayerItems,
                session.AlternateEquipmentActive));
        damage = CalculateLegacyUserMonsterLevelDamage(
            damage,
            defense,
            CheckedInt32(session.Character.Level, nameof(session.Character.Level)),
            monster.Level,
            range: false);

        var defense2 = ReadAttributeUInt32(monsterFamily.Attributes, "defense2", "defence2", "Defense2", "Defence2");
        if (defense2 == 0)
        {
            return 0;
        }

        damage = BigMulDivToUInt32(damage, defense2, 10_000);
        damage = Math.Max(1, damage);
        damage = ApplyLegacyPlayerMonsterPhysicalPostDefense2Effects(
            damage,
            fields,
            session.Character,
            GetLegacyWhirlSigilSageDamagePercent(session, worldData, spell.Id));
        damage = ApplyLegacyDeathSighInfinityMonsterPhysicalDamage(
            damage,
            session.Character,
            activeSpell: true);

        return LegacyMonsterDamageRules.ApplyLegacyPhysicalSpellNonLethalClamp(
            spell.Id,
            monster.Hitpoints,
            damage);
    }

    private static uint CalculateLegacyFastBladeScentSwordPlayerDamage(
        WorldSession attacker,
        WorldSession target,
        WorldDataCatalog? worldData)
    {
        var attackerFields = CalculateLegacyRuntimeStatusFields(attacker, worldData);
        var targetFields = CalculateLegacyRuntimeStatusFields(target, worldData);
        var damage = GetLegacyDeterministicAttack(
            attackerFields[LegacyPlayerStatusPacket.MaxAttackIndex],
            attackerFields[LegacyPlayerStatusPacket.MinAttackIndex]);
        damage = ApplyLegacyPlayerMonsterAjustAttack(damage, attackerFields, attacker);

        var attributeDefense = ReadAttributeUInt32(
            target.Character.Attributes,
            "AjustDefense",
            "AdjustDefense",
            "Defense",
            "Defence");
        var defense = Math.Max(attributeDefense, targetFields[LegacyPlayerStatusPacket.DefenceIndex]);
        damage = damage <= defense ? 1 : damage - defense;
        damage = MulDivToUInt32(damage, LegacyFastBladeScentSwordPlayerPhysicalPercent, 100);
        damage = DecreaseLegacyBless(damage, targetFields[LegacyPlayerStatusPacket.ItemBlessIndex]);
        damage = AddLegacySaturating(damage, attackerFields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex]);
        return Math.Max(1, damage);
    }

    private static uint CalculateLegacyFastBladeScentSwordSobNpcDamage(
        WorldSession session,
        WorldDataCatalog? worldData,
        LegacyMagicRuntimeSpell spell,
        uint rawSpellDamage)
    {
        var fields = CalculateLegacyRuntimeStatusFields(session, worldData);
        var damage = GetLegacyDeterministicAttack(
            fields[LegacyPlayerStatusPacket.MaxAttackIndex],
            fields[LegacyPlayerStatusPacket.MinAttackIndex]);
        damage = ApplyLegacyPlayerMonsterAjustAttack(damage, fields, session);
        damage = MulDivToUInt32(damage, Math.Max(10U, rawSpellDamage), 100);
        damage = ApplyLegacySobNpcPhysicalSpellEffects(
            damage,
            fields,
            session.Character,
            GetLegacyWhirlSigilSageDamagePercent(session, worldData, spell.Id));
        damage = AddLegacySaturating(damage, fields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex]);
        return Math.Max(1, damage);
    }

    private static uint ReadLegacyFastBladeScentSwordRawDamage(
        WorldDataCatalog? worldData,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell)
    {
        if (worldData is not null &&
            worldData.MagicByTypeId.TryGetValue(spell.Id, out var rows))
        {
            foreach (var row in rows)
            {
                if (row.Fields.Count <= LegacyFastBladeScentSwordMagicDamageField)
                {
                    continue;
                }

                if (row.Fields.Count > LegacyFastBladeScentSwordMagicLevelField &&
                    ushort.TryParse(row.Fields[LegacyFastBladeScentSwordMagicLevelField], out var rowLevel) &&
                    rowLevel != playerSpell.Level)
                {
                    continue;
                }

                if (TryParseUInt32(row.Fields[LegacyFastBladeScentSwordMagicDamageField], out var rawDamage))
                {
                    return rawDamage;
                }
            }
        }

        return spell.Damage;
    }

    private static async ValueTask<WorldSession> ApplyLegacyFastBladeScentSwordIncreaseExperienceAsync(
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
                        "WorldServer applied legacy FastBlade/ScentSword ManualLine experience side effects " +
                        $"{FormatSession(context with { Session = updatedSession })} progressDamage={boundedProgress} " +
                        $"applied=0 oblivion={experienceResult.OblivionExperienceGain} " +
                        $"huntingBlessing={experienceResult.HuntingBlessingGain} suppression={experienceResult.SuppressionReason}; " +
                        "source=Line.cs case 1045/1046 IncreaseExperience.Up.")
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
                "WorldServer sent legacy FastBlade/ScentSword ManualLine experience " +
                $"{FormatSession(context with { Session = updatedSession })} progressDamage={boundedProgress} " +
                $"gain={experienceResult.AppliedExperienceGain} oblivion={experienceResult.OblivionExperienceGain} " +
                $"huntingBlessing={experienceResult.HuntingBlessingGain} experience={updatedSession.Character.Experience} " +
                $"level={updatedSession.Character.Level}; source=Line.cs case 1045/1046 IncreaseExperience.Up.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask SendLegacyFastBladeScentSwordStaminaUpdateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(
                LegacyMsgUpdatePacket
                    .CreateLegacyRuntimeStaminaTickUpdate(
                        CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
                        ReadLegacyRuntimeStamina(session.Character))
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyFastBladeScentSwordAnimationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacySpellAnimationPacket animation,
        CancellationToken cancellationToken)
    {
        await SendPlayerSpellAnimationToVisibleAsync(context, session, animation, cancellationToken)
            .ConfigureAwait(false);

        foreach (var frame in animation.ToFrames())
        {
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }

    private static uint CalculateLegacyFastBladeScentSwordSoulDamage(uint damage, byte useSpellSoul)
    {
        // Source Base.CalculateSoul(Damage, LevelSoul) returns Damage unchanged.
        _ = useSpellSoul;
        return damage;
    }
}
