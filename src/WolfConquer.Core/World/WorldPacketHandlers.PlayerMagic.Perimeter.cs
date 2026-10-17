using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyLeftChopPerimeterSpellId = 14_570;
    private const ushort LegacyRightChopPerimeterSpellId = 14_580;
    private const int LegacyChopPerimeterSectorSize = 60;
    private const byte LegacyChopPerimeterDistance = 3;

    private static async ValueTask<bool> HandleLegacyPlayerPerimeterPlayerSobNpcMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.Perimeter ||
            !IsLegacyChopPerimeterSpell(spell.Id))
        {
            return false;
        }

        var playerPrimaryMatched = TryGetVisibleSessionByUid(
            context,
            session,
            query.OpponentUid,
            out var primaryPlayer);
        var sobNpcPrimaryMatched = !playerPrimaryMatched &&
            TryFindVisibleSobNpcByUid(context, session.Character, query.OpponentUid, out _);
        if (!playerPrimaryMatched && !sobNpcPrimaryMatched)
        {
            return false;
        }

        var anchoredQuery = playerPrimaryMatched
            ? query with
            {
                X = CheckedUInt16(primaryPlayer.Session.Character.X, nameof(primaryPlayer.Session.Character.X)),
                Y = CheckedUInt16(primaryPlayer.Session.Character.Y, nameof(primaryPlayer.Session.Character.Y)),
            }
            : query;
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

        var updatedSession = resourceSession;
        var playerTargets = SelectLegacyChopPerimeterPlayerTargets(
            context,
            updatedSession,
            anchoredQuery,
            spell);
        var sobNpcTargets = context.SobNpcStates is not null
            ? SelectLegacyChopPerimeterSobNpcTargets(context, updatedSession, anchoredQuery)
            : [];

        updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                updatedSession,
                playerTargets,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var playerResults = new List<(LegacyPlayerMagicDamageResult Result, uint Damage)>();
        var sobNpcResults = new List<(SobNpc SobNpc, SobNpcDamageResult Result, uint Damage)>();
        var killedTrainingSobNpcTargets = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var eventSobNpcSideEffectTargets = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            LegacySobNpcDeathBehavior DeathBehavior)>();

        foreach (var target in playerTargets)
        {
            var activeTarget = target.ActiveSession;
            (activeTarget, var wildPhoenixDefended) =
                await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                        context,
                        activeTarget,
                        cancellationToken)
                    .ConfigureAwait(false);
            var damage = wildPhoenixDefended
                ? 1U
                : CalculateLegacyChopPerimeterPlayerDamage(
                    updatedSession,
                    activeTarget.Session,
                    spell,
                    context.WorldData);
            if (!wildPhoenixDefended)
            {
                damage = ApplyLegacyCombatGearWildPhoenixAttackEffect(
                    updatedSession,
                    spell,
                    damage,
                    LegacyWildPhoenixDamageTargetKind.Player);
            }

            var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                context,
                activeTarget,
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
            if (result.DamageApplied == 0 && !wildPhoenixDefended)
            {
                continue;
            }

            if (result.UpdatedAttackerSession is not null)
            {
                updatedSession = result.UpdatedAttackerSession;
                context = context with { Session = updatedSession };
            }

            playerResults.Add((result, damage));
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                damage,
                Hit: false,
                MsgAttackEffect.None));
        }

        if (context.SobNpcStates is { } sobNpcStates)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var target in sobNpcTargets)
            {
                var damage = CalculateLegacySobNpcPhysicalSpellDamage(
                    updatedSession,
                    spell,
                    context.WorldData,
                    damageMultiplier: 1);
                damage = ApplyLegacyCombatGearWildPhoenixAttackEffect(
                    updatedSession,
                    spell,
                    damage,
                    LegacyWildPhoenixDamageTargetKind.MonsterOrSobNpc);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(
                    target.SobNpc,
                    context.WorldData?.StaticStatues);
                var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                sobNpcResults.Add((target.SobNpc, result, damage));
                if (result.AlreadyDead || result.DamageApplied == 0)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id)),
                    result.DamageApplied,
                    Hit: true,
                    MsgAttackEffect.None));

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

        if (animationTargets.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy LeftChop/RightChop Perimeter player/SOB magic " +
                    $"{FormatSession(context)} spellId={spell.Id}; no source sector targets.")
                .ConfigureAwait(false);
            return true;
        }

        foreach (var (result, _) in playerResults)
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
            anchoredQuery.X,
            anchoredQuery.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        foreach (var (result, _) in playerResults)
        {
            await SendLegacyPlayerMagicDamageUpdatesAsync(
                    context,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (context.SobNpcStates is { } currentSobNpcStates)
        {
            foreach (var killed in killedTrainingSobNpcTargets)
            {
                await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                        context,
                        updatedSession,
                        killed.SobNpc,
                        currentSobNpcStates.GetVisible(killed.SobNpc),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            foreach (var (sobNpc, result, _) in eventSobNpcSideEffectTargets)
            {
                await LogLegacySobNpcEventSideEffectGapAsync(context, sobNpc, result, spell)
                    .ConfigureAwait(false);
            }
        }

        var sobNpcProgress = sobNpcResults.Aggregate(
            0UL,
            (total, result) => total + CalculateLegacySobNpcExperienceDamage(result.SobNpc, result.Result));
        var proficiencyExperienceDamage = sobNpcResults.Aggregate(
            0UL,
            (total, result) => total + result.Result.DamageApplied);
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                sobNpcProgress,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                updatedSession,
                ClampLegacyProficiencyRawExperience(proficiencyExperienceDamage),
                cancellationToken,
                activity: "LeftChop/RightChop Perimeter SOB magic weapon proficiency experience")
            .ConfigureAwait(false);

        var totalAppliedDamage =
            playerResults.Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
            sobNpcResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy LeftChop/RightChop Perimeter player/SOB magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetX={anchoredQuery.X} targetY={anchoredQuery.Y} sectorSize={LegacyChopPerimeterSectorSize} " +
                $"sectorDistance={LegacyChopPerimeterDistance} playerTargets={playerResults.Count} " +
                $"sobNpcTargets={sobNpcResults.Count} sobNpcProgress={sobNpcProgress} " +
                $"totalAppliedDamage={totalAppliedDamage}; source=Perimeter.cs case 14570/14580.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool IsLegacyChopPerimeterSpell(ushort spellId)
    {
        return spellId is LegacyLeftChopPerimeterSpellId or LegacyRightChopPerimeterSpellId;
    }

    private static uint CalculateLegacyChopPerimeterPlayerDamage(
        WorldSession attacker,
        WorldSession target,
        LegacyMagicRuntimeSpell spell,
        WorldDataCatalog? worldData)
    {
        var effectiveSpell = spell.Damage == 0
            ? spell with { Damage = LegacyServerConfigPhysicalDamagePercent }
            : spell;
        return CalculateLegacyPlayerPlayerPhysicalSpellDamage(
            attacker,
            target,
            effectiveSpell,
            worldData,
            damageDivisor: 1);
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyChopPerimeterPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (!IsInsideLegacySector(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    character.X,
                    character.Y,
                    LegacyChopPerimeterSectorSize,
                    LegacyChopPerimeterDistance))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyChopPerimeterSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (!IsInsideLegacySector(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y,
                    LegacyChopPerimeterSectorSize,
                    LegacyChopPerimeterDistance))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }
}
