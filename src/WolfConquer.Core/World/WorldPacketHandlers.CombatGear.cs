using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static readonly ushort[] LegacyCombatGearDragonhowlAttackSpells = [17_460, 17_470, 17_480, 17_490];

    private static readonly ushort[] LegacyCombatGearBloodlustAttackSpells = [17_500, 17_510, 17_520, 17_530];

    private static readonly ushort[] LegacyCombatGearRedcurseAttackSpells = [17_540, 17_550, 17_560, 17_570];

    private static readonly ushort[] LegacyCombatGearStoneCrackerAttackSpells = [17_400, 17_401];

    private static readonly ushort[] LegacyCombatGearColdMoonAttackSpells = [11_950, 11_951, 11_952];

    private static readonly ushort[] LegacyCombatGearTharnCutterAttackSpells = [17_390, 17_391, 17_392];

    private static readonly ushort[] LegacyCombatGearPiratePassiveSpells =
        [21_670, 21_680, 21_690, 21_700, 22_070, 22_080, 22_090, 22_100, 22_110];

    private static readonly ushort[] LegacyCombatGearRuntimeFloorSpellIds =
        [21_650, 21_620, 21_610, 21_560, 21_580, 22_070, 22_080, 22_090, 22_100];

    private const int LegacyCombatGearEarthTop4WeeklyFlag = 0x10E | 0x40;

    private const int LegacyCombatGearLightningMagicShieldTop4WeeklyFlag = 0x110 | LegacyMagicShieldFlag | 0x40;

    private const uint LegacyCombatGearRedcurseItemId = 0xCB;

    private const uint LegacyCombatGearDragonhowlItemId = 0xC9;

    private const uint LegacyCombatGearBloodlustItemId = 0xCA;

    private const uint LegacyCombatGearStoneCrackerItemId = 0x191;

    private const uint LegacyCombatGearColdMoonItemId = 0x192;

    private const uint LegacyCombatGearTharnCutterItemId = 0x193;

    private const uint LegacyCombatGearTaoist1ItemId = 0x515;

    private const uint LegacyCombatGearTaoist2ItemId = 0x516;

    private const uint LegacyCombatGearTaoist3ItemId = 0x517;

    private const uint LegacyCombatGearTaoist4ItemId = 0x518;

    private const uint LegacyCombatGearTaoist5ItemId = 0x519;

    private const ushort LegacyCombatGearArmorOfImmunitySpellId = 17_580;

    private const ushort LegacyCombatGearHunterSpellId = 10_890;

    private const ushort LegacyCombatGearSolidBulwarkSpellId = 20_200;

    private const ushort LegacyCombatGearFlowKnackSpellId = 20_210;

    private const ushort LegacyCombatGearMagneticLightSpellId = 20_220;

    private const ushort LegacyCombatGearWaterAegisSpellId = 20_230;

    private const ushort LegacyCombatGearFantasyKnackSpellId = 20_240;

    private const ushort LegacyCombatGearDivineEmptinessSpellId = 20_260;

    private const ushort LegacyCombatGearFlameShieldSpellId = 20_290;

    private const ushort LegacyCombatGearPhoenixBlessingSpellId = 20_300;

    private const ushort LegacyCombatGearHolyProtectionSpellId = 20_350;

    private const ushort LegacyCombatGearWildPhoenixSpellId = 20_370;

    private const ushort LegacyCombatGearAblazeBladeSpellId = 20_360;

    private const ushort LegacyCombatGearColdBloodlineSpellId = 21_550;

    private const ushort LegacyCombatGearColdBloodlineSourceFloorSpellId = 22_090;

    private const ushort LegacyCombatGearStoneCrackerWaterAttackSpellId = 18_220;

    private const ushort LegacyCombatGearArchShadowSpellId = 18_980;

    private const ushort LegacyCombatGearStarFlowSpellId = 10_950;

    private const ushort LegacyCombatGearTharnCutterElementalArrowSpellId = 18_160;

    private const ushort LegacyCombatGearTharnCutterPrisonSpellId = 18_710;

    private const ushort LegacyCombatGearTharnCutterTop8SpellId = 18_630;

    private const int LegacyCombatGearWaterTop4WeeklyFlag = 0x10D | 0x40;

    private const int LegacyCombatGearSageSpeedModeFlag = 0x111;

    private const int LegacyCombatGearLightningTop4WeeklyFlag = 0x110 | LegacyTop4WeeklyFlag;

    private const int LegacyCombatGearSageSpeedModeTop4WeeklyFlag =
        LegacyCombatGearSageSpeedModeFlag | LegacyTop4WeeklyFlag;

    private const int LegacyCombatGearPrisonSigilFutilityCursedFlag = 0x11D | LegacyCursedFlag;

    private const int LegacyCombatGearWhirlSigilAuraCursedFlag = 0x11E | LegacyCursedFlag;

    private const int LegacyCombatGearTop8WeeklyHawksAmbitionFlag = LegacyTop8WeeklyFlag | LegacyHawksAmbitionFlag;

    private const int LegacyCombatGearPhoenixBlessingFlag =
        LegacyTop8WeeklyFlag | LegacyTop4WeeklyFlag | LegacyHawksAmbitionFlag;

    private const int LegacyCombatGearHolyProtectionFlag = LegacyCannonBarrageFlag | LegacyHawksAmbitionFlag;

    private const int LegacyCombatGearWildPhoenixFlag = LegacyOblivionFlag | LegacyHawksAmbitionFlag;

    private const int LegacyCombatGearTop4WeeklyHawksAmbitionFlag = LegacyTop4WeeklyFlag | LegacyHawksAmbitionFlag;

    private const int LegacyCombatGearTop2WeeklyHawksAmbitionFlag = LegacyTop2WeeklyFlag | LegacyHawksAmbitionFlag;

    private const int LegacyCombatGearArchShadowFlag = LegacyDeadFlag | LegacyTop4WeeklyFlag | LegacyHawksAmbitionFlag;

    private const int LegacyCombatGearColdBloodlineFlag = 0x19A;

    private const string RuntimeLegacyCombatGearArchShadowAttribute = "Archshadow";

    private const string RuntimeLegacyCombatGearArrowBladesAttribute = "ArrowBlades";

    private const string RuntimeLegacyCombatGearStarFlowAttribute = "StarFlow";

    private const string RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute =
        "LegacyCombatGearWildPhoenixPackedDamage";

    private const string RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute =
        "LegacyCombatGearWildPhoenixChargeCount";

    private const string RuntimeLegacyKylinSigilAttribute = "KylinSigil";

    private const string RuntimeLegacyCombatGearDamageRollAttribute = "LegacyCombatGearDamageRoll";

    private const string RuntimeLegacyCombatGearPassiveRollAttribute = "LegacyCombatGearPassiveRoll";

    private const string RuntimeLegacyCombatGearAttackSpellIndexAttribute = "LegacyCombatGearAttackSpellIndex";

    private const string RuntimeLegacyCombatGearAttackRateRollAttribute = "LegacyCombatGearAttackRateRoll";

    private const string RuntimeLegacyCombatGearTharnCutterNoJumpRollAttribute = "LegacyCombatGearTharnCutterNoJumpRoll";

    private const uint LegacyCombatGearTharnCutterNoJumpRatePercent = 20;


    private static CombatGearSnapshot WithCombatGearBlackJade(
        CombatGearSnapshot combatGear,
        uint blackJadeId)
    {
        var blackJades = combatGear.BlackJadeBag.BlackJades
            .Concat([blackJadeId])
            .ToArray();
        return combatGear with
        {
            BlackJadeBag = new CombatGearBlackJadeBagSnapshot(blackJades.Length, blackJades),
        };
    }


    private static bool TryFindCombatGearItem(
        CombatGearSnapshot combatGear,
        uint itemId,
        out int itemIndex,
        out CombatGearSnapshotItem item)
    {
        for (var i = 0; i < combatGear.Items.Count; i++)
        {
            if (combatGear.Items[i].ItemID == itemId)
            {
                itemIndex = i;
                item = combatGear.Items[i];
                return true;
            }
        }

        itemIndex = -1;
        item = default!;
        return false;
    }


    private static CombatGearSnapshot WithCombatGearItem(
        CombatGearSnapshot combatGear,
        int itemIndex,
        CombatGearSnapshotItem item)
    {
        var items = combatGear.Items.ToArray();
        items[itemIndex] = item;
        return combatGear with { Items = items };
    }


    private static bool TryGetLegacyCombatGearDamageBlockReason(CharacterSnapshot character, out string reason)
    {
        if (HasLegacyStatusFlag(character, LegacyCombatGearEarthTop4WeeklyFlag))
        {
            reason = "Earth|Top4Weekly";
            return true;
        }

        if (HasLegacyStatusFlag(character, LegacyCombatGearLightningMagicShieldTop4WeeklyFlag) &&
            CheckLegacyCombatGearDamageRateRoll(character))
        {
            reason = "Lightning|MagicShield|Top4Weekly";
            return true;
        }

        reason = "";
        return false;
    }


    private static bool CheckLegacyCombatGearDamageRateRoll(CharacterSnapshot character)
    {
        var roll = TryReadUInt32Attribute(
            character.Attributes,
            out var forcedRoll,
            RuntimeLegacyCombatGearDamageRollAttribute)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return roll < 10;
    }


    private static async ValueTask<WorldSession> TrySendLegacyCombatGearUpdateTargetAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (context.WorldData is null ||
            session.Character.MinHitPoints <= 0)
        {
            return session;
        }

        var updatedSession = session;
        var applied = 0;
        foreach (var spellId in EnumerateLegacyCombatGearUpdateTargetSpells(updatedSession))
        {
            var (spellSession, appliedSpell) = await TrySendLegacyCombatGearUpdateTargetSpellAsync(
                    context with { Session = updatedSession },
                    updatedSession,
                    monster,
                    monsterFamily,
                    spellId,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedSession = spellSession;
            if (appliedSpell)
            {
                applied++;
            }
        }

        if (applied > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy CombatGear UpdateTarget " +
                    $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} " +
                    $"applied={applied}; source=MsgMonster.ActionHandler.CheckRespouseDamage -> MyCombatGear.UpdateTarget.")
                .ConfigureAwait(false);
        }

        return updatedSession;
    }


    private static IEnumerable<ushort> EnumerateLegacyCombatGearUpdateTargetSpells(WorldSession session)
    {
        var combatGear = session.CombatGear ?? CombatGearSnapshot.Empty;
        var classCode = session.Character.Class;
        if (HasLegacyCombatGearEquippedForClass(combatGear, classCode, LegacyCombatGearRedcurseItemId))
        {
            yield return LegacyCombatGearArmorOfImmunitySpellId;
        }

        if (HasLegacyCombatGearEquippedForClass(combatGear, classCode, LegacyCombatGearColdMoonItemId))
        {
            yield return LegacyCombatGearHunterSpellId;
        }

        if (HasLegacyCombatGearEquippedForClass(combatGear, classCode, LegacyCombatGearTaoist1ItemId))
        {
            yield return LegacyCombatGearDivineEmptinessSpellId;
        }

        if (HasLegacyCombatGearEquippedForClass(combatGear, classCode, LegacyCombatGearTaoist2ItemId))
        {
            yield return LegacyCombatGearWaterAegisSpellId;
            yield return LegacyCombatGearFlowKnackSpellId;
        }

        if (HasLegacyCombatGearEquippedForClass(combatGear, classCode, LegacyCombatGearTaoist3ItemId))
        {
            yield return LegacyCombatGearSolidBulwarkSpellId;
        }

        if (HasLegacyCombatGearEquippedForClass(combatGear, classCode, LegacyCombatGearTaoist4ItemId))
        {
            yield return LegacyCombatGearMagneticLightSpellId;
            yield return LegacyCombatGearFantasyKnackSpellId;
        }

        if (HasLegacyCombatGearEquippedForClass(combatGear, classCode, LegacyCombatGearTaoist5ItemId))
        {
            yield return LegacyCombatGearFlameShieldSpellId;
            yield return LegacyCombatGearAblazeBladeSpellId;
        }
    }


    private static async ValueTask<(WorldSession Session, bool Applied)> TrySendLegacyCombatGearUpdateTargetSpellAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        ushort spellId,
        CancellationToken cancellationToken)
    {
        if (context.WorldData is not { } worldData ||
            !TryResolveLegacyCombatGearUpdateTargetSpell(session, worldData, spellId, out var playerSpell, out var spell) ||
            !CheckLegacyCombatGearPassiveRateRoll(session.Character, worldData, spellId, playerSpell.Level))
        {
            return (session, false);
        }

        return spellId switch
        {
            LegacyCombatGearArmorOfImmunitySpellId =>
                (await SendLegacyCombatGearPassiveStatusAsync(
                        context,
                        session,
                        playerSpell,
                        spellId,
                        LegacyCombatGearLightningMagicShieldTop4WeeklyFlag,
                        duration: 2,
                        sendArchiveSkill: true,
                        archiveVisible: false,
                        archiveDamage: 0,
                        archiveSpellLevel: 0,
                        animationTargetSelf: true,
                        cancellationToken)
                    .ConfigureAwait(false), true),
            LegacyCombatGearHunterSpellId =>
                (await SendLegacyCombatGearPassiveStatusAsync(
                        context,
                        session,
                        playerSpell,
                        spellId,
                        LegacyCombatGearLightningMagicShieldTop4WeeklyFlag,
                        duration: 10,
                        sendArchiveSkill: false,
                        archiveVisible: false,
                        archiveDamage: 0,
                        archiveSpellLevel: 0,
                        animationTargetSelf: true,
                        cancellationToken,
                        sendEquipmentRefresh: true)
                    .ConfigureAwait(false), true),
            LegacyCombatGearDivineEmptinessSpellId =>
                (await SendLegacyCombatGearPassiveStatusAsync(
                        context,
                        session,
                        playerSpell,
                        spellId,
                        LegacyAzureShieldFlag | LegacyPaperDanceFlag,
                        spell.Duration,
                        sendArchiveSkill: true,
                        archiveVisible: true,
                        archiveDamage: ReadLegacyMagicUInt32FieldOrDefault(worldData, spellId, playerSpell.Level, 35),
                        archiveSpellLevel: 0,
                        animationTargetSelf: true,
                        cancellationToken,
                        sendEquipmentRefresh: true)
                    .ConfigureAwait(false), true),
            LegacyCombatGearWaterAegisSpellId =>
                (await SendLegacyCombatGearPassiveStatusAsync(
                        context,
                        session,
                        playerSpell,
                        spellId,
                        LegacyAzureShieldFlag,
                        duration: 10,
                        sendArchiveSkill: false,
                        archiveVisible: false,
                        archiveDamage: 0,
                        archiveSpellLevel: 0,
                        animationTargetSelf: true,
                        cancellationToken,
                        sendEquipmentRefresh: true)
                    .ConfigureAwait(false), true),
            LegacyCombatGearFlowKnackSpellId =>
                (await SendLegacyCombatGearPassiveStatusAsync(
                        context,
                        session,
                        playerSpell,
                        spellId,
                        LegacyTopWaterTaoistFlag | LegacyTop4WeeklyFlag | LegacyHawksAmbitionFlag,
                        spell.Duration,
                        sendArchiveSkill: true,
                        archiveVisible: true,
                        archiveDamage: ReadLegacyMagicUInt32FieldOrDefault(worldData, spellId, playerSpell.Level, 10),
                        archiveSpellLevel: ReadLegacyMagicUInt32FieldOrDefault(worldData, spellId, playerSpell.Level, 44),
                        animationTargetSelf: true,
                        cancellationToken,
                        sendEquipmentRefresh: true)
                    .ConfigureAwait(false), true),
            LegacyCombatGearSolidBulwarkSpellId =>
                (await SendLegacyCombatGearPassiveStatusAsync(
                        context,
                        session,
                        playerSpell,
                        spellId,
                        LegacyFireAuraFlag | LegacyHawksAmbitionFlag,
                        duration: 5,
                        sendArchiveSkill: true,
                        archiveVisible: false,
                        archiveDamage: 0,
                        archiveSpellLevel: 0,
                        animationTargetSelf: false,
                        cancellationToken)
                    .ConfigureAwait(false), true),
            LegacyCombatGearMagneticLightSpellId =>
                (await TrySendLegacyCombatGearMagneticLightAsync(
                        context,
                        session,
                        monster,
                        monsterFamily,
                        playerSpell,
                        spell,
                        cancellationToken)
                    .ConfigureAwait(false), true),
            LegacyCombatGearFantasyKnackSpellId =>
                (await SendLegacyCombatGearPassiveStatusAsync(
                        context,
                        session,
                        playerSpell,
                        spellId,
                        LegacyBlackbeardsRageFlag | LegacyHawksAmbitionFlag,
                        spell.Duration,
                        sendArchiveSkill: true,
                        archiveVisible: true,
                        archiveDamage: 0,
                        archiveSpellLevel: 0,
                        animationTargetSelf: true,
                        cancellationToken,
                        sendEquipmentRefresh: true)
                    .ConfigureAwait(false), true),
            LegacyCombatGearFlameShieldSpellId =>
                (await SendLegacyCombatGearPassiveStatusAsync(
                        context,
                        session,
                        playerSpell,
                        spellId,
                        LegacyConfusedFlag | LegacyTop4WeeklyFlag | LegacyHawksAmbitionFlag,
                        spell.Duration,
                        sendArchiveSkill: true,
                        archiveVisible: true,
                        archiveDamage: ReadLegacyMagicUInt32FieldOrDefault(worldData, spellId, playerSpell.Level, 35),
                        archiveSpellLevel: ReadLegacyMagicUInt32FieldOrDefault(worldData, spellId, playerSpell.Level, 36),
                        animationTargetSelf: true,
                        cancellationToken,
                        sendEquipmentRefresh: true)
                    .ConfigureAwait(false), true),
            LegacyCombatGearAblazeBladeSpellId =>
                (await TrySendLegacyCombatGearPassiveMonsterAttackAsync(
                        context,
                        session,
                        monster,
                        monsterFamily,
                        playerSpell,
                        spell,
                        cancellationToken)
                    .ConfigureAwait(false), true),
            _ => (session, false),
        };
    }


    private static async ValueTask SendLegacyCombatGearArchiveSkillUpdateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        int statusFlag,
        uint duration,
        uint damage,
        uint spellLevel,
        bool visible,
        CancellationToken cancellationToken)
    {
        var packet = new LegacyMsgUpdatePacket(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.ArchiveSkill,
                    Value: 0,
                    Flag: checked((uint)statusFlag),
                    Time: duration,
                    Damage: damage,
                    SpellLevel: spellLevel),
            ]);
        var frame = packet.ToFrameBytes();
        if (visible)
        {
            await SendFrameToPlayerAndVisiblePeersAsync(
                    context,
                    context.Connection,
                    session,
                    frame,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
    }


    private static async ValueTask<WorldSession> TrySendLegacyCombatGearPassiveMonsterAttackAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (context.MonsterStates is not { } monsterStates ||
            context.WorldData is not { } worldData)
        {
            return session;
        }

        var damageResult = LegacyMagicRuntime.CalculatePlayerMonsterDamageWithEffects(
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
        var result = ApplyLegacyPlayerMonsterDamage(
            monsterStates,
            monster,
            damageResult.Damage,
            DateTimeOffset.UtcNow,
            session);
        if (result.AlreadyDead)
        {
            return session;
        }

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            0,
            0,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            [new LegacySpellAnimationTarget(monster.Uid, result.DamageApplied, result.DamageApplied > 0, damageResult.Effect)]);
        await SendPlayerSpellAnimationToVisibleAsync(context, session, animation, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Killed && LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(monster))
        {
            var damagedMonster = monster with { Hitpoints = result.RemainingHitpoints };
            foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
            {
                await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            }
        }

        if (result.Killed)
        {
            var death = new MsgAttackPacket(new MsgAttackQuery(
                Uid: CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Death,
                Damage: 0,
                KilledMonster: 1,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0));
            await SendMonsterCombatFrameToVisibleAsync(context, session, monster, death, cancellationToken)
                .ConfigureAwait(false);
            await TrySendLegacyMonsterDeathDropAsync(context, session, monster, monsterFamily, cancellationToken)
                .ConfigureAwait(false);
            session = await TryApplyLegacyBossDeathRewardsAsync(
                    context,
                    session,
                    monster,
                    monsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
            await TryApplyLegacyMonsterTeamExperienceAsync(context, session, monster, cancellationToken)
                .ConfigureAwait(false);
        }

        session = await TryApplyLegacyMonsterExperienceAsync(
                context,
                session,
                monster,
                monsterFamily,
                result.DamageApplied,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent legacy CombatGear UpdateTarget passive monster attack " +
                $"{FormatSession(context with { Session = session })} monsterUid={monster.Uid} spellId={spell.Id} " +
                $"damageApplied={result.DamageApplied} remainingHitpoints={result.RemainingHitpoints} killed={result.Killed}; " +
                "source=MyCombatGear.UpdateTarget -> CombatGearAttack AblazeBlade.")
            .ConfigureAwait(false);
        return session;
    }


    private static async ValueTask<LegacyMonsterResponseDamageResult> TrySendLegacyMonsterCombatGearDamageResponseAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster)
    {
        if (session.Character.MinHitPoints <= 0 ||
            !TryGetLegacyCombatGearDamageBlockReason(session.Character, out var reason))
        {
            return new LegacyMonsterResponseDamageResult(session, Matched: false, InterruptsRetaliation: false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer blocked legacy monster retaliation with CombatGear CheckDamage " +
                $"{FormatSession(context)} monsterUid={monster.Uid} reason={reason}; " +
                "source=MsgMonster.ActionHandler.CheckRespouseDamage -> MyCombatGear.CheckDamage.")
            .ConfigureAwait(false);
        return new LegacyMonsterResponseDamageResult(session, Matched: true, InterruptsRetaliation: true);
    }


}
