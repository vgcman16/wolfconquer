using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyKineticSparkPlayerReceiveGap = "kineticspark-player-receive-side-effects";

    private readonly record struct LegacyKineticSparkPlayerHit(
        LegacyPlayerMagicDamageResult Result);

    private static async ValueTask<bool> TryHandleLegacyKineticSparkPassivePlayerAttackAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (!IsLegacyKineticSparkPassivePlayerProcAttackType(packet.Query.AttackType) ||
            context.WorldData is not { } worldData ||
            !HasLegacyStatusFlag(session.Character, LegacyKineticSparkFlag) ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                session.PlayerSpells,
                worldData,
                LegacyKineticSparkSpellId,
                out var playerSpell,
                out var spell) ||
            spell.Sort != LegacyMagicSort.KineticSpark ||
            !TryGetVisibleSessionByUid(context, session, packet.Query.OpponentUid, out var primary))
        {
            return false;
        }

        if (!await TryPassLegacyArcherPlayerCallerCanAttackAsync(
                context,
                session,
                primary,
                spell,
                "KineticSpark passive player proc",
                cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (!CheckLegacyKineticSparkPassiveRateRoll(session.Character))
        {
            return false;
        }

        var targetBudget = GetLegacyKineticSparkTargetBudget(session, worldData);
        var primaryCharacter = primary.Session.Character;
        var originX = CheckedUInt16(primaryCharacter.X, nameof(primaryCharacter.X));
        var originY = CheckedUInt16(primaryCharacter.Y, nameof(primaryCharacter.Y));
        var selectedTargets = new List<LegacyPlayerMagicTarget>();
        var hits = new List<LegacyKineticSparkPlayerHit>();
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var updatedSession = session;

        targetBudget--;
        if (targetBudget <= 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy KineticSpark passive player proc " +
                    $"{FormatSession(context)} spellId={spell.Id} primaryPlayerUid={primaryCharacter.Uid}; " +
                    "source player branch exhausted target budget before primary hit.")
                .ConfigureAwait(false);
            return true;
        }

        var primarySession = await AddLegacyKineticSparkPlayerHitAsync(
                context,
                updatedSession,
                primary,
                spell,
                remainingBudget: null,
                selectedTargets,
                hits,
                animationTargets,
                cancellationToken)
            .ConfigureAwait(false);
        if (primarySession is null)
        {
            return true;
        }
        updatedSession = primarySession;
        context = context with { Session = updatedSession };

        foreach (var candidate in EnumerateLegacyKineticSparkVisiblePlayerChainCandidates(
                     context,
                     updatedSession,
                     primary,
                     originX,
                     originY,
                     spell))
        {
            if (!await TryPassLegacyArcherPlayerCallerCanAttackAsync(
                    context,
                    updatedSession,
                    candidate.ActiveSession,
                    spell,
                    "KineticSpark passive player proc",
                    cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            targetBudget--;
            if (targetBudget <= 0)
            {
                break;
            }

            var chainedSession = await AddLegacyKineticSparkPlayerHitAsync(
                    context,
                    updatedSession,
                    candidate.ActiveSession,
                    spell,
                    targetBudget,
                    selectedTargets,
                    hits,
                    animationTargets,
                    cancellationToken)
                .ConfigureAwait(false);
            if (chainedSession is null)
            {
                return true;
            }
            updatedSession = chainedSession;
            context = context with { Session = updatedSession };
        }

        if (animationTargets.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy KineticSpark passive player proc with no targets " +
                    $"{FormatSession(context)} spellId={spell.Id} primaryPlayerUid={primaryCharacter.Uid}; " +
                    "source=KineticSpark.cs AttackSpell case 11590 player branch.")
                .ConfigureAwait(false);
            return true;
        }

        foreach (var hit in hits)
        {
            await SendLegacyKineticSparkPlayerReceivePreAnimationUpdatesAsync(
                    context,
                    updatedSession,
                    hit.Result,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var hit in hits)
        {
            if (hit.Result.DamageApplied == 0)
            {
                continue;
            }

            await SendLegacyPlayerMagicDamageUpdatesAsync(
                    context,
                    updatedSession,
                    hit.Result,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            originX,
            originY,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                0,
                cancellationToken)
            .ConfigureAwait(false);

        var totalAppliedDamage = hits.Aggregate(0UL, (total, hit) => total + hit.Result.DamageApplied);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy KineticSpark passive player proc " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} " +
                $"spellLevel={playerSpell.Level} soulLevel={playerSpell.UseSpellSoul} " +
                $"primaryPlayerUid={primaryCharacter.Uid} targets={animationTargets.Count} " +
                $"spellProgressDamage=0 totalAppliedDamage={totalAppliedDamage}; " +
                "source=KineticSpark.cs AttackSpell case 11590 player branch.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<WorldSession?> AddLegacyKineticSparkPlayerHitAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        WorldActiveSession target,
        LegacyMagicRuntimeSpell spell,
        int? remainingBudget,
        List<LegacyPlayerMagicTarget> selectedTargets,
        List<LegacyKineticSparkPlayerHit> hits,
        List<LegacySpellAnimationTarget> animationTargets,
        CancellationToken cancellationToken)
    {
        selectedTargets.Add(new LegacyPlayerMagicTarget(target));
        var updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                session,
                selectedTargets,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var activeTarget = target;
        if (context.Sessions?.TryGet(target.Connection, out var refreshedTarget) == true &&
            refreshedTarget is not null)
        {
            activeTarget = target with { Session = refreshedTarget };
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

        var isRangeChainHit = remainingBudget.HasValue;
        LegacyPlayerPlayerPhysicalSpellDamageResult physicalDamage;
        if (statusDefended)
        {
            var statusReturnDamage = isRangeChainHit
                ? CalculateLegacyPlayerRangeStatusReturnDamage(
                    updatedSession,
                    activeTarget.Session,
                    context.WorldData)
                : 1U;
            physicalDamage = new LegacyPlayerPlayerPhysicalSpellDamageResult(
                statusReturnDamage,
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
                isRangeChainHit ? spell with { Id = 0 } : spell,
                context.WorldData,
                damageDivisor: 1);
        }

        var damage = physicalDamage.Damage;
        var effect = physicalDamage.Effect;
        var defensiveStanceDefended =
            !statusDefended &&
            !wildPhoenixDefended &&
            HasLegacyStatusFlag(activeTarget.Session.Character, LegacyDefensiveStanceFlag);
        if (defensiveStanceDefended)
        {
            damage = isRangeChainHit
                ? LegacyCombatDamageCalculator.DecreaseLegacyBless(damage, 40)
                : Math.Max(1U, damage / 2U);
            effect = MsgAttackEffect.Block;
        }

        damage = CalculateLegacyKineticSparkPlayerSoulDamage(damage);
        if (remainingBudget is { } budget)
        {
            damage = ScaleLegacyKineticSparkChainedDamage(damage, budget);
        }

        var suppressBackDamage =
            statusDefended ||
            wildPhoenixDefended ||
            (isRangeChainHit && defensiveStanceDefended);
        var receive = suppressBackDamage
            ? await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                    context,
                    activeTarget,
                    updatedSession.Character,
                    damage,
                    cancellationToken)
                .ConfigureAwait(false)
            : await ApplyLegacyBackDamageEligiblePlayerDamageAsync(
                    context,
                    activeTarget,
                    updatedSession.Character,
                    damage,
                    cancellationToken)
                .ConfigureAwait(false);
        if (receive.AttackerSession is { } carriedAttacker)
        {
            updatedSession = carriedAttacker;
            context = context with { Session = updatedSession };
        }
        if (receive.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
        {
            return null;
        }
        if (receive.Result is not { } result)
        {
            return updatedSession; // TargetLost carries the refreshed surviving original caster, not a hit.
        }

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

        (updatedSession, result) = ApplyLegacyKineticSparkPlayerThundercloudSight(
            context,
            updatedSession,
            result);
        context = context with { Session = updatedSession };
        hits.Add(new LegacyKineticSparkPlayerHit(result));
        animationTargets.Add(new LegacySpellAnimationTarget(
            CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
            damage,
            Hit: true,
            effect));

        await LogLegacyKineticSparkPlayerReceiveSideEffectGapAsync(context, result, spell)
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask SendLegacyKineticSparkPlayerReceivePreAnimationUpdatesAsync(
        WorldPacketHandlerContext context,
        WorldSession attacker,
        LegacyPlayerMagicDamageResult result,
        CancellationToken cancellationToken)
    {
        await SendLegacyKineticSparkPlayerReceiveInfiniteMistArchiveSkillAsync(
                context,
                attacker,
                result,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static (WorldSession Session, LegacyPlayerMagicDamageResult Result)
        ApplyLegacyKineticSparkPlayerThundercloudSight(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyPlayerMagicDamageResult result)
    {
        var attackerUid = session.Character.Uid.ToString(CultureInfo.InvariantCulture);
        var targetUid = result.UpdatedSession.Character.Uid.ToString(CultureInfo.InvariantCulture);
        var updatedSession = session with
        {
            Character = WithCharacterAttribute(
                session.Character,
                RuntimeLegacyThundercloudSightAttribute,
                targetUid),
        };
        var updatedTargetSession = result.UpdatedSession with
        {
            Character = WithCharacterAttribute(
                result.UpdatedSession.Character,
                RuntimeLegacyThundercloudSightAttribute,
                attackerUid),
        };

        context.Sessions?.Set(context.Connection, updatedSession);
        context.Sessions?.Set(result.ActiveSession.Connection, updatedTargetSession);
        return (
            updatedSession,
            result with
            {
                ActiveSession = result.ActiveSession with { Session = updatedTargetSession },
                UpdatedSession = updatedTargetSession,
            });
    }

    private static IEnumerable<LegacyPlayerMagicTarget> EnumerateLegacyKineticSparkVisiblePlayerChainCandidates(
        WorldPacketHandlerContext context,
        WorldSession session,
        WorldActiveSession primary,
        ushort originX,
        ushort originY,
        LegacyMagicRuntimeSpell spell)
    {
        if (context.Sessions is not { } sessions)
        {
            yield break;
        }

        foreach (var active in sessions.Snapshot())
        {
            var character = active.Session.Character;
            if (ReferenceEquals(active.Connection, context.Connection) ||
                character.Uid == session.Character.Uid ||
                character.Uid == primary.Session.Character.Uid ||
                !CanSeePlayer(session.Character, character) ||
                LegacyGetDistance(originX, originY, character.X, character.Y) >= spell.Range / 2)
            {
                continue;
            }

            yield return new LegacyPlayerMagicTarget(active);
        }
    }

    private static bool IsLegacyKineticSparkPassivePlayerProcAttackType(MsgAttackType attackType)
    {
        return attackType == MsgAttackType.Archer;
    }

    private static uint CalculateLegacyKineticSparkPlayerSoulDamage(uint damage)
    {
        // Source Base.CalculateSoul(Damage, LevelSoul) returns Damage unchanged.
        return damage;
    }

    private static async ValueTask LogLegacyKineticSparkPlayerReceiveSideEffectGapAsync(
        WorldPacketHandlerContext context,
        LegacyPlayerMagicDamageResult result,
        LegacyMagicRuntimeSpell spell)
    {
        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.Create(
                    LegacyParityGapCategory.Magic,
                    LegacyKineticSparkPlayerReceiveGap,
                    "KineticSpark player HP/death, ThundercloudSight, bounded defender CombatGear UpdateTarget status passives, defender UpdateTarget MagneticLight/AblazeBlade player branches, bounded attacker-side CombatGear Update StoneCracker/Dragonhowl/Bloodlust/Taoist/Pirate branches, Pirate black-jade UseRune status branches, attacker Venom speffect, bounded defender speffect_attribute Solid/Bloodthirst/Elan self-effects, StarFlow 10950 post-receive player animation damage/update ordering, and recursive 10950 attacker CombatGear counter/update loops are modeled, but source ReceiveAttack.Player.Execute side effects such as unsupported individual UseRune execution edges, perfection/rune/tournament counters, and PK bookkeeping are not modeled in this passive branch.",
                    severity: LegacyParityGapSeverity.Info,
                    facts:
                    [
                        LegacyParityGapDiagnostics.Fact("opcode", WorldPacketOpcodes.MsgAttack),
                        LegacyParityGapDiagnostics.Fact("sort", (int)spell.Sort),
                        LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                        LegacyParityGapDiagnostics.Fact("targetKind", "player"),
                        LegacyParityGapDiagnostics.Fact("targetUid", result.UpdatedSession.Character.Uid),
                    ],
                    dedupeFacts:
                    [
                        LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                        LegacyParityGapDiagnostics.Fact("targetKind", "player"),
                    ]))
            .ConfigureAwait(false);
    }
}
