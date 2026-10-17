using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryGetLegacyBossAoeSpellTargeting(
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        ushort spellId,
        out LegacyBossAoeSpellTargeting targeting)
    {
        var targetX = CheckedUInt16(session.Character.X, nameof(session.Character.X));
        var targetY = CheckedUInt16(session.Character.Y, nameof(session.Character.Y));
        if (spellId == LegacyBossSpellBranch.DizzyAreaSpellId)
        {
            targeting = new LegacyBossAoeSpellTargeting(
                monster.X,
                monster.Y,
                targetX,
                targetY,
                LegacyBossSpellBranch.AreaRadius);
            return true;
        }

        if ((IsLegacyMonster213883BossId(monster.MonsterId) ||
                monsterFamily.Id >= 0 && IsLegacyMonster213883BossId((uint)monsterFamily.Id)) &&
            IsLegacyMonster213883BossAoeSpellId(spellId))
        {
            targeting = new LegacyBossAoeSpellTargeting(
                targetX,
                targetY,
                targetX,
                targetY,
                spellId == 10_362 ? 10 : 18);
            return true;
        }

        if ((IsLegacyMonster3971BossId(monster.MonsterId) ||
                monsterFamily.Id >= 0 && IsLegacyMonster3971BossId((uint)monsterFamily.Id)) &&
            IsLegacyMonster3971BossAoeSpellId(spellId))
        {
            targeting = new LegacyBossAoeSpellTargeting(
                targetX,
                targetY,
                targetX,
                targetY,
                spellId == LegacyBoss3971SpellBranch.DizzyAreaSpellId
                    ? LegacyBoss3971SpellBranch.DizzyAreaRadius
                    : LegacyBoss3971SpellBranch.FrightenedAreaRadius);
            return true;
        }

        targeting = default;
        return false;
    }

    private static async ValueTask TrySendLegacyBossAoePhysicalSpellRetaliationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        ushort spellId,
        bool forcedIndex,
        LegacyBossAoeSpellTargeting targeting,
        CancellationToken cancellationToken)
    {
        var selectedTargets = SelectLegacyBossAoeTargets(context, session, monster, targeting);
        if (selectedTargets.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy boss area spell retaliation " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} spellId={spellId}; no targets in range.")
                .ConfigureAwait(false);
            return;
        }

        var spellTargets = new List<LegacySpellAnimationTarget>(selectedTargets.Count);
        var damageResults = new List<LegacyBossPlayerDamageResult>(selectedTargets.Count);
        foreach (var active in selectedTargets)
        {
            var damageResult = CalculateLegacyMonsterPhysicalDamage(
                monster,
                monsterFamily,
                active.Session,
                context.WorldData);
            var damage = damageResult.Damage;
            if (damage == 0)
            {
                continue;
            }

            var updatedTargetSession = await TryConsumeLegacyHawksAmbitionAsync(
                    context,
                    active.Connection,
                    active.Session,
                    damageResult.ConsumesHawksAmbition,
                    cancellationToken)
                .ConfigureAwait(false);
            var updatedActive = active with { Session = updatedTargetSession };
            var result = ApplyLegacyMonsterPhysicalDamageToPlayer(context, updatedActive, monster, damage);
            if (result.DamageApplied == 0)
            {
                continue;
            }

            damageResults.Add(result);
            spellTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(active.Session.Character.Uid, nameof(active.Session.Character.Uid)),
                damage,
                Hit: true,
                MsgAttackEffect.None));
        }

        if (spellTargets.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy boss area spell retaliation " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} spellId={spellId}; calculated damage is zero.")
                .ConfigureAwait(false);
            return;
        }

        var spell = new LegacySpellAnimationPacket(
            monster.Uid,
            AttackedUid: 0,
            targeting.AnimationX,
            targeting.AnimationY,
            spellId,
            SpellLevel: 0,
            NextSpell: 0,
            LevelHu: 0,
            SpellEffect: 0,
            spellTargets);

        var statusApplied = 0;
        foreach (var result in damageResults)
        {
            var updatedSession = await TryApplyLegacyMonsterHitActivePickRemovalAsync(
                    context,
                    result.ActiveSession.Connection,
                    result.UpdatedSession,
                    result.Damage,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedSession = await TryApplyLegacyMonsterHitSitInterruptionAsync(
                    context,
                    result.ActiveSession.Connection,
                    updatedSession,
                    result.Damage,
                    result.Killed,
                    cancellationToken)
                .ConfigureAwait(false);
            var updatedResult = result with
            {
                ActiveSession = result.ActiveSession with { Session = updatedSession },
                UpdatedSession = updatedSession,
            };

            updatedSession = await SendLegacyBossPlayerDamageUpdatesAsync(
                    context,
                    monster,
                    updatedResult,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedResult = updatedResult with
            {
                ActiveSession = updatedResult.ActiveSession with { Session = updatedSession },
                UpdatedSession = updatedSession,
            };

            var statusResult = await TryApplyLegacyBossStatusEffectAsync(
                    context,
                    updatedResult.ActiveSession.Connection,
                    updatedResult.UpdatedSession,
                    monster,
                    spellId,
                    session,
                    cancellationToken)
                .ConfigureAwait(false);
            if (statusResult.Applied)
            {
                statusApplied++;
            }
        }

        await SendMonsterSpellAnimationToVisibleAsync(context, session, monster, spell, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer sent legacy boss area physical spell retaliation " +
                $"{FormatSession(context)} monsterUid={monster.Uid} spellId={spellId} " +
                $"targets={spellTargets.Count} radius={targeting.Radius} " +
                $"forcedIndex={forcedIndex} statusApplied={statusApplied}.")
            .ConfigureAwait(false);
    }

    private static List<WorldActiveSession> SelectLegacyBossAoeTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        LegacyBossAoeSpellTargeting targeting)
    {
        var selected = new List<WorldActiveSession>();
        var candidates = context.Sessions?.Snapshot() ??
            [new WorldActiveSession(context.Connection, session)];
        foreach (var active in candidates)
        {
            var character = active.Session.Character;
            if (character.MinHitPoints <= 0 ||
                HasLegacyStatusFlag(character, LegacyDeadFlag) ||
                HasLegacyStatusFlag(character, LegacyGhostFlag) ||
                !CanSeeMonster(character, monster) ||
                LegacyGetDistance(
                    targeting.SelectionOriginX,
                    targeting.SelectionOriginY,
                    character.X,
                    character.Y) > targeting.Radius)
            {
                continue;
            }

            selected.Add(active);
        }

        return selected;
    }

    private static LegacyBossPlayerDamageResult ApplyLegacyMonsterPhysicalDamageToPlayer(
        WorldPacketHandlerContext context,
        WorldActiveSession target,
        LegacyMonsterInstance monster,
        uint damage)
    {
        var targetSession = target.Session;
        var currentHitpoints = CheckedUInt32(targetSession.Character.MinHitPoints, nameof(targetSession.Character.MinHitPoints));
        if (currentHitpoints == 0 || damage == 0)
        {
            return new LegacyBossPlayerDamageResult(target, targetSession, currentHitpoints, damage, 0, Killed: false);
        }

        var applied = Math.Min(damage, currentHitpoints);
        var killed = applied >= currentHitpoints;
        var updatedCharacter = targetSession.Character with
        {
            MinHitPoints = currentHitpoints - applied,
        };
        if (killed)
        {
            updatedCharacter = ApplyLegacyMonsterPlayerDeath(updatedCharacter, monster);
        }

        var updatedSession = targetSession with { Character = updatedCharacter };
        context.Sessions?.Set(target.Connection, updatedSession);
        return new LegacyBossPlayerDamageResult(
            target with { Session = updatedSession },
            updatedSession,
            currentHitpoints,
            damage,
            applied,
            killed);
    }

    private static async ValueTask<WorldSession> SendLegacyBossPlayerDamageUpdatesAsync(
        WorldPacketHandlerContext context,
        LegacyMonsterInstance monster,
        LegacyBossPlayerDamageResult result,
        CancellationToken cancellationToken)
    {
        var updatedSession = result.UpdatedSession;
        if (result.Killed)
        {
            updatedSession = await SendLegacyOwnedThundercloudDeathCleanupAsync(
                    context,
                    result.ActiveSession.Connection,
                    updatedSession,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var updatedCharacter = updatedSession.Character;
        var maxHitpoints = ComputeLegacyRuntimeMaxHitpoints(
            updatedCharacter,
            result.PreviousHitpoints,
            updatedSession.PlayerItems,
            updatedSession.CombatGear ?? CombatGearSnapshot.Empty,
            context.WorldData,
            updatedSession.AlternateEquipmentActive);
        foreach (var update in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(
            CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
            maxHitpoints,
            CheckedUInt32(updatedCharacter.MinHitPoints, nameof(updatedCharacter.MinHitPoints))))
        {
            await result.ActiveSession.Connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        if (!result.Killed)
        {
            return updatedSession;
        }

        await SendLegacyPlayerDeadTalkAsync(result.ActiveSession.Connection, cancellationToken)
            .ConfigureAwait(false);

        await SendPlayerStatusFlagVectorToVisibleAsync(
                context,
                result.ActiveSession.Connection,
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);

        var death = new MsgAttackPacket(new MsgAttackQuery(
            Uid: monster.Uid,
            OpponentUid: CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
            X: CheckedUInt16(updatedCharacter.X, nameof(updatedCharacter.X)),
            Y: CheckedUInt16(updatedCharacter.Y, nameof(updatedCharacter.Y)),
            AttackType: MsgAttackType.Death,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0));
        await SendFrameToPlayerAndVisiblePeersAsync(
                context,
                result.ActiveSession.Connection,
                updatedSession,
                death.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        return updatedSession;
    }
}
