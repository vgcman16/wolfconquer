using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private enum LegacyCombatGearActiveAttackTargetKind
    {
        Player,
        Monster,
        SobNpc,
    }

    private readonly record struct LegacyCombatGearActiveAttackTarget(
        long Uid,
        long X,
        long Y,
        LegacyCombatGearActiveAttackTargetKind Kind)
    {
        public static LegacyCombatGearActiveAttackTarget FromPlayer(CharacterSnapshot character)
        {
            return new(
                character.Uid,
                character.X,
                character.Y,
                LegacyCombatGearActiveAttackTargetKind.Player);
        }

        public static LegacyCombatGearActiveAttackTarget FromMonster(LegacyMonsterInstance monster)
        {
            return new(
                monster.Uid,
                monster.X,
                monster.Y,
                LegacyCombatGearActiveAttackTargetKind.Monster);
        }

        public static LegacyCombatGearActiveAttackTarget FromSobNpc(SobNpc sobNpc)
        {
            return new(
                sobNpc.Id,
                sobNpc.X,
                sobNpc.Y,
                LegacyCombatGearActiveAttackTargetKind.SobNpc);
        }
    }

    private static async ValueTask<bool> TryHandleLegacyCombatGearMonsterAttackReplacementAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (context.WorldData is not { } worldData || context.MonsterStates is not { } monsterStates)
            return false;
        if (HasLegacyStoneCrackerSource(session) &&
            HasLegacyStatusFlag(session.Character, LegacyArrowBladesFlagRegistration.WaterFlag) &&
            context.ClientCompatibility?.IsLegacyCombatGearMonsterHitPassiveDisabled(
                LegacyCombatGearStoneCrackerWaterAttackSpellId) == true)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CombatGear monster-hit passive replacement " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} spellId={LegacyCombatGearStoneCrackerWaterAttackSpellId}; " +
                    "source=MyCombatGear.CheckCombatGear compatibility fence.").ConfigureAwait(false);
            return false; // Preserve the existing explicit compatibility opt-out/ordinary fallback policy.
        }
        if (await TryHandleLegacyCombatGearArrowBladesCallerAsync(
                context, session, packet.Query with { X = monster.X, Y = monster.Y }, cancellationToken)
            .ConfigureAwait(false)) return true;
        if (!TryResolveLegacyCombatGearActiveAttackSpell(
                session, worldData, LegacyCombatGearActiveAttackTarget.FromMonster(monster),
                out var playerSpell, out var spell)) return false;

        if (context.ClientCompatibility?.IsLegacyCombatGearMonsterHitPassiveDisabled(spell.Id) == true)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CombatGear monster-hit passive replacement " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} spellId={spell.Id}; " +
                    "source=MyCombatGear.CheckCombatGear compatibility fence.")
                .ConfigureAwait(false);
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
            Damage = 0,
            SpellId = spell.Id,
            SpellLevel = playerSpell.Level,
            X = monster.X,
            Y = monster.Y,
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

        var targets = SelectLegacyCombatGearAttackMonsterTargets(context, resourceSession, query, spell);
        if (targets.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CombatGear active monster attack " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} " +
                    $"monsterUid={monster.Uid}; no visible monster matched legacy rectangle.")
                .ConfigureAwait(false);
            return true;
        }

        var now = DateTimeOffset.UtcNow;
        var updatedSession = resourceSession;
        var attackerUid = resourceSession.Character.Uid;
        var results = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result)>();
        var animationTargets = new List<LegacySpellAnimationTarget>();
        foreach (var target in targets)
        {
            var requestedDamage = CalculateLegacyPlayerMonsterDamage(
                context,
                updatedSession,
                target.Monster,
                target.MonsterFamily,
                packet.Query);
            if (HasLegacyStoneCrackerSource(updatedSession))
            {
                if (!TryRefreshLegacyPlayerMagicResourceSession(context, attackerUid, ref updatedSession)) return true;
                updatedSession = WithLegacyThundercloudSight(
                    context with { Session = updatedSession }, updatedSession, target.Monster);
            }
            context = context with { Session = updatedSession };
            var counterSession = await ReceiveLegacyStoneCrackerNonPlayerCounterAsync(
                    context with { Session = updatedSession }, updatedSession,
                    target.Monster.X, target.Monster.Y, cancellationToken).ConfigureAwait(false);
            if (counterSession is null) return true;
            updatedSession = counterSession;
            context = context with { Session = updatedSession };
            var result = ApplyLegacyPlayerMonsterDamage(
                monsterStates,
                target.Monster,
                requestedDamage,
                now,
                updatedSession);
            results.Add((target, result));
            if (result.AlreadyDead)
            {
                continue;
            }

            animationTargets.Add(new LegacySpellAnimationTarget(
                target.Monster.Uid,
                result.DamageApplied,
                Hit: result.DamageApplied > 0,
                packet.Query.Effect));
        }

        if (animationTargets.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CombatGear active monster attack " +
                    $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id}; " +
                    "all selected monsters are waiting to respawn.")
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

        LegacyPlayerMonsterMagicTarget? firstRetaliationTarget = null;
        foreach (var (target, result) in results)
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
                    result.DamageApplied,
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
            .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);
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
                monster,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer replaced legacy monster physical attack with CombatGear spell " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} " +
                $"spellId={spell.Id} sort={(int)spell.Sort} targets={animationTargets.Count} " +
                $"totalDamage={totalDamage}; source=MyCombatGear.CheckCombatGear -> GET_Passive/GetSpell.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool TryResolveLegacyCombatGearActiveAttackSpell(
        WorldSession session,
        WorldDataCatalog worldData,
        LegacyCombatGearActiveAttackTarget target,
        out PlayerSpellSnapshotEntry playerSpell,
        out LegacyMagicRuntimeSpell spell)
    {
        playerSpell = default!;
        spell = default!;

        if (HasLegacyPirateCombatGearEquipped(session) &&
            TryResolveLegacyCombatGearPiratePassiveSpell(session, worldData, out playerSpell, out spell))
        {
            return true;
        }

        var selectedSpells = Array.Empty<ushort>();
        if (HasLegacyCombatGearEquippedForClass(
                session.CombatGear ?? CombatGearSnapshot.Empty,
                session.Character.Class,
                LegacyCombatGearStoneCrackerItemId))
        {
            selectedSpells = LegacyCombatGearStoneCrackerAttackSpells;
            if (HasLegacyStatusFlag(session.Character, LegacyCombatGearWaterTop4WeeklyFlag) &&
                TryResolveLegacyPlayerMagicRuntimeSpell(
                    session,
                    worldData,
                    LegacyCombatGearStoneCrackerWaterAttackSpellId,
                    out playerSpell,
                    out spell))
            {
                return true;
            }
        }

        if (HasLegacyCombatGearEquippedForClass(
                session.CombatGear ?? CombatGearSnapshot.Empty,
                session.Character.Class,
                LegacyCombatGearColdMoonItemId))
        {
            selectedSpells = LegacyCombatGearColdMoonAttackSpells;
        }

        if (HasLegacyCombatGearEquippedForClass(
                session.CombatGear ?? CombatGearSnapshot.Empty,
                session.Character.Class,
                LegacyCombatGearTharnCutterItemId))
        {
            selectedSpells = LegacyCombatGearTharnCutterAttackSpells;
        }

        if (HasLegacyCombatGearEquippedForClass(
                session.CombatGear ?? CombatGearSnapshot.Empty,
                session.Character.Class,
                LegacyCombatGearDragonhowlItemId))
        {
            selectedSpells = LegacyCombatGearDragonhowlAttackSpells;
            if (LegacyGetDistance(session.Character.X, session.Character.Y, target.X, target.Y) <= 3 &&
                TryResolveLegacyCombatGearRateSpell(session, worldData, 17_490, rateDivisor: 1, out playerSpell, out spell))
            {
                return true;
            }

            if (TryResolveLegacyCombatGearRateSpell(session, worldData, 17_650, rateDivisor: 2, out playerSpell, out spell))
            {
                return true;
            }
        }

        if (HasLegacyCombatGearEquippedForClass(
                session.CombatGear ?? CombatGearSnapshot.Empty,
                session.Character.Class,
                LegacyCombatGearBloodlustItemId))
        {
            selectedSpells = LegacyCombatGearBloodlustAttackSpells;
            if (LegacyGetDistance(session.Character.X, session.Character.Y, target.X, target.Y) <= 3 &&
                TryResolveLegacyCombatGearRateSpell(session, worldData, 17_530, rateDivisor: 1, out playerSpell, out spell))
            {
                return true;
            }
        }

        if (HasLegacyCombatGearEquippedForClass(
                session.CombatGear ?? CombatGearSnapshot.Empty,
                session.Character.Class,
                LegacyCombatGearRedcurseItemId))
        {
            selectedSpells = LegacyCombatGearRedcurseAttackSpells;
            if (LegacyGetDistance(session.Character.X, session.Character.Y, target.X, target.Y) <= 3 &&
                TryResolveLegacyCombatGearRateSpell(session, worldData, 17_530, rateDivisor: 1, out _, out _) &&
                TryResolveLegacyPlayerMagicRuntimeSpell(session, worldData, 17_570, out playerSpell, out spell))
            {
                return true;
            }
        }

        return TrySelectLegacyCombatGearAttackSpell(
            session,
            worldData,
            selectedSpells,
            out playerSpell,
            out spell);
    }

    private static bool HasLegacyPirateCombatGearEquipped(WorldSession session)
    {
        if (session.Character.Class / 1000 != 7)
        {
            return false;
        }

        var combatGear = session.CombatGear ?? CombatGearSnapshot.Empty;
        return combatGear.Items.Any(item =>
            item.Equip == 1 &&
            IsLegacyCombatGearForClass(session.Character.Class, item.ItemID));
    }


    private static bool HasLegacyCombatGearActiveAttackCandidate(WorldSession session)
    {
        var combatGear = session.CombatGear ?? CombatGearSnapshot.Empty;
        return combatGear.Items.Any(item =>
            item.Equip == 1 &&
            IsLegacyCombatGearForClass(session.Character.Class, item.ItemID));
    }


    private static bool HasLegacyCombatGearActiveAttackSource(WorldSession session)
    {
        if (HasLegacyPirateCombatGearEquipped(session))
        {
            return true;
        }

        var combatGear = session.CombatGear ?? CombatGearSnapshot.Empty;
        var classCode = session.Character.Class;
        return HasLegacyCombatGearEquippedForClass(combatGear, classCode, LegacyCombatGearStoneCrackerItemId) ||
            HasLegacyCombatGearEquippedForClass(combatGear, classCode, LegacyCombatGearColdMoonItemId) ||
            HasLegacyCombatGearEquippedForClass(combatGear, classCode, LegacyCombatGearTharnCutterItemId) ||
            HasLegacyCombatGearEquippedForClass(combatGear, classCode, LegacyCombatGearDragonhowlItemId) ||
            HasLegacyCombatGearEquippedForClass(combatGear, classCode, LegacyCombatGearBloodlustItemId) ||
            HasLegacyCombatGearEquippedForClass(combatGear, classCode, LegacyCombatGearRedcurseItemId);
    }


    private static bool TryResolveLegacyCombatGearPiratePassiveSpell(
        WorldSession session,
        WorldDataCatalog worldData,
        out PlayerSpellSnapshotEntry playerSpell,
        out LegacyMagicRuntimeSpell spell)
    {
        playerSpell = default!;
        spell = default!;
        for (var attempt = 0; attempt < LegacyCombatGearPiratePassiveSpells.Length; attempt++)
        {
            var selectedIndex = ReadLegacyCombatGearPiratePassiveSpellIndex(session.Character, attempt);
            var spellId = LegacyCombatGearPiratePassiveSpells[selectedIndex];
            if (!TryResolveLegacyPlayerMagicRuntimeSpell(
                    session,
                    worldData,
                    spellId,
                    out var candidatePlayerSpell,
                    out var candidateSpell) ||
                !CheckLegacyCombatGearPiratePassiveRateRoll(
                    session.Character,
                    worldData,
                    spellId,
                    candidatePlayerSpell.Level))
            {
                continue;
            }

            playerSpell = candidatePlayerSpell;
            spell = candidateSpell;
            return true;
        }

        return false;
    }


    private static int ReadLegacyCombatGearPiratePassiveSpellIndex(
        CharacterSnapshot character,
        int attempt)
    {
        var count = LegacyCombatGearPiratePassiveSpells.Length;
        if (TryReadInt64Attribute(
                character.Attributes,
                RuntimeLegacyCombatGearAttackSpellIndexAttribute + "." +
                    attempt.ToString(CultureInfo.InvariantCulture),
                out var forcedIndex))
        {
            var normalized = forcedIndex % count;
            if (normalized < 0)
            {
                normalized += count;
            }

            return checked((int)normalized);
        }

        return ReadLegacyCombatGearAttackSpellIndex(character, count);
    }


    private static bool CheckLegacyCombatGearPiratePassiveRateRoll(
        CharacterSnapshot character,
        WorldDataCatalog worldData,
        ushort spellId,
        ushort spellLevel)
    {
        if (!TryReadLegacyMagicUInt32Field(worldData, spellId, spellLevel, LegacyMagicRateField, out var rate))
        {
            return false;
        }

        var effectiveRate = spellId is 21_690 or 22_090 or 22_100 or 22_110
            ? rate % 1000
            : rate;
        return CheckLegacyCombatGearAttackRateRoll(character, spellId, effectiveRate);
    }


    private static bool TryResolveLegacyCombatGearRateSpell(
        WorldSession session,
        WorldDataCatalog worldData,
        ushort spellId,
        uint rateDivisor,
        out PlayerSpellSnapshotEntry playerSpell,
        out LegacyMagicRuntimeSpell spell)
    {
        playerSpell = default!;
        spell = default!;
        if (!TryResolveLegacyPlayerMagicRuntimeSpell(session, worldData, spellId, out var candidatePlayerSpell, out var candidateSpell) ||
            !TryReadLegacyMagicUInt32Field(worldData, spellId, candidatePlayerSpell.Level, LegacyMagicRateField, out var rate))
        {
            return false;
        }

        var effectiveRate = rateDivisor <= 1 ? rate : rate / rateDivisor;
        if (!CheckLegacyCombatGearAttackRateRoll(session.Character, spellId, effectiveRate))
        {
            return false;
        }

        playerSpell = candidatePlayerSpell;
        spell = candidateSpell;
        return true;
    }


    private static bool TrySelectLegacyCombatGearAttackSpell(
        WorldSession session,
        WorldDataCatalog worldData,
        IReadOnlyList<ushort> spellIds,
        out PlayerSpellSnapshotEntry playerSpell,
        out LegacyMagicRuntimeSpell spell)
    {
        playerSpell = default!;
        spell = default!;
        if (spellIds.Count == 0)
        {
            return false;
        }

        var candidates = new List<(PlayerSpellSnapshotEntry PlayerSpell, LegacyMagicRuntimeSpell Spell)>();
        foreach (var spellId in spellIds)
        {
            if (TryResolveLegacyPlayerMagicRuntimeSpell(session, worldData, spellId, out var candidatePlayerSpell, out var candidateSpell))
            {
                candidates.Add((candidatePlayerSpell, candidateSpell));
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        var selectedIndex = ReadLegacyCombatGearAttackSpellIndex(session.Character, candidates.Count);
        (playerSpell, spell) = candidates[selectedIndex];
        return true;
    }


    private static int ReadLegacyCombatGearAttackSpellIndex(CharacterSnapshot character, int count)
    {
        if (count <= 1)
        {
            return 0;
        }

        if (TryReadInt64Attribute(character.Attributes, RuntimeLegacyCombatGearAttackSpellIndexAttribute, out var forcedIndex))
        {
            var normalized = forcedIndex % count;
            if (normalized < 0)
            {
                normalized += count;
            }

            return checked((int)normalized);
        }

        return Random.Shared.Next(0, count);
    }


    private static bool CheckLegacyCombatGearAttackRateRoll(
        CharacterSnapshot character,
        ushort spellId,
        uint rate)
    {
        if (rate == 0)
        {
            return false;
        }

        var roll = TryReadUInt32Attribute(
            character.Attributes,
            out var forcedRoll,
            RuntimeLegacyCombatGearAttackRateRollAttribute + "." + spellId.ToString(CultureInfo.InvariantCulture),
            RuntimeLegacyCombatGearAttackRateRollAttribute + spellId.ToString(CultureInfo.InvariantCulture),
            RuntimeLegacyCombatGearAttackRateRollAttribute)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return rate > roll;
    }


    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyCombatGearAttackMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        if (!TryFindVisibleMonsterTargetByUid(context, session.Character, query.OpponentUid, out var primary))
        {
            return [];
        }

        var originX = primary.Monster.X;
        var originY = primary.Monster.Y;
        if (TryGetLegacyCombatGearAttackRadius(spell, out var radius))
        {
            return SelectLegacyCombatGearAttackMonsterTargetsByRadius(
                context,
                session,
                originX,
                originY,
                radius);
        }

        var lineRange = GetLegacyCombatGearAttackLineRange(spell);
        var lineWidth = GetLegacyCombatGearAttackLineWidth(spell);
        var selected = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (RequiresLegacyCombatGearAttackDistanceCheck(spell.Id) &&
                LegacyGetDistance(session.Character.X, session.Character.Y, candidate.Monster.X, candidate.Monster.Y) > spell.Range)
            {
                continue;
            }

            if (!IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    originX,
                    originY,
                    candidate.Monster.X,
                    candidate.Monster.Y,
                    lineRange,
                    lineWidth))
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }


    private static bool TryGetLegacyCombatGearAttackRadius(LegacyMagicRuntimeSpell spell, out byte radius)
    {
        switch (spell.Id)
        {
            case 21_670:
                radius = checked((byte)(spell.MaxTargets / 2));
                return true;
            case 21_690:
            case 22_090:
                radius = spell.MaxTargets;
                return true;
            default:
                radius = 0;
                return false;
        }
    }


    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyCombatGearAttackMonsterTargetsByRadius(
        WorldPacketHandlerContext context,
        WorldSession session,
        ushort originX,
        ushort originY,
        byte radius)
    {
        var selected = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (LegacyGetDistance(originX, originY, candidate.Monster.X, candidate.Monster.Y) <= radius)
            {
                selected.Add(candidate);
            }
        }

        return selected;
    }

    private static byte GetLegacyCombatGearAttackLineRange(LegacyMagicRuntimeSpell spell)
    {
        return spell.Id switch
        {
            21_530 or 22_110 or 21_570 or 21_680 or 21_660 or 21_700 => spell.Range,
            17_460 or 17_470 or 17_480 => 20,
            17_500 or 17_510 or 17_520 or 17_540 or 17_550 or 17_560 => spell.Range,
            _ => 15,
        };
    }


    private static byte GetLegacyCombatGearAttackLineWidth(LegacyMagicRuntimeSpell spell)
    {
        return spell.Id switch
        {
            21_530 or 22_110 => checked((byte)Math.Min(byte.MaxValue, spell.Duration)),
            21_570 or 21_680 or 21_660 or 21_700 => spell.MaxTargets,
            _ => 2,
        };
    }


    private static bool RequiresLegacyCombatGearAttackDistanceCheck(ushort spellId)
    {
        return spellId is 17_500 or 17_510 or 17_520 or 17_540 or 17_550 or 17_560;
    }


    private static (WorldSession Session, uint Damage) ApplyLegacyKylinSigilPhysicalSpellDamage(
        WorldPacketHandlerContext context,
        WorldSession session,
        ushort spellId,
        uint damage)
    {
        if (spellId is not (16_330 or 16_340))
        {
            return (session, damage);
        }

        var current = ReadAttributeUInt32(session.Character.Attributes, RuntimeLegacyKylinSigilAttribute);
        var next = LegacyPlayerMonsterPhysicalDamageCalculator.GetNextSourceKylinSigil(current);
        var updatedCharacter = WithCharacterAttribute(session.Character, RuntimeLegacyKylinSigilAttribute, next);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        return (
            updatedSession,
            LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourceKylinSigilDamage(damage, next));
    }


    private static bool HasLegacyCombatGearEquippedForClass(
        CombatGearSnapshot combatGear,
        long classCode,
        uint itemId)
    {
        if (!IsLegacyCombatGearForClass(classCode, itemId))
        {
            return false;
        }

        return combatGear.Items.Any(item => item.ItemID == itemId && item.Equip == 1);
    }


    private static bool IsLegacyCombatGearForClass(long classCode, uint itemId)
    {
        return (classCode / 1000) switch
        {
            2 => itemId is >= LegacyCombatGearRedcurseItemId - 2 and <= LegacyCombatGearRedcurseItemId,
            4 => itemId is >= 401 and <= LegacyCombatGearColdMoonItemId + 1,
            7 => itemId is >= 701 and <= 703,
            13 or 14 => itemId is >= LegacyCombatGearTaoist1ItemId and <= LegacyCombatGearTaoist5ItemId,
            _ => false,
        };
    }
}
