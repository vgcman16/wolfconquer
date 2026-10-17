using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> HandleLegacyPlayerMonsterMagicAttackAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        CancellationToken cancellationToken,
        bool sourceFirstFamilyRootEntry = false)
    {
        if (context.Session is not { } session ||
            context.WorldData is not { } worldData)
        {
            return false;
        }

        // Every root call owns fresh invocation metadata, including nested same-spell calls.
        context = context with { RootFirstFamilyInvocation = null };
        if (sourceFirstFamilyRootEntry && await TryRejectLegacyRootFirstFamilyRideAsync(context, session, packet.Query, cancellationToken)
            .ConfigureAwait(false)) return true;
        var pendingLegacyRandomSpell = session.LegacyRandomSpell;
        if (!TryResolveLegacyPlayerMagicAttackQuery(
                session,
                worldData,
                packet.Query,
                out var query,
                out var resolvedSession,
                out var playerSpell,
                out var spell,
                out var resolutionFailureDiagnostic,
                deferFirstFamilyToken: sourceFirstFamilyRootEntry))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy player magic " +
                    $"{FormatSession(context)} spellId={packet.Query.SpellId}; {resolutionFailureDiagnostic}.")
                .ConfigureAwait(false);
            return true;
        }

        if (!ReferenceEquals(session, resolvedSession))
        {
            session = resolvedSession;
            context = context with { Session = session };
            context.Sessions?.Set(context.Connection, session);
        }

        var ignoreAttackStamp = pendingLegacyRandomSpell != 0 &&
            pendingLegacyRandomSpell == spell.Id;

        if (sourceFirstFamilyRootEntry && IsLegacyRootFirstFamilyInvocation(session, spell))
        {
            var entered = await TryEnterLegacyRootFirstFamilyAsync(
                    context, session, query, spell, ignoreAttackStamp: false, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (entered is null) return true;
            session = entered.Value.Session;
            playerSpell = entered.Value.Book;
            spell = entered.Value.Spell;
            context = context with { Session = session, RootFirstFamilyInvocation = entered.Value.Capture };
        }

        if (await HandleLegacyPlayerArrowBladesMagicAsync(
                context, session, query, playerSpell, spell, ignoreAttackStamp, cancellationToken)
            .ConfigureAwait(false)) return true;

        if (await HandleLegacyPlayerLineSourceSilentMagicAsync(
                context,
                session,
                query,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacySourceSilentPlayerMagicAsync(context, query, spell).ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerMountControlMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerShieldBlockMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerOblivionMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerWhirlwindKickMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerPounceMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerRemoveBuffersMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerBurntFrostMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerSwirlingStormMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerPitchingMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerHeavensWrathMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerAirKickMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerMortalDragMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerStarArrowMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerFatalSpinMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerRemoveStaminMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerTransformMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerPetAttachStatusMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerAurasMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerCounterKill16350MagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (spell.Sort == LegacyMagicSort.AttachStatus &&
            await HandleLegacyPlayerAttachStatusMagicAsync(
                    context,
                    query,
                    playerSpell,
                    spell,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerHealingMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerReviveMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerAddManaMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerBreathFocusMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerDispatchXpMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerCallPetMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerPhysicalSpellsMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerHellVortexMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerFatalCrossMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerNebulousHuntSectorBackMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerCleanSweepSectorBackMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerMortalStrikeSectorBackMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerScarOfEarthMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerDragonWhirlMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerEagleEyeMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerShockMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerToxicMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerCruelShadeMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerAttackStatusMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerBlackSpotMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerAddBlackSpotMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerMoveLineMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyCombatGearFloorSpellMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerLayTrapMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerPirateSpecialMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerShurikenVortexMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerManiacDanceMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerSpecialXpSkillMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerKineticSparkMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerSeaBurialWildwindMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerAxeShadowWildwindMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerWhirlShurikenWildwindMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerLightningSlashWildwindMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerWildFireballWildwindMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerCelestialDanceBombMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerClassicBombMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerAxeShadowCelestialDanceStatusMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerFastBladeScentSwordManualLineMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerViperFangLineMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerDragonTailLineMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerDragonSlashLineMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerThunderStrikerLineMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerSpeedGunLineMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerHookMoonLineMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerRageOfWarLineMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyCombatGearSectorAttackMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyCombatGearWidthLineAttackMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyCombatGearCenteredAreaAttackMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyCombatGearDirectLineAttackMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyCombatGearCrackShotMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerStrike13050MagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerStrikeMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerStraightFistMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerSpecialPerimeterMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                ignoreAttackStamp,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerPerimeterDefaultFanMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerPerimeterPlayerSobNpcMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerPlayerMagicAttackAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerSobNpcMagicAttackAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (context.MonsterStates is not { } monsterStates)
        {
            if (TryFindVisibleMonsterTargetByUid(context, session.Character, query.OpponentUid, out var missingRuntimeTarget))
            {
                await LogParityGapAsync(
                        context,
                        LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                            MonsterRuntimeStateResource,
                            "player-monster-magic",
                            "visible monster magic target was found, but monster runtime state is not loaded",
                            facts:
                            [
                                LegacyParityGapDiagnostics.Fact("map", missingRuntimeTarget.Monster.MapId),
                                LegacyParityGapDiagnostics.Fact("monsterId", missingRuntimeTarget.Monster.MonsterId),
                                LegacyParityGapDiagnostics.Fact("opcode", WorldPacketOpcodes.MsgAttack),
                                LegacyParityGapDiagnostics.Fact("sort", (int)spell.Sort),
                                LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                                LegacyParityGapDiagnostics.Fact("targetKind", "monster"),
                            ],
                            dedupeFacts:
                            [
                                LegacyParityGapDiagnostics.Fact("sort", (int)spell.Sort),
                                LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                                LegacyParityGapDiagnostics.Fact("targetKind", "monster"),
                            ]))
                    .ConfigureAwait(false);
            }

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
                    "WorldServer skipped legacy player magic " +
                    $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort}; unsupported monster magic sort.")
                .ConfigureAwait(false);
            return true;
        }

        if (!PassesLegacyPlayerMonsterMagicPreResourceRangeGate(
                context,
                session,
                query,
                spell,
                out var failureDiagnostic))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy player magic " +
                    $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort}; {failureDiagnostic}.")
                .ConfigureAwait(false);
            return true;
        }

        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context,
                session,
                spell,
                cancellationToken).ConfigureAwait(false);
        if (resourceSession is null)
        {
            return true;
        }

        if (!TrySelectLegacyPlayerMonsterMagicTargets(
                context,
                resourceSession,
                query,
                spell,
                out var targets,
                out failureDiagnostic))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy player magic " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} sort={(int)spell.Sort}; {failureDiagnostic}.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = resourceSession;

        var statusFields = CalculateLegacyRuntimeStatusFields(updatedSession, worldData);
        var results = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result, uint RequestedDamage)>();
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var now = DateTimeOffset.UtcNow;

        foreach (var target in targets)
        {
            var damageResult = CalculateLegacyPlayerMonsterVisibleMagicDamageWithEffects(
                updatedSession,
                worldData,
                target,
                spell,
                statusFields);
            updatedSession = await TryConsumeLegacySlayerAsync(
                    context,
                    updatedSession,
                    damageResult.ConsumesSlayer,
                    cancellationToken)
                .ConfigureAwait(false);
            var requestedDamage = damageResult.Damage.Damage;
            var result = ApplyLegacyPlayerMonsterDamage(
                monsterStates,
                target.Monster,
                requestedDamage,
                now,
                updatedSession);
            results.Add((target, result, requestedDamage));

            if (!result.AlreadyDead)
            {
                animationTargets.Add(new LegacySpellAnimationTarget(
                    target.Monster.Uid,
                    GetLegacyPlayerMonsterMagicAnimationDamage(spell, result.RequestedDamage),
                    Hit: result.RequestedDamage > 0,
                    damageResult.Damage.Effect));
            }
        }

        if (animationTargets.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy player magic " +
                    $"{FormatSession(context)} spellId={spell.Id}; all selected monsters are waiting to respawn.")
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
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        LegacyPlayerMonsterMagicTarget? firstRetaliationTarget = null;
        foreach (var (target, result, requestedDamage) in results)
        {
            if (result.AlreadyDead)
            {
                continue;
            }

            if (!result.Killed && LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(target.Monster))
            {
                var damagedMonster = target.Monster with { Hitpoints = result.RemainingHitpoints };
                foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
                {
                    await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                }
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
                await SendMonsterCombatFrameToVisibleAsync(context, updatedSession, target.Monster, death, cancellationToken)
                    .ConfigureAwait(false);
                await TrySendLegacyMonsterDeathDropAsync(
                        context,
                        updatedSession,
                        target.Monster,
                        target.MonsterFamily,
                        cancellationToken)
                    .ConfigureAwait(false);
                updatedSession = await TryApplyLegacyBossDeathRewardsAsync(
                        context,
                        updatedSession,
                        target.Monster,
                        target.MonsterFamily,
                        cancellationToken)
                    .ConfigureAwait(false);
                await TryApplyLegacyMonsterTeamExperienceAsync(
                        context,
                        updatedSession,
                        target.Monster,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (firstRetaliationTarget is null)
            {
                firstRetaliationTarget = target;
            }

            updatedSession = await TryApplyLegacyMonsterExperienceAsync(
                    context,
                    updatedSession,
                    target.Monster,
                    target.MonsterFamily,
                    requestedDamage,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (firstRetaliationTarget is { } retaliationTarget)
        {
            await TrySendLegacyMonsterRetaliationAsync(
                    context,
                    updatedSession,
                    retaliationTarget.Monster,
                    retaliationTarget.MonsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var totalDamage = results
            .Where(result => !result.Result.AlreadyDead)
            .Aggregate(0UL, (total, result) => total + result.RequestedDamage);
        var totalAppliedDamage = results
            .Where(result => !result.Result.AlreadyDead)
            .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);
        var targetDamageAudit = string.Join(
            ",",
            results
                .Where(result => !result.Result.AlreadyDead)
                .Take(5)
                .Select(result =>
                    $"{result.Target.Monster.Uid}:{result.RequestedDamage}/{result.Result.DamageApplied}/" +
                    $"{result.Result.RemainingHitpoints}/{result.Result.Killed}"));
        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                updatedSession,
                ClampLegacyProficiencyRawExperience(totalDamage),
                cancellationToken,
                activity: "player-monster magic weapon proficiency experience")
            .ConfigureAwait(false);
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                totalDamage,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy player magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} targets={animationTargets.Count} totalDamage={totalDamage} " +
                $"totalAppliedDamage={totalAppliedDamage} targetDamageAudit={targetDamageAudit}; " +
                "source=Magic.OnMonster/Physical.OnMonster damage audit.")
            .ConfigureAwait(false);
        return true;
    }

    private readonly record struct LegacyPlayerMonsterVisibleMagicDamageResult(
        LegacyPlayerMonsterMagicDamageResult Damage,
        bool ConsumesSlayer);

    private static uint GetLegacyPlayerMonsterMagicAnimationDamage(
        LegacyMagicRuntimeSpell spell,
        uint requestedDamage)
    {
        return spell.Id is 1_045 or 1_046
            ? LegacyCombatDamageCalculator.AddLegacySaturating(requestedDamage, requestedDamage)
            : requestedDamage;
    }

    private static LegacyPlayerMonsterVisibleMagicDamageResult CalculateLegacyPlayerMonsterVisibleMagicDamageWithEffects(
        WorldSession session,
        WorldDataCatalog worldData,
        LegacyPlayerMonsterMagicTarget target,
        LegacyMagicRuntimeSpell spell,
        IReadOnlyList<uint> statusFields)
    {
        if (UsesLegacyPlayerMonsterPhysicalMagicDamage(spell))
        {
            var physicalDamage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                session,
                worldData,
                target.Monster,
                target.MonsterFamily,
                spell);
            physicalDamage = ApplyLegacyCombatGearWildPhoenixAttackEffect(
                session,
                spell,
                physicalDamage,
                LegacyWildPhoenixDamageTargetKind.MonsterOrSobNpc);
            return new LegacyPlayerMonsterVisibleMagicDamageResult(
                new LegacyPlayerMonsterMagicDamageResult(
                    physicalDamage,
                    MsgAttackEffect.None),
                ConsumesSlayer: false);
        }

        var hasSlayer = HasLegacyStatusFlag(session.Character, LegacySlayerFlag);
        var damageResult = LegacyMagicRuntime.CalculatePlayerMonsterDamageWithEffects(
            session,
            worldData,
            target.MonsterFamily,
            spell,
            statusFields,
            HasLegacyStatusFlag(session.Character, LegacyInfinityFlag),
            ReadAttributeUInt32(session.Character.Attributes, "InfinityDamage", "InfinityMonsterDamage"),
            ReadAttributeUInt32(session.Character.Attributes, "InfinitySDamage", "InfinitySpellDamage"),
            hasSlayer,
            ReadAttributeUInt32(session.Character.Attributes, "SlayerPercent", "SlayerDamagePercent"),
            target.Monster.Hitpoints,
            ReadLegacyPhysicalEffectRoll(session.Character, RuntimeLegacyMagicLuckyStrikeRollAttribute),
            ReadLegacyPhysicalEffectRoll(session.Character, RuntimeLegacyMagicRefineryRollAttribute),
            CalculateLegacyPlayerMonsterMagicDamageBonusPercent(session, worldData, target.Monster));
        damageResult = damageResult with
        {
            Damage = ApplyLegacyCombatGearWildPhoenixAttackEffect(
                session,
                spell,
                damageResult.Damage,
                LegacyWildPhoenixDamageTargetKind.MonsterOrSobNpc),
        };
        return new LegacyPlayerMonsterVisibleMagicDamageResult(
            damageResult,
            ConsumesSlayer: hasSlayer);
    }

    private static bool UsesLegacyPlayerMonsterPhysicalMagicDamage(LegacyMagicRuntimeSpell spell)
    {
        return spell.Sort is LegacyMagicSort.Line ||
            LegacyMagicSortCapabilities.IsSupportedPerimeterVisibleMonsterAttackSpell(spell.Sort, spell.Id) ||
            (spell.Sort == LegacyMagicSort.Sector && spell.Id != 8_001);
    }

    private static bool PassesLegacyPlayerMonsterMagicPreResourceRangeGate(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (spell.Sort is not (LegacyMagicSort.Attack or LegacyMagicSort.DirectAttack))
        {
            return true;
        }

        if (!TryFindVisibleMonsterTargetByUid(context, session.Character, query.OpponentUid, out var target))
        {
            failureDiagnostic = $"monsterUid={query.OpponentUid}; target is not visible";
            return false;
        }

        var distance = LegacyGetDistance(
            session.Character.X,
            session.Character.Y,
            target.Monster.X,
            target.Monster.Y);
        if (distance <= spell.Range)
        {
            return true;
        }

        failureDiagnostic = $"monsterUid={target.Monster.Uid} distance={distance} range={spell.Range}";
        return false;
    }

}
