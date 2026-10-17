using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class LegacyPlayerStatusCalculator
{
    private const int DaoqiFirstAttributeTypeFieldIndex = 2;
    private const int DaoqiFirstAttributeValueFieldIndex = 3;
    private const int DaoqiSecondAttributeTypeFieldIndex = 4;
    private const int DaoqiSecondAttributeValueFieldIndex = 5;
    private const int DaoqiScoreFieldIndex = 6;
    private const int DaoqiTypeRuneFieldIndex = 9;
    private const int DaoqiTypeCombatGearFieldIndex = 14;
    private const int CombatGearNutBookOverallCollectionId = 700;
    private const int LegacyCombatGearTharnCutterElementalArrowSpellId = 18_160;
    private const int LegacyMagicGDamageFieldIndex = 10;
    private const int LegacyMagicDamageOnHumanFieldIndex = 35;
    private const int LegacyMagicDamage2FieldIndex = 36;
    private const int LegacyMagicDamage3FieldIndex = 37;
    private const int LegacyMagicDamageOnMonsterFieldIndex = 44;
    private const int LegacyCombatGearFlowKnackSpellId = 20_210;
    private const int LegacyCombatGearDivineEmptinessSpellId = 20_260;
    private const int LegacyCombatGearFlameShieldSpellId = 20_290;
    private const int LegacyCombatGearPhoenixBlessingSpellId = 20_300;
    private const int LegacyCombatGearWackeSpiritSpellId = 20_310;
    private const int LegacyCombatGearHolyProtectionSpellId = 20_350;
    private const int LegacyCombatGearCrackMantraSpellId = 20_380;
    private const int LegacyCombatGearNobleSpiritSpellId = 20_390;
    private const int LegacyCombatGearColdBloodlineSpellId = 21_550;
    private const int LegacyCombatGearDiabolizeSpellId = 21_630;
    private const int LegacyCombatGearBarrierSpellId = 21_730;
    private const int LegacyCombatGearSenseSpellId = 21_720;
    private const int LegacyCombatGearShellSpellId = 21_740;
    private const int LegacyCombatGearStormSpellId = 21_750;
    private const int LegacyCombatGearThrashSpellId = 21_760;
    private const int LegacyCombatGearThunderSpellId = 21_770;
    private const int LegacyCombatGearTorrentSpellId = 21_780;
    private const int LegacyCombatGearTideSpellId = 21_790;
    private const int LegacyCombatGearSplashSpellId = 21_800;
    private const int LegacyCombatGearSailingSpellId = 21_810;
    private const int LegacyCombatGearVastSpellId = 21_820;
    private const int LegacyCombatGearMudWallSpellId = 16_440;
    private const int LegacyNinjaMudSigilSolidityType = 302;
    private const int LegacyMudSigilSolidityPowerValueIndex = 5;
    private const int LegacyMsgNinjaHeaderFieldCount = 20;
    private const int LegacyMsgNinjaItemRecordFieldCount = 5;
    private const int LegacyPrisonSigilFutilityCursedFlag = 0x11D | 0x20;
    private const int LegacyWhirlSigilAuraCursedFlag = 0x11E | 0x20;
    private const int LegacyMudWallFlag = 0x112;
    private const int LegacyTop8WeeklyHawksAmbitionFlag = 0x3F | 0x100;
    private const int LegacyHolyProtectionFlag = 0x78 | 0x100;
    private const int LegacyPhoenixBlessingFlag = 0x3F | 0x40 | 0x100;
    private const int LegacyNobleSpiritFlag = 0x134 | 0x40;
    private const int LegacyFlameShieldFlag = 0x3C | 0x40 | 0x100;
    private const int LegacyFlowKnackFlag = 0x29 | 0x40 | 0x100;
    private const int LegacyDivineEmptinessFlag = 0x5D | 0x120;
    private const int LegacyWackeSpiritFlag = 0x76 | 0x100;
    private const int LegacyCrackMantraPhysicalFlag = 0x17A;
    private const int LegacyCrackMantraMagicFlag = 0x17B;
    private const int LegacyDiabolizeFlag = 0x18A;
    private const int LegacyBarrierFlag = 0x18D;
    private const int LegacyVastFlag = 0x18E;
    private const int LegacyTideFlag = 0x18F;
    private const int LegacyTorrentFlag = 0x190;
    private const int LegacyThunderFlag = 0x191;
    private const int LegacyStormFlag = 0x192;
    private const int LegacyThrashFlag = 0x193;
    private const int LegacySplashFlag = 0x196;
    private const int LegacySenseFlag = 0x19C;
    private const int LegacyShellFlag = 0x197;
    private const int LegacyColdBloodlineFlag = 0x19A;
    private const int LegacySailingFlag = 0x1A3;

    private const int LegacyDaoqiMaxHpAttrType = 1;
    private const int LegacyDaoqiPAttackAttrType = 2;
    private const int LegacyDaoqiPDefenseAttrType = 3;
    private const int LegacyDaoqiFinalPDamageAttrType = 7;
    private const int LegacyDaoqiPStrikeAttrType = 10;
    private const int LegacyDaoqiBreakAttrType = 13;
    private const int LegacyDaoqiAntiBreakAttrType = 14;

    private static void ApplyCombatGearAttributes(
        uint[] fields,
        long classCode,
        IReadOnlyDictionary<string, string> characterAttributes,
        CombatGearSnapshot? combatGear,
        bool alternateEquipmentActive,
        PlayerSpellSnapshot? playerSpells,
        IReadOnlyDictionary<(int GearTypeId, int Level), CombatGear>? combatGearByGearTypeAndLevel,
        IReadOnlyDictionary<int, DaoqiDictType>? daoqiDictTypesById,
        IReadOnlyDictionary<int, IReadOnlyList<CollectionRewardTier>>? collectionRewardTiersByCollectionId,
        IReadOnlyDictionary<int, IReadOnlyList<GouyuType>>? gouyuTypesByType,
        IReadOnlyDictionary<int, IReadOnlyList<MagicType>>? magicByTypeId)
    {
        ApplyCombatGearMudWallStatusBonuses(
            fields,
            classCode,
            characterAttributes,
            alternateEquipmentActive,
            playerSpells,
            gouyuTypesByType,
            magicByTypeId);

        if (combatGear is null)
        {
            return;
        }

        CombatGearSnapshotItem? equipped = null;
        foreach (var item in combatGear.Items)
        {
            if (item.Equip != 0 && IsCombatGearForClass(classCode, item.ItemID))
            {
                equipped = item;
                break;
            }
        }

        if (equipped is null)
        {
            return;
        }

        ApplyCombatGearItemAttributes(fields, equipped, combatGearByGearTypeAndLevel, daoqiDictTypesById);
        ApplyCombatGearNutBookAttributes(
            fields,
            classCode,
            combatGear,
            combatGearByGearTypeAndLevel,
            daoqiDictTypesById,
            collectionRewardTiersByCollectionId);

        foreach (var item in combatGear.Items)
        {
            if (item.ItemID == equipped.ItemID || !IsCombatGearForClass(classCode, item.ItemID))
            {
                continue;
            }

            ApplyCombatGearItemAttributes(fields, item, combatGearByGearTypeAndLevel, daoqiDictTypesById: null, percent: 5);
        }

        ApplyCombatGearTharnCutterElementalArrowStatusBonuses(fields, characterAttributes, magicByTypeId);
        ApplyCombatGearStatusItemBlessBonuses(fields, characterAttributes, magicByTypeId);
        ApplyCombatGearNobleSpiritStatusBonuses(fields, characterAttributes, magicByTypeId);
        ApplyCombatGearFinalDamageDecreaseStatusBonuses(fields, characterAttributes, magicByTypeId);
        ApplyCombatGearDodgeRateStatusBonuses(fields, characterAttributes, magicByTypeId);
        ApplyCombatGearCrackMantraStatusBonuses(fields, characterAttributes, magicByTypeId);
        ApplyCombatGearSenseAndDiabolizeStatusBonuses(fields, characterAttributes, magicByTypeId);
        ApplyCombatGearSailingStatusBonuses(fields, characterAttributes, magicByTypeId);
        ApplyCombatGearShellStatusBonuses(fields, characterAttributes, magicByTypeId);
        ApplyCombatGearDaoqiStyleStatusBonuses(fields, characterAttributes, magicByTypeId);
    }

    private static void ApplyCombatGearMudWallStatusBonuses(
        uint[] fields,
        long classCode,
        IReadOnlyDictionary<string, string> characterAttributes,
        bool alternateEquipmentActive,
        PlayerSpellSnapshot? playerSpells,
        IReadOnlyDictionary<int, IReadOnlyList<GouyuType>>? gouyuTypesByType,
        IReadOnlyDictionary<int, IReadOnlyList<MagicType>>? magicByTypeId)
    {
        if (!IsLegacyNinjaClass(classCode) ||
            !HasLegacyStatusFlag(characterAttributes, LegacyMudWallFlag) ||
            !TryGetLegacyMudWallMagicRow(playerSpells, magicByTypeId, out var mudWall))
        {
            return;
        }

        var gDamage = ReadUIntMagicField(mudWall, LegacyMagicGDamageFieldIndex);
        Add(fields, LegacyPlayerStatusPacket.DefenceIndex, gDamage / 10_000);
        Add(fields, LegacyPlayerStatusPacket.MagicDefenceIndex, gDamage % 1_000);

        if (TryGetLegacyMudSigilSolidityPower(
                characterAttributes,
                alternateEquipmentActive,
                gouyuTypesByType,
                out var solidityPower))
        {
            Add(fields, LegacyPlayerStatusPacket.DefenceIndex, solidityPower / 10_000);
            Add(fields, LegacyPlayerStatusPacket.MagicDefenceIndex, solidityPower % 10_000);
        }
    }

    private static bool TryGetLegacyMudWallMagicRow(
        PlayerSpellSnapshot? playerSpells,
        IReadOnlyDictionary<int, IReadOnlyList<MagicType>>? magicByTypeId,
        out MagicType magic)
    {
        magic = null!;
        if (playerSpells is null)
        {
            return false;
        }

        foreach (var playerSpell in playerSpells.Spells)
        {
            if (playerSpell.Id == LegacyCombatGearMudWallSpellId)
            {
                return TryGetLegacyMagicRow(magicByTypeId, playerSpell.Id, playerSpell.Level, out magic);
            }
        }

        return false;
    }

    private static bool TryGetLegacyMudSigilSolidityPower(
        IReadOnlyDictionary<string, string> characterAttributes,
        bool alternateEquipmentActive,
        IReadOnlyDictionary<int, IReadOnlyList<GouyuType>>? gouyuTypesByType,
        out uint power)
    {
        power = 0;
        if (!characterAttributes.TryGetValue("MsgNinja", out var raw) ||
            !TryReadLegacyMsgNinjaEquippedItemLevel(
                raw,
                LegacyNinjaMudSigilSolidityType,
                alternateEquipmentActive,
                out var level))
        {
            return false;
        }

        return TryGetLegacyMudSigilSolidityGouyuPower(gouyuTypesByType, level, out power);
    }

    private static void ApplyCombatGearTharnCutterElementalArrowStatusBonuses(
        uint[] fields,
        IReadOnlyDictionary<string, string> characterAttributes,
        IReadOnlyDictionary<int, IReadOnlyList<MagicType>>? magicByTypeId)
    {
        if (!TryGetLegacyMagicRow(
                magicByTypeId,
                LegacyCombatGearTharnCutterElementalArrowSpellId,
                spellLevel: 0,
                out var magic))
        {
            return;
        }

        if (HasLegacyStatusFlag(characterAttributes, LegacyPrisonSigilFutilityCursedFlag))
        {
            var attack = ReadUIntMagicField(magic, LegacyMagicGDamageFieldIndex);
            Add(fields, LegacyPlayerStatusPacket.MaxAttackIndex, attack);
            Add(fields, LegacyPlayerStatusPacket.MinAttackIndex, attack);
        }

        if (HasLegacyStatusFlag(characterAttributes, LegacyWhirlSigilAuraCursedFlag))
        {
            Add(fields, LegacyPlayerStatusPacket.DefenceIndex, ReadUIntMagicField(magic, LegacyMagicDamageOnHumanFieldIndex));
            Add(fields, LegacyPlayerStatusPacket.MagicDefenceIndex, ReadUIntMagicField(magic, LegacyMagicDamage2FieldIndex));
        }

        if (HasLegacyStatusFlag(characterAttributes, LegacyTop8WeeklyHawksAmbitionFlag))
        {
            var value = ReadUIntMagicField(magic, LegacyMagicDamage3FieldIndex);
            Add(fields, LegacyPlayerStatusPacket.BreakthroughIndex, value);
            Add(fields, LegacyPlayerStatusPacket.CounteractionIndex, value);
        }
    }

    private static void ApplyCombatGearStatusItemBlessBonuses(
        uint[] fields,
        IReadOnlyDictionary<string, string> characterAttributes,
        IReadOnlyDictionary<int, IReadOnlyList<MagicType>>? magicByTypeId)
    {
        AddCombatGearStatusItemBlessBonus(
            fields,
            characterAttributes,
            magicByTypeId,
            LegacyHolyProtectionFlag,
            LegacyCombatGearHolyProtectionSpellId);
        AddCombatGearStatusItemBlessBonus(
            fields,
            characterAttributes,
            magicByTypeId,
            LegacyPhoenixBlessingFlag,
            LegacyCombatGearPhoenixBlessingSpellId);
    }

    private static void AddCombatGearStatusItemBlessBonus(
        uint[] fields,
        IReadOnlyDictionary<string, string> characterAttributes,
        IReadOnlyDictionary<int, IReadOnlyList<MagicType>>? magicByTypeId,
        int statusFlag,
        int spellId)
    {
        if (!HasLegacyStatusFlag(characterAttributes, statusFlag) ||
            !TryGetLegacyMagicRow(magicByTypeId, spellId, spellLevel: 0, out var magic))
        {
            return;
        }

        var value = ReadUIntMagicField(magic, LegacyMagicDamageOnHumanFieldIndex);
        fields[LegacyPlayerStatusPacket.ItemBlessIndex] = Math.Min(
            60u,
            checked(fields[LegacyPlayerStatusPacket.ItemBlessIndex] + value));
    }

    private static void ApplyCombatGearNobleSpiritStatusBonuses(
        uint[] fields,
        IReadOnlyDictionary<string, string> characterAttributes,
        IReadOnlyDictionary<int, IReadOnlyList<MagicType>>? magicByTypeId)
    {
        if (!HasLegacyStatusFlag(characterAttributes, LegacyNobleSpiritFlag) ||
            !TryGetLegacyMagicRow(magicByTypeId, LegacyCombatGearNobleSpiritSpellId, spellLevel: 0, out var magic))
        {
            return;
        }

        var value = ReadUIntMagicField(magic, LegacyMagicGDamageFieldIndex);
        Add(fields, LegacyPlayerStatusPacket.MagicDefenceIndex, value / 100);
        Add(fields, LegacyPlayerStatusPacket.PenetrationIndex, checked((value % 100) * 100));
    }

    private static void ApplyCombatGearFinalDamageDecreaseStatusBonuses(
        uint[] fields,
        IReadOnlyDictionary<string, string> characterAttributes,
        IReadOnlyDictionary<int, IReadOnlyList<MagicType>>? magicByTypeId)
    {
        AddCombatGearFinalDamageDecreaseStatusBonus(
            fields,
            characterAttributes,
            magicByTypeId,
            LegacyFlameShieldFlag,
            LegacyCombatGearFlameShieldSpellId,
            LegacyMagicDamageOnHumanFieldIndex,
            LegacyMagicDamage2FieldIndex);
        AddCombatGearFinalDamageDecreaseStatusBonus(
            fields,
            characterAttributes,
            magicByTypeId,
            LegacyFlowKnackFlag,
            LegacyCombatGearFlowKnackSpellId,
            LegacyMagicGDamageFieldIndex,
            LegacyMagicDamageOnMonsterFieldIndex);
    }

    private static void AddCombatGearFinalDamageDecreaseStatusBonus(
        uint[] fields,
        IReadOnlyDictionary<string, string> characterAttributes,
        IReadOnlyDictionary<int, IReadOnlyList<MagicType>>? magicByTypeId,
        int statusFlag,
        int spellId,
        int physicalDecreaseFieldIndex,
        int magicDecreaseFieldIndex)
    {
        if (!HasLegacyStatusFlag(characterAttributes, statusFlag) ||
            !TryGetLegacyMagicRow(magicByTypeId, spellId, spellLevel: 0, out var magic))
        {
            return;
        }

        Add(fields, LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex, ReadUIntMagicField(magic, physicalDecreaseFieldIndex));
        Add(fields, LegacyPlayerStatusPacket.MagicDamageDecreaseIndex, ReadUIntMagicField(magic, magicDecreaseFieldIndex));
    }

    private static void ApplyCombatGearDodgeRateStatusBonuses(
        uint[] fields,
        IReadOnlyDictionary<string, string> characterAttributes,
        IReadOnlyDictionary<int, IReadOnlyList<MagicType>>? magicByTypeId)
    {
        if (HasLegacyStatusFlag(characterAttributes, LegacyDivineEmptinessFlag) &&
            TryGetLegacyMagicRow(magicByTypeId, LegacyCombatGearDivineEmptinessSpellId, spellLevel: 0, out var divineEmptiness))
        {
            var damageOnHuman = ReadUIntMagicField(divineEmptiness, LegacyMagicDamageOnHumanFieldIndex);
            var dodgeRate = (uint)(fields[LegacyPlayerStatusPacket.DodgeIndex] * (damageOnHuman / 100.0 / 100.0));
            Add(fields, LegacyPlayerStatusPacket.DodgeRateIndex, dodgeRate);
        }

        if (HasLegacyStatusFlag(characterAttributes, LegacyWackeSpiritFlag) &&
            TryGetLegacyMagicRow(magicByTypeId, LegacyCombatGearWackeSpiritSpellId, spellLevel: 0, out var wackeSpirit))
        {
            Add(fields, LegacyPlayerStatusPacket.DodgeRateIndex, ReadUIntMagicField(wackeSpirit, LegacyMagicGDamageFieldIndex));
        }
    }

    private static void ApplyCombatGearCrackMantraStatusBonuses(
        uint[] fields,
        IReadOnlyDictionary<string, string> characterAttributes,
        IReadOnlyDictionary<int, IReadOnlyList<MagicType>>? magicByTypeId)
    {
        if (!TryGetLegacyMagicRow(magicByTypeId, LegacyCombatGearCrackMantraSpellId, spellLevel: 0, out var crackMantra))
        {
            return;
        }

        if (HasLegacyStatusFlag(characterAttributes, LegacyCrackMantraPhysicalFlag))
        {
            SubtractOrZero(
                fields,
                LegacyPlayerStatusPacket.DefenceIndex,
                ReadUIntMagicField(crackMantra, LegacyMagicDamageOnHumanFieldIndex));
        }

        if (HasLegacyStatusFlag(characterAttributes, LegacyCrackMantraMagicFlag))
        {
            SubtractOrZero(
                fields,
                LegacyPlayerStatusPacket.MagicDefenceIndex,
                ReadUIntMagicField(crackMantra, LegacyMagicDamage2FieldIndex));
        }
    }

    private static void ApplyCombatGearSenseAndDiabolizeStatusBonuses(
        uint[] fields,
        IReadOnlyDictionary<string, string> characterAttributes,
        IReadOnlyDictionary<int, IReadOnlyList<MagicType>>? magicByTypeId)
    {
        if (HasLegacyStatusFlag(characterAttributes, LegacySenseFlag) &&
            TryGetLegacyMagicRow(magicByTypeId, LegacyCombatGearSenseSpellId, spellLevel: 0, out var sense))
        {
            Add(fields, LegacyPlayerStatusPacket.DodgeRateIndex, ReadUIntMagicField(sense, LegacyMagicDamage2FieldIndex));
        }

        if (HasLegacyStatusFlag(characterAttributes, LegacyDiabolizeFlag) &&
            TryGetLegacyMagicRow(magicByTypeId, LegacyCombatGearDiabolizeSpellId, spellLevel: 0, out var diabolize))
        {
            Add(fields, LegacyPlayerStatusPacket.DodgeRateIndex, ReadUIntMagicField(diabolize, LegacyMagicGDamageFieldIndex));
        }
    }

    private static void ApplyCombatGearSailingStatusBonuses(
        uint[] fields,
        IReadOnlyDictionary<string, string> characterAttributes,
        IReadOnlyDictionary<int, IReadOnlyList<MagicType>>? magicByTypeId)
    {
        if (!HasLegacyStatusFlag(characterAttributes, LegacySailingFlag) ||
            !TryGetLegacyMagicRow(magicByTypeId, LegacyCombatGearSailingSpellId, spellLevel: 0, out var sailing))
        {
            return;
        }

        Add(fields, LegacyPlayerStatusPacket.BlockIndex, checked(ReadUIntMagicField(sailing, LegacyMagicGDamageFieldIndex) * 100));
    }

    private static void ApplyCombatGearShellStatusBonuses(
        uint[] fields,
        IReadOnlyDictionary<string, string> characterAttributes,
        IReadOnlyDictionary<int, IReadOnlyList<MagicType>>? magicByTypeId)
    {
        if (!HasLegacyStatusFlag(characterAttributes, LegacyShellFlag) ||
            !TryGetLegacyMagicRow(magicByTypeId, LegacyCombatGearShellSpellId, spellLevel: 0, out var shell))
        {
            return;
        }

        Add(fields, LegacyPlayerStatusPacket.ImmunityIndex, ReadUIntMagicField(shell, LegacyMagicGDamageFieldIndex));
    }

    private static void ApplyCombatGearDaoqiStyleStatusBonuses(
        uint[] fields,
        IReadOnlyDictionary<string, string> characterAttributes,
        IReadOnlyDictionary<int, IReadOnlyList<MagicType>>? magicByTypeId)
    {
        AddCombatGearDaoqiStyleStatusBonus(
            fields,
            characterAttributes,
            magicByTypeId,
            LegacyBarrierFlag,
            LegacyCombatGearBarrierSpellId,
            LegacyDaoqiAntiBreakAttrType,
            multiplier: 10);
        AddCombatGearDaoqiStyleStatusBonus(
            fields,
            characterAttributes,
            magicByTypeId,
            LegacyStormFlag,
            LegacyCombatGearStormSpellId,
            LegacyDaoqiPStrikeAttrType);
        AddCombatGearDaoqiStyleStatusBonus(
            fields,
            characterAttributes,
            magicByTypeId,
            LegacyThrashFlag,
            LegacyCombatGearThrashSpellId,
            LegacyDaoqiFinalPDamageAttrType);
        AddCombatGearDaoqiStyleStatusBonus(
            fields,
            characterAttributes,
            magicByTypeId,
            LegacyThunderFlag,
            LegacyCombatGearThunderSpellId,
            LegacyDaoqiFinalPDamageAttrType);
        AddCombatGearDaoqiStyleStatusBonus(
            fields,
            characterAttributes,
            magicByTypeId,
            LegacyTorrentFlag,
            LegacyCombatGearTorrentSpellId,
            LegacyDaoqiPAttackAttrType);
        AddCombatGearDaoqiStyleStatusBonus(
            fields,
            characterAttributes,
            magicByTypeId,
            LegacyTideFlag,
            LegacyCombatGearTideSpellId,
            LegacyDaoqiPDefenseAttrType);
        AddCombatGearDaoqiStyleStatusBonus(
            fields,
            characterAttributes,
            magicByTypeId,
            LegacySplashFlag,
            LegacyCombatGearSplashSpellId,
            LegacyDaoqiMaxHpAttrType);
        AddCombatGearDaoqiStyleStatusBonus(
            fields,
            characterAttributes,
            magicByTypeId,
            LegacyVastFlag,
            LegacyCombatGearVastSpellId,
            LegacyDaoqiBreakAttrType,
            multiplier: 10);
        AddCombatGearDaoqiStyleStatusBonus(
            fields,
            characterAttributes,
            magicByTypeId,
            LegacyColdBloodlineFlag,
            LegacyCombatGearColdBloodlineSpellId,
            LegacyDaoqiPDefenseAttrType,
            sourceFieldIndex: LegacyMagicDamageOnHumanFieldIndex);
    }

    private static void AddCombatGearDaoqiStyleStatusBonus(
        uint[] fields,
        IReadOnlyDictionary<string, string> characterAttributes,
        IReadOnlyDictionary<int, IReadOnlyList<MagicType>>? magicByTypeId,
        int statusFlag,
        int spellId,
        int attrType,
        uint multiplier = 1,
        int sourceFieldIndex = LegacyMagicGDamageFieldIndex)
    {
        if (!HasLegacyStatusFlag(characterAttributes, statusFlag) ||
            !TryGetLegacyMagicRow(magicByTypeId, spellId, spellLevel: 0, out var magic))
        {
            return;
        }

        ApplyDaoqiAttribute(fields, attrType, checked(ReadUIntMagicField(magic, sourceFieldIndex) * multiplier));
    }

    private static void ApplyCombatGearItemAttributes(
        uint[] fields,
        CombatGearSnapshotItem item,
        IReadOnlyDictionary<(int GearTypeId, int Level), CombatGear>? combatGearByGearTypeAndLevel,
        IReadOnlyDictionary<int, DaoqiDictType>? daoqiDictTypesById,
        int percent = 100)
    {
        if (combatGearByGearTypeAndLevel is not null &&
            item.ItemID <= int.MaxValue &&
            item.Level <= int.MaxValue &&
            combatGearByGearTypeAndLevel.TryGetValue(((int)item.ItemID, (int)item.Level), out var gear))
        {
            var attack = ScaleCombatGearValue(gear.Attack, percent);
            Add(fields, LegacyPlayerStatusPacket.MinAttackIndex, attack);
            Add(fields, LegacyPlayerStatusPacket.MaxAttackIndex, attack);
            Add(fields, LegacyPlayerStatusPacket.DefenceIndex, ScaleCombatGearValue(gear.Defense, percent));
            Add(fields, LegacyPlayerStatusPacket.MagicDefenceIndex, ScaleCombatGearValue(gear.MagicDefense, percent));
        }

        if (daoqiDictTypesById is null)
        {
            return;
        }

        foreach (var blackJadeId in item.BlackJades)
        {
            if (blackJadeId == 0 ||
                blackJadeId > int.MaxValue ||
                !daoqiDictTypesById.TryGetValue((int)blackJadeId, out var blackJade))
            {
                continue;
            }

            ApplyDaoqiDictTypeAttributes(fields, blackJade);
        }
    }

    private static void ApplyCombatGearNutBookAttributes(
        uint[] fields,
        long classCode,
        CombatGearSnapshot combatGear,
        IReadOnlyDictionary<(int GearTypeId, int Level), CombatGear>? combatGearByGearTypeAndLevel,
        IReadOnlyDictionary<int, DaoqiDictType>? daoqiDictTypesById,
        IReadOnlyDictionary<int, IReadOnlyList<CollectionRewardTier>>? collectionRewardTiersByCollectionId)
    {
        if (classCode / 1000 != 7 ||
            daoqiDictTypesById is null ||
            collectionRewardTiersByCollectionId is null)
        {
            return;
        }

        for (var collectionId = CombatGearNutBookOverallCollectionId; collectionId <= 703; collectionId++)
        {
            var score = CalculateCombatGearNutBookScore(
                collectionId,
                combatGear,
                combatGearByGearTypeAndLevel,
                daoqiDictTypesById);
            if (score == 0 ||
                !collectionRewardTiersByCollectionId.TryGetValue(collectionId, out var tiers))
            {
                continue;
            }

            foreach (var tier in tiers)
            {
                if (score >= tier.MinInclusive &&
                    score <= tier.MaxInclusive &&
                    tier.RewardValue > 0)
                {
                    ApplyDaoqiAttribute(fields, tier.RewardType, checked((uint)tier.RewardValue));
                    break;
                }
            }
        }
    }

    private static int CalculateCombatGearNutBookScore(
        int collectionId,
        CombatGearSnapshot combatGear,
        IReadOnlyDictionary<(int GearTypeId, int Level), CombatGear>? combatGearByGearTypeAndLevel,
        IReadOnlyDictionary<int, DaoqiDictType> daoqiDictTypesById)
    {
        var score = 0;
        if (collectionId != CombatGearNutBookOverallCollectionId)
        {
            var item = combatGear.Items.FirstOrDefault(candidate => candidate.ItemID == collectionId);
            if (item is not null)
            {
                score = checked(score + GetCombatGearScore(item, combatGearByGearTypeAndLevel) + (int)item.MasteryScore);
            }
        }

        foreach (var blackJadeId in combatGear.BlackJadeBag.BlackJades)
        {
            if (blackJadeId == 0 ||
                blackJadeId > int.MaxValue ||
                !daoqiDictTypesById.TryGetValue((int)blackJadeId, out var blackJade) ||
                !TryReadIntField(blackJade.Fields, DaoqiScoreFieldIndex, out var blackJadeScore) ||
                blackJadeScore <= 0)
            {
                continue;
            }

            if (collectionId == CombatGearNutBookOverallCollectionId)
            {
                if (TryReadIntField(blackJade.Fields, DaoqiTypeCombatGearFieldIndex, out var typeCombatGear) &&
                    typeCombatGear == 0 &&
                    TryReadIntField(blackJade.Fields, DaoqiTypeRuneFieldIndex, out var typeRune) &&
                    typeRune % 100 == 7)
                {
                    score = checked(score + blackJadeScore);
                }

                continue;
            }

            if (TryReadIntField(blackJade.Fields, DaoqiTypeCombatGearFieldIndex, out var itemTypeCombatGear) &&
                itemTypeCombatGear == collectionId)
            {
                score = checked(score + blackJadeScore);
            }
        }

        return score;
    }

    private static void ApplyDaoqiDictTypeAttributes(uint[] fields, DaoqiDictType blackJade)
    {
        ApplyDaoqiFieldAttribute(
            fields,
            blackJade.Fields,
            DaoqiFirstAttributeTypeFieldIndex,
            DaoqiFirstAttributeValueFieldIndex);
        ApplyDaoqiFieldAttribute(
            fields,
            blackJade.Fields,
            DaoqiSecondAttributeTypeFieldIndex,
            DaoqiSecondAttributeValueFieldIndex);
    }

    private static void ApplyDaoqiFieldAttribute(
        uint[] fields,
        IReadOnlyList<string> values,
        int typeFieldIndex,
        int valueFieldIndex)
    {
        if (!TryReadIntField(values, typeFieldIndex, out var type) ||
            !TryReadIntField(values, valueFieldIndex, out var value) ||
            type <= 0 ||
            value <= 0)
        {
            return;
        }

        ApplyDaoqiAttribute(fields, type, checked((uint)value));
    }

    private static void ApplyDaoqiAttribute(uint[] fields, int type, uint value)
    {
        switch (type)
        {
            case 1:
                Add(fields, LegacyPlayerStatusPacket.MaxHPIndex, value);
                break;
            case 2:
                Add(fields, LegacyPlayerStatusPacket.MinAttackIndex, value);
                Add(fields, LegacyPlayerStatusPacket.MaxAttackIndex, value);
                break;
            case 3:
                Add(fields, LegacyPlayerStatusPacket.DefenceIndex, value);
                break;
            case 4:
                Add(fields, LegacyPlayerStatusPacket.MagicAttackIndex, value);
                break;
            case 5:
                Add(fields, LegacyPlayerStatusPacket.MagicDefenceIndex, value);
                break;
            case 6:
                Add(fields, LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex, value);
                break;
            case 7:
                Add(fields, LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex, value);
                break;
            case 8:
                Add(fields, LegacyPlayerStatusPacket.MagicDamageIncreaseIndex, value);
                break;
            case 9:
                Add(fields, LegacyPlayerStatusPacket.MagicDamageDecreaseIndex, value);
                break;
            case 10:
                Add(fields, LegacyPlayerStatusPacket.CriticalStrikeIndex, value);
                break;
            case 11:
                Add(fields, LegacyPlayerStatusPacket.SkillCStrikeIndex, value);
                break;
            case 12:
                Add(fields, LegacyPlayerStatusPacket.ImmunityIndex, value);
                break;
            case 13:
                Add(fields, LegacyPlayerStatusPacket.BreakthroughIndex, value / 10);
                break;
            case 14:
                Add(fields, LegacyPlayerStatusPacket.CounteractionIndex, value / 10);
                break;
        }
    }

    private static bool IsCombatGearForClass(long classCode, uint itemId)
    {
        return (classCode / 1000) switch
        {
            2 => itemId is >= 201 and <= 203,
            4 => itemId is >= 401 and <= 403,
            7 => itemId is >= 701 and <= 703,
            13 or 14 => itemId is >= 1301 and <= 1305,
            _ => false,
        };
    }

    private static int GetCombatGearScore(
        CombatGearSnapshotItem item,
        IReadOnlyDictionary<(int GearTypeId, int Level), CombatGear>? combatGearByGearTypeAndLevel)
    {
        if (combatGearByGearTypeAndLevel is null ||
            item.ItemID > int.MaxValue ||
            item.Level > int.MaxValue ||
            !combatGearByGearTypeAndLevel.TryGetValue(((int)item.ItemID, (int)item.Level), out var gear))
        {
            return 0;
        }

        return Math.Max(0, gear.Score);
    }

    private static uint ScaleCombatGearValue(int value, int percent)
    {
        return value > 0
            ? checked((uint)(value * percent / 100))
            : 0;
    }

    private static bool TryGetLegacyMudSigilSolidityGouyuPower(
        IReadOnlyDictionary<int, IReadOnlyList<GouyuType>>? gouyuTypesByType,
        uint level,
        out uint power)
    {
        power = 0;
        if (level > int.MaxValue ||
            gouyuTypesByType is null ||
            !gouyuTypesByType.TryGetValue(LegacyNinjaMudSigilSolidityType, out var rows))
        {
            return false;
        }

        foreach (var row in rows)
        {
            if (row.Level != (int)level ||
                row.Values.Count <= LegacyMudSigilSolidityPowerValueIndex ||
                row.Values[LegacyMudSigilSolidityPowerValueIndex] <= 0)
            {
                continue;
            }

            power = checked((uint)row.Values[LegacyMudSigilSolidityPowerValueIndex]);
            return true;
        }

        return false;
    }

    private static bool TryReadLegacyMsgNinjaEquippedItemLevel(
        string raw,
        int itemType,
        bool alternateEquipmentActive,
        out uint level)
    {
        level = 0;
        var fields = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length <= LegacyMsgNinjaHeaderFieldCount)
        {
            return false;
        }

        for (var index = LegacyMsgNinjaHeaderFieldCount;
             index + 2 < fields.Length;
             index += LegacyMsgNinjaItemRecordFieldCount)
        {
            if (!uint.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var currentType) ||
                currentType != itemType ||
                !uint.TryParse(fields[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var currentLevel) ||
                currentLevel == 0 ||
                !uint.TryParse(fields[index + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var position) ||
                !IsLegacyNinjaGouyuEquippedPosition(position, alternateEquipmentActive))
            {
                continue;
            }

            level = currentLevel;
            return true;
        }

        return false;
    }

    private static bool IsLegacyNinjaGouyuEquippedPosition(uint position, bool alternateEquipmentActive)
    {
        return alternateEquipmentActive
            ? position is >= 1 and <= 16
            : position is >= 1 and <= 8;
    }

    private static bool IsLegacyNinjaClass(long classCode)
    {
        return classCode / 1000 == 5;
    }
}
