using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const int LegacyScarOfEarthFlag = 180;
    internal const string RuntimeLegacyScarOfEarthRateRollAttribute = "LegacyScarOfEarthRateRoll";
    internal const string RuntimeLegacyScarOfEarthExpiresAtUtcAttribute = "LegacyScarOfEarthExpiresAtUtc";
    internal const string RuntimeLegacyScarOfEarthNextInvokeAtUtcAttribute = "LegacyScarOfEarthNextInvokeAtUtc";
    internal const string RuntimeLegacyScarOfEarthCasterUidAttribute = "LegacyScarOfEarthCasterUid";
    internal const string RuntimeLegacyScarOfEarthSpellIdAttribute = "LegacyScarOfEarthSpellId";
    internal const string RuntimeLegacyScarOfEarthSpellLevelAttribute = "LegacyScarOfEarthSpellLevel";
    internal const string RuntimeLegacyScarOfEarthDamage2Attribute = "LegacyScarOfEarthDamage2";

    private const ushort LegacyScarOfEarthSpellId = 12_670;
    private const byte LegacyScarOfEarthLineRange = 5;
    private const byte LegacyScarOfEarthLineWidth = 1;
    private static readonly TimeSpan LegacyScarOfEarthDuration = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan LegacyScarOfEarthInvokeInterval = TimeSpan.FromSeconds(2);

    private static async ValueTask<bool> HandleLegacyPlayerScarOfEarthMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.SectorPassive)
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

        if (spell.Id != LegacyScarOfEarthSpellId)
        {
            return true;
        }

        var resourceContext = context with { Session = resourceSession };
        var updatedSession = resourceSession;
        var monsterTargets = context.MonsterStates is not null
            ? SelectLegacyScarOfEarthMonsterTargets(resourceContext, updatedSession, query)
            : [];
        var playerTargets = SelectLegacyScarOfEarthPlayerTargets(resourceContext, updatedSession, query, spell);
        var sobNpcTargets = context.SobNpcStates is not null
            ? SelectLegacyScarOfEarthSobNpcTargets(resourceContext, updatedSession, query)
            : [];

        foreach (var target in monsterTargets)
        {
            if (!IsLegacyGuardMonsterFamily(target.Monster, target.MonsterFamily))
            {
                continue;
            }

            updatedSession = await TryApplyLegacyGuardAttackFlashingNameAsync(
                    resourceContext,
                    updatedSession,
                    cancellationToken)
                .ConfigureAwait(false);
            resourceContext = resourceContext with { Session = updatedSession };
            break;
        }

        updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                resourceContext,
                updatedSession,
                playerTargets,
                cancellationToken)
            .ConfigureAwait(false);
        resourceContext = resourceContext with { Session = updatedSession };

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var monsterResults = new List<(
            LegacyPlayerMonsterMagicTarget Target,
            MonsterDamageResult Result,
            uint Damage,
            ulong Progress,
            LegacyMonsterScarOfEarthApplyResult Scar)>();
        var playerResults = new List<(
            LegacyPlayerMagicDamageResult Result,
            uint Damage,
            int Chance,
            bool ScarApplied,
            bool ScarInserted)>();
        var sobNpcResults = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            uint Damage,
            ulong Progress)>();
        var killedTrainingSobNpcTargets = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var eventSobNpcSideEffectTargets = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            LegacySobNpcDeathBehavior DeathBehavior)>();
        var now = DateTimeOffset.UtcNow;
        var casterUid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));

        if (resourceContext.MonsterStates is { } monsterStates)
        {
            foreach (var target in monsterTargets)
            {
                var damage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                    updatedSession,
                    resourceContext.WorldData,
                    target.Monster,
                    target.MonsterFamily,
                    spell);
                damage = CalculateLegacyScarOfEarthSoulDamage(damage, playerSpell.UseSpellSoul);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    target.Monster,
                    damage,
                    now,
                    updatedSession);
                var progress = result.AlreadyDead
                    ? 0UL
                    : CalculateLegacyScarOfEarthMonsterProgress(updatedSession, target, damage);
                var scar = default(LegacyMonsterScarOfEarthApplyResult);
                if (!result.AlreadyDead && !result.Killed && result.RemainingHitpoints != 0)
                {
                    scar = monsterStates.ApplyScarOfEarth(
                        target.Monster,
                        LegacyScarOfEarthFlag,
                        LegacyScarOfEarthDuration,
                        LegacyScarOfEarthInvokeInterval,
                        now,
                        casterUid,
                        spell.Id,
                        spell.Level,
                        spell.Damage2);
                }

                monsterResults.Add((target, result, damage, progress, scar));
                if (result.AlreadyDead)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    target.Monster.Uid,
                    damage,
                    Hit: true,
                    MsgAttackEffect.None));
            }
        }

        foreach (var target in playerTargets)
        {
            var activeTarget = target.ActiveSession;
            (activeTarget, var wildPhoenixDefended) =
                await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                        resourceContext,
                        activeTarget,
                        cancellationToken)
                    .ConfigureAwait(false);
            var physicalDamage = wildPhoenixDefended
                ? new LegacyPlayerPlayerPhysicalSpellDamageResult(1U, MsgAttackEffect.None)
                : CalculateLegacyPlayerPlayerPhysicalSpellDamageResult(
                    updatedSession,
                    activeTarget.Session,
                    spell,
                    resourceContext.WorldData,
                    damageDivisor: 1);
            var damage = CalculateLegacyScarOfEarthSoulDamage(
                    physicalDamage.Damage,
                    playerSpell.UseSpellSoul) /
                2;
            var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                resourceContext,
                activeTarget,
                updatedSession.Character,
                damage,
                cancellationToken)
            .ConfigureAwait(false);
            if (receiveOutcome.AttackerSession is { } currentReceiveAttacker)
            {
                updatedSession = currentReceiveAttacker;
                resourceContext = resourceContext with { Session = updatedSession };
            }

            if (receiveOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return true;
            }

            if (receiveOutcome.Result is not { } result)
            {
                continue;
            }
            if (result.UpdatedAttackerSession is { } updatedAttacker)
            {
                updatedSession = updatedAttacker;
                resourceContext = resourceContext with { Session = updatedSession };
            }

            var chance = unchecked(100 - (5 * (
                CalculateLegacyPitchingBattlePower(result.UpdatedSession) -
                CalculateLegacyPitchingBattlePower(updatedSession))));
            var scarApplied = false;
            var scarInserted = false;
            if (!result.Killed &&
                result.UpdatedSession.Character.MinHitPoints > 0 &&
                CheckLegacyScarOfEarthRateRoll(updatedSession.Character, chance))
            {
                var scarApply = ApplyLegacyScarOfEarthToPlayer(
                    result.UpdatedSession,
                    casterUid,
                    spell,
                    now);
                var scarSession = scarApply.Session;
                scarInserted = scarApply.Inserted;
                resourceContext.Sessions?.Set(result.ActiveSession.Connection, scarSession);
                result = result with
                {
                    ActiveSession = result.ActiveSession with { Session = scarSession },
                    UpdatedSession = scarSession,
                };
                scarApplied = true;
            }
            else if (result.Killed && HasLegacyScarOfEarthState(result.UpdatedSession.Character))
            {
                var clearedSession = result.UpdatedSession with
                {
                    Character = WithoutLegacyScarOfEarthState(result.UpdatedSession.Character),
                };
                resourceContext.Sessions?.Set(result.ActiveSession.Connection, clearedSession);
                result = result with
                {
                    ActiveSession = result.ActiveSession with { Session = clearedSession },
                    UpdatedSession = clearedSession,
                };
            }

            playerResults.Add((result, damage, chance, scarApplied, scarInserted));
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                damage,
                Hit: false,
                physicalDamage.Effect));
        }

        if (resourceContext.SobNpcStates is { } sobNpcStates)
        {
            foreach (var target in sobNpcTargets)
            {
                var damage = CalculateLegacyAttackStatus7000SobNpcRangeDamage(
                    updatedSession,
                    spell,
                    resourceContext.WorldData);
                damage = CalculateLegacyScarOfEarthSoulDamage(damage, playerSpell.UseSpellSoul);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc);
                var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                var progress = result.AlreadyDead
                    ? 0UL
                    : CalculateLegacySobNpcExperienceDamage(target.SobNpc, result);
                sobNpcResults.Add((target.SobNpc, result, damage, progress));
                if (result.AlreadyDead)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id)),
                    damage,
                    Hit: true,
                    MsgAttackEffect.None));

                if (result.DamageApplied == 0)
                {
                    continue;
                }

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

        foreach (var (target, result, _, _, scar) in monsterResults)
        {
            if (result.AlreadyDead)
            {
                continue;
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
                await SendMonsterCombatFrameToVisibleAsync(
                        resourceContext,
                        updatedSession,
                        target.Monster,
                        death,
                        cancellationToken)
                    .ConfigureAwait(false);
                await TrySendLegacyMonsterDeathDropAsync(
                        resourceContext,
                        updatedSession,
                        target.Monster,
                        target.MonsterFamily,
                        cancellationToken)
                    .ConfigureAwait(false);
                updatedSession = await TryApplyLegacyBossDeathRewardsAsync(
                        resourceContext,
                        updatedSession,
                        target.Monster,
                        target.MonsterFamily,
                        cancellationToken)
                    .ConfigureAwait(false);
                resourceContext = resourceContext with { Session = updatedSession };
                await TryApplyLegacyMonsterTeamExperienceAsync(
                        resourceContext,
                        updatedSession,
                        target.Monster,
                        cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(target.Monster))
            {
                var damagedMonster = target.Monster with { Hitpoints = result.RemainingHitpoints };
                foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
                {
                    await resourceContext.Connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            if (scar.Applied && resourceContext.MonsterStates is { } currentMonsterStates)
            {
                var statusFlags = currentMonsterStates.CreateStatusFlagVector(
                    target.Monster,
                    now,
                    LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength);
                await SendMonsterStatusFlagVectorToVisibleAsync(
                        resourceContext,
                        updatedSession,
                        target.Monster,
                        statusFlags,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        foreach (var (result, _, _, _, scarInserted) in playerResults)
        {
            await SendLegacyPlayerMagicPreAnimationUpdatesAsync(
                    resourceContext,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyPlayerMagicDamageUpdatesAsync(
                    resourceContext,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
            if (scarInserted)
            {
                await SendPlayerStatusFlagVectorToVisibleAsync(
                        resourceContext,
                        result.ActiveSession.Connection,
                        result.UpdatedSession,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (resourceContext.SobNpcStates is { } currentSobNpcStates)
        {
            foreach (var killed in killedTrainingSobNpcTargets)
            {
                await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                        resourceContext,
                        updatedSession,
                        killed.SobNpc,
                        currentSobNpcStates.GetVisible(killed.SobNpc),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            foreach (var (sobNpc, result, _) in eventSobNpcSideEffectTargets)
            {
                await LogLegacySobNpcEventSideEffectGapAsync(resourceContext, sobNpc, result, spell)
                    .ConfigureAwait(false);
            }
        }

        var monsterProgress = monsterResults.Aggregate(0UL, (total, result) => total + result.Progress);
        var sobNpcProgress = sobNpcResults.Aggregate(0UL, (total, result) => total + result.Progress);
        var progressDamage = monsterProgress + sobNpcProgress;
        updatedSession = await ApplyLegacyScarOfEarthIncreaseExperienceAsync(
                resourceContext,
                updatedSession,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        resourceContext = resourceContext with { Session = updatedSession };
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                resourceContext,
                updatedSession,
                playerSpell,
                spell,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        resourceContext = resourceContext with { Session = updatedSession };

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(
                resourceContext,
                updatedSession,
                animation,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy Scar of Earth magic " +
                $"{FormatSession(resourceContext)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetX={query.X} targetY={query.Y} range={LegacyScarOfEarthLineRange} " +
                $"width={LegacyScarOfEarthLineWidth} increasedRange=true targets={animationTargets.Count} " +
                $"monsterTargets={monsterResults.Count} playerTargets={playerResults.Count} " +
                $"sobNpcTargets={sobNpcResults.Count} monsterProgress={monsterProgress} " +
                $"sobNpcProgress={sobNpcProgress} progressDamage={progressDamage}; " +
                "source=matching DecompiledSource SectorPasive.cs spell 12670 sort 82.")
            .ConfigureAwait(false);
        return true;
    }

    internal static async ValueTask<bool> TrySendDueLegacyPlayerScarOfEarthTickAsync(
        WorldPacketHandlerContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (context.Session is null || context.Sessions is not { } sessions)
        {
            return false;
        }

        if (!sessions.TryGet(context.Connection, out var currentSession) || currentSession is null)
        {
            return false;
        }

        var character = currentSession.Character;
        if (!HasLegacyStatusFlag(character, LegacyScarOfEarthFlag))
        {
            if (HasLegacyScarOfEarthMetadata(character))
            {
                sessions.Set(
                    context.Connection,
                    currentSession with { Character = WithoutLegacyScarOfEarthMetadata(character) });
            }

            return false;
        }

        var isDead = character.MinHitPoints <= 0 ||
            HasLegacyStatusFlag(character, LegacyDeadFlag) ||
            HasLegacyStatusFlag(character, LegacyGhostFlag);
        var hasExpiry = TryReadLegacyScarOfEarthTimestamp(
            character,
            RuntimeLegacyScarOfEarthExpiresAtUtcAttribute,
            out var expiresAtUtc);
        var expired = !hasExpiry || now >= expiresAtUtc;
        var due = TryReadLegacyScarOfEarthTimestamp(
                character,
                RuntimeLegacyScarOfEarthNextInvokeAtUtcAttribute,
                out var nextInvokeAtUtc) &&
            now >= nextInvokeAtUtc;
        var hasTickState = TryReadLegacyPlayerScarOfEarthState(character, out var scar);

        var tickSession = currentSession;
        if (isDead || expired)
        {
            tickSession = currentSession with
            {
                Character = WithoutLegacyScarOfEarthState(character),
            };
            sessions.Set(context.Connection, tickSession);
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    context with { Session = tickSession },
                    context.Connection,
                    tickSession,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (due)
        {
            tickSession = currentSession with
            {
                Character = WithCharacterAttribute(
                    character,
                    RuntimeLegacyScarOfEarthNextInvokeAtUtcAttribute,
                    (now + LegacyScarOfEarthInvokeInterval).ToString("O", CultureInfo.InvariantCulture)),
            };
            sessions.Set(context.Connection, tickSession);
        }

        // ThreadPool.cs removes the expired player flag, then independently processes this captured flag.
        if (isDead || !due || !hasTickState)
        {
            return isDead || expired;
        }

        if (!TryGetLegacyScarOfEarthCaster(sessions, scar.CasterUid, out var caster))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped due legacy Scar of Earth player tick " +
                    $"victimUid={tickSession.Character.Uid} casterUid={scar.CasterUid}; " +
                    "original caster is not active, so cadence advanced without an offline attacker model.")
                .ConfigureAwait(false);
            return true;
        }

        var casterContext = context with
        {
            Connection = caster.Connection,
            Session = caster.Session,
        };
        var victim = new WorldActiveSession(context.Connection, tickSession);
        var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
            casterContext,
            victim,
            caster.Session.Character,
            scar.Damage,
            cancellationToken)
        .ConfigureAwait(false);
        if (receiveOutcome.Result is not { } result)
        {
            // This victim's cadence is already consumed; continue the global actor loop.
            return true;
        }
        if (result.Killed && HasLegacyScarOfEarthState(result.UpdatedSession.Character))
        {
            var clearedSession = result.UpdatedSession with
            {
                Character = WithoutLegacyScarOfEarthState(result.UpdatedSession.Character),
            };
            sessions.Set(context.Connection, clearedSession);
            result = result with
            {
                ActiveSession = result.ActiveSession with { Session = clearedSession },
                UpdatedSession = clearedSession,
            };
        }

        var attackerSession = receiveOutcome.AttackerSession ?? result.UpdatedAttackerSession ?? caster.Session;
        await SendLegacyPlayerMagicPreAnimationUpdatesAsync(
                casterContext with { Session = attackerSession },
                attackerSession,
                result,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyPlayerMagicDamageUpdatesAsync(
                casterContext with { Session = attackerSession },
                attackerSession,
                result,
                cancellationToken)
            .ConfigureAwait(false);

        var victimSession = result.UpdatedSession;
        var victimUid = CheckedUInt32(victimSession.Character.Uid, nameof(victimSession.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            victimUid,
            CheckedUInt16(victimSession.Character.X, nameof(victimSession.Character.X)),
            CheckedUInt16(victimSession.Character.Y, nameof(victimSession.Character.Y)),
            scar.SpellId,
            scar.SpellLevel,
            levelHu: 0,
            targets: [new LegacySpellAnimationTarget(
                victimUid,
                scar.Damage,
                Hit: true,
                MsgAttackEffect.None)],
            bombFlag: 1);
        await SendPlayerSpellAnimationToVisibleAsync(
                context with { Session = victimSession },
                victimSession,
                animation,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    internal static async ValueTask<bool> TrySendDueLegacyMonsterScarOfEarthTickAsync(
        WorldPacketHandlerContext context,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        LegacyMonsterScarOfEarthState scar,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } casterSession || context.MonsterStates is not { } monsterStates)
        {
            return false;
        }

        var result = ApplyLegacyPlayerMonsterDamage(
            monsterStates,
            monster,
            scar.Damage,
            now,
            casterSession);
        if (result.AlreadyDead)
        {
            return false;
        }

        var updatedSession = casterSession;
        if (result.Killed)
        {
            var death = new MsgAttackPacket(new MsgAttackQuery(
                Uid: CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Death,
                Damage: 0,
                KilledMonster: 1,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0));
            await SendMonsterCombatFrameToVisibleAsync(
                    context,
                    updatedSession,
                    monster,
                    death,
                    cancellationToken)
                .ConfigureAwait(false);
            await TrySendLegacyMonsterDeathDropAsync(
                    context,
                    updatedSession,
                    monster,
                    monsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedSession = await TryApplyLegacyBossDeathRewardsAsync(
                    context,
                    updatedSession,
                    monster,
                    monsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = updatedSession };
            await TryApplyLegacyMonsterTeamExperienceAsync(
                    context,
                    updatedSession,
                    monster,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(monster))
        {
            var damagedMonster = monster with { Hitpoints = result.RemainingHitpoints };
            foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
            {
                await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            monster.Uid,
            monster.X,
            monster.Y,
            scar.SpellId,
            scar.SpellLevel,
            levelHu: 0,
            targets: [new LegacySpellAnimationTarget(
                monster.Uid,
                scar.Damage,
                Hit: true,
                MsgAttackEffect.None)],
            bombFlag: 1);
        await SendMonsterSpellAnimationToVisibleAsync(
                context,
                updatedSession,
                monster,
                animation,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static (WorldSession Session, bool Inserted) ApplyLegacyScarOfEarthToPlayer(
        WorldSession target,
        uint casterUid,
        LegacyMagicRuntimeSpell spell,
        DateTimeOffset now)
    {
        var inserted = !HasLegacyStatusFlag(target.Character, LegacyScarOfEarthFlag);
        var character = inserted
            ? WithLegacyStatusFlag(target.Character, LegacyScarOfEarthFlag)
            : target.Character;
        if (inserted || !character.Attributes.ContainsKey(RuntimeLegacyScarOfEarthExpiresAtUtcAttribute))
        {
            character = WithCharacterAttribute(
                character,
                RuntimeLegacyScarOfEarthExpiresAtUtcAttribute,
                (now + LegacyScarOfEarthDuration).ToString("O", CultureInfo.InvariantCulture));
        }

        if (inserted || !character.Attributes.ContainsKey(RuntimeLegacyScarOfEarthNextInvokeAtUtcAttribute))
        {
            character = WithCharacterAttribute(
                character,
                RuntimeLegacyScarOfEarthNextInvokeAtUtcAttribute,
                (now + LegacyScarOfEarthInvokeInterval).ToString("O", CultureInfo.InvariantCulture));
        }

        character = WithCharacterAttribute(character, RuntimeLegacyScarOfEarthCasterUidAttribute, casterUid);
        character = WithCharacterAttribute(character, RuntimeLegacyScarOfEarthSpellIdAttribute, spell.Id);
        character = WithCharacterAttribute(character, RuntimeLegacyScarOfEarthSpellLevelAttribute, spell.Level);
        character = WithCharacterAttribute(character, RuntimeLegacyScarOfEarthDamage2Attribute, spell.Damage2);
        return (target with { Character = character }, inserted);
    }

    private static CharacterSnapshot WithoutLegacyScarOfEarthState(CharacterSnapshot character)
    {
        return WithoutLegacyScarOfEarthMetadata(
            WithoutLegacyStatusFlag(character, LegacyScarOfEarthFlag));
    }

    private static CharacterSnapshot WithoutLegacyScarOfEarthMetadata(CharacterSnapshot character)
    {
        character = WithoutCharacterAttribute(character, RuntimeLegacyScarOfEarthExpiresAtUtcAttribute);
        character = WithoutCharacterAttribute(character, RuntimeLegacyScarOfEarthNextInvokeAtUtcAttribute);
        character = WithoutCharacterAttribute(character, RuntimeLegacyScarOfEarthCasterUidAttribute);
        character = WithoutCharacterAttribute(character, RuntimeLegacyScarOfEarthSpellIdAttribute);
        character = WithoutCharacterAttribute(character, RuntimeLegacyScarOfEarthSpellLevelAttribute);
        return WithoutCharacterAttribute(character, RuntimeLegacyScarOfEarthDamage2Attribute);
    }

    private static bool HasLegacyScarOfEarthState(CharacterSnapshot character)
    {
        return HasLegacyStatusFlag(character, LegacyScarOfEarthFlag) ||
            HasLegacyScarOfEarthMetadata(character);
    }

    private static bool HasLegacyScarOfEarthMetadata(CharacterSnapshot character)
    {
        return character.Attributes.ContainsKey(RuntimeLegacyScarOfEarthExpiresAtUtcAttribute) ||
            character.Attributes.ContainsKey(RuntimeLegacyScarOfEarthNextInvokeAtUtcAttribute) ||
            character.Attributes.ContainsKey(RuntimeLegacyScarOfEarthCasterUidAttribute) ||
            character.Attributes.ContainsKey(RuntimeLegacyScarOfEarthSpellIdAttribute) ||
            character.Attributes.ContainsKey(RuntimeLegacyScarOfEarthSpellLevelAttribute) ||
            character.Attributes.ContainsKey(RuntimeLegacyScarOfEarthDamage2Attribute);
    }

    private static bool TryReadLegacyPlayerScarOfEarthState(
        CharacterSnapshot character,
        out LegacyPlayerScarOfEarthState state)
    {
        state = default;
        if (!TryReadUInt32Attribute(
                character.Attributes,
                out var casterUid,
                RuntimeLegacyScarOfEarthCasterUidAttribute) ||
            !TryReadUInt32Attribute(
                character.Attributes,
                out var spellId,
                RuntimeLegacyScarOfEarthSpellIdAttribute) ||
            spellId > ushort.MaxValue ||
            !TryReadUInt32Attribute(
                character.Attributes,
                out var spellLevel,
                RuntimeLegacyScarOfEarthSpellLevelAttribute) ||
            spellLevel > byte.MaxValue ||
            !TryReadUInt32Attribute(
                character.Attributes,
                out var damage,
                RuntimeLegacyScarOfEarthDamage2Attribute))
        {
            return false;
        }

        state = new LegacyPlayerScarOfEarthState(
            casterUid,
            (ushort)spellId,
            (byte)spellLevel,
            damage);
        return true;
    }

    private static bool TryReadLegacyScarOfEarthTimestamp(
        CharacterSnapshot character,
        string attribute,
        out DateTimeOffset timestamp)
    {
        timestamp = default;
        return character.Attributes.TryGetValue(attribute, out var raw) &&
            DateTimeOffset.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out timestamp);
    }

    private static bool TryGetLegacyScarOfEarthCaster(
        WorldSessionStore sessions,
        uint casterUid,
        out WorldActiveSession caster)
    {
        foreach (var candidate in sessions.Snapshot())
        {
            if (candidate.Session.Character.Uid != casterUid ||
                !sessions.TryGet(candidate.Connection, out var current) ||
                current is null)
            {
                continue;
            }

            caster = candidate with { Session = current };
            return true;
        }

        caster = default!;
        return false;
    }

    private static bool CheckLegacyScarOfEarthRateRoll(CharacterSnapshot caster, int chance)
    {
        var roll = TryReadUInt32Attribute(
                caster.Attributes,
                out var forcedRoll,
                RuntimeLegacyScarOfEarthRateRollAttribute,
                "ScarOfEarthRateRoll")
            ? checked((int)(forcedRoll % 100))
            : Random.Shared.Next(0, 100);
        return chance > roll;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyScarOfEarthMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (IsLegacyMortalStrikeInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.Monster.X,
                    candidate.Monster.Y,
                    LegacyScarOfEarthLineRange,
                    LegacyScarOfEarthLineWidth))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyScarOfEarthPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (IsLegacyMortalStrikeInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    character.X,
                    character.Y,
                    LegacyScarOfEarthLineRange,
                    LegacyScarOfEarthLineWidth))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyScarOfEarthSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (IsLegacyMortalStrikeInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y,
                    LegacyScarOfEarthLineRange,
                    LegacyScarOfEarthLineWidth))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static ulong CalculateLegacyScarOfEarthMonsterProgress(
        WorldSession session,
        LegacyPlayerMonsterMagicTarget target,
        uint requestedDamage)
    {
        if (requestedDamage == 0 || LegacyMonsterDamageRules.IsLegacyGuardFamily(target.MonsterFamily))
        {
            return 0;
        }

        return checked((uint)AdjustLegacyMonsterExperience(
            checked((int)Math.Min(requestedDamage, target.Monster.MaxLife)),
            CheckedInt32(session.Character.Level, nameof(session.Character.Level)),
            target.Monster.Level));
    }

    private static async ValueTask<WorldSession> ApplyLegacyScarOfEarthIncreaseExperienceAsync(
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
        var updatedSession = ApplyLegacyExperienceSideEffects(
            context,
            context.Connection,
            session,
            experienceResult);
        if (experienceResult.AppliedExperienceGain == 0)
        {
            return updatedSession;
        }

        return await ApplyLegacyFinalExperienceGainAsync(
                context,
                context.Connection,
                updatedSession,
                experienceResult.AppliedExperienceGain,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static uint CalculateLegacyScarOfEarthSoulDamage(uint damage, byte useSpellSoul)
    {
        // Base.CalculateSoul is an identity transform in the matching source.
        _ = useSpellSoul;
        return damage;
    }

    private readonly record struct LegacyPlayerScarOfEarthState(
        uint CasterUid,
        ushort SpellId,
        byte SpellLevel,
        uint Damage);
}
