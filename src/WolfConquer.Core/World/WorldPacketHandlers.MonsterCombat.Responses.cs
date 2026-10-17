using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TrySendLegacyMonsterEagleEyeCountdownAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (context.WorldData is not { } worldData ||
            !IsLegacyEagleEyeCountDownActive(session.Character) ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                session.PlayerSpells,
                worldData,
                LegacyEagleEyeCountdownSpellId,
                out var playerSpell,
                out var spell) ||
            !TryReadLegacyMagicByteField(
                worldData,
                LegacyEagleEyeCountdownSpellId,
                playerSpell.Level,
                LegacyMagicRateField,
                out var rate) ||
            !CheckLegacyEagleEyeResponseRateRoll(session.Character, rate))
        {
            return session;
        }

        var uid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var animation = new LegacySpellAnimationPacket(
            uid,
            AttackedUid: uid,
            X: 0,
            Y: 0,
            LegacyEagleEyeCountdownSpellId,
            spell.Level,
            NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul,
            SpellEffect: 0,
            [new LegacySpellAnimationTarget(uid, LegacyEagleEyeCountdownSpellId, Hit: false, MsgAttackEffect.None)]);
        await context.Connection.SendAsync(animation.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(LegacyMagicColdTimePacket.ZeroEagleEye().ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        var updatedCharacter = WithCharacterAttribute(
            WithoutCharacterAttribute(
                WithoutCharacterAttribute(session.Character, "EagleEyeCountDown"),
                "EagleEyeCountdown"),
            RuntimeLegacyEagleEyeCountDownAttribute,
            "0");
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        await context.Log.WriteLineAsync(
                "WorldServer sent legacy monster EagleEye countdown response " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={LegacyEagleEyeCountdownSpellId} " +
                $"rate={rate}; source=MsgMonster.ActionHandler.CheckRespouseDamage EagleEyeCountDown.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TrySendLegacyMonsterRevengeTailResponseAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        var changes = ReadAttributeUInt32(
            session.Character.Attributes,
            RuntimeLegacyRevengeTailChangeAttribute,
            "RevengeTailChange");
        if (context.MonsterStates is not { } monsterStates ||
            context.WorldData is not { } worldData ||
            session.Character.MinHitPoints <= 0 ||
            !HasLegacyStatusFlag(session.Character, LegacyRevengeTailFlag) ||
            changes == 0 ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                session.PlayerSpells,
                worldData,
                LegacyRevengeTailSpellId,
                out var playerSpell,
                out var spell))
        {
            return session;
        }

        var damage = spell.Damage;
        var result = ApplyLegacyPlayerMonsterDamage(
            monsterStates,
            monster,
            damage,
            DateTimeOffset.UtcNow,
            session);
        if (result.AlreadyDead)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy monster RevengeTail response " +
                    $"{FormatSession(context)} monsterUid={monster.Uid}; monster is waiting to respawn.")
                .ConfigureAwait(false);
            return session;
        }

        var uid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            uid,
            CheckedUInt16(session.Character.X, nameof(session.Character.X)),
            CheckedUInt16(session.Character.Y, nameof(session.Character.Y)),
            LegacyRevengeTailSpellId,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            [new LegacySpellAnimationTarget(monster.Uid, damage, Hit: true, MsgAttackEffect.None)],
            bombFlag: 1);
        await SendPlayerSpellAnimationToVisibleAsync(context, session, animation, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Killed && LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(monster))
        {
            var updatedMonster = monster with { Hitpoints = result.RemainingHitpoints };
            foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(updatedMonster))
            {
                await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            }
        }

        if (result.Killed)
        {
            var death = new MsgAttackPacket(new MsgAttackQuery(
                Uid: uid,
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Death,
                Damage: 0,
                KilledMonster: 1,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0));
            await SendMonsterCombatFrameToVisibleAsync(context, session, monster, death, cancellationToken)
                .ConfigureAwait(false);
            await TrySendLegacyMonsterDeathDropAsync(context, session, monster, monsterFamily, cancellationToken)
                .ConfigureAwait(false);
            session = await TryApplyLegacyBossDeathRewardsAsync(
                    context,
                    session,
                    monster,
                    monsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
            await TryApplyLegacyMonsterTeamExperienceAsync(context, session, monster, cancellationToken)
                .ConfigureAwait(false);
        }

        session = await TryApplyLegacyMonsterExperienceAsync(
                context,
                session,
                monster,
                monsterFamily,
                damage,
                cancellationToken)
            .ConfigureAwait(false);

        var remainingChanges = changes - 1;
        var updatedCharacter = WithCharacterAttribute(
            session.Character,
            RuntimeLegacyRevengeTailChangeAttribute,
            remainingChanges.ToString(CultureInfo.InvariantCulture));
        var removedFlag = remainingChanges == 0 && HasLegacyStatusFlag(updatedCharacter, LegacyRevengeTailFlag);
        if (removedFlag)
        {
            updatedCharacter = WithoutLegacyStatusFlag(updatedCharacter, LegacyRevengeTailFlag);
        }

        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        if (removedFlag)
        {
            await SendPlayerStatusFlagVectorToVisibleAsync(context, updatedSession, cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer sent legacy monster RevengeTail response " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} " +
                $"spellId={LegacyRevengeTailSpellId} damage={result.DamageApplied} " +
                $"remainingHitpoints={result.RemainingHitpoints} changesRemaining={remainingChanges} " +
                $"removedFlag={removedFlag} killed={result.Killed}; " +
                "source=MsgMonster.ActionHandler.CheckRespouseDamage RevengeTail.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<LegacyMonsterResponseDamageResult> TrySendLegacyMonsterBackfireResponseAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (context.MonsterStates is not { } monsterStates ||
            session.Character.MinHitPoints <= 0 ||
            !HasLegacyStatusFlag(session.Character, LegacyBackfireFlag))
        {
            return new LegacyMonsterResponseDamageResult(session, Matched: false, InterruptsRetaliation: false);
        }

        var updatedSession = session with
        {
            Character = WithoutLegacyStatusFlag(session.Character, LegacyBackfireFlag),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        await SendPlayerStatusFlagVectorToVisibleAsync(context, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        if (HasLegacyStatusFlag(updatedSession.Character, LegacyShurikenVortexFlag))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer consumed legacy monster Backfire response without interrupting retaliation " +
                    $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid}; " +
                    "source=MsgMonster.ActionHandler.CheckRespouseDamage Backfire ShurikenVortex guard.")
                .ConfigureAwait(false);
            return new LegacyMonsterResponseDamageResult(updatedSession, Matched: true, InterruptsRetaliation: false);
        }

        if (context.WorldData is null ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                updatedSession.PlayerSpells,
                context.WorldData,
                LegacyBackfireSpellId,
                out _,
                out _))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer consumed legacy monster Backfire response without damage " +
                    $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} " +
                    $"spellId={LegacyBackfireSpellId}; learned spell was not available.")
                .ConfigureAwait(false);
            return new LegacyMonsterResponseDamageResult(updatedSession, Matched: true, InterruptsRetaliation: true);
        }

        var damage = MulDivToUInt32(
            CheckedUInt32(updatedSession.Character.MinHitPoints, nameof(updatedSession.Character.MinHitPoints)),
            55,
            100);
        var result = ApplyLegacyPlayerMonsterDamage(
            monsterStates,
            monster,
            damage,
            DateTimeOffset.UtcNow,
            updatedSession);
        if (result.AlreadyDead)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy monster Backfire response " +
                    $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid}; " +
                    "monster is waiting to respawn.")
                .ConfigureAwait(false);
            return new LegacyMonsterResponseDamageResult(updatedSession, Matched: true, InterruptsRetaliation: true);
        }

        var attack = new MsgAttackPacket(new MsgAttackQuery(
            Uid: CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            OpponentUid: monster.Uid,
            X: CheckedUInt16(updatedSession.Character.X, nameof(updatedSession.Character.X)),
            Y: CheckedUInt16(updatedSession.Character.Y, nameof(updatedSession.Character.Y)),
            AttackType: MsgAttackType.BackFire,
            Damage: checked((int)damage),
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0));
        await SendMonsterCombatFrameToVisibleAsync(context, updatedSession, monster, attack, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Killed && LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(monster))
        {
            var updatedMonster = monster with { Hitpoints = result.RemainingHitpoints };
            foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(updatedMonster))
            {
                await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            }
        }

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
            await SendMonsterCombatFrameToVisibleAsync(context, updatedSession, monster, death, cancellationToken)
                .ConfigureAwait(false);
            await TrySendLegacyMonsterDeathDropAsync(context, updatedSession, monster, monsterFamily, cancellationToken)
                .ConfigureAwait(false);
            updatedSession = await TryApplyLegacyBossDeathRewardsAsync(
                    context,
                    updatedSession,
                    monster,
                    monsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
            await TryApplyLegacyMonsterTeamExperienceAsync(context, updatedSession, monster, cancellationToken)
                .ConfigureAwait(false);
        }

        updatedSession = await TryApplyLegacyMonsterExperienceAsync(
                context,
                updatedSession,
                monster,
                monsterFamily,
                damage,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer sent legacy monster Backfire response " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} " +
                $"spellId={LegacyBackfireSpellId} damage={result.DamageApplied} " +
                $"remainingHitpoints={result.RemainingHitpoints} killed={result.Killed}; " +
                "source=MsgMonster.ActionHandler.CheckRespouseDamage Backfire.")
            .ConfigureAwait(false);
        return new LegacyMonsterResponseDamageResult(updatedSession, Matched: true, InterruptsRetaliation: true);
    }

    private static async ValueTask<WorldSession> TrySendLegacyMonsterCounterPunchResponseAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.Boss ||
            context.MonsterStates is not { } monsterStates ||
            context.WorldData is not { } worldData ||
            session.Character.MinHitPoints <= 0 ||
            !CheckLegacyMonsterCounterPunchRateRoll(session.Character) ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                session.PlayerSpells,
                worldData,
                LegacyCounterPunchSpellId,
                out var playerSpell,
                out _))
        {
            return session;
        }

        var damage = CalculateLegacyPlayerMonsterPhysicalFactorDamage(
            session,
            worldData,
            monster,
            monsterFamily,
            factor: 8);
        var result = ApplyLegacyPlayerMonsterDamage(
            monsterStates,
            monster,
            damage,
            DateTimeOffset.UtcNow,
            session);
        if (result.AlreadyDead)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy monster CounterPunch response " +
                    $"{FormatSession(context)} monsterUid={monster.Uid}; monster is waiting to respawn.")
                .ConfigureAwait(false);
            return session;
        }

        var uid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var animation = new LegacySpellAnimationPacket(
            monster.Uid,
            uid,
            X: 0,
            Y: 0,
            LegacyCounterPunchSpellId,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            LevelHu: 0,
            SpellEffect: 0,
            [new LegacySpellAnimationTarget(monster.Uid, result.DamageApplied, Hit: true, MsgAttackEffect.None)]);
        await SendMonsterSpellAnimationToVisibleAsync(context, session, monster, animation, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Killed && LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(monster))
        {
            var updatedMonster = monster with { Hitpoints = result.RemainingHitpoints };
            foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(updatedMonster))
            {
                await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            }
        }

        var updatedSession = session;
        if (result.Killed)
        {
            var death = new MsgAttackPacket(new MsgAttackQuery(
                Uid: uid,
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Death,
                Damage: 0,
                KilledMonster: 1,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0));
            await SendMonsterCombatFrameToVisibleAsync(context, session, monster, death, cancellationToken)
                .ConfigureAwait(false);
            await TrySendLegacyMonsterDeathDropAsync(context, session, monster, monsterFamily, cancellationToken)
                .ConfigureAwait(false);
            updatedSession = await TryApplyLegacyBossDeathRewardsAsync(
                    context,
                    updatedSession,
                    monster,
                    monsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
            await TryApplyLegacyMonsterTeamExperienceAsync(context, updatedSession, monster, cancellationToken)
                .ConfigureAwait(false);
        }

        updatedSession = await TryApplyLegacyMonsterExperienceAsync(
                context,
                updatedSession,
                monster,
                monsterFamily,
                result.DamageApplied,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer sent legacy monster CounterPunch response " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} " +
                $"spellId={LegacyCounterPunchSpellId} damage={result.DamageApplied} " +
                $"remainingHitpoints={result.RemainingHitpoints} killed={result.Killed}; " +
                "source=MsgMonster.ActionHandler.AttackingTarget CounterPunch.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static WorldSession WithLegacyThundercloudSight(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster)
    {
        var updatedSession = session with
        {
            Character = WithCharacterAttribute(
                session.Character,
                RuntimeLegacyThundercloudSightAttribute,
                monster.Uid.ToString(CultureInfo.InvariantCulture)),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        return updatedSession;
    }

    private static async ValueTask<LegacyMonsterResponseDamageResult> TrySendLegacyMonsterResponseDamageAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        session = await TrySendLegacyCombatGearUpdateTargetAsync(
                context,
                session,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = session };

        var combatGearDamage = await TrySendLegacyMonsterCombatGearDamageResponseAsync(context, session, monster)
            .ConfigureAwait(false);
        if (combatGearDamage.Matched)
        {
            return combatGearDamage;
        }

        var hellVortex = await TrySendLegacyMonsterHellVortexResponseAsync(
                context,
                session,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        if (hellVortex.Matched)
        {
            return hellVortex;
        }

        session = await TrySendLegacyMonsterEagleEyeCountdownAsync(context, session, cancellationToken)
            .ConfigureAwait(false);
        session = await TrySendLegacyMonsterRevengeTailResponseAsync(
                context,
                session,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);

        var backfire = await TrySendLegacyMonsterBackfireResponseAsync(
                context,
                session,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        if (backfire.Matched)
        {
            return backfire;
        }

        if (!CheckLegacyMonsterResponseRateRoll(session.Character))
        {
            return new LegacyMonsterResponseDamageResult(
                WithLegacyThundercloudSight(context, session, monster),
                Matched: false,
                InterruptsRetaliation: false);
        }

        var reflected = await TrySendLegacyMonsterReflectResponseAsync(
                context,
                session,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        if (reflected)
        {
            return new LegacyMonsterResponseDamageResult(session, Matched: true, InterruptsRetaliation: true);
        }

        var counterKill = await TrySendLegacyMonsterCounterKillResponseAsync(
                context,
                session,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        if (counterKill.Matched)
        {
            return counterKill;
        }

        return new LegacyMonsterResponseDamageResult(
            WithLegacyThundercloudSight(context, counterKill.Session, monster),
            Matched: false,
            InterruptsRetaliation: false);
    }
}
