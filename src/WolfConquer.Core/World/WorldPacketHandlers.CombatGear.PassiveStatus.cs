using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool CheckLegacyCombatGearPassiveRateRoll(
        CharacterSnapshot character,
        WorldDataCatalog worldData,
        ushort spellId,
        ushort spellLevel)
    {
        return spellId switch
        {
            LegacyCombatGearArmorOfImmunitySpellId or LegacyCombatGearHunterSpellId =>
                CheckLegacyCombatGearPassiveRateRoll(character, spellId, 10, 100),
            LegacyCombatGearSolidBulwarkSpellId =>
                CheckLegacyCombatGearPassiveRateRoll(character, spellId, 20, 100),
            LegacyCombatGearAblazeBladeSpellId =>
                TryReadLegacyMagicUInt32Field(worldData, spellId, spellLevel, 12, out var ablazeRate) &&
                CheckLegacyCombatGearPassiveRateRoll(character, spellId, ablazeRate, 100),
            LegacyCombatGearMagneticLightSpellId =>
                HasLegacyStatusFlag(character, LegacyVenomFlag | LegacyMagicShieldFlag) ||
                TryReadLegacyMagicUInt32Field(worldData, spellId, spellLevel, 12, out var magneticRate) &&
                CheckLegacyCombatGearPassiveRateRoll(character, spellId, magneticRate, 10_000),
            LegacyCombatGearFlowKnackSpellId or
                LegacyCombatGearWaterAegisSpellId or
                LegacyCombatGearFantasyKnackSpellId or
                LegacyCombatGearDivineEmptinessSpellId or
                LegacyCombatGearFlameShieldSpellId =>
                TryReadLegacyMagicUInt32Field(worldData, spellId, spellLevel, 12, out var rate) &&
                CheckLegacyCombatGearPassiveRateRoll(character, spellId, rate, 10_000),
            _ => false,
        };
    }


    private static bool CheckLegacyCombatGearPassiveRateRoll(
        CharacterSnapshot character,
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
            character.Attributes,
            out var forcedRoll,
            RuntimeLegacyCombatGearPassiveRollAttribute + "." + spellId.ToString(CultureInfo.InvariantCulture),
            RuntimeLegacyCombatGearPassiveRollAttribute + spellId.ToString(CultureInfo.InvariantCulture),
            RuntimeLegacyCombatGearPassiveRollAttribute)
            ? forcedRoll % denominator
            : checked((uint)Random.Shared.Next(0, checked((int)denominator)));
        return roll < rate;
    }


    private static async ValueTask<WorldSession> SendLegacyCombatGearPassiveStatusAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        PlayerSpellSnapshotEntry playerSpell,
        ushort spellId,
        int statusFlag,
        uint duration,
        bool sendArchiveSkill,
        bool archiveVisible,
        uint archiveDamage,
        uint archiveSpellLevel,
        bool animationTargetSelf,
        CancellationToken cancellationToken,
        bool sendEquipmentRefresh = false,
        IReadOnlyList<LegacySpellAnimationTarget>? animationTargetsOverride = null,
        byte animationBombFlag = 0)
    {
        var updatedSession = session with
        {
            Character = WithLegacySourceAddSpellFlag(session.Character, statusFlag, removeOnDead: true),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        if (sendArchiveSkill)
        {
            await SendLegacyCombatGearArchiveSkillUpdateAsync(
                    updatedContext,
                    updatedSession,
                    statusFlag,
                    duration,
                    archiveDamage,
                    archiveSpellLevel,
                    archiveVisible,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var uid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
        IReadOnlyList<LegacySpellAnimationTarget> targets = animationTargetsOverride ?? (animationTargetSelf
            ? [new LegacySpellAnimationTarget(uid, 0, Hit: true, MsgAttackEffect.None)]
            : []);
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            uid,
            0,
            0,
            spellId,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            targets,
            bombFlag: animationBombFlag);
        await SendPlayerSpellAnimationToVisibleAsync(updatedContext, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        if (sendEquipmentRefresh)
        {
            await SendLegacyCombatGearPassiveEquipmentRefreshAsync(updatedContext, updatedSession, cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer sent legacy CombatGear UpdateTarget passive status " +
                $"{FormatSession(updatedContext)} spellId={spellId} statusFlag={statusFlag} duration={duration}; " +
                $"equipmentRefreshPackets={(sendEquipmentRefresh ? 1 : 0)}; " +
                "source=MyCombatGear.UpdateTarget -> MyCombatGear.ProcescMagic(ignoreStamp).")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask SendLegacyCombatGearPassiveEquipmentRefreshAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(
                LegacyShowEquipmentPacket.CreateEquipmentRefresh(
                    session.PlayerItems,
                    session.AlternateEquipmentActive).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }


    private static async ValueTask<WorldSession> TrySendLegacyCombatGearMagneticLightAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        const uint animationDamage = 15_000;

        IReadOnlyList<LegacySpellAnimationTarget> targets = [];
        MonsterDamageResult? result = null;
        if (context.MonsterStates is { } monsterStates &&
            context.WorldData is { } worldData)
        {
            var magicDamageResult = LegacyMagicRuntime.CalculatePlayerMonsterDamageWithEffects(
                session,
                worldData,
                monsterFamily,
                spell,
                CalculateLegacyRuntimeStatusFields(session, worldData),
                HasLegacyStatusFlag(session.Character, LegacyInfinityFlag),
                ReadAttributeUInt32(session.Character.Attributes, "InfinityDamage", "InfinityMonsterDamage"),
                ReadAttributeUInt32(session.Character.Attributes, "InfinitySDamage", "InfinitySpellDamage"),
                HasLegacyStatusFlag(session.Character, LegacySlayerFlag),
                ReadAttributeUInt32(session.Character.Attributes, "SlayerPercent", "SlayerDamagePercent"),
                monster.Hitpoints,
                ReadLegacyPhysicalEffectRoll(session.Character, RuntimeLegacyMagicLuckyStrikeRollAttribute),
                ReadLegacyPhysicalEffectRoll(session.Character, RuntimeLegacyMagicRefineryRollAttribute),
                CalculateLegacyPlayerMonsterMagicDamageBonusPercent(session, worldData, monster));
            session = await TryConsumeLegacySlayerAsync(
                    context,
                    session,
                    HasLegacyStatusFlag(session.Character, LegacySlayerFlag),
                    cancellationToken)
                .ConfigureAwait(false);
            result = ApplyLegacyPlayerMonsterDamage(
                monsterStates,
                monster,
                magicDamageResult.Damage,
                DateTimeOffset.UtcNow,
                session);
            if (!result.Value.AlreadyDead)
            {
                var targetX = monster.X;
                var targetY = monster.Y;
                if (TryCalculateLegacyMonsterPushbackPosition(
                        context,
                        monster,
                        ReadAttributeUInt32(session.Character.Attributes, "Angle", "Facing"),
                        distance: 2,
                        out var pushedX,
                        out var pushedY))
                {
                    targetX = pushedX;
                    targetY = pushedY;
                    if (!result.Value.Killed &&
                        monsterStates.TryMoveTo(
                            monster,
                            pushedX,
                            pushedY,
                            DateTimeOffset.UtcNow,
                            out var moved))
                    {
                        targetX = moved.X;
                        targetY = moved.Y;
                    }
                }

                targets =
                [
                    new LegacySpellAnimationTarget(
                        monster.Uid,
                        animationDamage,
                        Hit: true,
                        magicDamageResult.Effect,
                        NewX: targetX,
                        NewY: targetY),
                ];
            }
        }

        var updatedSession = await SendLegacyCombatGearPassiveStatusAsync(
                context,
                session,
                playerSpell,
                spell.Id,
                LegacyVenomFlag | LegacyMagicShieldFlag,
                duration: 10,
                sendArchiveSkill: true,
                archiveVisible: true,
                archiveDamage: 0,
                archiveSpellLevel: 0,
                animationTargetSelf: false,
                cancellationToken,
                animationTargetsOverride: targets,
                animationBombFlag: 1)
            .ConfigureAwait(false);

        if (context.MonsterStates is not null &&
            result is { AlreadyDead: false } damageResult)
        {
            if (!damageResult.Killed && LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(monster))
            {
                var damagedMonster = monster with { Hitpoints = damageResult.RemainingHitpoints };
                foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
                {
                    await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                }
            }

            if (damageResult.Killed)
            {
                var death = new MsgAttackPacket(new MsgAttackQuery(
                    Uid: CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
                    OpponentUid: monster.Uid,
                    X: monster.X,
                    Y: monster.Y,
                    AttackType: MsgAttackType.Death,
                    Damage: 0,
                    KilledMonster: 1,
                    ResponseDamage: 0,
                    Effect: MsgAttackEffect.None,
                    Unknown: 0));
                await SendMonsterCombatFrameToVisibleAsync(context, updatedSession, monster, death, cancellationToken)
                    .ConfigureAwait(false);
                await TrySendLegacyMonsterDeathDropAsync(context, updatedSession, monster, monsterFamily, cancellationToken)
                    .ConfigureAwait(false);
                updatedSession = await TryApplyLegacyBossDeathRewardsAsync(
                        context,
                        updatedSession,
                        monster,
                        monsterFamily,
                        cancellationToken)
                    .ConfigureAwait(false);
                await TryApplyLegacyMonsterTeamExperienceAsync(context, updatedSession, monster, cancellationToken)
                    .ConfigureAwait(false);
            }

            updatedSession = await TryApplyLegacyMonsterExperienceAsync(
                    context,
                    updatedSession,
                    monster,
                    monsterFamily,
                    damageResult.DamageApplied,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer sent legacy CombatGear UpdateTarget MagneticLight monster pushback " +
                    $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} " +
                    $"damageApplied={damageResult.DamageApplied} remainingHitpoints={damageResult.RemainingHitpoints} " +
                    $"killed={damageResult.Killed}; source=MyCombatGear.UpdateTarget -> CombatGearAttack MagneticLight.")
                .ConfigureAwait(false);
        }

        return updatedSession;
    }
}
