using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static ushort GetLegacyExtraBossSpellId(LegacyMonsterInstance monster)
    {
        var spellId = (ushort)9999;
        if (monster.Level < 80)
        {
            spellId = 9998;
        }
        else if (monster.Level < 60)
        {
            spellId = 9966;
        }

        return spellId;
    }

    private static bool TryGetLegacyBossSpellTable(
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        out IReadOnlyList<ushort> spellTable)
    {
        if (IsLegacyBansheeTyrantBossId(monster.MonsterId) ||
            monsterFamily.Id >= 0 && IsLegacyBansheeTyrantBossId((uint)monsterFamily.Id))
        {
            spellTable = LegacyBansheeTyrantBossSpellTable;
            return true;
        }

        if (IsLegacyMonster999996641BossId(monster.MonsterId) ||
            monsterFamily.Id >= 0 && IsLegacyMonster999996641BossId((uint)monsterFamily.Id))
        {
            spellTable = LegacyMonster999996641BossSpellTable;
            return true;
        }

        if (IsLegacyMonster6643BossId(monster.MonsterId) ||
            monsterFamily.Id >= 0 && IsLegacyMonster6643BossId((uint)monsterFamily.Id))
        {
            spellTable = LegacyMonster6643BossSpellTable;
            return true;
        }

        if (IsLegacyMonster7981BossId(monster.MonsterId) ||
            monsterFamily.Id >= 0 && IsLegacyMonster7981BossId((uint)monsterFamily.Id))
        {
            spellTable = LegacyMonster7981BossSpellTable;
            return true;
        }

        if (IsLegacySpookGluttonBossId(monster.MonsterId) ||
            monsterFamily.Id >= 0 && IsLegacySpookGluttonBossId((uint)monsterFamily.Id))
        {
            spellTable = LegacySpookGluttonBossSpellTable;
            return true;
        }

        if (IsLegacyMonster3971BossId(monster.MonsterId) ||
            monsterFamily.Id >= 0 && IsLegacyMonster3971BossId((uint)monsterFamily.Id))
        {
            spellTable = LegacyMonster3971BossSpellTable;
            return true;
        }

        if (IsLegacyMonster213883BossId(monster.MonsterId) ||
            monsterFamily.Id >= 0 && IsLegacyMonster213883BossId((uint)monsterFamily.Id))
        {
            spellTable = LegacyMonster213883BossSpellTable;
            return true;
        }

        spellTable = [];
        return false;
    }

    private static bool IsLegacyBansheeTyrantBossId(uint monsterFamilyId)
    {
        return monsterFamilyId is 3_970 or 3_976 or 3_978 or 4_171 or 4_220 or 7_984 or 7_986 or 32_070 or 40_948;
    }

    private static bool IsLegacyMonster999996641BossId(uint monsterFamilyId)
    {
        return monsterFamilyId is 999_996_641;
    }

    private static bool IsLegacyMonster6643BossId(uint monsterFamilyId)
    {
        return monsterFamilyId is 6_643 or 7_980 or 40_944 or 66_432;
    }

    private static bool IsLegacyMonster7981BossId(uint monsterFamilyId)
    {
        return LegacyBossSpellBranch.IsFamilyMonsterId(monsterFamilyId);
    }

    private static bool IsLegacyMonster6643BossSpellId(ushort spellId)
    {
        return spellId is 10_500 or 10_502 or 10_503 or 10_504 or 10_506;
    }

    private static bool IsLegacyMonster7981BossSpellId(ushort spellId)
    {
        return LegacyBossSpellBranch.IsSpellId(spellId);
    }

    private static bool IsLegacyMonster213883BossAoeSpellId(ushort spellId)
    {
        return spellId is 7_013 or 7_017 or 10_362 or 10_531;
    }

    private static bool IsLegacyMonster3971BossAoeSpellId(ushort spellId)
    {
        return spellId is 7_013 or 7_014 or 7_017;
    }

    private static bool IsLegacySpookGluttonBossId(uint monsterFamilyId)
    {
        return monsterFamilyId is 3_977 or 4_212 or 7_982 or 40_946;
    }

    private static bool IsLegacyMonster3971BossId(uint monsterFamilyId)
    {
        return LegacyBoss3971SpellBranch.IsFamilyMonsterId(monsterFamilyId);
    }

    private static bool IsLegacyMonster213883BossId(uint monsterFamilyId)
    {
        return monsterFamilyId is 213_883;
    }

    private static async ValueTask TrySendLegacyBossSpellRetaliationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        IReadOnlyList<ushort> spellTable,
        CancellationToken cancellationToken)
    {
        const long legacyBossSpellRange = 18;
        var distance = LegacyGetDistance(session.Character.X, session.Character.Y, monster.X, monster.Y);
        if (distance > legacyBossSpellRange)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy boss spell retaliation " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} distance={distance} attackRange={legacyBossSpellRange}.")
                .ConfigureAwait(false);
            return;
        }

        var spellId = SelectLegacyBossSpellId(session.Character, spellTable, out var forcedIndex);
        if (TryGetLegacyBossAoeSpellTargeting(session, monster, monsterFamily, spellId, out var aoeTargeting))
        {
            await TrySendLegacyBossAoePhysicalSpellRetaliationAsync(
                    context,
                    session,
                    monster,
                    monsterFamily,
                    spellId,
                    forcedIndex,
                    aoeTargeting,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (LegacyMagicRuntime.TryGetLegacyBossPhysicalSpellEffect(spellId, out var physicalEffect))
        {
            await TrySendLegacyPhysicalBossSpellRetaliationAsync(
                    context,
                    session,
                    monster,
                    monsterFamily,
                    spellId,
                    forcedIndex,
                    cancellationToken,
                    physicalEffect)
                .ConfigureAwait(false);
            return;
        }

        switch (spellId)
        {
            case 7_016:
                await TrySendLegacyBossSelfHealAsync(
                        context,
                        session,
                        monster,
                        spellId,
                        forcedIndex,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            case 10_372:
                await TrySendLegacyBossSelfHealAsync(
                        context,
                        session,
                        monster,
                        spellId,
                        forcedIndex,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            default:
                await context.Log.WriteLineAsync(
                        "WorldServer skipped legacy boss spell retaliation " +
                        $"{FormatSession(context)} monsterUid={monster.Uid}; boss spellId={spellId} is not modeled yet.")
                    .ConfigureAwait(false);
                return;
        }
    }

    private static ushort SelectLegacyBossSpellId(
        CharacterSnapshot character,
        IReadOnlyList<ushort> spellTable,
        out bool forcedIndex)
    {
        forcedIndex = TryReadInt64Attribute(character.Attributes, RuntimeLegacyBossSpellIndexAttribute, out var forced) &&
            forced >= 0;
        var index = forcedIndex
            ? checked((int)(forced % spellTable.Count))
            : Random.Shared.Next(0, spellTable.Count);
        return spellTable[index];
    }

    private static bool ShouldApplyLegacyBossStatusEffect(
        CharacterSnapshot character,
        LegacyBossStatusEffect effect,
        out bool forcedStatusRoll)
    {
        return ShouldLegacyBossStatusRateSucceed(
                character,
                RuntimeLegacyBossStatusRollAttribute,
                out forcedStatusRoll) &&
            !HasLegacyStatusFlag(character, effect.BlockingFlag);
    }

    private static bool ShouldLegacyBossStatusRateSucceed(
        CharacterSnapshot character,
        string attributeName,
        out bool forcedStatusRoll)
    {
        forcedStatusRoll = TryReadInt64Attribute(character.Attributes, attributeName, out var forcedRoll) &&
            forcedRoll >= 0;
        var roll = forcedStatusRoll
            ? forcedRoll % 100
            : Random.Shared.Next(0, 100);
        return roll < 5;
    }

    private static bool ShouldAvoidLegacyBossStatusWithAcalanatha(
        WorldSession runeOwnerSession,
        WorldDataCatalog? worldData,
        out bool forcedAcalanathaRoll)
    {
        forcedAcalanathaRoll = false;
        if (!HasLegacyBossActiveAcalanathaRune(runeOwnerSession, worldData))
        {
            return false;
        }

        forcedAcalanathaRoll = TryReadInt64Attribute(
                runeOwnerSession.Character.Attributes,
                RuntimeLegacyBossAcalanathaRollAttribute,
                out var forcedRoll) &&
            forcedRoll >= 0;
        var roll = forcedAcalanathaRoll
            ? forcedRoll % 100
            : Random.Shared.Next(0, 100);
        return roll < 55;
    }

    private static bool HasLegacyBossActiveAcalanathaRune(
        WorldSession runeOwnerSession,
        WorldDataCatalog? worldData)
    {
        var runeItems = runeOwnerSession.PlayerItems.RuneItems;
        foreach (var rune in runeItems)
        {
            if (!IsLegacyBossActiveRunePosition(
                    rune.Position,
                    runeItems,
                    runeOwnerSession.AlternateEquipmentActive) ||
                !TryGetLegacyItemType(worldData?.ItemsById, rune.ItemId, out var itemType) ||
                !ContainsLegacyItemText(itemType, "Acalanatha"))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsLegacyBossActiveRunePosition(
        ushort position,
        IReadOnlyList<PlayerItemSnapshotItem> runeItems,
        bool alternateEquipmentActive)
    {
        if (position is < 101 or > 131)
        {
            return false;
        }

        if (!alternateEquipmentActive)
        {
            return position <= 120;
        }

        if (position >= 120)
        {
            return true;
        }

        var alternatePosition = position + 20;
        return !runeItems.Any(rune => rune.Position == alternatePosition);
    }

    private static bool BypassesLegacyBossAcalanathaGuard(
        ushort spellId,
        LegacyBossStatusEffect effect)
    {
        return IsLegacyMonster6643BossSpellId(spellId) &&
            effect.StatusFlag == LegacyBossStatusEffect.FrightenedFlag;
    }

    private static bool TrySelectLegacyBossStatusEffect(
        CharacterSnapshot character,
        ushort spellId,
        out LegacyBossStatusEffect statusEffect,
        out string rollLog)
    {
        if (IsLegacyMonster7981BossSpellId(spellId))
        {
            return TrySelectLegacyMonster7981BossStatusEffect(character, spellId, out statusEffect, out rollLog);
        }

        if (IsLegacyMonster6643BossSpellId(spellId))
        {
            return TrySelectLegacyMonster6643BossStatusEffect(character, out statusEffect, out rollLog);
        }

        if (LegacyBossStatusEffect.TryGetForSpell(spellId, out statusEffect) &&
            ShouldApplyLegacyBossStatusEffect(character, statusEffect, out var forcedStatusRoll))
        {
            rollLog = $"forcedStatusRoll={forcedStatusRoll}";
            return true;
        }

        rollLog = "";
        statusEffect = default;
        return false;
    }

    private static bool TrySelectLegacyMonster7981BossStatusEffect(
        CharacterSnapshot character,
        ushort spellId,
        out LegacyBossStatusEffect statusEffect,
        out string rollLog)
    {
        statusEffect = spellId switch
        {
            10_001 => LegacyBossStatusEffect.Frightened(durationSeconds: 6),
            10_003 => LegacyBossStatusEffect.Dizzy(durationSeconds: 3),
            _ => default,
        };
        if (statusEffect.Equals(default(LegacyBossStatusEffect)))
        {
            rollLog = "";
            return false;
        }

        if (ShouldLegacyBossStatusRateSucceed(
                character,
                RuntimeLegacyBossStatusRollAttribute,
                out var forcedStatusRoll) &&
            !HasLegacyStatusFlag(character, statusEffect.BlockingFlag))
        {
            rollLog = $"forcedStatusRoll={forcedStatusRoll}";
            return true;
        }

        rollLog = $"forcedStatusRoll={forcedStatusRoll}";
        statusEffect = default;
        return false;
    }

    private static bool TrySelectLegacyMonster6643BossStatusEffect(
        CharacterSnapshot character,
        out LegacyBossStatusEffect statusEffect,
        out string rollLog)
    {
        if (ShouldLegacyBossStatusRateSucceed(
                character,
                RuntimeLegacyBossStatusRollAttribute,
                out var forcedPrimaryRoll))
        {
            statusEffect = LegacyBossStatusEffect.Frightened(durationSeconds: 6);
            rollLog = $"forcedStatusRoll={forcedPrimaryRoll} forcedSecondaryStatusRoll=False";
            return true;
        }

        if (ShouldLegacyBossStatusRateSucceed(
                character,
                RuntimeLegacyBossStatusSecondaryRollAttribute,
                out var forcedSecondaryRoll) &&
            !HasLegacyStatusFlag(character, LegacyBossStatusEffect.FrightenedFlag))
        {
            statusEffect = LegacyBossStatusEffect.Dizzy(durationSeconds: 3);
            rollLog = $"forcedStatusRoll={forcedPrimaryRoll} forcedSecondaryStatusRoll={forcedSecondaryRoll}";
            return true;
        }

        statusEffect = default;
        rollLog = $"forcedStatusRoll={forcedPrimaryRoll}";
        return false;
    }

    private static async ValueTask TrySendLegacyBossSelfHealAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        ushort spellId,
        bool forcedIndex,
        CancellationToken cancellationToken)
    {
        const uint legacyBossHealAmount = 80_000;
        var healResult = context.MonsterStates?.ApplyHeal(monster, legacyBossHealAmount, DateTimeOffset.UtcNow) ??
            CreateLegacyBossHealResult(monster, legacyBossHealAmount);
        if (healResult.AlreadyDead)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy boss spell retaliation " +
                    $"{FormatSession(context)} monsterUid={monster.Uid}; monster is waiting to respawn.")
                .ConfigureAwait(false);
            return;
        }

        var healedMonster = monster with { Hitpoints = healResult.RemainingHitpoints };
        var spell = LegacySpellAnimationPacket.ForMonsterMagic(
            monster.Uid,
            monster.Uid,
            monster.X,
            monster.Y,
            spellId,
            legacyBossHealAmount);
        await SendMonsterSpellAnimationToVisibleAsync(context, session, healedMonster, spell, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer sent legacy boss self-heal spell " +
                $"{FormatSession(context)} monsterUid={monster.Uid} spellId={spellId} heal={healResult.HealApplied} " +
                $"remainingHitpoints={healResult.RemainingHitpoints} forcedIndex={forcedIndex}.")
            .ConfigureAwait(false);
    }

    private static MonsterHealResult CreateLegacyBossHealResult(
        LegacyMonsterInstance monster,
        uint requestedHeal)
    {
        if (monster.Hitpoints >= monster.MaxLife)
        {
            return new MonsterHealResult(0, monster.Hitpoints, AlreadyDead: false);
        }

        var heal = Math.Min(requestedHeal, monster.MaxLife - monster.Hitpoints);
        return new MonsterHealResult(heal, monster.Hitpoints + heal, AlreadyDead: false);
    }

    private static async ValueTask TrySendLegacyPhysicalBossSpellRetaliationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        ushort spellId,
        bool forcedIndex,
        CancellationToken cancellationToken,
        MsgAttackEffect effect = MsgAttackEffect.None)
    {
        var currentHitpoints = CheckedUInt32(session.Character.MinHitPoints, nameof(session.Character.MinHitPoints));
        if (currentHitpoints == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy boss spell retaliation " +
                    $"{FormatSession(context)} monsterUid={monster.Uid}; target hitpoints are zero.")
                .ConfigureAwait(false);
            return;
        }

        var damageResult = CalculateLegacyMonsterPhysicalDamage(monster, monsterFamily, session, context.WorldData);
        var damage = damageResult.Damage;
        if (damage == 0 && !damageResult.SendZeroDamage)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy boss spell retaliation " +
                    $"{FormatSession(context)} monsterUid={monster.Uid}; calculated damage is zero.")
                .ConfigureAwait(false);
            return;
        }

        session = await TryConsumeLegacyHawksAmbitionAsync(
                context,
                session,
                damageResult.ConsumesHawksAmbition,
                cancellationToken)
            .ConfigureAwait(false);

        damage = ApplyLegacyBossPhysicalSpellDamageModifiers(spellId, damage);
        var applied = Math.Min(damage, currentHitpoints);
        var killedPlayer = applied >= currentHitpoints;
        var updatedCharacter = session.Character with
        {
            MinHitPoints = currentHitpoints - applied,
        };
        if (killedPlayer)
        {
            updatedCharacter = ApplyLegacyMonsterPlayerDeath(updatedCharacter, monster);
        }

        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);

        var spell = LegacySpellAnimationPacket.ForMonsterMagic(
            monster.Uid,
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            CheckedUInt16(session.Character.X, nameof(session.Character.X)),
            CheckedUInt16(session.Character.Y, nameof(session.Character.Y)),
            spellId,
            damage,
            effect);
        await SendMonsterSpellAnimationToVisibleAsync(context, updatedSession, monster, spell, cancellationToken)
            .ConfigureAwait(false);

        updatedSession = await TryApplyLegacyMonsterHitActivePickRemovalAsync(
                context,
                updatedSession,
                damage,
                cancellationToken)
            .ConfigureAwait(false);
        updatedCharacter = updatedSession.Character;

        updatedSession = await TryApplyLegacyMonsterHitSitInterruptionAsync(
                context,
                updatedSession,
                damage,
                killedPlayer,
                cancellationToken)
            .ConfigureAwait(false);
        updatedCharacter = updatedSession.Character;

        if (applied > 0)
        {
            var maxHitpoints = ComputeLegacyRuntimeMaxHitpoints(
                updatedCharacter,
                currentHitpoints,
                updatedSession.PlayerItems,
                updatedSession.CombatGear ?? CombatGearSnapshot.Empty,
                context.WorldData,
                updatedSession.AlternateEquipmentActive);
            foreach (var update in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(
                CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
                maxHitpoints,
                currentHitpoints - applied))
            {
                await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            }
        }

        if (killedPlayer)
        {
            updatedSession = await SendLegacyOwnedThundercloudDeathCleanupAsync(
                    context,
                    context.Connection,
                    updatedSession,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedCharacter = updatedSession.Character;

            await SendLegacyPlayerDeadTalkAsync(context.Connection, cancellationToken)
                .ConfigureAwait(false);

            var statusUpdate = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
                CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
                CreateLegacyRuntimeStatusFlagVector(updatedCharacter));
            var statusFrame = statusUpdate.ToFrameBytes();
            await context.Connection.SendAsync(statusFrame, cancellationToken).ConfigureAwait(false);
            if (context.Sessions is { } sessions)
            {
                foreach (var active in sessions.Snapshot())
                {
                    if (ReferenceEquals(active.Connection, context.Connection) ||
                        active.Session.Character.Uid == updatedCharacter.Uid ||
                        !CanSeePlayer(active.Session.Character, updatedCharacter))
                    {
                        continue;
                    }

                    await active.Connection.SendAsync(statusFrame, cancellationToken).ConfigureAwait(false);
                }
            }

            var death = new MsgAttackPacket(new MsgAttackQuery(
                Uid: monster.Uid,
                OpponentUid: CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
                X: CheckedUInt16(session.Character.X, nameof(session.Character.X)),
                Y: CheckedUInt16(session.Character.Y, nameof(session.Character.Y)),
                AttackType: MsgAttackType.Death,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0));
            await SendInteractionToVisibleAsync(context, updatedSession, death, cancellationToken)
                .ConfigureAwait(false);

            await context.Log.WriteLineAsync(
                    "WorldServer sent legacy monster player death " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} damage={damage} remainingHitpoints=0.")
                .ConfigureAwait(false);
        }

        var appliedStatus = false;
        var statusResult = await TryApplyLegacyBossStatusEffectAsync(
                context,
                context.Connection,
                updatedSession,
                monster,
                spellId,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = statusResult.Session;
        appliedStatus = statusResult.Applied;

        await context.Log.WriteLineAsync(
                "WorldServer sent legacy boss physical spell retaliation " +
                $"{FormatSession(context)} monsterUid={monster.Uid} spellId={spellId} damage={applied} " +
                $"remainingHitpoints={currentHitpoints - applied} forcedIndex={forcedIndex} " +
                $"statusApplied={appliedStatus}.")
            .ConfigureAwait(false);
    }

    private static uint ApplyLegacyBossPhysicalSpellDamageModifiers(ushort spellId, uint damage)
    {
        return spellId == 10_500
            ? MulDivToUInt32(damage, 130, 100)
            : damage;
    }

    private static async ValueTask<LegacyBossStatusApplyResult> TryApplyLegacyBossStatusEffectAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection subjectConnection,
        WorldSession session,
        LegacyMonsterInstance monster,
        ushort spellId,
        CancellationToken cancellationToken)
    {
        return await TryApplyLegacyBossStatusEffectAsync(
                context,
                subjectConnection,
                session,
                monster,
                spellId,
                session,
                (context.TimeProvider ?? TimeProvider.System).GetUtcNow(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<LegacyBossStatusApplyResult> TryApplyLegacyBossStatusEffectAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection subjectConnection,
        WorldSession session,
        LegacyMonsterInstance monster,
        ushort spellId,
        WorldSession acalanathaRuneOwnerSession,
        CancellationToken cancellationToken)
    {
        return await TryApplyLegacyBossStatusEffectAsync(
                context,
                subjectConnection,
                session,
                monster,
                spellId,
                acalanathaRuneOwnerSession,
                (context.TimeProvider ?? TimeProvider.System).GetUtcNow(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<LegacyBossStatusApplyResult> TryApplyLegacyBossStatusEffectAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection subjectConnection,
        WorldSession session,
        LegacyMonsterInstance monster,
        ushort spellId,
        WorldSession acalanathaRuneOwnerSession,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!TrySelectLegacyBossStatusEffect(session.Character, spellId, out var statusEffect, out var statusRollLog))
        {
            return new LegacyBossStatusApplyResult(session, Applied: false);
        }

        var bypassesOppositeStatusGuard = BypassesLegacyBossAcalanathaGuard(spellId, statusEffect);
        var acalanathaRollLog = "";
        if (!bypassesOppositeStatusGuard)
        {
            if (ShouldAvoidLegacyBossStatusWithAcalanatha(
                    acalanathaRuneOwnerSession,
                    context.WorldData,
                    out var forcedAcalanathaRoll))
            {
                return new LegacyBossStatusApplyResult(session, Applied: false);
            }

            acalanathaRollLog = $" forcedAcalanathaRoll={forcedAcalanathaRoll}";
        }

        var publication = TryPublishLegacyBossStatusLifetime(
            context.Sessions,
            subjectConnection,
            session,
            statusEffect,
            bypassesOppositeStatusGuard,
            now);
        if (!publication.Applied)
        {
            if (publication.Deferred)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer deferred legacy boss status effect publication after session churn " +
                        $"{FormatSession(context)} monsterUid={monster.Uid} spellId={spellId} " +
                        $"targetUid={publication.Session.Character.Uid} statusFlag={statusEffect.StatusFlag}.")
                    .ConfigureAwait(false);
            }

            return new LegacyBossStatusApplyResult(publication.Session, Applied: false);
        }

        var updatedSession = publication.Session;
        var updatedCharacter = updatedSession.Character;
        await SendPlayerStatusFlagVectorToVisibleAsync(
                context,
                subjectConnection,
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);
        await SendFrameToPlayerAndVisiblePeersAsync(
                context,
                subjectConnection,
                updatedSession,
                new LegacyGameUpdatePacket(
                        CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
                        statusEffect.DataType,
                        Active: true,
                        ShownAmount: 0,
                        Time: statusEffect.DurationSeconds,
                        Amount: 0)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy boss status effect " +
                $"{FormatSession(context)} monsterUid={monster.Uid} spellId={spellId} " +
                $"targetUid={updatedCharacter.Uid} statusFlag={statusEffect.StatusFlag} " +
                $"seconds={statusEffect.DurationSeconds} {statusRollLog}{acalanathaRollLog}.")
            .ConfigureAwait(false);
        return new LegacyBossStatusApplyResult(updatedSession, Applied: true);
    }

}
