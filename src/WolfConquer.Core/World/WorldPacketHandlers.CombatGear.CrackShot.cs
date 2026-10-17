using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int LegacyCombatGearCrackShotSort = 104;
    private const uint LegacyCombatGearCrackShotTailExperience = 1_000;

    private static async ValueTask<bool> HandleLegacyCombatGearCrackShotMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Id != LegacyCombatGearCrackShotSpellId ||
            (int)spell.Sort != LegacyCombatGearCrackShotSort)
        {
            return false;
        }

        var playerTargetMatched = TryGetVisibleSessionByUid(
            context,
            session,
            query.OpponentUid,
            out var primaryTarget);
        var monsterTargetMatched = false;
        var primaryMonsterTarget = default(LegacyPlayerMonsterMagicTarget);
        if (!playerTargetMatched)
        {
            monsterTargetMatched = TryFindVisibleMonsterTargetByUid(
                context,
                session.Character,
                query.OpponentUid,
                out primaryMonsterTarget);
        }

        var sobNpcTargetMatched = false;
        SobNpc? primarySobNpcTarget = null;
        if (!playerTargetMatched && !monsterTargetMatched)
        {
            sobNpcTargetMatched = TryFindVisibleSobNpcByUid(
                context,
                session.Character,
                query.OpponentUid,
                out var sobNpcTarget);
            if (sobNpcTargetMatched)
            {
                primarySobNpcTarget = sobNpcTarget;
            }
        }

        if (!playerTargetMatched && !monsterTargetMatched && !sobNpcTargetMatched)
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

        if (!playerTargetMatched)
        {
            if (sobNpcTargetMatched)
            {
                return await HandleLegacyCombatGearCrackShotSobNpcMagicAsync(
                        context,
                        resourceSession,
                        query,
                        primarySobNpcTarget!,
                        playerSpell,
                        spell,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return await HandleLegacyCombatGearCrackShotMonsterMagicAsync(
                    context,
                    resourceSession,
                    primaryMonsterTarget,
                    playerSpell,
                    spell,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!CanLegacyPlayerMagicDamageTarget(context, resourceSession, primaryTarget.Session, spell, out var failureDiagnostic))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CombatGear CrackShot magic " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} " +
                    $"targetUid={primaryTarget.Session.Character.Uid}; {failureDiagnostic}.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                resourceSession,
                [new LegacyPlayerMagicTarget(primaryTarget)],
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var activeTarget = primaryTarget;
        (activeTarget, var wildPhoenixDefended) =
            await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                    context,
                    activeTarget,
                    cancellationToken)
                .ConfigureAwait(false);

        var baseDamage = wildPhoenixDefended
            ? 1U
            : CalculateLegacyPlayerPlayerPhysicalSpellDamage(
                updatedSession,
                activeTarget.Session,
                spell,
                context.WorldData,
                damageDivisor: 1);
        var damage = MulDivToUInt32(baseDamage, 30, 100);
        var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
            context,
            activeTarget,
            updatedSession.Character,
            damage,
            cancellationToken)
        .ConfigureAwait(false);
        if (receiveOutcome.AttackerSession is { } currentAttacker)
        {
            updatedSession = currentAttacker;
            context = context with { Session = updatedSession };
        }

        if (receiveOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
        {
            return true;
        }

        var result = receiveOutcome.Result;
        var selectedCharacter = result?.UpdatedSession.Character ?? activeTarget.Session.Character;
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            0,
            0,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            result is { } receivedResult
                ? [new LegacySpellAnimationTarget(
                    CheckedUInt32(receivedResult.UpdatedSession.Character.Uid, nameof(receivedResult.UpdatedSession.Character.Uid)),
                    receivedResult.DamageApplied, Hit: true, MsgAttackEffect.None)]
                : [],
            bombFlag: GetLegacyCombatGearTharnCutterActiveBombFlag(spell.Id, updatedSession.Character));
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        if (result is { } damageResult)
        {
            await SendLegacyPlayerMagicDamageUpdatesAsync(context, updatedSession, damageResult, cancellationToken)
                .ConfigureAwait(false);
        }

        if (context.WorldData is { } worldData)
        {
            updatedSession = await TryGrantLegacyCombatGearTharnCutterMissingSigilAsync(
                    context,
                    updatedSession,
                    worldData,
                    spell.Id,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var noJumpOutcome = await TrySendLegacyCombatGearTharnCutterNoJumpEffectAsync(
                context,
                updatedSession,
                CheckedUInt32(selectedCharacter.Uid, nameof(selectedCharacter.Uid)),
                CheckedUInt16(selectedCharacter.X, nameof(selectedCharacter.X)),
                CheckedUInt16(selectedCharacter.Y, nameof(selectedCharacter.Y)),
                cancellationToken,
                result is { } noJumpResult
                    ? noJumpResult.ActiveSession with { Session = noJumpResult.UpdatedSession }
                    : null)
            .ConfigureAwait(false);
        if (noJumpOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
        {
            return true;
        }

        updatedSession = noJumpOutcome.Session;
        context = context with { Session = updatedSession };


        updatedSession = await ApplyLegacyCombatGearCrackShotTailExperienceAsync(
                context with { Session = updatedSession },
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                LegacyCombatGearCrackShotTailExperience,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy CombatGear CrackShot magic " +
                $"{FormatSession(context with { Session = updatedSession })} targetUid={selectedCharacter.Uid} " +
                $"baseDamage={baseDamage} damage={result?.DamageApplied ?? 0}; " +
                "source=CombatGearAttack.cs case 18690.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> HandleLegacyCombatGearCrackShotMonsterMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession resourceSession,
        LegacyPlayerMonsterMagicTarget primaryMonsterTarget,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (context.MonsterStates is not { } monsterStates)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CombatGear CrackShot magic " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} " +
                    $"monsterUid={primaryMonsterTarget.Monster.Uid}; monster runtime store unavailable.")
                .ConfigureAwait(false);
            return true;
        }

        if (!CanLegacyPlayerAttackMonster(
                resourceSession.Character,
                primaryMonsterTarget.Monster,
                primaryMonsterTarget.MonsterFamily,
                out var failureDiagnostic))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CombatGear CrackShot magic " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} " +
                    $"monsterUid={primaryMonsterTarget.Monster.Uid}; {failureDiagnostic}.")
                .ConfigureAwait(false);
            return true;
        }

        var requestedDamage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
            resourceSession,
            context.WorldData,
            primaryMonsterTarget.Monster,
            primaryMonsterTarget.MonsterFamily,
            spell);
        requestedDamage = ApplyLegacyCombatGearWildPhoenixAttackEffect(
            resourceSession,
            spell,
            requestedDamage,
            LegacyWildPhoenixDamageTargetKind.MonsterOrSobNpc);
        var now = DateTimeOffset.UtcNow;
        var result = ApplyLegacyPlayerMonsterDamage(
            monsterStates,
            primaryMonsterTarget.Monster,
            requestedDamage,
            now,
            resourceSession);
        if (result.AlreadyDead)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CombatGear CrackShot magic " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} " +
                    $"monsterUid={primaryMonsterTarget.Monster.Uid}; target is waiting to respawn.")
                .ConfigureAwait(false);
            return true;
        }

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid)),
            0,
            0,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            [
                new LegacySpellAnimationTarget(
                    primaryMonsterTarget.Monster.Uid,
                    requestedDamage,
                    Hit: requestedDamage > 0,
                    MsgAttackEffect.None),
            ],
            bombFlag: GetLegacyCombatGearTharnCutterActiveBombFlag(spell.Id, resourceSession.Character));
        await SendPlayerSpellAnimationToVisibleAsync(context, resourceSession, animation, cancellationToken)
            .ConfigureAwait(false);

        var updatedSession = resourceSession;
        if (context.WorldData is { } worldData)
        {
            updatedSession = await TryGrantLegacyCombatGearTharnCutterMissingSigilAsync(
                    context,
                    updatedSession,
                    worldData,
                    spell.Id,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var noJumpOutcome = await TrySendLegacyCombatGearTharnCutterNoJumpEffectAsync(
                context,
                updatedSession,
                primaryMonsterTarget.Monster.Uid,
                primaryMonsterTarget.Monster.X,
                primaryMonsterTarget.Monster.Y,
                cancellationToken)
            .ConfigureAwait(false);
        if (noJumpOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
        {
            return true;
        }

        updatedSession = noJumpOutcome.Session;
        context = context with { Session = updatedSession };


        if (!result.Killed && LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(primaryMonsterTarget.Monster))
        {
            var damagedMonster = primaryMonsterTarget.Monster with { Hitpoints = result.RemainingHitpoints };
            foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
            {
                await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            }
        }

        if (result.Killed)
        {
            var death = new MsgAttackPacket(new MsgAttackQuery(
                Uid: CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
                OpponentUid: primaryMonsterTarget.Monster.Uid,
                X: primaryMonsterTarget.Monster.X,
                Y: primaryMonsterTarget.Monster.Y,
                AttackType: MsgAttackType.Death,
                Damage: 0,
                KilledMonster: 1,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0));
            await SendMonsterCombatFrameToVisibleAsync(
                    context,
                    updatedSession,
                    primaryMonsterTarget.Monster,
                    death,
                    cancellationToken)
                .ConfigureAwait(false);
            await TrySendLegacyMonsterDeathDropAsync(
                    context,
                    updatedSession,
                    primaryMonsterTarget.Monster,
                    primaryMonsterTarget.MonsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedSession = await TryApplyLegacyBossDeathRewardsAsync(
                    context,
                    updatedSession,
                    primaryMonsterTarget.Monster,
                    primaryMonsterTarget.MonsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
            await TryApplyLegacyMonsterTeamExperienceAsync(
                    context,
                    updatedSession,
                    primaryMonsterTarget.Monster,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await TrySendLegacyMonsterRetaliationAsync(
                    context,
                    updatedSession,
                    primaryMonsterTarget.Monster,
                    primaryMonsterTarget.MonsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        updatedSession = await TryApplyLegacyMonsterExperienceAsync(
                context,
                updatedSession,
                primaryMonsterTarget.Monster,
                primaryMonsterTarget.MonsterFamily,
                requestedDamage,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await ApplyLegacyCombatGearCrackShotTailExperienceAsync(
                context with { Session = updatedSession },
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                LegacyCombatGearCrackShotTailExperience,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy CombatGear CrackShot magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} monsterTargets=1 " +
                $"totalDamage={requestedDamage} totalAppliedDamage={result.DamageApplied} " +
                $"targetDamageAudit={primaryMonsterTarget.Monster.Uid}:{requestedDamage}/{result.DamageApplied}/" +
                $"{result.RemainingHitpoints}/{result.Killed}; source=CombatGearAttack.cs case 18690.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> HandleLegacyCombatGearCrackShotSobNpcMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession resourceSession,
        MsgAttackQuery query,
        SobNpc primarySobNpcTarget,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (context.SobNpcStates is not { } sobNpcStates)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CombatGear CrackShot magic " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} " +
                    $"sobNpcUid={primarySobNpcTarget.Id}; SOB runtime store unavailable.")
                .ConfigureAwait(false);
            return true;
        }

        if (!CanLegacySobNpcMagicDamageTarget(
                context,
                resourceSession.Character,
                primarySobNpcTarget,
                out var failureDiagnostic))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CombatGear CrackShot magic " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} " +
                    $"sobNpcUid={primarySobNpcTarget.Id}; source=CanAttackNpc.Verified; {failureDiagnostic}.")
                .ConfigureAwait(false);
            return true;
        }

        var distance = LegacyGetDistance(
            resourceSession.Character.X,
            resourceSession.Character.Y,
            primarySobNpcTarget.X,
            primarySobNpcTarget.Y);
        if (distance > spell.Range)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CombatGear CrackShot magic " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} " +
                    $"sobNpcUid={query.OpponentUid}; distance={distance} range={spell.Range}.")
                .ConfigureAwait(false);
            return true;
        }

        var requestedDamage = CalculateLegacySobNpcPhysicalSpellDamage(
            resourceSession,
            spell,
            context.WorldData,
            damageMultiplier: 1);
        requestedDamage = ApplyLegacyCombatGearWildPhoenixAttackEffect(
            resourceSession,
            spell,
            requestedDamage,
            LegacyWildPhoenixDamageTargetKind.MonsterOrSobNpc);

        var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(
            primarySobNpcTarget,
            context.WorldData?.StaticStatues);
        var receiveGearOutcome = await TrySendLegacySobNpcReceiveCombatGearUpdateAsync(
                context with { Session = resourceSession },
                resourceSession,
                primarySobNpcTarget,
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
        resourceSession = WithLegacySobNpcReceiveHitAttributes(context, resourceSession, primarySobNpcTarget);
        var damage = ApplyLegacySobNpcReceiveDamageModifiers(resourceSession, requestedDamage);
        var result = sobNpcStates.ApplyDamage(
            primarySobNpcTarget,
            damage,
            DateTimeOffset.UtcNow,
            deathBehavior);
        if (result.DamageApplied == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CombatGear CrackShot magic " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} " +
                    $"sobNpcUid={primarySobNpcTarget.Id}; selected SOB NPC had no applicable damage.")
                .ConfigureAwait(false);
            return true;
        }

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid)),
            0,
            0,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            [
                new LegacySpellAnimationTarget(
                    CheckedUInt32(primarySobNpcTarget.Id, nameof(primarySobNpcTarget.Id)),
                    result.DamageApplied,
                    Hit: true,
                    MsgAttackEffect.None),
            ],
            bombFlag: GetLegacyCombatGearTharnCutterActiveBombFlag(spell.Id, resourceSession.Character));
        await SendPlayerSpellAnimationToVisibleAsync(context, resourceSession, animation, cancellationToken)
            .ConfigureAwait(false);

        resourceSession = context.WorldData is { } worldData
            ? await TryGrantLegacyCombatGearTharnCutterMissingSigilAsync(
                    context,
                    resourceSession,
                    worldData,
                    spell.Id,
                    cancellationToken)
                .ConfigureAwait(false)
            : resourceSession;

        var noJumpOutcome = await TrySendLegacyCombatGearTharnCutterNoJumpEffectAsync(
                context,
                resourceSession,
                CheckedUInt32(primarySobNpcTarget.Id, nameof(primarySobNpcTarget.Id)),
                CheckedUInt16(primarySobNpcTarget.X, nameof(primarySobNpcTarget.X)),
                CheckedUInt16(primarySobNpcTarget.Y, nameof(primarySobNpcTarget.Y)),
                cancellationToken)
            .ConfigureAwait(false);
        if (noJumpOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
        {
            return true;
        }

        resourceSession = noJumpOutcome.Session;
        context = context with { Session = resourceSession };


        if (result.Killed && LegacySobNpcEventRules.IsTrainingSob(primarySobNpcTarget))
        {
            await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                    context,
                    resourceSession,
                    primarySobNpcTarget,
                    sobNpcStates.GetVisible(primarySobNpcTarget),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (!result.AlreadyDead && !LegacySobNpcEventRules.IsTrainingSob(primarySobNpcTarget))
        {
            await LogLegacySobNpcEventSideEffectGapAsync(context, primarySobNpcTarget, result, spell)
                .ConfigureAwait(false);
        }

        var progressDamage = CalculateLegacySobNpcExperienceDamage(primarySobNpcTarget, result);
        var updatedSession = await ApplyLegacyCombatGearCrackShotTailExperienceAsync(
                context with { Session = resourceSession },
                resourceSession,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                LegacyCombatGearCrackShotTailExperience,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                updatedSession,
                ClampLegacyProficiencyRawExperience(result.DamageApplied),
                cancellationToken,
                activity: "CombatGear CrackShot SOB NPC weapon proficiency experience")
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy CombatGear CrackShot magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sobNpcTargets=1 " +
                $"progressDamage={progressDamage} requestedDamage={requestedDamage} " +
                $"totalAppliedDamage={result.DamageApplied} " +
                $"targetDamageAudit={primarySobNpcTarget.Id}:{requestedDamage}/{result.DamageApplied}/" +
                $"{result.RemainingHitpoints}/{result.Killed}; source=CombatGearAttack.cs case 18690.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<WorldSession> ApplyLegacyCombatGearCrackShotTailExperienceAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var updatedSession = await ApplyLegacyExperienceGainAsync(
                context,
                context.Connection,
                session,
                LegacyCombatGearCrackShotTailExperience,
                cancellationToken)
            .ConfigureAwait(false);

        if (updatedSession.Character.Experience != session.Character.Experience ||
            updatedSession.Character.Level != session.Character.Level)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer sent legacy CombatGear CrackShot fixed tail experience " +
                    $"{FormatSession(context with { Session = updatedSession })} " +
                    $"baseGain={LegacyCombatGearCrackShotTailExperience} " +
                    $"experience={updatedSession.Character.Experience} level={updatedSession.Character.Level}; " +
                    "source=CombatGearAttack.cs case 18690 IncreaseExperience.Up(..., 1000).")
                .ConfigureAwait(false);
        }

        return updatedSession;
    }
}
