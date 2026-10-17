using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> HandleLegacyPlayerBlackSpotMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.BlackSpot)
        {
            return false;
        }

        if (spell.Id != LegacyBlackSpotSpellId)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.UnsupportedMagic(
                        spell.Id,
                        sort: (int)spell.Sort,
                        reason:
                            "BlackSpot direct handling is currently modeled only for spell 11120 visible-monster targets."))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy BlackSpot magic " +
                    $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort}; unsupported BlackSpot spell id.")
                .ConfigureAwait(false);
            return true;
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

        if (!TryFindVisibleMonsterTargetByUid(context, resourceSession.Character, query.OpponentUid, out var target))
        {
            if (TryGetVisibleSessionByUid(context, resourceSession, query.OpponentUid, out var playerTarget))
            {
                var distance = LegacyGetDistance(
                    resourceSession.Character.X,
                    resourceSession.Character.Y,
                    playerTarget.Session.Character.X,
                    playerTarget.Session.Character.Y);
                var playerTargetFailureDiagnostic = string.Empty;
                if (distance > LegacyBlackSpotDirectRange ||
                    !CanLegacyPlayerMagicDamageTarget(
                        context,
                        resourceSession,
                        playerTarget.Session,
                        spell,
                        out playerTargetFailureDiagnostic))
                {
                    var emptyAnimation = LegacySpellAnimationPacket.ForPlayerMagic(
                        CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid)),
                        query.X,
                        query.Y,
                        spell.Id,
                        (byte)Math.Min(playerSpell.Level, byte.MaxValue),
                        playerSpell.UseSpellSoul,
                        []);
                    await SendPlayerSpellAnimationToVisibleAsync(context, resourceSession, emptyAnimation, cancellationToken)
                        .ConfigureAwait(false);
                    var skipReason = distance > LegacyBlackSpotDirectRange
                        ? "target is outside direct BlackSpot range"
                        : playerTargetFailureDiagnostic;
                    await context.Log.WriteLineAsync(
                            "WorldServer skipped legacy BlackSpot player magic " +
                            $"{FormatSession(context)} spellId={spell.Id} playerUid={playerTarget.Session.Character.Uid} " +
                            $"distance={distance} range={LegacyBlackSpotDirectRange} reason={skipReason}.")
                        .ConfigureAwait(false);
                    return true;
                }

                var playerBlackSpotNow = DateTimeOffset.UtcNow;
                var activePlayerTarget = playerTarget;
                (activePlayerTarget, var wildPhoenixDefended) =
                    await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                            context,
                            activePlayerTarget,
                            cancellationToken)
                        .ConfigureAwait(false);
                var playerDamage = wildPhoenixDefended
                    ? 1U
                    : CalculateLegacyPlayerPlayerPhysicalSpellDamage(
                        resourceSession,
                        activePlayerTarget.Session,
                        spell,
                        context.WorldData,
                        damageDivisor: 1);
                var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                    context,
                    activePlayerTarget,
                    resourceSession.Character,
                    playerDamage,
                    cancellationToken)
                .ConfigureAwait(false);
                if (receiveOutcome.AttackerSession is { } currentReceiveAttacker)
                {
                    resourceSession = currentReceiveAttacker;
                    context = context with { Session = resourceSession };
                }

                if (receiveOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
                {
                    return true;
                }

                if (receiveOutcome.Result is not { } playerResult)
                {
                    return true;
                }
                if (playerResult.DamageApplied == 0)
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer skipped legacy BlackSpot player magic " +
                            $"{FormatSession(context)} spellId={spell.Id} playerUid={playerTarget.Session.Character.Uid}; " +
                            "target had no applicable damage.")
                        .ConfigureAwait(false);
                    return true;
                }

                var playerAnimation = LegacySpellAnimationPacket.ForPlayerMagic(
                    CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid)),
                    query.X,
                    query.Y,
                    spell.Id,
                    (byte)Math.Min(playerSpell.Level, byte.MaxValue),
                    playerSpell.UseSpellSoul,
                    [
                        new LegacySpellAnimationTarget(
                            CheckedUInt32(playerResult.UpdatedSession.Character.Uid, nameof(playerResult.UpdatedSession.Character.Uid)),
                            playerResult.DamageApplied,
                            Hit: true,
                            MsgAttackEffect.None),
                    ]);
                await SendPlayerSpellAnimationToVisibleAsync(context, resourceSession, playerAnimation, cancellationToken)
                    .ConfigureAwait(false);

                DateTimeOffset? playerBlackSpotExpiresAt = null;
                if (!playerResult.Killed)
                {
                    playerBlackSpotExpiresAt = playerBlackSpotNow + TimeSpan.FromSeconds(spell.Duration);
                    if (TryMarkLegacyPlayerBlackSpotUntil(
                            context,
                            playerResult.ActiveSession,
                            playerBlackSpotExpiresAt.Value,
                            out var updatedTargetSession))
                    {
                        var playerBlackSpot = LegacyBlackspotPacket.CreateActive(
                            CheckedUInt32(updatedTargetSession.Character.Uid, nameof(updatedTargetSession.Character.Uid)));
                        await SendFrameToPlayerAndVisiblePeersAsync(
                                context,
                                context.Connection,
                                resourceSession,
                                playerBlackSpot.ToFrameBytes(),
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                await SendLegacyPlayerMagicDamageUpdatesAsync(
                        context,
                        resourceSession,
                        playerResult,
                        cancellationToken)
                    .ConfigureAwait(false);

                var updatedPlayerBlackSpotSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                        context,
                        resourceSession,
                        playerSpell,
                        spell,
                        playerResult.DamageApplied,
                        cancellationToken)
                    .ConfigureAwait(false);
                var playerBlackSpotExpiryDiagnostic = playerBlackSpotExpiresAt is { } playerBlackSpotExpires
                    ? playerBlackSpotExpires.ToString("O", CultureInfo.InvariantCulture)
                    : "none";
                await context.Log.WriteLineAsync(
                        "WorldServer handled legacy BlackSpot player magic " +
                        $"{FormatSession(context with { Session = updatedPlayerBlackSpotSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                        $"spellLevel={playerSpell.Level} playerUid={playerResult.UpdatedSession.Character.Uid} " +
                        $"damage={playerResult.DamageApplied} remainingHitpoints={playerResult.UpdatedSession.Character.MinHitPoints} " +
                        $"killed={playerResult.Killed} blackSpotExpiresAtUtc={playerBlackSpotExpiryDiagnostic}.")
                    .ConfigureAwait(false);
                return true;
            }

            if (TryFindVisibleSobNpcByUid(context, resourceSession.Character, query.OpponentUid, out var sobNpcTarget))
            {
                var distance = LegacyGetDistance(
                    resourceSession.Character.X,
                    resourceSession.Character.Y,
                    sobNpcTarget.X,
                    sobNpcTarget.Y);
                var sobFailureDiagnostic = string.Empty;
                if (distance > LegacyBlackSpotDirectRange ||
                    !CanLegacySobNpcMagicDamageTarget(context, resourceSession.Character, sobNpcTarget, out sobFailureDiagnostic))
                {
                    var emptyAnimation = LegacySpellAnimationPacket.ForPlayerMagic(
                        CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid)),
                        query.X,
                        query.Y,
                        spell.Id,
                        (byte)Math.Min(playerSpell.Level, byte.MaxValue),
                        playerSpell.UseSpellSoul,
                        []);
                    await SendPlayerSpellAnimationToVisibleAsync(context, resourceSession, emptyAnimation, cancellationToken)
                        .ConfigureAwait(false);
                    var skipReason = distance > LegacyBlackSpotDirectRange
                        ? "target is outside direct BlackSpot range"
                        : sobFailureDiagnostic;
                    await context.Log.WriteLineAsync(
                            "WorldServer skipped legacy BlackSpot SOB magic " +
                            $"{FormatSession(context)} spellId={spell.Id} sobNpcUid={sobNpcTarget.Id} " +
                            $"distance={distance} range={LegacyBlackSpotDirectRange} reason={skipReason}.")
                        .ConfigureAwait(false);
                    return true;
                }

                if (context.SobNpcStates is not { } sobNpcStates)
                {
                    await LogParityGapAsync(
                            context,
                            LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                                SobNpcRuntimeStateResource,
                                "blackspot-sob-magic",
                                "BlackSpot SOB target was found, but SOB runtime state is not loaded",
                                facts:
                                [
                                    LegacyParityGapDiagnostics.Fact("map", sobNpcTarget.MapId),
                                    LegacyParityGapDiagnostics.Fact("opcode", WorldPacketOpcodes.MsgAttack),
                                    LegacyParityGapDiagnostics.Fact("sobNpcKind", sobNpcTarget.Kind),
                                    LegacyParityGapDiagnostics.Fact("sort", (int)spell.Sort),
                                    LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                                    LegacyParityGapDiagnostics.Fact("targetKind", "sob-npc"),
                                ],
                                dedupeFacts:
                                [
                                    LegacyParityGapDiagnostics.Fact("sort", (int)spell.Sort),
                                    LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                                    LegacyParityGapDiagnostics.Fact("targetKind", "sob-npc"),
                                ]))
                        .ConfigureAwait(false);
                    await context.Log.WriteLineAsync(
                            "WorldServer skipped legacy BlackSpot SOB magic " +
                            $"{FormatSession(context)} spellId={spell.Id}; SOB runtime state is not loaded.")
                        .ConfigureAwait(false);
                    return true;
                }

                var sobBlackSpotNow = DateTimeOffset.UtcNow;
                var sobDamage = CalculateLegacySobNpcPhysicalSpellDamage(
                    resourceSession,
                    spell,
                    context.WorldData,
                    damageMultiplier: 1);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(sobNpcTarget);
                var sobResult = sobNpcStates.ApplyDamage(sobNpcTarget, sobDamage, sobBlackSpotNow, deathBehavior);
                if (sobResult.AlreadyDead || sobResult.DamageApplied == 0)
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer skipped legacy BlackSpot SOB magic " +
                            $"{FormatSession(context)} spellId={spell.Id} sobNpcUid={sobNpcTarget.Id}; " +
                            "SOB NPC had no applicable damage.")
                        .ConfigureAwait(false);
                    return true;
                }

                var sobAnimation = LegacySpellAnimationPacket.ForPlayerMagic(
                    CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid)),
                    query.X,
                    query.Y,
                    spell.Id,
                    (byte)Math.Min(playerSpell.Level, byte.MaxValue),
                    playerSpell.UseSpellSoul,
                    [
                        new LegacySpellAnimationTarget(
                            CheckedUInt32(sobNpcTarget.Id, nameof(sobNpcTarget.Id)),
                            sobResult.DamageApplied,
                            Hit: true,
                            MsgAttackEffect.None),
                    ]);
                await SendPlayerSpellAnimationToVisibleAsync(context, resourceSession, sobAnimation, cancellationToken)
                    .ConfigureAwait(false);

                if (sobResult.Killed && LegacySobNpcEventRules.IsTrainingSob(sobNpcTarget))
                {
                    await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                            context,
                            resourceSession,
                            sobNpcTarget,
                            sobNpcStates.GetVisible(sobNpcTarget),
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else if (!LegacySobNpcEventRules.IsTrainingSob(sobNpcTarget))
                {
                    await LogLegacySobNpcEventSideEffectGapAsync(context, sobNpcTarget, sobResult, spell)
                        .ConfigureAwait(false);
                }

                var experienceDamage = CalculateLegacySobNpcExperienceDamage(sobNpcTarget, sobResult);
                var updatedSobCasterSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                        context,
                        resourceSession,
                        playerSpell,
                        spell,
                        experienceDamage,
                        cancellationToken)
                    .ConfigureAwait(false);
                await context.Log.WriteLineAsync(
                        "WorldServer handled legacy BlackSpot SOB magic " +
                        $"{FormatSession(context with { Session = updatedSobCasterSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                        $"spellLevel={playerSpell.Level} sobNpcUid={sobNpcTarget.Id} damage={sobResult.DamageApplied} " +
                        $"remainingHitpoints={sobResult.RemainingHitpoints} killed={sobResult.Killed}.")
                    .ConfigureAwait(false);
                return true;
            }

            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy BlackSpot magic " +
                    $"{FormatSession(context)} spellId={spell.Id} targetUid={query.OpponentUid}; target is not visible.")
                .ConfigureAwait(false);
            return true;
        }

        if (!CanLegacyPlayerAttackMonster(
                resourceSession.Character,
                target.Monster,
                target.MonsterFamily,
                out var monsterAttackFailureDiagnostic))
        {
            var emptyAnimation = LegacySpellAnimationPacket.ForPlayerMagic(
                CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid)),
                query.X,
                query.Y,
                spell.Id,
                (byte)Math.Min(playerSpell.Level, byte.MaxValue),
                playerSpell.UseSpellSoul,
                []);
            await SendPlayerSpellAnimationToVisibleAsync(context, resourceSession, emptyAnimation, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy BlackSpot monster magic " +
                    $"{FormatSession(context)} spellId={spell.Id} monsterUid={target.Monster.Uid}; " +
                    $"{monsterAttackFailureDiagnostic}.")
                .ConfigureAwait(false);
            return true;
        }

        var targetDistance = LegacyGetDistance(
            resourceSession.Character.X,
            resourceSession.Character.Y,
            target.Monster.X,
            target.Monster.Y);
        if (targetDistance > LegacyBlackSpotDirectRange)
        {
            var emptyAnimation = LegacySpellAnimationPacket.ForPlayerMagic(
                CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid)),
                query.X,
                query.Y,
                spell.Id,
                (byte)Math.Min(playerSpell.Level, byte.MaxValue),
                playerSpell.UseSpellSoul,
                []);
            await SendPlayerSpellAnimationToVisibleAsync(context, resourceSession, emptyAnimation, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy BlackSpot monster magic " +
                    $"{FormatSession(context)} spellId={spell.Id} monsterUid={target.Monster.Uid} " +
                    $"distance={targetDistance} range={LegacyBlackSpotDirectRange}.")
                .ConfigureAwait(false);
            return true;
        }

        if (context.MonsterStates is not { } monsterStates)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.UnsupportedMagic(
                        spell.Id,
                        sort: (int)spell.Sort,
                        targetKind: "monster",
                        mapId: TryGetLegacyParityMapId(target.Monster.MapId),
                        resource: MonsterRuntimeStateResource,
                        reason:
                            "BlackSpot visible-monster target was found, but monster runtime state is not loaded."))
                .ConfigureAwait(false);
            return true;
        }

        if (IsLegacyGuardMonsterFamily(target.Monster, target.MonsterFamily))
        {
            resourceSession = await TryApplyLegacyGuardAttackFlashingNameAsync(
                    context,
                    resourceSession,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = resourceSession };
        }

        var now = DateTimeOffset.UtcNow;
        var requestedDamage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
            resourceSession,
            context.WorldData,
            target.Monster,
            target.MonsterFamily,
            spell);
        var result = ApplyLegacyPlayerMonsterDamage(
            monsterStates,
            target.Monster,
            requestedDamage,
            now,
            resourceSession);
        if (result.AlreadyDead)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy BlackSpot monster magic " +
                    $"{FormatSession(context)} spellId={spell.Id} monsterUid={target.Monster.Uid}; " +
                    "monster is waiting to respawn.")
                .ConfigureAwait(false);
            return true;
        }

        var animationTargets = new[]
        {
            new LegacySpellAnimationTarget(
                target.Monster.Uid,
                result.RequestedDamage,
                Hit: result.RequestedDamage > 0,
                MsgAttackEffect.None),
        };
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, resourceSession, animation, cancellationToken)
            .ConfigureAwait(false);

        var updatedSession = resourceSession;
        DateTimeOffset? blackSpotExpiresAt = null;
        if (result.Killed)
        {
            var death = new MsgAttackPacket(new MsgAttackQuery(
                Uid: CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid)),
                OpponentUid: target.Monster.Uid,
                X: target.Monster.X,
                Y: target.Monster.Y,
                AttackType: MsgAttackType.Death,
                Damage: 0,
                KilledMonster: 1,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0));
            await SendMonsterCombatFrameToVisibleAsync(context, resourceSession, target.Monster, death, cancellationToken)
                .ConfigureAwait(false);
            await TrySendLegacyMonsterDeathDropAsync(
                    context,
                    resourceSession,
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
        else
        {
            if (LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(target.Monster))
            {
                var damagedMonster = target.Monster with { Hitpoints = result.RemainingHitpoints };
                foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
                {
                    await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                }
            }

            blackSpotExpiresAt = now + TimeSpan.FromSeconds(spell.Duration);
            if (monsterStates.TryMarkBlackSpotUntil(target.Monster, now, blackSpotExpiresAt.Value))
            {
                var blackSpot = LegacyBlackspotPacket.CreateActive(target.Monster.Uid);
                await SendFrameToPlayerAndVisiblePeersAsync(
                        context,
                        context.Connection,
                        resourceSession,
                        blackSpot.ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);

                await context.Log.WriteLineAsync(
                        "WorldServer marked legacy BlackSpot monster state " +
                        $"{FormatSession(context)} spellId={spell.Id} monsterUid={target.Monster.Uid} " +
                        $"expiresAtUtc={blackSpotExpiresAt.Value:O}.")
                    .ConfigureAwait(false);
            }
        }

        updatedSession = await TryApplyLegacyMonsterExperienceAsync(
                context,
                updatedSession,
                target.Monster,
                target.MonsterFamily,
                result.DamageApplied,
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.Killed)
        {
            await TrySendLegacyMonsterRetaliationAsync(
                    context,
                    updatedSession,
                    target.Monster,
                    target.MonsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                result.DamageApplied,
                cancellationToken)
            .ConfigureAwait(false);

        var blackSpotExpiryDiagnostic = blackSpotExpiresAt is { } expiresAt
            ? expiresAt.ToString("O", CultureInfo.InvariantCulture)
            : "none";
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy BlackSpot monster magic " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} monsterUid={target.Monster.Uid} " +
                $"damage={result.DamageApplied} remainingHitpoints={result.RemainingHitpoints} " +
                $"killed={result.Killed} blackSpotExpiresAtUtc={blackSpotExpiryDiagnostic}.")
            .ConfigureAwait(false);
        _ = updatedSession;
        return true;
    }

    private static bool TryMarkLegacyPlayerBlackSpotUntil(
        WorldPacketHandlerContext context,
        WorldActiveSession target,
        DateTimeOffset blackSpotExpiresAt,
        out WorldSession updatedSession)
    {
        updatedSession = target.Session;
        if (target.Session.Character.MinHitPoints <= 0 ||
            HasLegacyStatusFlag(target.Session.Character, LegacyDeadFlag) ||
            HasLegacyStatusFlag(target.Session.Character, LegacyGhostFlag))
        {
            return false;
        }

        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(target.Session.Character, RuntimeBlackSpotAttribute, "1"),
            RuntimeBlackSpotExpiresAtUtcAttribute,
            blackSpotExpiresAt.ToString("O", CultureInfo.InvariantCulture));
        updatedSession = target.Session with { Character = updatedCharacter };
        context.Sessions?.Set(target.Connection, updatedSession);
        return true;
    }

    private static async ValueTask SendLegacyPlayerBlackSpotActiveReplayIfNeededAsync(
        ConquerTcpConnection connection,
        CharacterSnapshot character,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!IsLegacyPlayerBlackSpotActive(character, now))
        {
            return;
        }

        await connection.SendAsync(
                LegacyBlackspotPacket
                    .CreateActive(CheckedUInt32(character.Uid, nameof(character.Uid)))
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsLegacyPlayerBlackSpotActive(CharacterSnapshot character, DateTimeOffset now)
    {
        if (!character.Attributes.TryGetValue(RuntimeBlackSpotAttribute, out var blackSpot) ||
            blackSpot != "1")
        {
            return false;
        }

        if (!character.Attributes.TryGetValue(RuntimeBlackSpotExpiresAtUtcAttribute, out var rawExpiresAt) ||
            string.IsNullOrWhiteSpace(rawExpiresAt))
        {
            return true;
        }

        return DateTimeOffset.TryParse(
                rawExpiresAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var expiresAt) &&
            expiresAt > now;
    }

}
