using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private readonly record struct LegacyCombatGearSobNpcHit(
        SobNpc SobNpc,
        uint RequestedDamage,
        SobNpcDamageResult Result,
        LegacySobNpcDeathBehavior DeathBehavior);

    private static async ValueTask<bool> TryHandleLegacyCombatGearSobNpcAttackReplacementAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (packet.Query.AttackType != MsgAttackType.Archer ||
            context.WorldData is not { } worldData ||
            !HasLegacyCombatGearActiveAttackCandidate(session) ||
            !TryFindVisibleSobNpcByUid(
                context,
                session.Character,
                packet.Query.OpponentUid,
                out var primary))
        {
            return false;
        }

        if (!CanLegacySobNpcMagicDamageTarget(context, session.Character, primary, out var failureDiagnostic))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer rejected legacy CombatGear active SOB NPC target " +
                    $"{FormatSession(context)} targetUid={primary.Id}; " +
                    $"source=MsgAttackPacket.cs AttackID.Archer SOB branch CanAttackNpc.Verified before " +
                    $"MyCombatGear.CheckCombatGear; {failureDiagnostic}.")
                .ConfigureAwait(false);
            return true;
        }

        return await TryHandleLegacyCombatGearSobNpcAttackReplacementCoreAsync(
                context,
                packet,
                session,
                primary,
                worldData,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyCombatGearSobNpcAttackReplacementAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        WorldSession session,
        SobNpc primary,
        CancellationToken cancellationToken)
    {
        if (packet.Query.AttackType != MsgAttackType.Archer ||
            context.WorldData is not { } worldData ||
            !HasLegacyCombatGearActiveAttackCandidate(session))
        {
            return false;
        }

        return await TryHandleLegacyCombatGearSobNpcAttackReplacementCoreAsync(
                context,
                packet,
                session,
                primary,
                worldData,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyCombatGearSobNpcAttackReplacementCoreAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        WorldSession session,
        SobNpc primary,
        WorldDataCatalog worldData,
        CancellationToken cancellationToken)
    {
        if (await TryHandleLegacyCombatGearArrowBladesCallerAsync(
                context, session, packet.Query with
                {
                    X = CheckedUInt16(primary.X, nameof(primary.X)),
                    Y = CheckedUInt16(primary.Y, nameof(primary.Y)),
                }, cancellationToken).ConfigureAwait(false)) return true;

        if (!TryResolveLegacyCombatGearActiveAttackSpell(
                session,
                worldData,
                LegacyCombatGearActiveAttackTarget.FromSobNpc(primary),
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
            OpponentUid = CheckedUInt32(primary.Id, nameof(primary.Id)),
            X = CheckedUInt16(primary.X, nameof(primary.X)),
            Y = CheckedUInt16(primary.Y, nameof(primary.Y)),
            Damage = 0,
            KilledMonster = 0,
            ResponseDamage = 0,
            Effect = MsgAttackEffect.None,
            SpellId = spell.Id,
            SpellLevel = playerSpell.Level,
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

        if (context.SobNpcStates is not { } sobNpcStates)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        SobNpcRuntimeStateResource,
                        "combatgear-active-sob-npc",
                        "CombatGear active SOB target was visible, but SOB runtime state is not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", primary.MapId),
                            LegacyParityGapDiagnostics.Fact("opcode", WorldPacketOpcodes.MsgAttack),
                            LegacyParityGapDiagnostics.Fact("sobNpcKind", primary.Kind),
                            LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                            LegacyParityGapDiagnostics.Fact("targetKind", "sob-npc"),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                            LegacyParityGapDiagnostics.Fact("targetKind", "sob-npc"),
                        ]))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CombatGear active SOB NPC attack " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id}; " +
                    "SOB runtime state is not loaded.")
                .ConfigureAwait(false);
            return true;
        }

        var targets = SelectLegacyCombatGearAttackSobNpcTargets(context, resourceSession, primary, spell);
        if (targets.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CombatGear active SOB NPC attack " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} " +
                    $"sobNpcUid={primary.Id}; no visible SOB NPC matched legacy shape.")
                .ConfigureAwait(false);
            return true;
        }

        var now = DateTimeOffset.UtcNow;
        var hits = new List<LegacyCombatGearSobNpcHit>();
        var animationTargets = new List<LegacySpellAnimationTarget>();
        foreach (var target in targets)
        {
            var damage = CalculateLegacySobNpcPhysicalSpellDamage(
                resourceSession,
                spell,
                worldData,
                damageMultiplier: 1);
            var counterSession = await ReceiveLegacyStoneCrackerNonPlayerCounterAsync(
                    context with { Session = resourceSession }, resourceSession,
                    CheckedUInt16(target.SobNpc.X, nameof(target.SobNpc.X)),
                    CheckedUInt16(target.SobNpc.Y, nameof(target.SobNpc.Y)), cancellationToken).ConfigureAwait(false);
            if (counterSession is null) return true;
            resourceSession = counterSession;
            context = context with { Session = resourceSession };
            var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc, worldData.StaticStatues);
            var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
            hits.Add(new LegacyCombatGearSobNpcHit(target.SobNpc, damage, result, deathBehavior));
            if (result.AlreadyDead || result.DamageApplied == 0)
            {
                continue;
            }

            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id)),
                result.DamageApplied,
                Hit: true,
                MsgAttackEffect.None));
        }

        if (animationTargets.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CombatGear active SOB NPC attack " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id}; " +
                    "selected SOB NPCs had no applicable damage.")
                .ConfigureAwait(false);
            return true;
        }

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets,
            bombFlag: GetLegacyCombatGearTharnCutterActiveBombFlag(spell.Id, resourceSession.Character));
        await SendPlayerSpellAnimationToVisibleAsync(context, resourceSession, animation, cancellationToken)
            .ConfigureAwait(false);

        var updatedSession = await TryGrantLegacyCombatGearTharnCutterMissingSigilAsync(
                context,
                resourceSession,
                worldData,
                spell.Id,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var hit in hits)
        {
            if (hit.Result.Killed && LegacySobNpcEventRules.IsTrainingSob(hit.SobNpc))
            {
                await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                        context,
                        updatedSession,
                        hit.SobNpc,
                        sobNpcStates.GetVisible(hit.SobNpc),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (!hit.Result.AlreadyDead && !LegacySobNpcEventRules.IsTrainingSob(hit.SobNpc))
            {
                await LogLegacySobNpcEventSideEffectGapAsync(context, hit.SobNpc, hit.Result, spell)
                    .ConfigureAwait(false);
            }
        }

        var progressDamage = hits.Aggregate(
            0UL,
            (total, hit) => total + CalculateLegacySobNpcExperienceDamage(hit.SobNpc, hit.Result));
        var proficiencyDamage = hits
            .Where(hit => !hit.Result.AlreadyDead)
            .Aggregate(0UL, (total, hit) => total + hit.Result.DamageApplied);
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
                ClampLegacyProficiencyRawExperience(proficiencyDamage),
                cancellationToken,
                activity: "CombatGear active SOB NPC weapon proficiency experience")
            .ConfigureAwait(false);
        updatedSession = await TrySendLegacyCombatGearTharnCutterJumpEffectAsync(
                context,
                updatedSession,
                CreateLegacyCombatGearSobNpcTharnCutterTarget(primary),
                cancellationToken)
            .ConfigureAwait(false);

        var totalRequestedDamage = hits
            .Where(hit => !hit.Result.AlreadyDead)
            .Aggregate(0UL, (total, hit) => total + hit.RequestedDamage);
        var totalAppliedDamage = hits
            .Where(hit => !hit.Result.AlreadyDead)
            .Aggregate(0UL, (total, hit) => total + hit.Result.DamageApplied);
        await context.Log.WriteLineAsync(
                "WorldServer replaced legacy Archer SOB NPC attack with CombatGear spell " +
                $"{FormatSession(context with { Session = updatedSession })} sobNpcUid={primary.Id} " +
                $"spellId={spell.Id} sort={(int)spell.Sort} targets={animationTargets.Count} " +
                $"progressDamage={progressDamage} totalRequestedDamage={totalRequestedDamage} " +
                $"totalAppliedDamage={totalAppliedDamage}; source=MyCombatGear.CheckCombatGear -> " +
                "MsgAttackPacket.ProcescMagic(ignoreStamp:true).")
            .ConfigureAwait(false);
        return true;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyCombatGearAttackSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        SobNpc primary,
        LegacyMagicRuntimeSpell spell)
    {
        if (TryGetLegacyCombatGearAttackRadius(spell, out var radius))
        {
            return SelectLegacyCombatGearAttackSobNpcTargetsByRadius(
                context,
                session,
                CheckedUInt16(primary.X, nameof(primary.X)),
                CheckedUInt16(primary.Y, nameof(primary.Y)),
                radius);
        }

        var lineRange = GetLegacyCombatGearAttackLineRange(spell);
        var lineWidth = GetLegacyCombatGearAttackLineWidth(spell);
        var selected = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (RequiresLegacyCombatGearAttackDistanceCheck(spell.Id) &&
                LegacyGetDistance(session.Character.X, session.Character.Y, candidate.SobNpc.X, candidate.SobNpc.Y) > spell.Range)
            {
                continue;
            }

            if (!IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    primary.X,
                    primary.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y,
                    lineRange,
                    lineWidth))
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyCombatGearAttackSobNpcTargetsByRadius(
        WorldPacketHandlerContext context,
        WorldSession session,
        ushort originX,
        ushort originY,
        byte radius)
    {
        var selected = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (LegacyGetDistance(originX, originY, candidate.SobNpc.X, candidate.SobNpc.Y) <= radius)
            {
                selected.Add(candidate);
            }
        }

        return selected;
    }

    private static LegacyMonsterInstance CreateLegacyCombatGearSobNpcTharnCutterTarget(SobNpc sobNpc)
    {
        return new LegacyMonsterInstance(
            CheckedUInt32(sobNpc.Id, nameof(sobNpc.Id)),
            sobNpc.MapId,
            CheckedUInt16(sobNpc.X, nameof(sobNpc.X)),
            CheckedUInt16(sobNpc.Y, nameof(sobNpc.Y)),
            Mesh: 0,
            Level: 0,
            Hitpoints: 0,
            MaxLife: 0,
            MonsterId: 0,
            sobNpc.Name,
            Boss: false,
            GeneratorId: 0,
            SpawnIndex: 0);
    }
}
