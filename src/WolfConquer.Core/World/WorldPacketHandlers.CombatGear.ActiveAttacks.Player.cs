using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static readonly LegacyMagicRuntimeSpell LegacyCombatGearPlayerCallerCanAttackGateSpell = new(
        0,
        LegacyMagicSort.Attack,
        "CombatGearPlayerCallerCanAttackGate",
        Level: 0,
        UseMana: 0,
        Damage: 0,
        Experience: 0,
        MaxTargets: 0,
        Range: checked((byte)LegacyArcherDirectTargetRange),
        NeedLevel: 0,
        WeaponType: 0,
        UseStamina: 0,
        CoolDown: 0,
        ColdTime: 0);

    private static async ValueTask<bool> TryHandleLegacyCombatGearPlayerAttackReplacementAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (packet.Query.AttackType != MsgAttackType.Archer ||
            context.WorldData is not { } worldData ||
            !HasLegacyCombatGearActiveAttackSource(session) ||
            !TryGetVisibleSessionByUid(context, session, packet.Query.OpponentUid, out var primary))
        {
            return false;
        }

        var primaryCharacter = primary.Session.Character;
        var targetX = CheckedUInt16(primaryCharacter.X, nameof(primaryCharacter.X));
        var targetY = CheckedUInt16(primaryCharacter.Y, nameof(primaryCharacter.Y));
        if (!await TryPassLegacyArcherPlayerCallerCanAttackAsync(
                context,
                session,
                primary,
                LegacyCombatGearPlayerCallerCanAttackGateSpell,
                "CombatGear active player attack",
                cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyCombatGearArrowBladesCallerAsync(
                context, session, packet.Query with { X = targetX, Y = targetY }, cancellationToken)
            .ConfigureAwait(false)) return true;

        if (!TryResolveLegacyCombatGearActiveAttackSpell(
                session,
                worldData,
                LegacyCombatGearActiveAttackTarget.FromPlayer(primaryCharacter),
                out var playerSpell,
                out var spell))
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

        var query = packet.Query with
        {
            AttackType = MsgAttackType.Magic,
            OpponentUid = CheckedUInt32(primaryCharacter.Uid, nameof(primaryCharacter.Uid)),
            Damage = 0,
            KilledMonster = 0,
            ResponseDamage = 0,
            Effect = MsgAttackEffect.None,
            SpellId = spell.Id,
            SpellLevel = playerSpell.Level,
            X = targetX,
            Y = targetY,
        };
        if (TryGetLegacyCombatGearPirateFloorDamagePassiveEffect(
                spell.Id,
                out var floorDamageItemId,
                out var floorDamageColor,
                out var floorDamageDurationMilliseconds,
                out var floorDamageInvokeDelayMilliseconds))
        {
            await SendLegacyCombatGearPirateFloorPassiveEffectAsync(
                    context,
                    resourceSession,
                    query,
                    spell,
                    floorDamageItemId,
                    floorDamageColor,
                    floorDamageDurationMilliseconds,
                    floorDamageInvokeDelayMilliseconds,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (await TryHandleLegacyCombatGearPirateFloorPassiveAsync(
                    context,
                    resourceSession,
                    query,
                    playerSpell,
                    spell,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return true;
        }

        var targets = SelectLegacyCombatGearAttackPlayerTargets(context, resourceSession, primary, query, spell);
        if (targets.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CombatGear active player attack " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} " +
                    $"playerUid={primaryCharacter.Uid}; no visible player matched legacy shape.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                resourceSession,
                targets,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var results = new List<LegacyPlayerMagicDamageResult>();
        var animationTargets = new List<LegacySpellAnimationTarget>();
        foreach (var target in targets)
        {
            var damage = CalculateLegacyPlayerPlayerPhysicalSpellDamage(
                updatedSession,
                target.ActiveSession.Session,
                spell,
                worldData,
                damageDivisor: 1);
            var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                context,
                target.ActiveSession,
                updatedSession.Character,
                damage,
                cancellationToken)
            .ConfigureAwait(false);
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
            if (result.DamageApplied == 0)
            {
                continue;
            }

            results.Add(result);
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                result.DamageApplied,
                Hit: true,
                MsgAttackEffect.None));
        }

        if (animationTargets.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CombatGear active player attack " +
                    $"{FormatSession(context)} spellId={spell.Id}; selected players had no applicable damage.")
                .ConfigureAwait(false);
            return true;
        }

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets,
            bombFlag: GetLegacyCombatGearTharnCutterActiveBombFlag(spell.Id, updatedSession.Character));
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await TryGrantLegacyCombatGearTharnCutterMissingSigilAsync(
                context,
                updatedSession,
                worldData,
                spell.Id,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var result in results)
        {
            await SendLegacyPlayerMagicDamageUpdatesAsync(
                    context,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var totalDamage = results.Aggregate(0UL, (total, result) => total + result.DamageApplied);
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                totalDamage,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await TrySendLegacyCombatGearTharnCutterJumpEffectAsync(
                context,
                updatedSession,
                primary,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer replaced legacy Archer player physical attack with CombatGear spell " +
                $"{FormatSession(context with { Session = updatedSession })} playerUid={primaryCharacter.Uid} " +
                $"spellId={spell.Id} sort={(int)spell.Sort} targets={animationTargets.Count} " +
                $"totalDamage={totalDamage}; source=MsgAttackPacket.cs AttackID.Archer player branch " +
                "MyCombatGear.CheckCombatGear -> MsgAttackPacket.ProcescMagic(ignoreStamp:true).")
            .ConfigureAwait(false);
        return true;
    }


    private static List<LegacyPlayerMagicTarget> SelectLegacyCombatGearAttackPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        WorldActiveSession primary,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var primaryCharacter = primary.Session.Character;
        var originX = query.X;
        var originY = query.Y;
        if (TryGetLegacyCombatGearAttackRadius(spell, out var radius))
        {
            return SelectLegacyCombatGearAttackPlayerTargetsByRadius(
                context,
                session,
                primary,
                spell,
                originX,
                originY,
                radius);
        }

        var lineRange = GetLegacyCombatGearAttackLineRange(spell);
        var lineWidth = GetLegacyCombatGearAttackLineWidth(spell);
        var selected = new List<LegacyPlayerMagicTarget>();
        if ((!RequiresLegacyCombatGearAttackDistanceCheck(spell.Id) ||
                LegacyGetDistance(session.Character.X, session.Character.Y, primaryCharacter.X, primaryCharacter.Y) <= spell.Range) &&
            IsLegacyInLine(
                session.Character.X,
                session.Character.Y,
                originX,
                originY,
                primaryCharacter.X,
                primaryCharacter.Y,
                lineRange,
                lineWidth))
        {
            selected.Add(new LegacyPlayerMagicTarget(primary));
        }

        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (character.Uid == primaryCharacter.Uid)
            {
                continue;
            }

            if (RequiresLegacyCombatGearAttackDistanceCheck(spell.Id) &&
                LegacyGetDistance(session.Character.X, session.Character.Y, character.X, character.Y) > spell.Range)
            {
                continue;
            }

            if (!IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    originX,
                    originY,
                    character.X,
                    character.Y,
                    lineRange,
                    lineWidth))
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }


    private static List<LegacyPlayerMagicTarget> SelectLegacyCombatGearAttackPlayerTargetsByRadius(
        WorldPacketHandlerContext context,
        WorldSession session,
        WorldActiveSession primary,
        LegacyMagicRuntimeSpell spell,
        ushort originX,
        ushort originY,
        byte radius)
    {
        var primaryUid = primary.Session.Character.Uid;
        var selected = new List<LegacyPlayerMagicTarget>
        {
            new(primary),
        };
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (character.Uid == primaryUid ||
                LegacyGetDistance(originX, originY, character.X, character.Y) > radius)
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }


    private static async ValueTask<WorldSession> TrySendLegacyCombatGearTharnCutterJumpEffectAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        WorldActiveSession target,
        CancellationToken cancellationToken)
    {
        var targetCharacter = target.Session.Character;
        var targetX = CheckedUInt16(targetCharacter.X, nameof(targetCharacter.X));
        var targetY = CheckedUInt16(targetCharacter.Y, nameof(targetCharacter.Y));
        var prisonSigil = HasLegacyStatusFlag(session.Character, LegacyCombatGearPrisonSigilFutilityCursedFlag);
        var whirlSigil = HasLegacyStatusFlag(session.Character, LegacyCombatGearWhirlSigilAuraCursedFlag);
        var top8 = HasLegacyStatusFlag(session.Character, LegacyCombatGearTop8WeeklyHawksAmbitionFlag);
        var top4 = HasLegacyStatusFlag(session.Character, LegacyCombatGearTop4WeeklyHawksAmbitionFlag);
        var top2 = HasLegacyStatusFlag(session.Character, LegacyCombatGearTop2WeeklyHawksAmbitionFlag);

        var effect = 0;
        var flagsToRemove = Array.Empty<int>();
        if (LegacyGetDistance(session.Character.X, session.Character.Y, targetX, targetY) <= 1 &&
            prisonSigil &&
            whirlSigil &&
            top8 &&
            top4 &&
            top2)
        {
            effect = 6;
            flagsToRemove =
            [
                LegacyCombatGearPrisonSigilFutilityCursedFlag,
                LegacyCombatGearWhirlSigilAuraCursedFlag,
                LegacyCombatGearTop8WeeklyHawksAmbitionFlag,
                LegacyCombatGearTop4WeeklyHawksAmbitionFlag,
                LegacyCombatGearTop2WeeklyHawksAmbitionFlag,
            ];
        }
        else if (prisonSigil)
        {
            effect = 1;
            flagsToRemove = [LegacyCombatGearPrisonSigilFutilityCursedFlag];
        }
        else if (whirlSigil)
        {
            effect = 2;
            flagsToRemove = [LegacyCombatGearWhirlSigilAuraCursedFlag];
        }
        else if (top8)
        {
            effect = 3;
            flagsToRemove = [LegacyCombatGearTop8WeeklyHawksAmbitionFlag];
        }
        else if (top4)
        {
            effect = 4;
            flagsToRemove = [LegacyCombatGearTop4WeeklyHawksAmbitionFlag];
        }
        else if (top2)
        {
            effect = 5;
            flagsToRemove = [LegacyCombatGearTop2WeeklyHawksAmbitionFlag];
        }

        if (effect == 0)
        {
            return session;
        }

        var updatedCharacter = WithoutLegacyStatusFlags(session.Character, flagsToRemove);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        var uid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
        if (effect is 1 or 6)
        {
            var prisonAnimation = new LegacySpellAnimationPacket(
                uid,
                uid,
                targetX,
                targetY,
                LegacyCombatGearTharnCutterPrisonSpellId,
                0,
                0,
                0,
                0,
                [new LegacySpellAnimationTarget(uid, 0, Hit: true, MsgAttackEffect.None)]);
            await SendPlayerSpellAnimationToVisibleAsync(updatedContext, updatedSession, prisonAnimation, cancellationToken)
                .ConfigureAwait(false);
        }

        if (effect is 3 or 6)
        {
            var top8Animation = new LegacySpellAnimationPacket(
                uid,
                0,
                targetX,
                targetY,
                LegacyCombatGearTharnCutterTop8SpellId,
                1,
                0,
                0,
                0,
                []);
            await SendPlayerSpellAnimationToVisibleAsync(updatedContext, updatedSession, top8Animation, cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer sent legacy CombatGear TharnCutter jump effect " +
                $"{FormatSession(updatedContext)} playerUid={targetCharacter.Uid} effect={effect} " +
                $"removedFlags={string.Join(',', flagsToRemove)}; source=MyCombatGear.CheckCombatGear -> MyCombatGear.TharnCutter(jmp:true).")
            .ConfigureAwait(false);
        return updatedSession;
    }
}
