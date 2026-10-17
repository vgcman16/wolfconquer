using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> HandleLegacyPlayerPhysicalSpellsMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.PhysicalSpells)
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

        var handled = false;
        var sentAnimation = false;
        WorldSession updatedSession = resourceSession;
        if (context.MonsterStates is not null &&
            TryFindVisibleMonsterTargetByUid(context, resourceSession.Character, query.OpponentUid, out var monsterTarget))
        {
            handled = true;
            var inRange = LegacyGetDistance(
                    resourceSession.Character.X,
                    resourceSession.Character.Y,
                    monsterTarget.Monster.X,
                    monsterTarget.Monster.Y) <= LegacyPhysicalSpellsMonsterRange;
            var canAttackMonsterFailure = string.Empty;
            var canAttackMonster = inRange &&
                CanLegacyPlayerAttackMonster(
                    resourceSession.Character,
                    monsterTarget.Monster,
                    monsterTarget.MonsterFamily,
                    out canAttackMonsterFailure);
            if (canAttackMonster)
            {
                if (IsLegacyGuardMonsterFamily(monsterTarget.Monster, monsterTarget.MonsterFamily))
                {
                    updatedSession = await TryApplyLegacyGuardAttackFlashingNameAsync(
                            context,
                            updatedSession,
                            cancellationToken)
                        .ConfigureAwait(false);
                    context = context with { Session = updatedSession };
                }

                updatedSession = await ExecuteLegacyPhysicalSpellsMonsterAsync(
                        context,
                        updatedSession,
                        query,
                        playerSpell,
                        spell,
                        monsterTarget,
                        cancellationToken)
                    .ConfigureAwait(false);
                sentAnimation = true;
            }
            else if (inRange && !string.IsNullOrEmpty(canAttackMonsterFailure))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer rejected legacy PhysicalSpells monster magic " +
                        $"{FormatSession(context)} spellId={spell.Id} targetUid={monsterTarget.Monster.Uid}; " +
                        canAttackMonsterFailure + ".")
                    .ConfigureAwait(false);
            }
        }
        else if (TryGetVisibleSessionByUid(context, resourceSession, query.OpponentUid, out var playerTarget))
        {
            handled = true;
            if (LegacyGetDistance(
                    resourceSession.Character.X,
                    resourceSession.Character.Y,
                    playerTarget.Session.Character.X,
                    playerTarget.Session.Character.Y) <= LegacyPhysicalSpellsPlayerRange &&
                CanLegacyPlayerMagicDamageTarget(context, resourceSession, playerTarget.Session, spell, out _))
            {
                var playerSession = await ExecuteLegacyPhysicalSpellsPlayerAsync(
                        context with { Session = resourceSession },
                        resourceSession,
                        query,
                        playerSpell,
                        spell,
                        playerTarget,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (playerSession is null)
                {
                    return true;
                }
                updatedSession = playerSession;
                context = context with { Session = updatedSession };
                sentAnimation = true;
            }
        }
        else if (context.SobNpcStates is not null &&
            TryFindVisibleSobNpcByUid(context, resourceSession.Character, query.OpponentUid, out var sobNpcTarget))
        {
            handled = true;
            if (LegacyGetDistance(
                    resourceSession.Character.X,
                    resourceSession.Character.Y,
                    sobNpcTarget.X,
                    sobNpcTarget.Y) <= LegacyPhysicalSpellsMonsterRange &&
                CanLegacySobNpcMagicDamageTarget(context, resourceSession.Character, sobNpcTarget, out _))
            {
                updatedSession = await ExecuteLegacyPhysicalSpellsSobNpcAsync(
                        context,
                        resourceSession,
                        query,
                        playerSpell,
                        spell,
                        sobNpcTarget,
                        cancellationToken)
                    .ConfigureAwait(false);
                sentAnimation = true;
            }
        }

        if (!sentAnimation)
        {
            await SendLegacyPhysicalSpellsAnimationAsync(
                    context,
                    resourceSession,
                    query,
                    playerSpell,
                    spell,
                    [],
                    spellEffect: 0,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy PhysicalSpells magic with no modeled target hits " +
                    $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} handledTarget={handled}.")
                .ConfigureAwait(false);
        }

        if (spell.Id == LegacyWindstormSpellId)
        {
            await TrySendLegacyBloomOfDeathFollowUpAsync(
                    context,
                    updatedSession,
                    query,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return true;
    }

    private static async ValueTask<WorldSession> ExecuteLegacyPhysicalSpellsMonsterAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        LegacyPlayerMonsterMagicTarget target,
        CancellationToken cancellationToken)
    {
        if (context.MonsterStates is not { } monsterStates)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        MonsterRuntimeStateResource,
                        "physical-spells-monster-magic",
                        "PhysicalSpells monster magic target was found, but monster runtime state is not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", target.Monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", target.Monster.MonsterId),
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
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy PhysicalSpells monster magic " +
                    $"{FormatSession(context)} spellId={spell.Id}; monster runtime state is not loaded.")
                .ConfigureAwait(false);
            return session;
        }

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var results = new List<(MonsterDamageResult Result, uint RequestedDamage)>();
        var now = DateTimeOffset.UtcNow;
        var hasAssassinRune = HasLegacyAssassinAutoHuntRune(session, context.WorldData);
        var onAutoHunt = ReadAttributeBool(session.Character.Attributes, RuntimeAutoHuntAttribute);
        for (var index = 0; index < LegacyPhysicalSpellsHitCount; index++)
        {
            var requestedDamage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                session,
                context.WorldData,
                target.Monster,
                target.MonsterFamily,
                spell);
            var kylinResult = ApplyLegacyKylinSigilPhysicalSpellDamage(
                context,
                session,
                spell.Id,
                requestedDamage);
            session = kylinResult.Session;
            requestedDamage = kylinResult.Damage;
            requestedDamage = LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourceAssassinAutoHuntRuneBonus(
                requestedDamage,
                hasAssassinRune,
                onAutoHunt);
            var result = ApplyLegacyPlayerMonsterDamage(
                monsterStates,
                target.Monster,
                requestedDamage,
                now,
                session);
            results.Add((result, requestedDamage));
            if (result.AlreadyDead)
            {
                break;
            }

            animationTargets.Add(new LegacySpellAnimationTarget(
                target.Monster.Uid,
                result.RequestedDamage,
                Hit: result.RequestedDamage > 0,
                MsgAttackEffect.None));

            if (result.Killed)
            {
                break;
            }
        }

        await SendLegacyPhysicalSpellsAnimationAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                animationTargets,
                spellEffect: 0,
                cancellationToken)
            .ConfigureAwait(false);

        var updatedSession = session;
        var lastLiveResult = results.LastOrDefault(result => !result.Result.AlreadyDead);
        var killedResult = results.FirstOrDefault(result => result.Result.Killed);
        if (killedResult.Result.Killed)
        {
            var death = new MsgAttackPacket(new MsgAttackQuery(
                Uid: CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
                OpponentUid: target.Monster.Uid,
                X: target.Monster.X,
                Y: target.Monster.Y,
                AttackType: MsgAttackType.Death,
                Damage: 0,
                KilledMonster: 1,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0));
            await SendMonsterCombatFrameToVisibleAsync(context, session, target.Monster, death, cancellationToken)
                .ConfigureAwait(false);
            await TrySendLegacyMonsterDeathDropAsync(
                    context,
                    session,
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
        else if (!lastLiveResult.Result.Equals(default(MonsterDamageResult)) &&
            LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(target.Monster))
        {
            var damagedMonster = target.Monster with { Hitpoints = lastLiveResult.Result.RemainingHitpoints };
            foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
            {
                await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            }

            await TrySendLegacyMonsterRetaliationAsync(
                    context,
                    session,
                    target.Monster,
                    target.MonsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var result in results)
        {
            if (result.Result.AlreadyDead)
            {
                continue;
            }

            updatedSession = await TryApplyLegacyMonsterExperienceAsync(
                    context,
                    updatedSession,
                    target.Monster,
                    target.MonsterFamily,
                    result.RequestedDamage,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var totalDamage = results
            .Where(result => !result.Result.AlreadyDead)
            .Aggregate(0UL, (total, result) => total + result.RequestedDamage);
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                totalDamage,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy PhysicalSpells monster magic " +
                $"{FormatSession(context)} spellId={spell.Id} targetUid={target.Monster.Uid} " +
                $"targets={animationTargets.Count} totalDamage={totalDamage}.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool HasLegacyAssassinAutoHuntRune(WorldSession session, WorldDataCatalog? worldData)
    {
        if (session.Character.Reborn < 2 ||
            session.Character.Level < 15)
        {
            return false;
        }

        foreach (var rune in session.PlayerItems.RuneItems)
        {
            if (!IsLegacyWornRunePosition(rune.Position) ||
                !IsLegacyRuneItem(rune.ItemId) ||
                !TryGetLegacyItemType(worldData?.ItemsById, rune.ItemId, out var itemType) ||
                !ContainsLegacyItemText(itemType, "Assassin"))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsLegacyRuneItem(uint itemId)
    {
        var runeGroup = itemId / 10_000;
        return runeGroup is >= 401 and <= 403;
    }
}
