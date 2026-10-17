using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string RuntimeLegacyKineticSparkPassiveRateRollAttribute = "LegacyKineticSparkPassiveRateRoll";

    private readonly record struct LegacyKineticSparkMonsterHit(
        LegacyPlayerMonsterMagicTarget Target,
        uint RequestedDamage,
        MonsterDamageResult Result,
        MsgAttackEffect Effect,
        bool Primary);

    private readonly record struct LegacyKineticSparkMonsterDamage(
        uint Damage,
        MsgAttackEffect Effect);

    private static async ValueTask<bool> TryHandleLegacyKineticSparkPassiveMonsterAttackAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (!IsLegacyKineticSparkPassiveProcAttackType(packet.Query.AttackType) ||
            context.WorldData is not { } worldData ||
            context.MonsterStates is not { } monsterStates ||
            !HasLegacyStatusFlag(session.Character, LegacyKineticSparkFlag) ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                session.PlayerSpells,
                worldData,
                LegacyKineticSparkSpellId,
                out var playerSpell,
                out var spell) ||
            spell.Sort != LegacyMagicSort.KineticSpark ||
            !CheckLegacyKineticSparkPassiveRateRoll(session.Character))
        {
            return false;
        }

        if (!TryFindVisibleMonsterTargetByUid(
                context,
                session.Character,
                packet.Query.OpponentUid,
                out var primary) ||
            !CanLegacyPlayerAttackMonster(session.Character, primary.Monster, primary.MonsterFamily, out _))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var targetBudget = GetLegacyKineticSparkTargetBudget(session, worldData);
        var hits = new List<LegacyKineticSparkMonsterHit>();
        var animationTargets = new List<LegacySpellAnimationTarget>();

        session = await AddLegacyKineticSparkMonsterHitAsync(
                context,
                monsterStates,
                session,
                worldData,
                spell,
                primary,
                now,
                remainingBudget: null,
                primary: true,
                hits,
                animationTargets,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (candidate.Monster.Uid == primary.Monster.Uid ||
                LegacyGetDistance(primary.Monster.X, primary.Monster.Y, candidate.Monster.X, candidate.Monster.Y) >= spell.Range / 2)
            {
                continue;
            }

            targetBudget--;
            if (targetBudget == 0)
            {
                break;
            }

            session = await AddLegacyKineticSparkMonsterHitAsync(
                    context,
                    monsterStates,
                    session,
                    worldData,
                    spell,
                    candidate,
                    now,
                    targetBudget,
                    primary: false,
                    hits,
                    animationTargets,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (animationTargets.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy KineticSpark passive monster proc " +
                    $"{FormatSession(context)} monsterUid={primary.Monster.Uid}; all selected monsters are waiting to respawn.")
                .ConfigureAwait(false);
            return true;
        }

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            primary.Monster.X,
            primary.Monster.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, session, animation, cancellationToken)
            .ConfigureAwait(false);

        var updatedSession = session;
        var updatedContext = context;
        foreach (var hit in hits)
        {
            if (hit.Result.AlreadyDead)
            {
                continue;
            }

            if (!hit.Result.Killed && LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(hit.Target.Monster))
            {
                var damagedMonster = hit.Target.Monster with { Hitpoints = hit.Result.RemainingHitpoints };
                foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
                {
                    await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                }
            }

            if (hit.Result.Killed)
            {
                var death = new MsgAttackPacket(new MsgAttackQuery(
                    Uid: CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
                    OpponentUid: hit.Target.Monster.Uid,
                    X: hit.Target.Monster.X,
                    Y: hit.Target.Monster.Y,
                    AttackType: MsgAttackType.Death,
                    Damage: 0,
                    KilledMonster: 1,
                    ResponseDamage: 0,
                    Effect: MsgAttackEffect.None,
                    Unknown: 0));
                await SendMonsterCombatFrameToVisibleAsync(
                        updatedContext,
                        updatedSession,
                        hit.Target.Monster,
                        death,
                        cancellationToken)
                    .ConfigureAwait(false);
                await TrySendLegacyMonsterDeathDropAsync(
                        updatedContext,
                        updatedSession,
                        hit.Target.Monster,
                        hit.Target.MonsterFamily,
                        cancellationToken)
                    .ConfigureAwait(false);
                updatedSession = await TryApplyLegacyBossDeathRewardsAsync(
                        updatedContext,
                        updatedSession,
                        hit.Target.Monster,
                        hit.Target.MonsterFamily,
                        cancellationToken)
                    .ConfigureAwait(false);
                updatedContext = updatedContext with { Session = updatedSession };
                await TryApplyLegacyMonsterTeamExperienceAsync(
                        updatedContext,
                        updatedSession,
                        hit.Target.Monster,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var primaryDamage = hits.FirstOrDefault(hit => hit.Primary && !hit.Result.AlreadyDead).RequestedDamage;
        updatedSession = await TryApplyLegacyMonsterExperienceAsync(
                updatedContext,
                updatedSession,
                primary.Monster,
                primary.MonsterFamily,
                primaryDamage,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };
        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                updatedContext,
                updatedSession,
                primaryDamage,
                cancellationToken,
                activity: "KineticSpark passive monster weapon proficiency experience")
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                updatedContext,
                updatedSession,
                playerSpell,
                spell,
                primaryDamage,
                cancellationToken)
            .ConfigureAwait(false);

        var totalAppliedDamage = hits
            .Where(hit => !hit.Result.AlreadyDead)
            .Aggregate(0UL, (total, hit) => total + hit.Result.DamageApplied);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy KineticSpark passive monster proc " +
                $"{FormatSession(updatedContext with { Session = updatedSession })} spellId={spell.Id} " +
                $"spellLevel={playerSpell.Level} soulLevel={playerSpell.UseSpellSoul} primaryMonsterUid={primary.Monster.Uid} " +
                $"targets={animationTargets.Count} sourceExperienceDamage={primaryDamage} " +
                $"totalAppliedDamage={totalAppliedDamage}; source=KineticSpark.cs AttackSpell case 11590 monster branch.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<WorldSession> AddLegacyKineticSparkMonsterHitAsync(
        WorldPacketHandlerContext context,
        MonsterRuntimeStateStore monsterStates,
        WorldSession session,
        WorldDataCatalog worldData,
        LegacyMagicRuntimeSpell spell,
        LegacyPlayerMonsterMagicTarget target,
        DateTimeOffset now,
        int? remainingBudget,
        bool primary,
        List<LegacyKineticSparkMonsterHit> hits,
        List<LegacySpellAnimationTarget> animationTargets,
        CancellationToken cancellationToken)
    {
        (session, _) = await TrySendLegacyMonsterReceiveNinjaSageModeAsync(
                context with { Session = session },
                session,
                worldData,
                cancellationToken)
            .ConfigureAwait(false);

        var damageResult = CalculateLegacyKineticSparkMonsterRangeDamage(
            session,
            worldData,
            target.Monster,
            target.MonsterFamily,
            spell);
        var damage = damageResult.Damage;
        if (remainingBudget is { } budget)
        {
            damage = ScaleLegacyKineticSparkChainedDamage(damage, budget);
        }

        var result = ApplyLegacyPlayerMonsterDamage(monsterStates, target.Monster, damage, now, session);
        hits.Add(new LegacyKineticSparkMonsterHit(target, damage, result, damageResult.Effect, primary));
        if (!result.AlreadyDead)
        {
            animationTargets.Add(new LegacySpellAnimationTarget(
                target.Monster.Uid,
                result.RequestedDamage,
                Hit: true,
                damageResult.Effect));
        }

        return session;
    }

    private static bool IsLegacyKineticSparkPassiveProcAttackType(MsgAttackType attackType)
    {
        return attackType is MsgAttackType.Archer;
    }

    private static LegacyKineticSparkMonsterDamage CalculateLegacyKineticSparkMonsterRangeDamage(
        WorldSession session,
        WorldDataCatalog? worldData,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        LegacyMagicRuntimeSpell spell)
    {
        if (LegacyMonsterDamageRules.TakesOnePlayerDamage(monster, monsterFamily))
        {
            return new LegacyKineticSparkMonsterDamage(
                LegacyMonsterDamageRules.ApplyLegacyPhysicalSpellNonLethalClamp(
                    spell.Id,
                    monster.Hitpoints,
                    damage: 1),
                MsgAttackEffect.None);
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
            Math.Max(10, spell.Damage),
            CalculateLegacyPlayerMonsterPhysicalDamageBonusPercent(session, worldData, monster));
        damage = MulDivToUInt32(damage, spellDamagePercent, 100);
        var defense = ReadMonsterDefense(monsterFamily);
        damage = damage <= defense ? 0 : damage - defense;

        var defense2 = ReadAttributeUInt32(monsterFamily.Attributes, "defense2", "defence2", "Defense2", "Defence2");
        damage = BigMulDivToUInt32(damage, defense2, 10_000);
        damage = ApplyLegacyKineticSparkMonsterDodge(damage, monsterFamily);
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

        var postDefense2Result = CalculateLegacyKineticSparkMonsterPostDefense2Effects(
            damage,
            fields,
            session.Character);
        damage = postDefense2Result.Damage;
        if (!monster.Boss && HasLegacyStatusFlag(session.Character, LegacySupermanFlag))
        {
            damage = MulDivToUInt32(damage, 10, 1);
        }

        damage = ApplyLegacyDeathSighInfinityMonsterPhysicalDamage(
            damage,
            session.Character,
            activeSpell: true);

        return new LegacyKineticSparkMonsterDamage(
            LegacyMonsterDamageRules.ApplyLegacyPhysicalSpellNonLethalClamp(
                spell.Id,
                monster.Hitpoints,
                Math.Max(1, damage)),
            postDefense2Result.Effect);
    }

    private static LegacyPlayerMonsterPhysicalDamageCalculator.PostDefense2EffectResult CalculateLegacyKineticSparkMonsterPostDefense2Effects(
        uint damage,
        IReadOnlyList<uint> fields,
        CharacterSnapshot character)
    {
        var luckyStrikeRatePercent = fields[LegacyPlayerStatusPacket.LuckyStrikePercentIndex] / 100;
        var criticalStrike = fields[LegacyPlayerStatusPacket.CriticalStrikeIndex];
        var dashRate = fields[LegacyPlayerStatusPacket.DashRateIndex];
        var luckyStrikeRoll = ReadLegacyPhysicalEffectRoll(
            character,
            RuntimeLegacyPhysicalLuckyStrikeRollAttribute);
        var refineryRoll = ReadLegacyPhysicalEffectRoll(
            character,
            RuntimeLegacyPhysicalRefineryRollAttribute);
        var dashRoll = 99U;
        if (refineryRoll % 100 >= 40)
        {
            dashRoll = ReadLegacyPhysicalEffectRoll(
                character,
                RuntimeLegacyPhysicalDashRollAttribute);
        }

        return LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourcePostDefense2Effects(
            damage,
            luckyStrikeRatePercent,
            criticalStrike,
            dashRate,
            luckyStrikeRoll,
            refineryRoll,
            dashRoll);
    }

    private static uint ApplyLegacyKineticSparkMonsterDodge(uint damage, Monster monsterFamily)
    {
        var dodge = ReadAttributeUInt32(monsterFamily.Attributes, "Dodge", "dodge");
        var dodgeReduction = (uint)Math.Min(100, (int)(dodge * 4 / 10));
        return dodgeReduction >= 100
            ? 0
            : MulDivToUInt32(damage, 100 - dodgeReduction, 100);
    }

    private static bool CheckLegacyKineticSparkPassiveRateRoll(CharacterSnapshot character)
    {
        var roll = TryReadUInt32Attribute(
            character.Attributes,
            out var forcedRoll,
            RuntimeLegacyKineticSparkPassiveRateRollAttribute + ".11590",
            RuntimeLegacyKineticSparkPassiveRateRollAttribute + "11590",
            RuntimeLegacyKineticSparkPassiveRateRollAttribute)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return 30 > roll;
    }

    private static int GetLegacyKineticSparkTargetBudget(
        WorldSession session,
        WorldDataCatalog worldData)
    {
        if (!TryGetLegacyEquippedRuneLevel(session.PlayerItems, worldData, "StarRaid", out var itemLevel))
        {
            return 5;
        }

        return itemLevel switch
        {
            6 => 6,
            7 => 7,
            8 => 8,
            9 => 9,
            >= 10 and <= 26 => 10,
            27 => 12,
            _ => 5,
        };
    }

    private static uint ScaleLegacyKineticSparkChainedDamage(uint damage, int remainingBudget)
    {
        var scalePercent = 100 - (remainingBudget * 20);
        return scalePercent <= 0
            ? 0
            : MulDivToUInt32(damage, checked((uint)scalePercent), 100);
    }
}
