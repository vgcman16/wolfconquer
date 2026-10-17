using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyDustSigilStunnExpiresAtUtcAttribute =
        "LegacyDustSigilStunnExpiresAtUtc";

    internal const string RuntimeLegacyDustSigilStunnRemoveOnDeadAttribute =
        "LegacyDustSigilStunnRemoveOnDead";

    private const string RuntimeLegacyDustSigilStunnRollAttribute = "LegacyDustSigilStunnRoll";

    private const uint LegacyDustSigilStunnDefaultRate = 8;

    private const uint LegacyDustSigilStunnHigherBattlePowerRate = 2;

    private const uint LegacyDustSigilStunnDurationSeconds = 2;

    private static async ValueTask<bool> HandleLegacyPlayerDustDetachmentLayTrapMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (!LegacyLayTrap.IsDustDetachmentSpellId(spell.Id))
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

        var character = resourceSession.Character;
        var casterUid = CheckedUInt32(character.Uid, nameof(character.Uid));
        var casterX = CheckedUInt16(character.X, nameof(character.X));
        var casterY = CheckedUInt16(character.Y, nameof(character.Y));
        var spellLevel = (byte)Math.Min(playerSpell.Level, byte.MaxValue);
        var floorPacket = new LegacyFloorItemPacket(
            NextLegacyFloorItemUid(),
            LegacyLayTrap.DustDetachmentFloorItemId,
            query.X,
            query.Y,
            MaxLife: 0,
            LegacyFloorItemDropType.Effect,
            Life: 0,
            LegacyLayTrap.DustDetachmentFloorColor,
            ItemOwnerUid: 0,
            ReadAttributeUInt32(character.Attributes, "GuildID", "GuildId"),
            FlowerType: 0,
            Timer: 0,
            casterX,
            casterY,
            Name: "");

        var now = DateTimeOffset.UtcNow;
        var storedFloorEffectCount = 0;
        if (context.FloorItems is { } floorItems)
        {
            var effect = new FloorItemRuntimeEffect(
                spell.Id,
                casterX,
                casterY,
                now.AddMilliseconds(LegacyLayTrap.DustDetachmentInvokeMilliseconds),
                Distance: spell.MaxTargets,
                HitRadius: spell.MaxTargets,
                RemovalVisibilityRadius: LegacyLayTrap.DustDetachmentRemovalVisibilityRadius,
                RepeatMilliseconds: LegacyLayTrap.DustDetachmentRepeatMilliseconds,
                SpellLevel: spellLevel,
                LevelHu: playerSpell.UseSpellSoul,
                RuntimeOwnerUid: casterUid,
                NextRepeatAt: now);
            floorItems.AddVisible(FloorItemRuntimeEntry.ForEffect(character.MapId, floorPacket, effect), now);
            storedFloorEffectCount = 1;
        }
        else
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        FloorItemRuntimeStateResource,
                        "dustdetachment-floor-effect-runtime",
                        "DustDetachment floor effect was sent, but floor item runtime state is not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                            LegacyParityGapDiagnostics.Fact("floorUid", floorPacket.Uid),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                        ]))
                .ConfigureAwait(false);
        }

        await SendFloorItemVisibleAsync(context, character, floorPacket, cancellationToken)
            .ConfigureAwait(false);

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var killedTrainingSobNpcTargets = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var eventSobNpcSideEffectTargets = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            LegacySobNpcDeathBehavior DeathBehavior)>();
        var ownerContext = context with { Session = resourceSession };
        if (context.SobNpcStates is { } sobNpcStates)
        {
            foreach (var target in EnumerateVisibleSobNpcMagicTargets(ownerContext, resourceSession.Character))
            {
                if (LegacyGetDistance(casterX, casterY, target.SobNpc.X, target.SobNpc.Y) > LegacyLayTrap.DustDetachmentCastSobRadius ||
                    !CanLegacySobNpcMagicDamageTarget(ownerContext, resourceSession.Character, target.SobNpc, out _))
                {
                    continue;
                }

                var damage = CalculateLegacySobNpcPhysicalSpellDamage(
                    resourceSession,
                    spell,
                    context.WorldData,
                    damageMultiplier: 1);
                damage = MulDivToUInt32(
                    CalculateLegacyDustDetachmentSoulDamage(damage, playerSpell.UseSpellSoul),
                    1,
                    2);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc, context.WorldData?.StaticStatues);
                var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                if (result.AlreadyDead || result.DamageApplied == 0)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id)),
                    damage,
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

        var animation = new LegacySpellAnimationPacket(
            casterUid,
            AttackedUid: 0,
            query.X,
            query.Y,
            spell.Id,
            spellLevel,
            NextSpell: 0,
            playerSpell.UseSpellSoul,
            SpellEffect: 0,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(ownerContext, resourceSession, animation, cancellationToken)
            .ConfigureAwait(false);

        foreach (var killed in killedTrainingSobNpcTargets)
        {
            if (context.SobNpcStates is not null)
            {
                await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                        ownerContext,
                        resourceSession,
                        killed.SobNpc,
                        context.SobNpcStates.GetVisible(killed.SobNpc),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        foreach (var eventTarget in eventSobNpcSideEffectTargets)
        {
            await LogLegacySobNpcEventSideEffectGapAsync(
                    ownerContext,
                    eventTarget.SobNpc,
                    eventTarget.Result,
                    spell)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy DustDetachment LayTrap magic " +
                $"{FormatSession(ownerContext)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={spellLevel} soulLevel={playerSpell.UseSpellSoul} casterX={casterX} casterY={casterY} " +
                $"targetX={query.X} targetY={query.Y} floorItemId={floorPacket.ItemId} " +
                $"castSobTargetCount={animationTargets.Count} storedFloorEffectCount={storedFloorEffectCount}; " +
                "source=LayTrap.cs cases 16460/16470.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<int> SendDueLegacyDustDetachmentLayTrapFloorSpellsAsync(
        WorldPacketHandlerContext context,
        IReadOnlyList<WorldActiveSession> activeSessions,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (context.FloorItems is not { } floorItems)
        {
            return 0;
        }

        var processedCount = 0;
        var finalProcessedCount = 0;
        var repeatProcessedCount = 0;
        var animationTargetCount = 0;
        var dustSigilStunnTargetCount = 0;
        var removeViewerCount = 0;
        foreach (var spellId in new[] { LegacyLayTrap.DustDetachmentSpellId, LegacyLayTrap.DustDetachmentPassiveSpellId })
        {
            var repeatPulses = floorItems.ConsumeDueRepeatEffects(spellId, now);
            var repeatResult = await SendDueLegacyDustDetachmentRepeatPulsesAsync(
                    context,
                    activeSessions,
                    repeatPulses,
                    now,
                    cancellationToken)
                .ConfigureAwait(false);
            processedCount += repeatResult.ProcessedCount;
            repeatProcessedCount += repeatResult.ProcessedCount;
            animationTargetCount += repeatResult.AnimationTargetCount;
            dustSigilStunnTargetCount += repeatResult.DustSigilStunnTargetCount;

            var dueEffects = floorItems.ConsumeDueEffects(spellId, now);
            foreach (var entry in dueEffects)
            {
                if (entry.Effect is not { } effect)
                {
                    continue;
                }

                removeViewerCount += await SendLegacyLayTrapRemoveEffectAsync(
                        entry,
                        effect,
                        activeSessions,
                        cancellationToken,
                        visibilityCenterX: effect.OriginX,
                        visibilityCenterY: effect.OriginY)
                    .ConfigureAwait(false);
                processedCount++;
                finalProcessedCount++;
            }
        }

        if (repeatProcessedCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy DustDetachment LayTrap repeat pulse runtime " +
                    $"pulseCount={repeatProcessedCount} targetCount={animationTargetCount} " +
                    $"dustSigilStunnTargetCount={dustSigilStunnTargetCount}; " +
                    "source=ThreadPool.cs cases 16460/16470 CheckElseInvocke bomb=1; " +
                    "DustSigilStunn gear stun side effect runs after player damage when the caster has Ninja.ItemType.DustSigilStunn.")
                .ConfigureAwait(false);
        }

        if (finalProcessedCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy DustDetachment LayTrap final floor spell removal " +
                    $"trapCount={finalProcessedCount} removeViewerCount={removeViewerCount}; " +
                    "source=ThreadPool.cs cases 16460/16470 CheckInvocke sends RemoveEffect only.")
                .ConfigureAwait(false);
        }

        return processedCount;
    }

    private static async ValueTask<(int ProcessedCount, int AnimationTargetCount, int DustSigilStunnTargetCount)> SendDueLegacyDustDetachmentRepeatPulsesAsync(
        WorldPacketHandlerContext context,
        IReadOnlyList<WorldActiveSession> activeSessions,
        IReadOnlyList<FloorItemRuntimeEntry> repeatPulses,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (repeatPulses.Count == 0)
        {
            return (0, 0, 0);
        }

        var processedCount = 0;
        var animationTargetCount = 0;
        var dustSigilStunnTargetCount = 0;
        foreach (var entry in repeatPulses)
        {
            if (entry.Effect is not { } effect)
            {
                continue;
            }

            var ownerUid = effect.RuntimeOwnerUid != 0 ? effect.RuntimeOwnerUid : entry.Packet.ItemOwnerUid;
            if (ownerUid == 0 ||
                context.WorldData is not { } worldData ||
                !TryResolveLegacyLayTrapSourceCaseRuntimeSpell(worldData, effect, out var spell) ||
                !TryGetActiveSessionByUid(activeSessions, ownerUid, out var owner) ||
                owner.Session.Character.MapId != entry.MapId)
            {
                continue;
            }

            var ownerContext = context with
            {
                Connection = owner.Connection,
                Session = owner.Session,
            };
            var ownerSession = owner.Session;
            var animationTargets = new List<LegacySpellAnimationTarget>();
            var monsterResults = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result)>();
            var playerResults = new List<LegacyPlayerMagicDamageResult>();

            if (context.MonsterStates is { } monsterStates)
            {
                foreach (var target in EnumerateVisibleMonsterTargets(ownerContext, ownerSession.Character))
                {
                    if (!IsWithinLegacyLayTrapHitRadius(entry, target.Monster.X, target.Monster.Y) ||
                        !CanLegacyPlayerAttackMonster(ownerSession.Character, target.Monster, target.MonsterFamily, out _))
                    {
                        continue;
                    }

                    var damage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                        ownerSession,
                        worldData,
                        target.Monster,
                        target.MonsterFamily,
                        spell);
                    var result = ApplyLegacyPlayerMonsterDamage(
                        monsterStates,
                        target.Monster,
                        damage,
                        now,
                        ownerSession);
                    monsterResults.Add((target, result));
                    if (result.AlreadyDead || result.DamageApplied == 0)
                    {
                        continue;
                    }

                    animationTargets.Add(new LegacySpellAnimationTarget(
                        target.Monster.Uid,
                        MulDivToUInt32(CalculateLegacyDustDetachmentSoulDamage(damage, effect.LevelHu), 1, 4),
                        Hit: true,
                        MsgAttackEffect.None));
                }
            }

            var ownerLostDuringReceive = false;
            foreach (var target in EnumerateVisiblePlayerMagicTargets(ownerContext, ownerSession, spell))
            {
                var targetCharacter = target.ActiveSession.Session.Character;
                if (!IsWithinLegacyLayTrapHitRadius(
                        entry,
                        CheckedUInt16(targetCharacter.X, nameof(targetCharacter.X)),
                        CheckedUInt16(targetCharacter.Y, nameof(targetCharacter.Y))) ||
                    !CanLegacyPlayerMagicDamageTarget(ownerContext, ownerSession, target.ActiveSession.Session, spell, out _))
                {
                    continue;
                }

                var damage = CalculateLegacyPlayerPlayerPhysicalSpellDamage(
                    ownerSession,
                    target.ActiveSession.Session,
                    spell,
                    worldData,
                    damageDivisor: 1);
                var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                        ownerContext,
                        target.ActiveSession,
                        ownerSession.Character,
                        damage,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (receiveOutcome.AttackerSession is { } receiveOwner)
                {
                    ownerSession = receiveOwner;
                    ownerContext = ownerContext with { Session = ownerSession };
                }

                if (receiveOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
                {
                    ownerLostDuringReceive = true;
                    break;
                }

                if (receiveOutcome.Result is not { } result)
                {
                    continue;
                }

                if (result.DamageApplied == 0)
                {
                    continue;
                }

                var stunResult = await TryApplyLegacyDustSigilStunnAsync(
                        ownerContext,
                        ownerSession,
                        effect.SpellId,
                        result,
                        now,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (stunResult.Applied)
                {
                    result = stunResult.Result;
                    dustSigilStunnTargetCount++;
                }

                playerResults.Add(result);
                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                    damage,
                    Hit: true,
                    MsgAttackEffect.None));
            }

            if (ownerLostDuringReceive)
            {
                // Abort only this consumed pulse; repeating/final cleanup remains with its owner.
                processedCount++;
                continue;
            }

            var animation = LegacySpellAnimationPacket.ForPlayerMagic(
                entry.Packet.Uid,
                entry.Packet.X,
                entry.Packet.Y,
                effect.SpellId,
                effect.SpellLevel,
                effect.LevelHu,
                animationTargets,
                bombFlag: 1);
            await SendPlayerSpellAnimationToVisibleAsync(ownerContext, ownerSession, animation, cancellationToken)
                .ConfigureAwait(false);

            var updatedOwnerSession = ownerSession;
            foreach (var (target, result) in monsterResults)
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
                        await owner.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                    }
                }

                if (result.Killed)
                {
                    var death = new MsgAttackPacket(new MsgAttackQuery(
                        Uid: ownerUid,
                        OpponentUid: target.Monster.Uid,
                        X: target.Monster.X,
                        Y: target.Monster.Y,
                        AttackType: MsgAttackType.Death,
                        Damage: 0,
                        KilledMonster: 1,
                        ResponseDamage: 0,
                        Effect: MsgAttackEffect.None,
                        Unknown: 0));
                    await SendMonsterCombatFrameToVisibleAsync(
                            ownerContext,
                            ownerSession,
                            target.Monster,
                            death,
                            cancellationToken)
                        .ConfigureAwait(false);
                    await TrySendLegacyMonsterDeathDropAsync(
                            ownerContext,
                            ownerSession,
                            target.Monster,
                            target.MonsterFamily,
                            cancellationToken)
                        .ConfigureAwait(false);
                    updatedOwnerSession = await TryApplyLegacyBossDeathRewardsAsync(
                            ownerContext,
                            updatedOwnerSession,
                            target.Monster,
                            target.MonsterFamily,
                            cancellationToken)
                        .ConfigureAwait(false);
                    ownerContext = ownerContext with { Session = updatedOwnerSession };
                    await TryApplyLegacyMonsterTeamExperienceAsync(
                            ownerContext,
                            updatedOwnerSession,
                            target.Monster,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                updatedOwnerSession = await TryApplyLegacyMonsterExperienceAsync(
                        ownerContext,
                        updatedOwnerSession,
                        target.Monster,
                        target.MonsterFamily,
                        result.DamageApplied,
                        cancellationToken)
                    .ConfigureAwait(false);
                ownerContext = ownerContext with { Session = updatedOwnerSession };
            }

            foreach (var result in playerResults)
            {
                await SendLegacyPlayerMagicDamageUpdatesAsync(
                        ownerContext,
                        updatedOwnerSession,
                        result,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            processedCount++;
            animationTargetCount += animationTargets.Count;
        }

        return (processedCount, animationTargetCount, dustSigilStunnTargetCount);
    }

    private static uint CalculateLegacyDustDetachmentSoulDamage(uint damage, byte useSpellSoul)
    {
        _ = useSpellSoul;
        return damage;
    }

    private static async ValueTask<(LegacyPlayerMagicDamageResult Result, bool Applied)> TryApplyLegacyDustSigilStunnAsync(
        WorldPacketHandlerContext context,
        WorldSession casterSession,
        ushort spellId,
        LegacyPlayerMagicDamageResult result,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (result.Killed || !HasLegacyNinjaDustSigilStunn(casterSession))
        {
            return (result, false);
        }

        var targetSession = result.UpdatedSession;
        if (context.Sessions is { } sessions &&
            sessions.TryGet(result.ActiveSession.Connection, out var currentTargetSession) &&
            currentTargetSession is not null)
        {
            targetSession = currentTargetSession;
        }

        if (targetSession.Character.MinHitPoints <= 0 ||
            LegacyPlayerDeathState.HasStatusFlag(targetSession.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag))
        {
            return (result, false);
        }

        var rate = CalculateLegacyDustSigilStunnRate(casterSession, targetSession);
        if (!CheckLegacyDustSigilStunnRateRoll(casterSession.Character, spellId, rate, denominator: 100))
        {
            return (result, false);
        }

        if (LegacyPlayerDeathState.HasStatusFlag(
                targetSession.Character.Attributes,
                LegacyBossStatusEffect.DizzyFlag))
        {
            return (result, false);
        }

        var updatedTargetCharacter = WithLegacyStatusFlag(
            targetSession.Character,
            LegacyBossStatusEffect.DizzyFlag);
        updatedTargetCharacter = WithCharacterAttribute(
            updatedTargetCharacter,
            RuntimeLegacyDustSigilStunnExpiresAtUtcAttribute,
            now.ToUniversalTime()
                .AddSeconds(LegacyDustSigilStunnDurationSeconds)
                .ToString("O", CultureInfo.InvariantCulture));
        updatedTargetCharacter = WithCharacterAttribute(
            updatedTargetCharacter,
            RuntimeLegacyDustSigilStunnRemoveOnDeadAttribute,
            bool.TrueString);
        var updatedTargetSession = targetSession with
        {
            Character = updatedTargetCharacter,
        };
        context.Sessions?.Set(result.ActiveSession.Connection, updatedTargetSession);
        var updatedResult = result with
        {
            ActiveSession = result.ActiveSession with { Session = updatedTargetSession },
            UpdatedSession = updatedTargetSession,
        };

        await SendPlayerStatusFlagVectorToVisibleAsync(
                context,
                result.ActiveSession.Connection,
                updatedTargetSession,
                cancellationToken)
            .ConfigureAwait(false);
        await SendFrameToPlayerAndVisiblePeersAsync(
                context,
                result.ActiveSession.Connection,
                updatedTargetSession,
                new LegacyGameUpdatePacket(
                        CheckedUInt32(updatedTargetSession.Character.Uid, nameof(updatedTargetSession.Character.Uid)),
                        LegacyGameUpdateDataType.Stun,
                        Active: true,
                        ShownAmount: 0,
                        Time: LegacyDustSigilStunnDurationSeconds,
                        Amount: 0)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy DustDetachment DustSigilStunn side effect " +
                $"{FormatSession(context with { Session = casterSession })} targetUid={updatedTargetSession.Character.Uid} " +
                $"statusFlag={LegacyBossStatusEffect.DizzyFlag} dataType={(uint)LegacyGameUpdateDataType.Stun} " +
                $"seconds={LegacyDustSigilStunnDurationSeconds} rate={rate}; " +
                "source=ThreadPool.cs cases 16460/16470 Ninja.ItemType.DustSigilStunn AddFlag(Dizzy, 2, RemoveOnDead).")
            .ConfigureAwait(false);
        return (updatedResult, true);
    }

    private static uint CalculateLegacyDustSigilStunnRate(
        WorldSession casterSession,
        WorldSession targetSession)
    {
        return CalculateLegacyDustSigilStunnBattlePower(targetSession) >
            CalculateLegacyDustSigilStunnBattlePower(casterSession)
            ? LegacyDustSigilStunnHigherBattlePowerRate
            : LegacyDustSigilStunnDefaultRate;
    }

    private static ulong CalculateLegacyDustSigilStunnBattlePower(WorldSession session)
    {
        var character = session.Character;
        var battlePower = ReadAttributeUInt64(character.Attributes, "BattlePower", "BattlePowerValue");
        if (battlePower != 0)
        {
            return battlePower;
        }

        var guildBattlePower = ReadAttributeUInt64(character.Attributes, "GuildBattlePower");
        var rawExtraBattlePower = ReadAttributeUInt64(character.Attributes, "ExtraBattlePower");
        var extraBattlePower = rawExtraBattlePower > guildBattlePower ? rawExtraBattlePower - guildBattlePower : 0;
        var levelPower = Math.Max(0, character.Level + (character.Reborn * 5));
        return checked((ulong)levelPower +
            extraBattlePower +
            LegacyBattlePowerCalculator.CalculateEquipmentBattlePower(session.PlayerItems, session.AlternateEquipmentActive));
    }

    private static bool CheckLegacyDustSigilStunnRateRoll(
        CharacterSnapshot caster,
        ushort spellId,
        uint rate,
        uint denominator)
    {
        if (denominator == 0)
        {
            return false;
        }

        if (rate >= denominator)
        {
            return true;
        }

        var roll = TryReadUInt32Attribute(
            caster.Attributes,
            out var forcedRoll,
            RuntimeLegacyDustSigilStunnRollAttribute + "." + spellId.ToString(CultureInfo.InvariantCulture),
            RuntimeLegacyDustSigilStunnRollAttribute + spellId.ToString(CultureInfo.InvariantCulture),
            RuntimeLegacyDustSigilStunnRollAttribute,
            RuntimeLegacyCombatGearPassiveRollAttribute + "." + spellId.ToString(CultureInfo.InvariantCulture),
            RuntimeLegacyCombatGearPassiveRollAttribute + spellId.ToString(CultureInfo.InvariantCulture),
            RuntimeLegacyCombatGearPassiveRollAttribute)
            ? forcedRoll % denominator
            : checked((uint)Random.Shared.Next(0, checked((int)denominator)));
        return roll < rate;
    }
}
