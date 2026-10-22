using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> HandleLegacyPlayerPlayerMagicAttackAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        var nativeQueryCast = spell.Sort == LegacyMagicSort.Attack &&
            IsLegacyNativeAttackPlayerCompletion(spell.Id);
        var nativeMagicCasterIdentity = nativeQueryCast
            ? context.RootFirstFamilyInvocation?.NativeClientIdentity ??
                CaptureLegacyNativeMagicQueryCaster(context, session) : null;
        if (nativeQueryCast && nativeMagicCasterIdentity is null) return true;
        WorldActiveSession primaryTarget;
        if (spell.Sort == LegacyMagicSort.Attack && context.Sessions is { } sessions)
        {
            var selected = FindLegacyMapBlockPlayer(sessions, session, query.OpponentUid);
            if (selected is null)
            {
                return false;
            }

            primaryTarget = selected;
        }
        else if (!TryGetVisibleSessionByUid(context, session, query.OpponentUid, out primaryTarget))
        {
            return false;
        }

        if (!LegacyMagicRuntime.IsSupportedVisibleMonsterAttack(spell))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.UnsupportedMagic(
                        spell.Id,
                        sort: (int)spell.Sort,
                        reason: LegacyMagicSortCapabilities.FormatDiagnostic(spell.Sort, spell.Id)))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy player-player magic " +
                    $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort}; unsupported player magic sort.")
                .ConfigureAwait(false);
            return true;
        }

        if (!PassesLegacyPlayerPlayerMagicPreResourceRangeGate(
                session,
                primaryTarget,
                query,
                spell,
                out var failureDiagnostic,
                out _))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy player-player magic " +
                    $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort}; {failureDiagnostic}.")
                .ConfigureAwait(false);
            return true;
        }

        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context,
                session,
                spell,
                cancellationToken,
                nativeMagicCasterIdentity).ConfigureAwait(false);
        if (resourceSession is null)
        {
            return true;
        }

        if (nativeQueryCast)
        {
            if (!TryRefreshLegacyNativeMagicQueryCaster(context, session.Character.Uid,
                    nativeMagicCasterIdentity!, out var nativeCaster)) return true;
            resourceSession = nativeCaster;
        }

        if (!TrySelectLegacyPlayerMagicTargets(
                context,
                resourceSession,
                primaryTarget,
                query,
                spell,
                out var targets,
                out failureDiagnostic,
                out var failureEffect))
        {
            if (failureEffect == LegacyPlayerMagicGateFailureEffect.ForbiddenPirate)
            {
                await SendScalarUpdateAsync(
                        context,
                        resourceSession.Character,
                        LegacyMsgUpdateDataType.ForbiddenPirate,
                        1,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (failureEffect == LegacyPlayerMagicGateFailureEffect.HellVortexCounter)
            {
                await TrySendLegacyPlayerHellVortexCounterAsync(
                        context,
                        resourceSession,
                        primaryTarget,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy player-player magic " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} sort={(int)spell.Sort}; {failureDiagnostic}.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = resourceSession;
        updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                updatedSession,
                targets,
                cancellationToken)
            .ConfigureAwait(false);
        if (nativeQueryCast)
        {
            if (!TryRefreshLegacyNativeMagicQueryCaster(context, session.Character.Uid,
                    nativeMagicCasterIdentity!, out var nativeCaster)) return true;
            updatedSession = nativeCaster;
        }
        context = context with { Session = updatedSession };

        var results = new List<LegacyPlayerMagicDamageResult>();
        var animationTargets = new List<LegacySpellAnimationTarget>();
        foreach (var target in targets)
        {
            var activeTarget = target.ActiveSession;
            var usesMagicOnPlayer = spell.Sort == LegacyMagicSort.Attack;
            var completesNativeAttackPlayer = usesMagicOnPlayer &&
                IsLegacyNativeAttackPlayerCompletion(spell.Id);
            (activeTarget, var wildPhoenixDefended) =
                await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                        context,
                        activeTarget,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (completesNativeAttackPlayer)
            {
                if (!TryRefreshLegacyNativeMagicQueryCaster(context, session.Character.Uid,
                        nativeMagicCasterIdentity!, out var nativeCaster)) return true;
                updatedSession = nativeCaster;
                context = context with { Session = nativeCaster };
            }
            var maniacDanceDefended =
                usesMagicOnPlayer &&
                !wildPhoenixDefended &&
                HasLegacyStatusFlag(activeTarget.Session.Character, LegacyManiacDanceFlag);
            var shurikenVortexDefended =
                usesMagicOnPlayer &&
                !wildPhoenixDefended &&
                !maniacDanceDefended &&
                HasLegacyStatusFlag(activeTarget.Session.Character, LegacyShurikenVortexFlag);
            var magicDefenderDefended =
                usesMagicOnPlayer &&
                !wildPhoenixDefended &&
                !maniacDanceDefended &&
                !shurikenVortexDefended &&
                HasLegacyStatusFlag(activeTarget.Session.Character, LegacyMagicDefenderFlag);
            var statusDefended =
                maniacDanceDefended ||
                shurikenVortexDefended ||
                magicDefenderDefended;
            var damage = wildPhoenixDefended || statusDefended
                ? 1U
                : CalculateLegacyPlayerPlayerMagicDamage(
                    updatedSession,
                    activeTarget.Session,
                    spell,
                    context.WorldData,
                    sourceStatus: completesNativeAttackPlayer
                        ? (LegacyNativeMagicQueryFields?)ReadLegacyNativeMagicQueryCaster(
                            context, session.Character.Uid, nativeMagicCasterIdentity!)
                        : null);
            if (!wildPhoenixDefended && !statusDefended)
            {
                var baseDamage = damage;
                damage = ApplyLegacyCombatGearWildPhoenixAttackEffect(
                    updatedSession,
                    spell,
                    damage,
                    LegacyWildPhoenixDamageTargetKind.Player);
                if (damage != baseDamage)
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer applied legacy WildPhoenix attacker damage bonus " +
                            $"{FormatSession(context)} spellId={spell.Id} targetKind=Player " +
                            $"baseDamage={baseDamage} boostedDamage={damage}; " +
                            "source=MyCombatGear.AttackEffect.")
                        .ConfigureAwait(false);
                }
            }

            var receive = usesMagicOnPlayer && !wildPhoenixDefended && !statusDefended
                ? await ApplyLegacyBackDamageEligiblePlayerDamageAsync(
                        context,
                        activeTarget,
                        updatedSession.Character,
                        damage,
                        cancellationToken,
                        postBackMagicSpellId: completesNativeAttackPlayer ? spell.Id : null,
                        postBackNativeMagicCasterIdentity: completesNativeAttackPlayer
                            ? nativeMagicCasterIdentity : null)
                    .ConfigureAwait(false)
                : await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                        context,
                        activeTarget,
                        updatedSession.Character,
                        completesNativeAttackPlayer
                            ? CompleteLegacyNativeAttackPlayerDamage(spell.Id, damage)
                            : damage,
                        cancellationToken,
                        nativeIncomingClientIdentity: completesNativeAttackPlayer
                            ? nativeMagicCasterIdentity : null)
                    .ConfigureAwait(false);
            if (completesNativeAttackPlayer && !TryRefreshLegacyNativeMagicQueryCaster(
                    context, session.Character.Uid, nativeMagicCasterIdentity!, out _)) return true;
            if (receive.AttackerSession is { } carriedAttacker)
            {
                updatedSession = carriedAttacker;
                context = context with { Session = updatedSession };
            }
            if (receive.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return true;
            }
            if (receive.Result is not { } result)
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

            if (!completesNativeAttackPlayer && result.DamageApplied == 0 &&
                !result.HawksAmbitionConsumed &&
                !result.DuelCleanupApplied &&
                !result.AttackerAbsolutionConsumed &&
                !result.TargetAbsolutionConsumed &&
                !result.BloodTideActivated &&
                !result.ReverseMagicApplied &&
                !result.CapriceStaminaTransferred &&
                !result.CapriceXpListRemoved &&
                !result.XpBoosterApplied &&
                !result.XpKillerApplied &&
                !result.TidalWaveApplied &&
                !result.QuenchApplied &&
                !result.AttackHitApplied &&
                !result.TournamentOutcome.IsActive &&
                !result.MirrorOfSinResult.IsActive &&
                !result.LightOfStaminaResult.IsActive &&
                !result.CalmWindResult.IsActive &&
                !result.DivineGuardResult.IsActive &&
                !result.KillingFlashResult.IsActive &&
                !result.DrainingTouchResult.IsActive &&
                !result.BloodSpawnResult.IsActive &&
                !result.ResponseEffectsResult.IsActive &&
                !result.InfiniteMistResult.IsActive &&
                result.LightningShieldActivationLeft is null &&
                result.LightningShieldDrainLeft is null)
            {
                continue;
            }

            results.Add(result);
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                completesNativeAttackPlayer
                    ? result.ReceiveDamage ?? throw new InvalidOperationException(
                        "Completed native Attack player receive must carry post-modifier damage.")
                    : result.DamageApplied,
                Hit: true,
                magicDefenderDefended ? MsgAttackEffect.Imunity : MsgAttackEffect.None));
        }

        if (animationTargets.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy player-player magic " +
                    $"{FormatSession(context)} spellId={spell.Id}; selected players had no applicable damage.")
                .ConfigureAwait(false);
            return true;
        }

        foreach (var result in results)
        {
            await SendLegacyPlayerMagicPreAnimationUpdatesAsync(
                    context,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
        }

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

        foreach (var result in results)
        {
            await SendLegacyPlayerMagicDamageUpdatesAsync(
                    context,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await TryApplyLegacyPlayerMagicChillingSnowAsync(
                context,
                updatedSession,
                results,
                cancellationToken)
            .ConfigureAwait(false);

        var totalDamage = results.Aggregate(0UL, (total, result) => total + result.DamageApplied);
        // Attack1000/1001 accumulates num3/num16 only from Monster/Npc.Execute, never Player.Execute.
        var callerProgress = spell.Sort == LegacyMagicSort.Attack && IsLegacyNativeAttackPlayerCompletion(spell.Id)
            ? 0UL
            : totalDamage;
        var proficiencyExperience = callerProgress > uint.MaxValue ? uint.MaxValue : (uint)callerProgress;
        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                updatedSession,
                proficiencyExperience,
                cancellationToken,
                activity: "player magic weapon proficiency experience")
            .ConfigureAwait(false);
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                callerProgress,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy player-player magic " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} targets={animationTargets.Count} totalDamage={totalDamage}.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool PassesLegacyPlayerPlayerMagicPreResourceRangeGate(
        WorldSession session,
        WorldActiveSession primaryTarget,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        out string failureDiagnostic,
        out LegacyPlayerMagicGateFailureEffect failureEffect)
    {
        failureDiagnostic = string.Empty;
        failureEffect = LegacyPlayerMagicGateFailureEffect.None;
        if (spell.Sort is not (LegacyMagicSort.Attack or LegacyMagicSort.DirectAttack))
        {
            return true;
        }

        var distance = LegacyGetDistance(
            session.Character.X,
            session.Character.Y,
            primaryTarget.Session.Character.X,
            primaryTarget.Session.Character.Y);
        if (distance <= spell.Range)
        {
            return true;
        }

        failureDiagnostic = $"playerUid={query.OpponentUid} distance={distance} range={spell.Range}";
        return false;
    }

    private static LegacyPlayerMagicReceivePreparation PrepareLegacyPlayerMagicReceive(
        WorldPacketHandlerContext context,
        LegacyPlayerReceiveEarlyModifierResult early)
    {
        var target = early.Target;
        var targetSession = target.Session;
        var attacker = early.Attacker;
        var attackerSession = early.AttackerSession;
        WorldSession? updatedAttackerSession = early.AttackerChanged ? attackerSession : null;
        WorldSession? attackerRuntimeSession = attackerSession;
        var infiniteMistResult = early.InfiniteMistResult;
        var damage = early.Damage;
        // Earlier mutable damage/status phases and their publications completed
        // once. Tournament/lethality/XY selection now sees their callback state.
        var modifierResult = ApplyLegacyPlayerReceiveDamageModifiers(
            attacker, attackerRuntimeSession, targetSession, early, context.WorldData);
        damage = modifierResult.Damage;
        if (attackerRuntimeSession is not null && !modifierResult.Attacker.Equals(attackerRuntimeSession.Character))
        {
            updatedAttackerSession = attackerRuntimeSession with { Character = modifierResult.Attacker };
            attackerRuntimeSession = updatedAttackerSession;
            context.Sessions?.Set(context.Connection, updatedAttackerSession);
        }

        targetSession = targetSession with { Character = modifierResult.Target };
        var currentHitpoints = CheckedUInt32(targetSession.Character.MinHitPoints, nameof(targetSession.Character.MinHitPoints));

        if (TryApplyLegacyPlayerReceiveTournamentRuntime(
                context,
                target,
                targetSession,
                attackerRuntimeSession,
                modifierResult,
                currentHitpoints,
                out var tournamentResult))
        {
            return new(tournamentResult with { InfiniteMistResult = infiniteMistResult, ReceiveDamage = damage },
                modifierResult.Attacker);
        }

        var mirrorOfSinResult = TryApplyLegacyPlayerReceiveMirrorOfSin(
            modifierResult.Attacker,
            attackerRuntimeSession,
            out var mirrorOfSinAttacker);
        if (mirrorOfSinResult.IsActive)
        {
            modifierResult = modifierResult with { Attacker = mirrorOfSinAttacker };
            if (attackerRuntimeSession is not null)
            {
                updatedAttackerSession = attackerRuntimeSession with { Character = mirrorOfSinAttacker };
                attackerRuntimeSession = updatedAttackerSession;
                context.Sessions?.Set(context.Connection, updatedAttackerSession);
            }
        }

        var lightOfStaminaResult = TryApplyLegacyPlayerReceiveLightOfStamina(
            modifierResult.Attacker,
            targetSession.Character,
            attackerRuntimeSession,
            out var lightOfStaminaAttacker);
        if (lightOfStaminaResult.IsActive)
        {
            modifierResult = modifierResult with { Attacker = lightOfStaminaAttacker };
            if (attackerRuntimeSession is not null)
            {
                updatedAttackerSession = attackerRuntimeSession with { Character = lightOfStaminaAttacker };
                attackerRuntimeSession = updatedAttackerSession;
                context.Sessions?.Set(context.Connection, updatedAttackerSession);
            }
        }

        var calmWindResult = TryApplyLegacyPlayerReceiveCalmWind(modifierResult.Attacker);

        var divineGuardResult = TryApplyLegacyPlayerReceiveDivineGuard(
            modifierResult.Attacker,
            targetSession.Character,
            attackerRuntimeSession,
            out var divineGuardAttacker);
        if (divineGuardResult.IsActive)
        {
            modifierResult = modifierResult with { Attacker = divineGuardAttacker };
            if (attackerRuntimeSession is not null)
            {
                updatedAttackerSession = attackerRuntimeSession with { Character = divineGuardAttacker };
                attackerRuntimeSession = updatedAttackerSession;
                context.Sessions?.Set(context.Connection, updatedAttackerSession);
            }
        }

        var killingFlashResult = TryApplyLegacyPlayerReceiveKillingFlash(
            modifierResult.Attacker,
            attackerRuntimeSession,
            out var killingFlashAttacker);
        if (killingFlashResult.IsActive)
        {
            modifierResult = modifierResult with { Attacker = killingFlashAttacker };
            if (attackerRuntimeSession is not null)
            {
                updatedAttackerSession = attackerRuntimeSession with { Character = killingFlashAttacker };
                attackerRuntimeSession = updatedAttackerSession;
                context.Sessions?.Set(context.Connection, updatedAttackerSession);
            }
        }

        var drainingTouchResult = TryApplyLegacyPlayerReceiveDrainingTouch(
            modifierResult.Attacker,
            attackerRuntimeSession,
            context.WorldData,
            out var drainingTouchAttacker);
        if (drainingTouchResult.IsActive)
        {
            modifierResult = modifierResult with { Attacker = drainingTouchAttacker };
            if (attackerRuntimeSession is not null)
            {
                updatedAttackerSession = attackerRuntimeSession with { Character = drainingTouchAttacker };
                attackerRuntimeSession = updatedAttackerSession;
                context.Sessions?.Set(context.Connection, updatedAttackerSession);
            }
        }

        var bloodSpawnResult = TryApplyLegacyPlayerReceiveBloodSpawn(
            targetSession,
            context.WorldData,
            out var bloodSpawnTarget);
        if (bloodSpawnResult.IsActive)
        {
            targetSession = targetSession with { Character = bloodSpawnTarget };
            context.Sessions?.Set(target.Connection, targetSession);
        }

        currentHitpoints = CheckedUInt32(targetSession.Character.MinHitPoints, nameof(targetSession.Character.MinHitPoints));

        if (currentHitpoints == 0 || damage == 0)
        {
            context.Sessions?.Set(target.Connection, targetSession);
            return new(CreateLegacyPlayerMagicDamageResult(
                target with { Session = targetSession },
                targetSession,
                currentHitpoints,
                0,
                false,
                modifierResult,
                updatedAttackerSession,
                mirrorOfSinResult: mirrorOfSinResult,
                lightOfStaminaResult: lightOfStaminaResult,
                calmWindResult: calmWindResult,
                divineGuardResult: divineGuardResult,
                killingFlashResult: killingFlashResult,
                drainingTouchResult: drainingTouchResult,
                bloodSpawnResult: bloodSpawnResult,
                infiniteMistResult: infiniteMistResult) with
            { ReceiveDamage = damage }, modifierResult.Attacker);
        }

        var applied = Math.Min(damage, currentHitpoints);
        var killed = applied >= currentHitpoints;
        var responseEffectsResult = !killed
            ? TryApplyLegacyPlayerReceiveResponseEffects(targetSession.Character)
            : default;
        var pending = CreateLegacyPlayerMagicDamageResult(
            target with { Session = targetSession },
            targetSession,
            currentHitpoints,
            applied,
            killed,
            modifierResult,
            updatedAttackerSession,
            mirrorOfSinResult: mirrorOfSinResult,
            lightOfStaminaResult: lightOfStaminaResult,
            calmWindResult: calmWindResult,
            divineGuardResult: divineGuardResult,
            killingFlashResult: killingFlashResult,
            drainingTouchResult: drainingTouchResult,
            bloodSpawnResult: bloodSpawnResult,
            responseEffectsResult: responseEffectsResult,
            infiniteMistResult: infiniteMistResult) with
        { ReceiveDamage = damage };
        return new(pending, modifierResult.Attacker,
            RequiresDamageCommit: true,
            FinishAttackerTransform: killed && LegacyTransformState.TryClear(modifierResult.Attacker, out _),
            FinishTargetTransform: killed && LegacyTransformState.TryClear(targetSession.Character, out _),
            DeathX: killed ? CheckedUInt16(targetSession.Character.X, nameof(targetSession.Character.X)) : (ushort)0,
            DeathY: killed ? CheckedUInt16(targetSession.Character.Y, nameof(targetSession.Character.Y)) : (ushort)0);
    }

    private static async ValueTask SendLegacyPlayerMagicPreAnimationUpdatesAsync(
        WorldPacketHandlerContext context,
        WorldSession attacker,
        LegacyPlayerMagicDamageResult result,
        CancellationToken cancellationToken)
    {
        await SendLegacyPlayerReceiveInfiniteMistAsync(context, attacker, result, cancellationToken)
            .ConfigureAwait(false);

        await SendLegacyPlayerReceiveResponseEffectsAsync(context, result, cancellationToken)
            .ConfigureAwait(false);

        await SendLegacyPlayerReceiveQualifierScoreAsync(context, attacker, result, cancellationToken)
            .ConfigureAwait(false);

        if (!result.BloodSpawnResult.IsActive)
        {
            return;
        }

        var targetSession = result.UpdatedSession;
        var targetUid = CheckedUInt32(targetSession.Character.Uid, nameof(targetSession.Character.Uid));
        var effect = LegacyMsgRefineEffectPacket.BloodSpawn(targetUid).ToFrameBytes();
        await SendFrameToPlayerAndVisiblePeersAsync(
                context,
                result.ActiveSession.Connection,
                targetSession,
                effect,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy BloodSpawn player-hit refine effect " +
                $"{FormatSession(context with { Session = targetSession })} targetUid={targetUid}; " +
                "source=ReceiveAttack.Player.Execute BloodSpawn.")
            .ConfigureAwait(false);

        if (!result.BloodSpawnResult.ResourcesRestored)
        {
            return;
        }

        foreach (var update in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(
            targetUid,
            result.BloodSpawnResult.MaxHitpoints,
            result.BloodSpawnResult.Hitpoints))
        {
            await result.ActiveSession.Connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        await SendScalarUpdateAsync(
                context with
                {
                    Connection = result.ActiveSession.Connection,
                    Session = targetSession,
                },
                targetSession.Character,
                LegacyMsgUpdateDataType.Mana,
                result.BloodSpawnResult.Mana,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy BloodSpawn player-hit resource restore " +
                $"{FormatSession(context with { Session = targetSession })} " +
                $"hitpoints={result.BloodSpawnResult.Hitpoints} mana={result.BloodSpawnResult.Mana}; " +
                "source=ReceiveAttack.Player.Execute BloodSpawn.")
            .ConfigureAwait(false);
    }

    private static bool ShouldPublishLegacyPlayerMagicUnchangedHitpoints(LegacyPlayerMagicDamageResult result)
    {
        // Receive.Player:799 reaches the HP setter for ordinary zero hits, including NeptuneCurse.
        return result.ReceiveDamage == 0 && result.PreviousHitpoints > 0 && !result.TournamentOutcome.IsActive;
    }

    private static async ValueTask SendLegacyPlayerMagicDamageUpdatesAsync(
        WorldPacketHandlerContext context,
        WorldSession attacker,
        LegacyPlayerMagicDamageResult result,
        CancellationToken cancellationToken)
    {
        var targetSession = result.UpdatedSession;
        var targetUid = CheckedUInt32(targetSession.Character.Uid, nameof(targetSession.Character.Uid));
        if (!result.EarlyModifierPhasesPublished && result.LightningShieldActivationLeft is { } activationLeft)
        {
            await SendLegacyLightningShieldAzureUpdateAsync(
                    context with { Connection = result.ActiveSession.Connection, Session = targetSession },
                    targetSession,
                    activationLeft,
                    result.LightningShieldSpellLevel,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (result.LightningShieldDrainLeft is { } drainLeft)
        {
            if (!result.EarlyModifierPhasesPublished)
            {
                await SendLegacyLightningShieldAzureUpdateAsync(
                        context with { Connection = result.ActiveSession.Connection, Session = targetSession },
                        targetSession,
                        drainLeft,
                        result.LightningShieldSpellLevel,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy LightningShield player-hit absorption " +
                    $"{FormatSession(context with { Session = targetSession })} shieldLeft={drainLeft} " +
                    $"damage={result.DamageApplied}; source=ReceiveAttack.Player.Execute LightningShieldActivated.")
                .ConfigureAwait(false);
        }

        if (result.TournamentOutcome.Kind == LegacyPlayerReceiveTournamentOutcomeKind.SkillTournamentLifeLost)
        {
            await result.ActiveSession.Connection.SendAsync(
                    LegacyMsgMessagePacket
                        .SystemCenter($"You have {result.TournamentOutcome.SkillTournamentLifesLeft} more life's left.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy SkillTournament player-hit life decrement " +
                    $"{FormatSession(context with { Session = targetSession })} " +
                    $"lifesLeft={result.TournamentOutcome.SkillTournamentLifesLeft}; " +
                    "source=ReceiveAttack.Player.Execute lines 577-622.")
                .ConfigureAwait(false);
        }

        if (result.TournamentOutcome.SendsSkillTournamentDeathEffect)
        {
            var effect = LegacyStringPacket.PlayerEffect(targetUid, "accession1").ToFrameBytes();
            await SendFrameToPlayerAndVisiblePeersAsync(
                    context,
                    result.ActiveSession.Connection,
                    targetSession,
                    effect,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy SkillTournament player-hit final death " +
                    $"{FormatSession(context with { Session = targetSession })} targetUid={targetUid}; " +
                    "source=ReceiveAttack.Player.Execute accession1/death branch.")
                .ConfigureAwait(false);
        }

        if (result.MirrorOfSinResult.IsActive)
        {
            var effect = LegacyMsgRefineEffectPacket.MirrorOfSin(targetUid).ToFrameBytes();
            await SendFrameToPlayerAndVisiblePeersAsync(
                    context,
                    context.Connection,
                    attacker,
                    effect,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy MirrorOfSin player-hit refine effect " +
                    $"{FormatSession(context with { Session = attacker })} targetUid={targetUid}; " +
                    "source=ReceiveAttack.Player.Execute MirrorOfSin.")
                .ConfigureAwait(false);
        }

        if (result.LightOfStaminaResult.IsActive)
        {
            var effect = LegacyMsgRefineEffectPacket.LightOfStamina(targetUid).ToFrameBytes();
            await SendFrameToPlayerAndVisiblePeersAsync(
                    context,
                    context.Connection,
                    attacker,
                    effect,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy LightOfStamina player-hit refine effect " +
                    $"{FormatSession(context with { Session = attacker })} targetUid={targetUid}; " +
                    "source=ReceiveAttack.Player.Execute LightOfStamina.")
                .ConfigureAwait(false);
        }

        if (result.CalmWindResult.IsActive)
        {
            var attackerUid = CheckedUInt32(attacker.Character.Uid, nameof(attacker.Character.Uid));
            var effect = LegacyMsgRefineEffectPacket.CalmWind(attackerUid).ToFrameBytes();
            await SendFrameToPlayerAndVisiblePeersAsync(
                    context,
                    context.Connection,
                    attacker,
                    effect,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy CalmWind player-hit refine effect " +
                    $"{FormatSession(context with { Session = attacker })} attackerUid={attackerUid}; " +
                    "source=ReceiveAttack.Player.Execute CalmWind.")
                .ConfigureAwait(false);
        }

        if (result.DivineGuardResult.IsActive)
        {
            var attackerUid = CheckedUInt32(attacker.Character.Uid, nameof(attacker.Character.Uid));
            var effect = LegacyMsgRefineEffectPacket.DivineGuard(attackerUid).ToFrameBytes();
            await SendFrameToPlayerAndVisiblePeersAsync(
                    context,
                    context.Connection,
                    attacker,
                    effect,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy DivineGuard player-hit refine effect " +
                    $"{FormatSession(context with { Session = attacker })} attackerUid={attackerUid}; " +
                    "source=ReceiveAttack.Player.Execute DivineGuard.")
                .ConfigureAwait(false);
        }

        if (result.KillingFlashResult.IsActive)
        {
            var attackerUid = CheckedUInt32(attacker.Character.Uid, nameof(attacker.Character.Uid));
            var effect = LegacyMsgRefineEffectPacket.KillingFlash(attackerUid).ToFrameBytes();
            await SendFrameToPlayerAndVisiblePeersAsync(
                    context,
                    context.Connection,
                    attacker,
                    effect,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy KillingFlash player-hit refine effect " +
                    $"{FormatSession(context with { Session = attacker })} attackerUid={attackerUid}; " +
                    "source=ReceiveAttack.Player.Execute KillingFlash.")
                .ConfigureAwait(false);
        }

        if (result.DrainingTouchResult.IsActive)
        {
            var attackerUid = CheckedUInt32(attacker.Character.Uid, nameof(attacker.Character.Uid));
            var effect = LegacyMsgRefineEffectPacket.DrainingTouch(attackerUid).ToFrameBytes();
            await SendFrameToPlayerAndVisiblePeersAsync(
                    context,
                    context.Connection,
                    attacker,
                    effect,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy DrainingTouch player-hit refine effect " +
                    $"{FormatSession(context with { Session = attacker })} attackerUid={attackerUid}; " +
                    "source=ReceiveAttack.Player.Execute DrainingTouch.")
                .ConfigureAwait(false);
        }

        if (!result.NormalDeathPhasesPublished &&
            (result.DamageApplied != 0 || ShouldPublishLegacyPlayerMagicUnchangedHitpoints(result)))
        {
            var maxHitpoints = GetLegacyEquipmentRuntimeMaxHitpoints(
                targetSession, context.WorldData, result.PreviousHitpoints);

            foreach (var update in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(
                targetUid,
                maxHitpoints,
                CheckedUInt32(targetSession.Character.MinHitPoints, nameof(targetSession.Character.MinHitPoints))))
            {
                await result.ActiveSession.Connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if ((result.HawksAmbitionConsumed && !result.EarlyModifierPhasesPublished) ||
            result.DuelCleanupApplied ||
            result.TargetAbsolutionConsumed ||
            result.TournamentOutcome.SendsTargetStatusVector)
        {
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    context,
                    result.ActiveSession.Connection,
                    targetSession,
                    cancellationToken)
                .ConfigureAwait(false);
            if (result.HawksAmbitionConsumed && !result.EarlyModifierPhasesPublished)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer consumed legacy HawksAmbition player-hit reduction " +
                        $"{FormatSession(context with { Session = targetSession })} statusFlag={LegacyHawksAmbitionFlag}; " +
                        "source=ReceiveAttack.Player.Execute RemoveFlag(HawksAmbition).")
                    .ConfigureAwait(false);
            }

            if (result.DuelCleanupApplied)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer applied legacy Duel player-hit cleanup " +
                        $"{FormatSession(context with { Session = targetSession })} statusFlag={LegacyDuelFlag}; " +
                        "source=ReceiveAttack.Player.Execute Duel cleanup.")
                    .ConfigureAwait(false);
            }

            if (result.TargetAbsolutionConsumed)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer consumed legacy target Absolution player-hit cleanup " +
                        $"{FormatSession(context with { Session = targetSession })} statusFlag={LegacyAbsolutionFlag}; " +
                        "source=ReceiveAttack.Player.Execute RemoveFlag(Absolution).")
                    .ConfigureAwait(false);
            }

            if (result.TournamentOutcome.Kind == LegacyPlayerReceiveTournamentOutcomeKind.FreezeWarFrozen)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer applied legacy FreezeWar player-hit freeze " +
                        $"{FormatSession(context with { Session = targetSession })} statusFlag={LegacyFreezeFlag} " +
                        $"durationSeconds={LegacyFreezeWarFreezeSeconds}; source=ReceiveAttack.Player.Execute FreezeWar.")
                    .ConfigureAwait(false);
            }

            if (result.TournamentOutcome.Kind == LegacyPlayerReceiveTournamentOutcomeKind.FreezeWarThawed)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer applied legacy FreezeWar player-hit thaw " +
                        $"{FormatSession(context with { Session = targetSession })} statusFlag={LegacyFreezeFlag}; " +
                        "source=ReceiveAttack.Player.Execute FreezeWar same-garment RemoveFlag(Freeze).")
                    .ConfigureAwait(false);
            }

            if (result.TournamentOutcome.Kind == LegacyPlayerReceiveTournamentOutcomeKind.FootballPassedBall)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer applied legacy Football player-hit ball removal " +
                        $"{FormatSession(context with { Session = targetSession })} statusFlag={LegacyFootballBallFlag}; " +
                        "source=ReceiveAttack.Player.Execute FootBall PassTheBall target RemoveFlag(lianhuaran04).")
                    .ConfigureAwait(false);
            }

            if (result.TournamentOutcome.Kind == LegacyPlayerReceiveTournamentOutcomeKind.DragonWarTargetKilled)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer applied legacy DragonWar player-hit target DragonSwing removal " +
                        $"{FormatSession(context with { Session = targetSession })} statusFlag={LegacyDragonSwingFlag}; " +
                        "source=ReceiveAttack.Player.Execute DragonWar attacked.RemoveFlag(DragonSwing).")
                    .ConfigureAwait(false);
            }
        }


        if ((result.AttackerAbsolutionConsumed || result.TournamentOutcome.SendsAttackerStatusVector) &&
            result.UpdatedAttackerSession is { } updatedAttackerSession)
        {
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    context,
                    context.Connection,
                    updatedAttackerSession,
                    cancellationToken)
                .ConfigureAwait(false);
            if (result.AttackerAbsolutionConsumed)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer consumed legacy attacker Absolution player-hit cleanup " +
                        $"{FormatSession(context with { Session = updatedAttackerSession })} statusFlag={LegacyAbsolutionFlag}; " +
                        "source=ReceiveAttack.Player.Execute client.Player.RemoveFlag(Absolution).")
                    .ConfigureAwait(false);
            }

            if (result.TournamentOutcome.Kind == LegacyPlayerReceiveTournamentOutcomeKind.FootballPassedBall)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer applied legacy Football player-hit ball transfer " +
                        $"{FormatSession(context with { Session = updatedAttackerSession })} statusFlag={LegacyFootballBallFlag} " +
                        $"durationSeconds={LegacyFootballBallSeconds}; " +
                        "source=MsgFootball.PassTheBall AddFlag(lianhuaran04).")
                    .ConfigureAwait(false);
            }

            if (result.TournamentOutcome.Kind == LegacyPlayerReceiveTournamentOutcomeKind.DragonWarTargetKilled)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer applied legacy DragonWar player-hit attacker DragonSwing transfer " +
                        $"{FormatSession(context with { Session = updatedAttackerSession })} statusFlag={LegacyDragonSwingFlag} " +
                        $"durationSeconds={LegacyDragonSwingSeconds}; " +
                        "source=ReceiveAttack.Player.Execute DragonWar client.Player.AddFlag(DragonSwing).")
                    .ConfigureAwait(false);
            }
        }

        if (result.TournamentOutcome.DragonWarKillSystemResult.RewardGranted &&
            result.UpdatedAttackerSession is { } dragonWarKillSystemRewardSession)
        {
            await SendLegacyDragonWarKillSystemRewardAsync(
                    context,
                    dragonWarKillSystemRewardSession,
                    result.TournamentOutcome.DragonWarKillSystemResult,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy DragonWar KillerSystem reward " +
                    $"{FormatSession(context with { Session = dragonWarKillSystemRewardSession })} " +
                    $"flag={result.TournamentOutcome.DragonWarKillSystemResult.FlagName} " +
                    $"conquerPoints={result.TournamentOutcome.DragonWarKillSystemResult.ConquerPointsReward} " +
                    $"sound={result.TournamentOutcome.DragonWarKillSystemResult.SoundPath}; " +
                    "source=KillerSystem.Update/Send.")
                .ConfigureAwait(false);
        }
        else if (result.TournamentOutcome.DragonWarKillSystemResult.DeadMarked &&
            result.UpdatedAttackerSession is { } dragonWarKillSystemDeadSession)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy DragonWar KillerSystem CheckDead " +
                    $"{FormatSession(context with { Session = dragonWarKillSystemDeadSession })}; " +
                    "source=KillerSystem.CheckDead.")
                .ConfigureAwait(false);
        }

        if (result.BloodTideActivated && result.UpdatedAttackerSession is { } bloodTideSession)
        {
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    context,
                    context.Connection,
                    bloodTideSession,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyBloodTideFineRainUpdateAsync(
                    context,
                    bloodTideSession,
                    result.BloodTideHitpoints,
                    result.BloodTideDuration,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyBloodTideSelfAnimationAsync(
                    context,
                    bloodTideSession,
                    result.BloodTideSpellLevel,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyBloodTideEquipmentRefreshAsync(
                    context,
                    bloodTideSession,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy BloodTide player-hit activation " +
                    $"{FormatSession(context with { Session = bloodTideSession })} statusFlag={LegacyBloodTideFlag} " +
                    $"bloodTideHP={result.BloodTideHitpoints} duration={result.BloodTideDuration}; " +
                    "source=ReceiveAttack.Player.Execute BloodTide.")
                .ConfigureAwait(false);
        }

        if (result.MirrorOfSinResult.XpListOpened && result.UpdatedAttackerSession is { } mirrorOfSinSession)
        {
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    context,
                    context.Connection,
                    mirrorOfSinSession,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Connection.SendAsync(
                    LegacyMsgUpdatePacket
                        .CreateLegacyRuntimeXPListUpdate(
                            CheckedUInt32(
                                mirrorOfSinSession.Character.Uid,
                                nameof(mirrorOfSinSession.Character.Uid)))
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy MirrorOfSin player-hit XPList grant " +
                    $"{FormatSession(context with { Session = mirrorOfSinSession })} " +
                    $"statusFlag={LegacyRuntimeUpdateTicker.XpListFlag} durationSeconds={LegacyMirrorOfSinXpListSeconds}; " +
                    "source=ReceiveAttack.Player.Execute MirrorOfSin AddFlag(XPList).")
                .ConfigureAwait(false);
        }

        if (result.KillingFlashResult.XpListOpened && result.UpdatedAttackerSession is { } killingFlashSession)
        {
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    context,
                    context.Connection,
                    killingFlashSession,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Connection.SendAsync(
                    LegacyMsgUpdatePacket
                        .CreateLegacyRuntimeXPListUpdate(
                            CheckedUInt32(
                                killingFlashSession.Character.Uid,
                                nameof(killingFlashSession.Character.Uid)))
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy KillingFlash player-hit XPList grant " +
                    $"{FormatSession(context with { Session = killingFlashSession })} " +
                    $"statusFlag={LegacyRuntimeUpdateTicker.XpListFlag} durationSeconds={LegacyKillingFlashXpListSeconds}; " +
                    "source=ReceiveAttack.Player.Execute KillingFlash AddFlag(XPList).")
                .ConfigureAwait(false);
        }

        if (result.DrainingTouchResult.ResourcesRestored &&
            result.UpdatedAttackerSession is { } drainingTouchSession)
        {
            var attackerUid = CheckedUInt32(
                drainingTouchSession.Character.Uid,
                nameof(drainingTouchSession.Character.Uid));
            foreach (var update in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(
                attackerUid,
                result.DrainingTouchResult.MaxHitpoints,
                result.DrainingTouchResult.Hitpoints))
            {
                await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
            }

            await SendScalarUpdateAsync(
                    context with { Session = drainingTouchSession },
                    drainingTouchSession.Character,
                    LegacyMsgUpdateDataType.Mana,
                    result.DrainingTouchResult.Mana,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy DrainingTouch player-hit resource restore " +
                    $"{FormatSession(context with { Session = drainingTouchSession })} " +
                    $"hitpoints={result.DrainingTouchResult.Hitpoints} mana={result.DrainingTouchResult.Mana}; " +
                    "source=ReceiveAttack.Player.Execute DrainingTouch.")
                .ConfigureAwait(false);
        }

        if (result.LightOfStaminaResult.StaminaRestored &&
            result.UpdatedAttackerSession is { } lightOfStaminaSession)
        {
            var attackerUid = CheckedUInt32(
                lightOfStaminaSession.Character.Uid,
                nameof(lightOfStaminaSession.Character.Uid));
            await context.Connection.SendAsync(
                    LegacyMsgUpdatePacket
                        .CreateLegacyRuntimeStaminaTickUpdate(
                            attackerUid,
                            ReadLegacyRuntimeStamina(lightOfStaminaSession.Character))
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy LightOfStamina player-hit stamina restore " +
                    $"{FormatSession(context with { Session = lightOfStaminaSession })} stamina=100; " +
                    "source=ReceiveAttack.Player.Execute LightOfStamina.")
                .ConfigureAwait(false);
        }

        if (result.DivineGuardResult.StatusOpened &&
            result.UpdatedAttackerSession is { } divineGuardSession)
        {
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    context,
                    context.Connection,
                    divineGuardSession,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyDivineGuardAzureShieldUpdateAsync(
                    context,
                    divineGuardSession,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy DivineGuard player-hit status grant " +
                    $"{FormatSession(context with { Session = divineGuardSession })} " +
                    $"statusFlag={LegacyDivineGuardFlag} durationSeconds={LegacyDivineGuardStatusSeconds}; " +
                    "source=ReceiveAttack.Player.Execute DivineGuard AddFlag/AzureShield.")
                .ConfigureAwait(false);
        }

        if (result.CapriceStaminaTransferred && result.UpdatedAttackerSession is { } capriceStaminaSession)
        {
            var attackerUid = CheckedUInt32(
                capriceStaminaSession.Character.Uid,
                nameof(capriceStaminaSession.Character.Uid));
            await context.Connection.SendAsync(
                    LegacyMsgUpdatePacket
                        .CreateLegacyRuntimeStaminaTickUpdate(
                            attackerUid,
                            ReadLegacyRuntimeStamina(capriceStaminaSession.Character))
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await result.ActiveSession.Connection.SendAsync(
                    LegacyMsgUpdatePacket
                        .CreateLegacyRuntimeStaminaTickUpdate(
                            targetUid,
                            ReadLegacyRuntimeStamina(targetSession.Character))
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy Caprice player-hit stamina transfer " +
                    $"{FormatSession(context with { Session = capriceStaminaSession })} " +
                    $"targetUid={targetUid}; source=ReceiveAttack.Player.Execute Caprice branch 1.")
                .ConfigureAwait(false);
        }

        if (result.CapriceXpListRemoved)
        {
            await result.ActiveSession.Connection.SendAsync(
                    LegacyMsgUpdatePacket.CreateLegacyRuntimeXPListUpdate(targetUid, value: 0).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    context,
                    result.ActiveSession.Connection,
                    targetSession,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy Caprice player-hit XPList removal " +
                    $"{FormatSession(context with { Session = targetSession })}; " +
                    "source=ReceiveAttack.Player.Execute Caprice branch 2.")
                .ConfigureAwait(false);
        }

        if (result.ReverseMagicApplied && result.UpdatedAttackerSession is { } reverseMagicSession)
        {
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    context,
                    context.Connection,
                    reverseMagicSession,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy ReverseMagic player-hit reaction " +
                    $"{FormatSession(context with { Session = reverseMagicSession })}; " +
                    "source=ReceiveAttack.Player.Execute ReverseMagic.")
                .ConfigureAwait(false);
        }

        if (result.XpBoosterApplied && result.UpdatedAttackerSession is { } xpBoosterSession)
        {
            await context.Connection.SendAsync(
                    CreateLegacyPlayerReceiveXpDurationUpdate(
                        xpBoosterSession.Character,
                        result.XpBoosterDepleted ? 1UL : 0UL,
                        1)
                    .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy XPBooster player-hit reaction " +
                    $"{FormatSession(context with { Session = xpBoosterSession })} " +
                    $"depleted={result.XpBoosterDepleted}; source=ReceiveAttack.Player.Execute XPBooster.")
                .ConfigureAwait(false);
        }

        if (result.XpKillerApplied)
        {
            await result.ActiveSession.Connection.SendAsync(
                    CreateLegacyPlayerReceiveXpDurationUpdate(targetSession.Character, time: 0, spellLevel: 0)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy XPKiller player-hit reaction " +
                    $"{FormatSession(context with { Session = targetSession })}; " +
                    "source=ReceiveAttack.Player.Execute XPKiller.")
                .ConfigureAwait(false);
        }

        if (result.TidalWaveApplied && result.UpdatedAttackerSession is { } tidalWaveSession)
        {
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    context,
                    context.Connection,
                    tidalWaveSession,
                    cancellationToken)
                .ConfigureAwait(false);
            await result.ActiveSession.Connection.SendAsync(
                    CreateLegacyPlayerReceiveTidalWaveUpdate(targetSession.Character, result.TidalWaveValue)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy TidalWave player-hit reaction " +
                    $"{FormatSession(context with { Session = tidalWaveSession })} value={result.TidalWaveValue}; " +
                    "source=ReceiveAttack.Player.Execute TidalWave.")
                .ConfigureAwait(false);
        }

        if (result.QuenchApplied)
        {
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    context,
                    result.ActiveSession.Connection,
                    targetSession,
                    cancellationToken)
                .ConfigureAwait(false);
            await result.ActiveSession.Connection.SendAsync(
                    CreateLegacyPlayerReceiveQuenchUpdate(targetSession.Character, result.QuenchValue)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy Quench player-hit reaction " +
                    $"{FormatSession(context with { Session = targetSession })} value={result.QuenchValue}; " +
                    "source=ReceiveAttack.Player.Execute Quench.")
                .ConfigureAwait(false);
        }

        if (result.TargetNeptuneCurseBlocked)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy target NeptuneCurse player-hit damage block " +
                    $"{FormatSession(context with { Session = targetSession })} statusFlag={LegacyNeptuneCurseFlag}; " +
                    "source=ReceiveAttack.Player.Execute target NeptuneCurse branch.")
                .ConfigureAwait(false);
        }

        if (result.AttackerNeptuneCurseCapped)
        {
            var attackerSession = result.UpdatedAttackerSession ?? attacker;
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy attacker NeptuneCurse player-hit damage cap " +
                    $"{FormatSession(context with { Session = attackerSession })} targetUid={targetUid} " +
                    $"damage={result.DamageApplied}; source=ReceiveAttack.Player.Execute attacker NeptuneCurse branch.")
                .ConfigureAwait(false);
        }

        await TrySendLegacyPlayerReceiveEagleEyeCountdownAsync(
                context,
                result.ActiveSession with { Session = targetSession },
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.Killed)
        {
            return;
        }

        targetSession = await SendLegacyOwnedThundercloudDeathCleanupAsync(
                context,
                result.ActiveSession.Connection,
                targetSession,
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.NormalDeathPhasesPublished)
        {
            await SendLegacyPlayerDeadTalkAsync(result.ActiveSession.Connection, cancellationToken)
                .ConfigureAwait(false);
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    context, result.ActiveSession.Connection, targetSession, cancellationToken)
                .ConfigureAwait(false);
        }

        var deathPosition = LegacyPlayerDeathState.ResolveDeadPosition(targetSession.Character);
        var death = new MsgAttackPacket(new MsgAttackQuery(
            Uid: CheckedUInt32(attacker.Character.Uid, nameof(attacker.Character.Uid)),
            OpponentUid: targetUid,
            X: result.NormalDeathPhasesPublished ? deathPosition.InteractionX
                : CheckedUInt16(targetSession.Character.X, nameof(targetSession.Character.X)),
            Y: result.NormalDeathPhasesPublished ? deathPosition.InteractionY
                : CheckedUInt16(targetSession.Character.Y, nameof(targetSession.Character.Y)),
            AttackType: MsgAttackType.Death,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0));
        await SendFrameToPlayerAndVisiblePeersAsync(
                context,
                result.ActiveSession.Connection,
                targetSession,
                death.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static uint CalculateLegacyPlayerPlayerMagicDamage(
        WorldSession attacker,
        WorldSession target,
        LegacyMagicRuntimeSpell spell,
        WorldDataCatalog? worldData,
        LegacyNativeMagicQueryFields? sourceStatus = null)
    {
        var attackerFields = CalculateLegacyRuntimeStatusFields(attacker, worldData);
        var targetFields = CalculateLegacyRuntimeStatusFields(target, worldData);

        if (sourceStatus is { } cached)
        {
            attackerFields[LegacyPlayerStatusPacket.MagicAttackIndex] = cached.MagicAttack;
            attackerFields[LegacyPlayerStatusPacket.MagicPercentIndex] = cached.MagicPercent;
            attackerFields[LegacyPlayerStatusPacket.SkillCStrikeIndex] = cached.SkillCStrike;
            attackerFields[LegacyPlayerStatusPacket.MagicDamageIncreaseIndex] = cached.MagicDamageIncrease;
        }
        var damage = attackerFields[LegacyPlayerStatusPacket.MagicAttackIndex];
        damage = AddLegacySaturating(damage, spell.Damage);
        damage = AddLegacyPercentBonus(damage, attackerFields[LegacyPlayerStatusPacket.MagicPercentIndex]);
        damage = LegacyCombatDamageCalculator.DecreaseLegacyBless(
            damage,
            targetFields[LegacyPlayerStatusPacket.ItemBlessIndex]);

        var magicDefence = targetFields[LegacyPlayerStatusPacket.MagicDefenceIndex];
        damage = damage <= magicDefence ? 1 : damage - magicDefence;
        damage = LegacyCombatDamageCalculator.CalculateLegacyExtraAttack(
            damage,
            attackerFields[LegacyPlayerStatusPacket.MagicDamageIncreaseIndex],
            targetFields[LegacyPlayerStatusPacket.MagicDamageDecreaseIndex]);
        return Math.Max(1, damage);
    }

    private static CharacterSnapshot ApplyLegacyPlayerPlayerDeath(
        CharacterSnapshot character,
        CharacterSnapshot attacker)
    {
        return LegacyPlayerDeathState.ApplyDeath(
                character,
                DateTimeOffset.UtcNow,
                CheckedUInt16(character.X, nameof(character.X)),
                CheckedUInt16(character.Y, nameof(character.Y)),
                CheckedUInt32(attacker.Uid, nameof(attacker.Uid)),
                attacker.Name)
            .Character;
    }

    private static ValueTask SendLegacyPlayerDeadTalkAsync(
        ConquerTcpConnection connection,
        CancellationToken cancellationToken)
    {
        return connection.SendAsync(
            LegacyMsgMessagePacket.SystemTalk(LegacyPlayerDeathState.DeadTalkText).ToFrameBytes(),
            cancellationToken);
    }

}
