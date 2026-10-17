using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool IsLegacyCounterKillActive(CharacterSnapshot character)
    {
        var raw = ReadAttributeString(character.Attributes, RuntimeLegacyCounterKillActiveAttribute);
        return raw.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            (ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value != 0);
    }

    private static bool IsLegacyCounterKillSpell(LegacyMagicRuntimeSpell spell)
    {
        return spell.Id == LegacyCounterKillSpellId && (int)spell.Sort == 29;
    }

    private static async ValueTask<LegacyMonsterResponseDamageResult> TrySendLegacyMonsterCounterKillResponseAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (context.MonsterStates is not { } monsterStates ||
            session.Character.MinHitPoints <= 0 ||
            !IsLegacyCounterKillActive(session.Character))
        {
            return new LegacyMonsterResponseDamageResult(session, Matched: false, InterruptsRetaliation: false);
        }

        if (HasLegacyStatusFlag(session.Character, LegacyShurikenVortexFlag))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy monster CounterKill response without interrupting retaliation " +
                    $"{FormatSession(context)} monsterUid={monster.Uid}; " +
                    "source=MsgMonster.ActionHandler.CheckRespouseDamage CounterKill ShurikenVortex guard.")
                .ConfigureAwait(false);
            return new LegacyMonsterResponseDamageResult(session, Matched: true, InterruptsRetaliation: false);
        }

        if (context.WorldData is null ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                session.PlayerSpells,
                context.WorldData,
                LegacyCounterKillSpellId,
                out _,
                out var spell) ||
            !IsLegacyCounterKillSpell(spell))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer consumed legacy monster CounterKill response without damage " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} spellId=6003; " +
                    "learned CounterKill spell was not available.")
                .ConfigureAwait(false);
            return new LegacyMonsterResponseDamageResult(session, Matched: true, InterruptsRetaliation: true);
        }

        var damage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
            session,
            context.WorldData,
            monster,
            monsterFamily,
            spell);
        var result = ApplyLegacyPlayerMonsterDamage(
            monsterStates,
            monster,
            damage,
            DateTimeOffset.UtcNow,
            session);
        if (result.AlreadyDead)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy monster CounterKill response " +
                    $"{FormatSession(context)} monsterUid={monster.Uid}; monster is waiting to respawn.")
                .ConfigureAwait(false);
            return new LegacyMonsterResponseDamageResult(session, Matched: true, InterruptsRetaliation: true);
        }

        var attack = new MsgAttackPacket(new MsgAttackQuery(
            Uid: CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            OpponentUid: monster.Uid,
            X: CheckedUInt16(session.Character.X, nameof(session.Character.X)),
            Y: CheckedUInt16(session.Character.Y, nameof(session.Character.Y)),
            AttackType: MsgAttackType.Scapegoat,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: damage,
            Effect: MsgAttackEffect.None,
            Unknown: 0));
        await SendMonsterCombatFrameToVisibleAsync(context, session, monster, attack, cancellationToken)
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
                Uid: CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
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

        await context.Log.WriteLineAsync(
                "WorldServer sent legacy monster CounterKill response " +
                $"{FormatSession(context with { Session = session })} monsterUid={monster.Uid} spellId=6003 " +
                $"responseDamage={damage} damageApplied={result.DamageApplied} " +
                $"remainingHitpoints={result.RemainingHitpoints} killed={result.Killed}; " +
                "source=MsgMonster.ActionHandler.CheckRespouseDamage CounterKill.")
            .ConfigureAwait(false);
        return new LegacyMonsterResponseDamageResult(session, Matched: true, InterruptsRetaliation: true);
    }
}
