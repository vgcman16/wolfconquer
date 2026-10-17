using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyAttackStatus7000SpellId = 7_000;

    private readonly record struct LegacyAttackStatus7000RangeDamage(
        uint Damage,
        MsgAttackEffect Effect);

    private static async ValueTask<bool> HandleLegacyPlayerAttackStatus7000MagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttackStatus ||
            spell.Id != LegacyAttackStatus7000SpellId)
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
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var monsterResults = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result, uint Damage)>();
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
            foreach (var target in SelectLegacyAttackStatus7000MonsterTargets(context, resourceSession, spell))
            {
                var rangeDamage = CalculateLegacyAttackStatus7000MonsterRangeDamage(
                    resourceSession,
                    context.WorldData,
                    target.Monster,
                    target.MonsterFamily,
                    spell);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    target.Monster,
                    rangeDamage.Damage,
                    now,
                    resourceSession);
                monsterResults.Add((target, result, rangeDamage.Damage));
                if (result.AlreadyDead)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    target.Monster.Uid,
                    result.RequestedDamage,
                    Hit: result.RequestedDamage > 0,
                    rangeDamage.Effect));
            }
        }

        foreach (var target in SelectLegacyAttackStatus7000PlayerTargets(context, updatedSession, spell))
        {
            var activeTarget = target.ActiveSession;
            if (context.Sessions?.TryGet(target.ActiveSession.Connection, out var refreshedTarget) == true &&
                refreshedTarget is not null)
            {
                activeTarget = target.ActiveSession with { Session = refreshedTarget };
            }

            var statusDefended =
                HasLegacyStatusFlag(activeTarget.Session.Character, LegacyShurikenVortexFlag) ||
                HasLegacyStatusFlag(activeTarget.Session.Character, LegacyManiacDanceFlag);
            var wildPhoenixDefended = false;
            if (!statusDefended)
            {
                (activeTarget, wildPhoenixDefended) =
                    await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                            context,
                            activeTarget,
                            cancellationToken)
                        .ConfigureAwait(false);
            }

            LegacyPlayerPlayerPhysicalSpellDamageResult physicalDamage;
            if (statusDefended)
            {
                physicalDamage = new LegacyPlayerPlayerPhysicalSpellDamageResult(
                    CalculateLegacyPlayerRangeStatusReturnDamage(
                        updatedSession,
                        activeTarget.Session,
                        context.WorldData),
                    MsgAttackEffect.None);
            }
            else if (wildPhoenixDefended)
            {
                physicalDamage = new LegacyPlayerPlayerPhysicalSpellDamageResult(1U, MsgAttackEffect.None);
            }
            else
            {
                physicalDamage = CalculateLegacyPlayerPlayerPhysicalSpellDamageResult(
                    updatedSession,
                    activeTarget.Session,
                    spell,
                    context.WorldData,
                    damageDivisor: 1);
            }

            var damage = physicalDamage.Damage;
            damage = CalculateLegacyAttackStatus7000SoulDamage(damage, playerSpell.UseSpellSoul);
            var effect = physicalDamage.Effect;
            LegacyPlayerMagicReceiveOutcome receiveOutcome;
            if (statusDefended || wildPhoenixDefended)
            {
                receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                    context,
                    activeTarget,
                    updatedSession.Character,
                    damage,
                    cancellationToken)
                .ConfigureAwait(false);
            }
            else if (HasLegacyStatusFlag(
                         activeTarget.Session.Character,
                         LegacyDefensiveStanceFlag))
            {
                damage = LegacyCombatDamageCalculator.DecreaseLegacyBless(damage, 40);
                effect = MsgAttackEffect.Block;
                receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                    context,
                    activeTarget,
                    updatedSession.Character,
                    damage,
                    cancellationToken)
                .ConfigureAwait(false);
            }
            else
            {
                receiveOutcome = await ApplyLegacyBackDamageEligiblePlayerDamageAsync(
                        context,
                        activeTarget,
                        updatedSession.Character,
                        damage,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            if (receiveOutcome.AttackerSession is { } currentReceiveAttacker)
            {
                updatedSession = currentReceiveAttacker;
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

            if (result.UpdatedAttackerSession is not null)
            {
                updatedSession = result.UpdatedAttackerSession;
                context = context with { Session = updatedSession };
            }

            context = context with { Session = updatedSession };

            (updatedSession, result) = await TrySendLegacyReceiveAttackPlayerSpeffectAttributesAsync(
                    context,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = updatedSession };

            (updatedSession, _) = await TrySendLegacyPlayerReceiveNinjaSageModeAsync(
                    context,
                    updatedSession,
                    context.WorldData,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = updatedSession };

            playerResults.Add(result);
            await SendLegacyPlayerMagicPreAnimationUpdatesAsync(
                    context,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyPlayerMagicDamageUpdatesAsync(
                    context,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                damage,
                Hit: true,
                effect));
        }

        if (context.SobNpcStates is { } sobNpcStates)
        {
            foreach (var target in SelectLegacyAttackStatus7000SobNpcTargets(context, updatedSession, spell))
            {
                var damage = CalculateLegacyAttackStatus7000SobNpcRangeDamage(
                    updatedSession,
                    spell,
                    context.WorldData);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc);
                var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                sobNpcResults.Add((target.SobNpc, result));
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

        LegacyPlayerMonsterMagicTarget? firstRetaliationTarget = null;
        foreach (var (target, result, damage) in monsterResults)
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
                context = context with { Session = updatedSession };
                await TryApplyLegacyMonsterTeamExperienceAsync(
                        context,
                        updatedSession,
                        target.Monster,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (firstRetaliationTarget is null)
            {
                firstRetaliationTarget = target;
            }

            updatedSession = await TryApplyLegacyMonsterExperienceAsync(
                    context,
                    updatedSession,
                    target.Monster,
                    target.MonsterFamily,
                    damage,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = updatedSession };
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

        await TryApplyLegacyPlayerMagicChillingSnowAsync(
                context,
                updatedSession,
                playerResults,
                cancellationToken)
            .ConfigureAwait(false);

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

            foreach (var eventTarget in eventSobNpcSideEffectTargets)
            {
                await LogLegacySobNpcEventSideEffectGapAsync(
                        context,
                        eventTarget.SobNpc,
                        eventTarget.Result,
                        spell)
                    .ConfigureAwait(false);
            }
        }

        var totalMonsterDamage = monsterResults
            .Where(result => !result.Result.AlreadyDead)
            .Aggregate(0UL, (total, result) => total + result.Damage);
        var totalSobNpcExperienceDamage = sobNpcResults.Aggregate(
            0UL,
            (total, result) => total + CalculateLegacySobNpcExperienceDamage(result.SobNpc, result.Result));
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                totalMonsterDamage + totalSobNpcExperienceDamage,
                cancellationToken)
            .ConfigureAwait(false);

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy AttackStatus 7000 magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} targets={animationTargets.Count} monsterTargets={monsterResults.Count} " +
                $"playerTargets={playerResults.Count} sobNpcTargets={sobNpcResults.Count} " +
                $"totalMonsterDamage={totalMonsterDamage} totalSobNpcExperienceDamage={totalSobNpcExperienceDamage}; " +
                "source=AttackStatus.cs case 7000 Range.OnMonster/OnPlayer/OnNpcs slice.")
            .ConfigureAwait(false);
        return true;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyAttackStatus7000MonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    candidate.Monster.X,
                    candidate.Monster.Y) >= spell.Range)
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyAttackStatus7000PlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    character.X,
                    character.Y) >= spell.Range)
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyAttackStatus7000SobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y) >= spell.Range)
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static LegacyAttackStatus7000RangeDamage CalculateLegacyAttackStatus7000MonsterRangeDamage(
        WorldSession session,
        WorldDataCatalog? worldData,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        LegacyMagicRuntimeSpell spell)
    {
        if (LegacyMonsterDamageRules.TakesOnePlayerDamage(monster, monsterFamily))
        {
            return new LegacyAttackStatus7000RangeDamage(1, MsgAttackEffect.None);
        }

        var fields = CalculateLegacyRuntimeStatusFields(session, worldData);
        var damage = GetLegacyDeterministicAttack(
            fields[LegacyPlayerStatusPacket.MaxAttackIndex],
            fields[LegacyPlayerStatusPacket.MinAttackIndex]);
        damage = ApplyLegacyPlayerMonsterAjustAttack(damage, fields, session);
        damage = ApplyLegacyPlayerMonsterAjustMaxAttack(damage, session.Character);
        if (session.Character.Level > monster.Level)
        {
            damage = MulDivToUInt32(damage, 2, 1);
        }

        var spellDamagePercent = AddLegacySaturating(
            spell.Damage,
            CalculateLegacyPlayerMonsterPhysicalDamageBonusPercent(session, worldData, monster));
        damage = MulDivToUInt32(damage, spellDamagePercent, 100);
        var defense = ReadMonsterDefense(monsterFamily);
        damage = damage <= defense ? 0 : damage - defense;

        var defense2 = ReadAttributeUInt32(monsterFamily.Attributes, "defense2", "defence2", "Defense2", "Defence2");
        damage = BigMulDivToUInt32(damage, defense2, 10_000);
        var dodge = ReadAttributeUInt32(monsterFamily.Attributes, "Dodge", "dodge");
        var dodgeReduction = (uint)Math.Min(100, (int)(dodge * 4 / 10));
        damage = dodgeReduction >= 100
            ? 0
            : MulDivToUInt32(damage, 100 - dodgeReduction, 100);
        damage = CalculateLegacyUserMonsterLevelDamage(
            damage,
            defense,
            CheckedInt32(session.Character.Level, nameof(session.Character.Level)),
            monster.Level,
            range: true);
        damage = Math.Max(
            damage,
            CalculateLegacyMinimumUserMonsterDamage(
                session.Character,
                session.PlayerItems,
                session.AlternateEquipmentActive));

        if (defense2 == 0)
        {
            damage = 1;
        }

        var result = ApplyLegacyAttackStatus7000MonsterRangeEffects(
            damage,
            fields,
            session.Character);
        damage = result.Damage;
        if (!monster.Boss && HasLegacyStatusFlag(session.Character, LegacySupermanFlag))
        {
            damage = MulDivToUInt32(damage, 10, 1);
        }

        if (IsLegacyAttackStatus7000ForcedOneDamageMonster(monster))
        {
            damage = 1;
        }

        return new LegacyAttackStatus7000RangeDamage(
            Math.Max(1, damage),
            result.Effect);
    }

    private static uint CalculateLegacyAttackStatus7000SoulDamage(uint damage, byte useSpellSoul)
    {
        // Source Base.CalculateSoul(Damage, LevelSoul) returns Damage unchanged.
        _ = useSpellSoul;
        return damage;
    }

    private static uint CalculateLegacyAttackStatus7000SobNpcRangeDamage(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        WorldDataCatalog? worldData)
    {
        return CalculateLegacySobNpcPhysicalSpellDamage(
            session,
            spell,
            worldData,
            damageMultiplier: 1);
    }

    private static LegacyAttackStatus7000RangeDamage ApplyLegacyAttackStatus7000MonsterRangeEffects(
        uint damage,
        IReadOnlyList<uint> fields,
        CharacterSnapshot character)
    {
        var effect = MsgAttackEffect.None;
        var luckyStrikeRatePercent = fields[LegacyPlayerStatusPacket.LuckyStrikePercentIndex] / 100;
        var luckyStrikeRoll = ReadLegacyPhysicalEffectRoll(
            character,
            RuntimeLegacyPhysicalLuckyStrikeRollAttribute);
        if (IsLegacyAttackStatus7000SourceRate(luckyStrikeRatePercent, luckyStrikeRoll))
        {
            effect |= MsgAttackEffect.LuckyStrike;
            damage = MulDivToUInt32(damage, 200, 100);
        }

        var refineryRoll = ReadLegacyPhysicalEffectRoll(
            character,
            RuntimeLegacyPhysicalRefineryRollAttribute);
        var criticalStrike = fields[LegacyPlayerStatusPacket.CriticalStrikeIndex];
        if (IsLegacyAttackStatus7000SourceRate(40, refineryRoll) && criticalStrike != 0)
        {
            effect |= MsgAttackEffect.CriticalStrike;
            var criticalBonus = (ulong)damage * (criticalStrike / 100UL) / 100UL;
            damage = AddLegacySaturating(
                damage,
                criticalBonus > uint.MaxValue ? uint.MaxValue : (uint)criticalBonus);
        }

        return new LegacyAttackStatus7000RangeDamage(damage, effect);
    }

    private static bool IsLegacyAttackStatus7000SourceRate(uint value, uint roll)
    {
        return value > roll % 100;
    }

    private static bool IsLegacyAttackStatus7000ForcedOneDamageMonster(LegacyMonsterInstance monster)
    {
        return monster.Name.Equals("AncientDragon", StringComparison.Ordinal) ||
            monster.Name.Equals("WarDevil", StringComparison.Ordinal) ||
            monster.Name.Contains("Sprite", StringComparison.Ordinal);
    }
}
