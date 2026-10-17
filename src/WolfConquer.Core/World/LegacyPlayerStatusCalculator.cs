using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class LegacyPlayerStatusCalculator
{
    private const int MaxAttackFieldIndex = 13;
    private const int MinAttackFieldIndex = 14;
    private const int PhysicalDefenceFieldIndex = 15;
    private const int FrequencyFieldIndex = 16;
    private const int DodgeFieldIndex = 17;
    private const int ItemHitpointsFieldIndex = 18;
    private const int ItemManaFieldIndex = 19;
    private const int MagicAttackFieldIndex = 29;
    private const int MagicDefenceFieldIndex = 30;
    private const int CriticalStrikeFieldIndex = 39;
    private const int SkillCriticalStrikeFieldIndex = 40;
    private const int ImmunityFieldIndex = 41;
    private const int PenetrationFieldIndex = 42;
    private const int BlockFieldIndex = 43;
    private const int BreakthroughFieldIndex = 44;
    private const int CounteractionFieldIndex = 45;
    private const int DetoxicationFieldIndex = 46;
    private const int MetalResistanceFieldIndex = 47;
    private const int WoodResistanceFieldIndex = 48;
    private const int WaterResistanceFieldIndex = 49;
    private const int FireResistanceFieldIndex = 50;
    private const int EarthResistanceFieldIndex = 51;
    private const int PhysicalDamageIncreaseFieldIndex = 60;
    private const int MagicDamageIncreaseFieldIndex = 61;
    private const int PhysicalDamageDecreaseFieldIndex = 62;
    private const int MagicDamageDecreaseFieldIndex = 63;

    private const int ItemAddHitpointsValueIndex = 0;
    private const int ItemAddMinAttackValueIndex = 1;
    private const int ItemAddMaxAttackValueIndex = 2;
    private const int ItemAddPhysicalDefenceValueIndex = 3;
    private const int ItemAddMagicAttackValueIndex = 4;
    private const int ItemAddMagicDefenceValueIndex = 5;
    private const int ItemAddAgilityValueIndex = 6;
    private const int ItemAddDodgeValueIndex = 7;

    private const ushort NormalPhoenixGem = 1;
    private const ushort RefinedPhoenixGem = 2;
    private const ushort SuperPhoenixGem = 3;
    private const ushort NormalDragonGem = 11;
    private const ushort RefinedDragonGem = 12;
    private const ushort SuperDragonGem = 13;
    private const ushort NormalTortoiseGem = 71;
    private const ushort RefinedTortoiseGem = 72;
    private const ushort SuperTortoiseGem = 73;
    private const ushort NormalThunderGem = 101;
    private const ushort RefinedThunderGem = 102;
    private const ushort SuperThunderGem = 103;
    private const ushort NormalGloryGem = 121;
    private const ushort RefinedGloryGem = 122;
    private const ushort SuperGloryGem = 123;
    private const ushort NoSocket = 0;
    private const ushort EmptySocket = byte.MaxValue;

    private const int RelicPosition = 13;
    private const int LegacyMagicLevelFieldIndex = 8;
    private const int LegacyRefineryMDefenceType = 1;
    private const int LegacyRefineryFinalMDamageType = 11;
    private const int LegacyRefineryFinalMAttackType = 12;

    public static LegacyPlayerStatusPacket Create(
        CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems,
        WorldDataCatalog? catalog,
        uint maxHitpoints,
        uint prestigeLevel,
        CombatGearSnapshot? combatGear = null,
        bool alternateEquipmentActive = false,
        PlayerSpellSnapshot? playerSpells = null)
    {
        var fields = CalculateFields(
            snapshot,
            playerItems,
            catalog,
            maxHitpoints,
            prestigeLevel,
            combatGear,
            alternateEquipmentActive,
            playerSpells);
        return new LegacyPlayerStatusPacket(fields);
    }

    public static uint[] CalculateFields(
        CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems,
        WorldDataCatalog? catalog,
        uint maxHitpoints,
        uint prestigeLevel,
        CombatGearSnapshot? combatGear = null,
        bool alternateEquipmentActive = false,
        PlayerSpellSnapshot? playerSpells = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(playerItems);

        var fields = new uint[LegacyPlayerStatusPacket.FieldCount];
        fields[LegacyPlayerStatusPacket.UidIndex] = CheckedUInt32(snapshot.Uid, nameof(snapshot.Uid));
        fields[LegacyPlayerStatusPacket.MaxHPIndex] = maxHitpoints;
        fields[LegacyPlayerStatusPacket.MaxManaIndex] = CheckedUInt32(snapshot.MaxMana, nameof(snapshot.MaxMana));
        fields[LegacyPlayerStatusPacket.MaxAttackIndex] = CheckedUInt32(snapshot.Strength + 1, nameof(snapshot.Strength));
        fields[LegacyPlayerStatusPacket.MinAttackIndex] = CheckedUInt32(snapshot.Strength, nameof(snapshot.Strength));
        fields[LegacyPlayerStatusPacket.MagicAttackIndex] = CheckedUInt32(snapshot.Spirit, nameof(snapshot.Spirit));
        fields[LegacyPlayerStatusPacket.PrestigeLevelIndex] = prestigeLevel;
        fields[LegacyPlayerStatusPacket.RuneScoreIndex] = LegacyRuneHitpointsCalculator.CalculateStorageScore(playerItems.RuneItems);

        var gemValues = new uint[14];
        var tortoiseGemCount = 0;
        var luckyStrike = 0u;

        foreach (var item in LegacyEquipmentSelector.SelectCurrentEquipment(playerItems, alternateEquipmentActive))
        {
            if (item.Durability == 0)
            {
                continue;
            }

            var position = item.Position % 50;
            var plus = Math.Min((int)item.Plus, 12);
            var bless = Math.Min((uint)item.Bless, 7u);
            var socketOne = (ushort)item.SocketOne;
            var socketTwo = (ushort)item.SocketTwo;

            if (IsLegacySocketClearedPosition(position))
            {
                socketOne = NoSocket;
                socketTwo = NoSocket;
            }
            else
            {
                NormalizeSocketPair(position, ref socketOne, ref socketTwo);
            }

            if (IsLegacyBlessEnchantClampedPosition(position))
            {
                if (bless > 1)
                {
                    bless = 1;
                }

                if (position == 12)
                {
                    bless = 0;
                }
            }

            if (IsLegacyPlusClearedPosition(position))
            {
                plus = 0;
            }

            AddGem(fields, gemValues, ref tortoiseGemCount, socketOne);
            AddGem(fields, gemValues, ref tortoiseGemCount, socketTwo);

            if (!TryGetItemType(catalog?.ItemsById, item.ItemId, out var itemType))
            {
                continue;
            }

            ApplyBaseItem(fields, position, itemType);
            if (position != 12)
            {
                Add(fields, LegacyPlayerStatusPacket.DodgeIndex, ReadUIntField(itemType, DodgeFieldIndex));
                Add(fields, LegacyPlayerStatusPacket.AgilityAttackIndex, ReadUIntField(itemType, FrequencyFieldIndex));
                Add(fields, LegacyPlayerStatusPacket.ItemBlessIndex, bless);
            }

            if (IsPurificationInLife(item) &&
                TryGetItemType(catalog?.ItemsById, item.PurificationItemId, out var purification))
            {
                ApplyPurification(fields, purification);
            }

            if (IsRefineryEffectInLife(item) &&
                TryGetRefineryMaterial(catalog?.RefineryMaterialsByItemType, item.RefineryEffectId, out var refinery))
            {
                ApplyRefineryEffect(fields, refinery);
            }

            if (plus > 0 &&
                TryGetItemAddBonus(catalog?.ItemAddBonusByItemTypeIdAndLevel, item.ItemId, plus, out var itemAdd))
            {
                ApplyItemAdd(fields, position, itemAdd, GetAnimaPlusPercent(catalog?.ItemsById, item.AnimaItemId));
            }

            if (TryGetItemType(catalog?.ItemsById, item.AnimaItemId, out var anima))
            {
                Add(fields, LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex, ReadUIntField(anima, PhysicalDamageIncreaseFieldIndex));
                Add(fields, LegacyPlayerStatusPacket.MagicDamageIncreaseIndex, ReadUIntField(anima, MagicDamageIncreaseFieldIndex));
                Add(fields, LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex, ReadUIntField(anima, PhysicalDamageDecreaseFieldIndex));
                Add(fields, LegacyPlayerStatusPacket.MagicDamageDecreaseIndex, ReadUIntField(anima, MagicDamageDecreaseFieldIndex));
            }

            if (position == RelicPosition)
            {
                ApplyRelicAttributes(fields, item, ref luckyStrike);
            }
        }

        ApplyActiveRuneAttackAttributes(fields, playerItems, catalog?.ItemsById, alternateEquipmentActive);
        Add(fields, LegacyPlayerStatusPacket.MagicAttackIndex, 1);
        Add(fields, LegacyPlayerStatusPacket.MagicDefenceIndex, gemValues[NormalGloryGem / 10]);
        Add(fields, LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex, gemValues[NormalGloryGem / 10]);
        Add(fields, LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex, gemValues[NormalThunderGem / 10]);
        Add(fields, LegacyPlayerStatusPacket.MagicDamageIncreaseIndex, gemValues[NormalThunderGem / 10]);
        fields[LegacyPlayerStatusPacket.DamageIndex] = gemValues[NormalTortoiseGem / 10];
        fields[LegacyPlayerStatusPacket.LuckyStrikePercentIndex] = checked(luckyStrike * 100);
        ApplyChiAttributes(fields, snapshot, catalog?.FateExperienceByType);
        ApplyPerfectionAttributes(fields, prestigeLevel, catalog?.ItemRefineEffectsById);
        ApplyCombatGearAttributes(
            fields,
            snapshot.Class,
            snapshot.Attributes,
            combatGear,
            alternateEquipmentActive,
            playerSpells,
            catalog?.CombatGearByGearTypeAndLevel,
            catalog?.DaoqiDictTypesById,
            catalog?.CollectionRewardTiersByCollectionId,
            catalog?.GouyuTypesByType,
            catalog?.MagicByTypeId);
        ApplyMythsoulBagAttributes(fields, playerItems, catalog?.SpecialEffectAttributesById);
        ApplyBeastAttributes(fields, snapshot, catalog?.BeastAttributesByLevel);
        ApplyProfessionAttributes(fields, snapshot, catalog?.ProfessionLevelBenefitsByProfessionCode);
        ApplyRuneStorageAttributes(fields, playerItems, catalog?.RuneStorageAttributes);
        ApplyActiveRuneDefenceAttributes(fields, playerItems, catalog?.ItemsById);
        ApplyLegacyAuraAttributes(fields, snapshot);

        return fields;
    }

    private static void ApplyBaseItem(uint[] fields, int position, ItemType itemType)
    {
        var maxAttack = ReadUIntField(itemType, MaxAttackFieldIndex);
        var minAttack = ReadUIntField(itemType, MinAttackFieldIndex);
        var magicAttack = ReadUIntField(itemType, MagicAttackFieldIndex);

        if (position is 10 or 19)
        {
            Add(fields, LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex, maxAttack);
            Add(fields, LegacyPlayerStatusPacket.MagicDamageIncreaseIndex, magicAttack);
        }
        else if (position == 5)
        {
            Add(fields, LegacyPlayerStatusPacket.MaxAttackIndex, maxAttack / 2);
            Add(fields, LegacyPlayerStatusPacket.MinAttackIndex, minAttack / 2);
            Add(fields, LegacyPlayerStatusPacket.MagicAttackIndex, magicAttack / 2);
        }
        else
        {
            Add(fields, LegacyPlayerStatusPacket.MaxAttackIndex, maxAttack);
            Add(fields, LegacyPlayerStatusPacket.MinAttackIndex, minAttack);
            Add(fields, LegacyPlayerStatusPacket.MagicAttackIndex, magicAttack);
        }

        if (position is 11 or 19)
        {
            Add(fields, LegacyPlayerStatusPacket.MagicDamageDecreaseIndex, ReadUIntField(itemType, MagicDefenceFieldIndex));
            Add(fields, LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex, ReadUIntField(itemType, PhysicalDefenceFieldIndex));
        }
        else
        {
            Add(fields, LegacyPlayerStatusPacket.ImmunityIndex, ReadUIntField(itemType, ImmunityFieldIndex));
            Add(fields, LegacyPlayerStatusPacket.CriticalStrikeIndex, ReadUIntField(itemType, CriticalStrikeFieldIndex));
            Add(fields, LegacyPlayerStatusPacket.SkillCStrikeIndex, ReadUIntField(itemType, SkillCriticalStrikeFieldIndex));
            Add(fields, LegacyPlayerStatusPacket.BreakthroughIndex, ReadUIntField(itemType, BreakthroughFieldIndex));
            Add(fields, LegacyPlayerStatusPacket.CounteractionIndex, ReadUIntField(itemType, CounteractionFieldIndex));
            Add(fields, LegacyPlayerStatusPacket.MDefenceIndex, unchecked((byte)ReadUIntField(itemType, MagicDefenceFieldIndex)));
            Add(fields, LegacyPlayerStatusPacket.DefenceIndex, ReadUIntField(itemType, PhysicalDefenceFieldIndex));
        }

        Add(fields, LegacyPlayerStatusPacket.MaxManaIndex, ReadUIntField(itemType, ItemManaFieldIndex));
    }

    private static void ApplyPurification(uint[] fields, ItemType itemType)
    {
        Add(fields, LegacyPlayerStatusPacket.MaxAttackIndex, ReadUIntField(itemType, MaxAttackFieldIndex));
        Add(fields, LegacyPlayerStatusPacket.MinAttackIndex, ReadUIntField(itemType, MinAttackFieldIndex));
        Add(fields, LegacyPlayerStatusPacket.MagicAttackIndex, ReadUIntField(itemType, MagicAttackFieldIndex));
        Add(fields, LegacyPlayerStatusPacket.MDefenceIndex, unchecked((byte)ReadUIntField(itemType, MagicDefenceFieldIndex)));
        Add(fields, LegacyPlayerStatusPacket.DefenceIndex, ReadUIntField(itemType, PhysicalDefenceFieldIndex));
        Add(fields, LegacyPlayerStatusPacket.CriticalStrikeIndex, ReadUIntField(itemType, CriticalStrikeFieldIndex));
        Add(fields, LegacyPlayerStatusPacket.SkillCStrikeIndex, ReadUIntField(itemType, SkillCriticalStrikeFieldIndex));
        Add(fields, LegacyPlayerStatusPacket.ImmunityIndex, ReadUIntField(itemType, ImmunityFieldIndex));
        Add(fields, LegacyPlayerStatusPacket.PenetrationIndex, ReadUIntField(itemType, PenetrationFieldIndex));
        Add(fields, LegacyPlayerStatusPacket.BlockIndex, ReadUIntField(itemType, BlockFieldIndex));
        Add(fields, LegacyPlayerStatusPacket.BreakthroughIndex, ReadUIntField(itemType, BreakthroughFieldIndex));
        Add(fields, LegacyPlayerStatusPacket.CounteractionIndex, ReadUIntField(itemType, CounteractionFieldIndex));
        Add(fields, LegacyPlayerStatusPacket.DetoxicationIndex, ReadUIntField(itemType, DetoxicationFieldIndex));
        Add(fields, LegacyPlayerStatusPacket.MetalResistanceIndex, ReadUIntField(itemType, MetalResistanceFieldIndex));
        Add(fields, LegacyPlayerStatusPacket.WoodResistanceIndex, ReadUIntField(itemType, WoodResistanceFieldIndex));
        Add(fields, LegacyPlayerStatusPacket.WaterResistanceIndex, ReadUIntField(itemType, WaterResistanceFieldIndex));
        Add(fields, LegacyPlayerStatusPacket.FireResistanceIndex, ReadUIntField(itemType, FireResistanceFieldIndex));
        Add(fields, LegacyPlayerStatusPacket.EarthResistanceIndex, ReadUIntField(itemType, EarthResistanceFieldIndex));
    }

    private static void ApplyRefineryEffect(uint[] fields, RefineryMaterial refinery)
    {
        ApplyRefineryEffect(fields, refinery.EffectType, refinery.PrimaryValue);

        if (refinery.Parameters.Count >= 4)
        {
            ApplyRefineryEffect(fields, refinery.Parameters[2], refinery.Parameters[3]);
        }
    }

    private static void ApplyRefineryEffect(uint[] fields, int effectType, int value)
    {
        if (value <= 0)
        {
            return;
        }

        var unsignedValue = checked((uint)value);
        switch (effectType)
        {
            case LegacyRefineryMDefenceType:
                Add(fields, LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex, unsignedValue);
                break;
            // The old refinery enum name is misleading: FinalMDamage adds physical damage increase.
            case LegacyRefineryFinalMDamageType:
                Add(fields, LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex, unsignedValue);
                break;
            case LegacyRefineryFinalMAttackType:
                Add(fields, LegacyPlayerStatusPacket.MagicDamageIncreaseIndex, unsignedValue);
                break;
        }
    }

    private static void ApplyItemAdd(uint[] fields, int position, ItemAddBonus itemAdd, uint animaPercent)
    {
        var maxAttack = ReadItemAddValue(itemAdd, ItemAddMaxAttackValueIndex);
        var minAttack = ReadItemAddValue(itemAdd, ItemAddMinAttackValueIndex);
        var physicalDefence = ReadItemAddValue(itemAdd, ItemAddPhysicalDefenceValueIndex);
        var magicAttack = ReadItemAddValue(itemAdd, ItemAddMagicAttackValueIndex);
        var magicDefence = ReadItemAddValue(itemAdd, ItemAddMagicDefenceValueIndex);
        var agility = ReadItemAddValue(itemAdd, ItemAddAgilityValueIndex);
        var dodge = ReadItemAddValue(itemAdd, ItemAddDodgeValueIndex);

        if (position is 4 or 5)
        {
            Add(fields, LegacyPlayerStatusPacket.AccuracyIndex, ApplyAnimaPercent(agility, animaPercent));
        }

        if (position is 10 or 19)
        {
            Add(fields, LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex, maxAttack);
            Add(fields, LegacyPlayerStatusPacket.MagicDamageIncreaseIndex, magicAttack);
        }
        else
        {
            Add(fields, LegacyPlayerStatusPacket.MaxAttackIndex, ApplyAnimaPercent(maxAttack, animaPercent));
            Add(fields, LegacyPlayerStatusPacket.MinAttackIndex, ApplyAnimaPercent(minAttack, animaPercent));
            Add(fields, LegacyPlayerStatusPacket.MagicAttackIndex, ApplyAnimaPercent(magicAttack, animaPercent));
        }

        if (position is 11 or 19)
        {
            Add(fields, LegacyPlayerStatusPacket.MagicDamageDecreaseIndex, magicDefence);
            Add(fields, LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex, physicalDefence);
        }
        else
        {
            Add(fields, LegacyPlayerStatusPacket.MagicDefenceIndex, ApplyAnimaPercent(magicDefence, animaPercent));
            Add(fields, LegacyPlayerStatusPacket.DefenceIndex, ApplyAnimaPercent(physicalDefence, animaPercent));
        }

        if (position != 12)
        {
            Add(fields, LegacyPlayerStatusPacket.DodgeIndex, ApplyAnimaPercent(dodge, animaPercent));
        }
    }

    private static void ApplyRelicAttributes(uint[] fields, PlayerItemSnapshotItem item, ref uint luckyStrike)
    {
        foreach (var encoded in new[] { item.Attribute1, item.Attribute2, item.Attribute3, item.Attribute4, item.Attribute5 })
        {
            if (encoded == 0)
            {
                continue;
            }

            var type = encoded % 100;
            var value = encoded / 1000;
            switch (type)
            {
                case 1:
                    Add(fields, LegacyPlayerStatusPacket.CriticalStrikeIndex, value);
                    break;
                case 2:
                    Add(fields, LegacyPlayerStatusPacket.SkillCStrikeIndex, value);
                    break;
                case 3:
                    Add(fields, LegacyPlayerStatusPacket.ImmunityIndex, value);
                    break;
                case 4:
                    Add(fields, LegacyPlayerStatusPacket.BreakthroughIndex, value / 10);
                    break;
                case 5:
                    Add(fields, LegacyPlayerStatusPacket.CounteractionIndex, value / 10);
                    break;
                case 7:
                    Add(fields, LegacyPlayerStatusPacket.MinAttackIndex, value);
                    Add(fields, LegacyPlayerStatusPacket.MaxAttackIndex, value);
                    break;
                case 8:
                    Add(fields, LegacyPlayerStatusPacket.MagicAttackIndex, value);
                    break;
                case 9:
                    Add(fields, LegacyPlayerStatusPacket.MagicDefenceIndex, value);
                    break;
                case 10:
                    Add(fields, LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex, value);
                    break;
                case 11:
                    Add(fields, LegacyPlayerStatusPacket.MagicDamageIncreaseIndex, value);
                    break;
                case 12:
                    Add(fields, LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex, value);
                    break;
                case 13:
                    Add(fields, LegacyPlayerStatusPacket.MagicDamageDecreaseIndex, value);
                    break;
                case 14:
                    luckyStrike = checked(luckyStrike + value);
                    break;
            }
        }
    }

    private static void ApplyPerfectionAttributes(
        uint[] fields,
        uint prestigeLevel,
        IReadOnlyDictionary<int, IReadOnlyList<ItemRefineEffect>>? itemRefineEffectsById)
    {
        if (prestigeLevel == 0 ||
            prestigeLevel > int.MaxValue ||
            itemRefineEffectsById is null ||
            !itemRefineEffectsById.TryGetValue((int)prestigeLevel, out var rows) ||
            rows.Count == 0)
        {
            return;
        }

        var effect = rows[0];
        var physicalAttack = ReadEffectValue(effect, 0);
        var magicAttack = ReadEffectValue(effect, 1);
        var physicalDefence = ReadEffectValue(effect, 2);
        var magicDefence = ReadEffectValue(effect, 3);

        Add(fields, LegacyPlayerStatusPacket.MinAttackIndex, physicalAttack);
        Add(fields, LegacyPlayerStatusPacket.MaxAttackIndex, physicalAttack);
        Add(fields, LegacyPlayerStatusPacket.MagicAttackIndex, magicAttack);
        Add(fields, LegacyPlayerStatusPacket.DefenceIndex, physicalDefence);
        Add(fields, LegacyPlayerStatusPacket.MagicDefenceIndex, magicDefence);
    }

    private static void AddGem(uint[] fields, uint[] gemValues, ref int tortoiseGemCount, ushort gem)
    {
        var value = gem switch
        {
            NormalDragonGem => 5u,
            RefinedPhoenixGem or RefinedDragonGem => 10u,
            SuperPhoenixGem or SuperDragonGem => 15u,
            NormalTortoiseGem => 2u,
            RefinedTortoiseGem => 4u,
            SuperTortoiseGem => 6u,
            NormalThunderGem or NormalGloryGem => 100u,
            RefinedThunderGem or RefinedGloryGem => 300u,
            SuperThunderGem or SuperGloryGem => 500u,
            _ => 0u,
        };

        if (value == 0)
        {
            return;
        }

        if (value is 5 or 10 or 15)
        {
            if (gem is NormalDragonGem or RefinedDragonGem or SuperDragonGem)
            {
                Add(fields, LegacyPlayerStatusPacket.PhysicalPercentIndex, value);
            }
            else
            {
                Add(fields, LegacyPlayerStatusPacket.MagicPercentIndex, value);
            }
        }

        if (gem is >= NormalTortoiseGem and <= SuperTortoiseGem)
        {
            tortoiseGemCount++;
        }

        if (tortoiseGemCount <= 8)
        {
            gemValues[gem / 10] = checked(gemValues[gem / 10] + value);
        }
    }

    private static void NormalizeSocketPair(int position, ref ushort socketOne, ref ushort socketTwo)
    {
        if (position == 10)
        {
            if (!IsSocketEmptyOrInRange(socketOne, NormalThunderGem, SuperThunderGem))
            {
                socketOne = NoSocket;
            }

            if (!IsSocketEmptyOrInRange(socketTwo, NormalThunderGem, SuperThunderGem))
            {
                socketTwo = NoSocket;
            }
        }
        else if (position == 11)
        {
            if (!IsSocketEmptyOrInRange(socketOne, NormalGloryGem, SuperGloryGem))
            {
                socketOne = NoSocket;
            }

            if (!IsSocketEmptyOrInRange(socketTwo, NormalGloryGem, SuperGloryGem))
            {
                socketTwo = NoSocket;
            }
        }
        else if (position == 19)
        {
            if (!IsSocketEmptyOrInRange(socketOne, NormalThunderGem, SuperThunderGem))
            {
                socketOne = NoSocket;
            }

            if (!IsSocketEmptyOrInRange(socketTwo, NormalGloryGem, SuperGloryGem))
            {
                socketTwo = NoSocket;
            }
        }
    }

    private static bool IsSocketEmptyOrInRange(ushort value, ushort min, ushort max)
    {
        return value is NoSocket or EmptySocket || value >= min && value <= max;
    }

    private static bool TryGetItemType(
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>>? itemsById,
        uint itemId,
        out ItemType itemType)
    {
        itemType = default!;
        if (itemId > int.MaxValue ||
            itemsById is null ||
            !itemsById.TryGetValue((int)itemId, out var rows) ||
            rows.Count == 0)
        {
            return false;
        }

        itemType = rows[0];
        return true;
    }

    private static bool TryGetItemAddBonus(
        IReadOnlyDictionary<(int ItemTypeId, int Level), ItemAddBonus>? itemAddBonusByItemTypeIdAndLevel,
        uint itemId,
        int plus,
        out ItemAddBonus itemAdd)
    {
        itemAdd = default!;
        if (itemAddBonusByItemTypeIdAndLevel is null)
        {
            return false;
        }

        var baseId = GetLegacyItemAddBaseId(itemId);
        if (itemAddBonusByItemTypeIdAndLevel.TryGetValue(((int)baseId, plus), out var exact) ||
            itemAddBonusByItemTypeIdAndLevel.TryGetValue(((int)baseId + 10, plus), out exact) ||
            itemAddBonusByItemTypeIdAndLevel.TryGetValue(((int)baseId + 20, plus), out exact))
        {
            itemAdd = exact;
            return true;
        }

        return false;
    }

    private static bool TryGetRefineryMaterial(
        IReadOnlyDictionary<int, RefineryMaterial>? refineryMaterialsByItemType,
        uint refineryEffectId,
        out RefineryMaterial refinery)
    {
        refinery = default!;
        if (refineryEffectId > int.MaxValue ||
            refineryMaterialsByItemType is null ||
            !refineryMaterialsByItemType.TryGetValue((int)refineryEffectId, out var matched))
        {
            return false;
        }

        refinery = matched;
        return true;
    }

    private static uint GetAnimaPlusPercent(
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>>? itemsById,
        uint animaItemId)
    {
        return TryGetItemType(itemsById, animaItemId, out var anima)
            ? ReadUIntField(anima, ItemHitpointsFieldIndex) / 100
            : 0;
    }

    private static bool IsPurificationInLife(PlayerItemSnapshotItem item)
    {
        if (item.PurificationDuration == 0)
        {
            return item.PurificationItemId > 1000;
        }

        try
        {
            return DateTime.FromBinary(item.PurificationAddedOn).AddSeconds(item.PurificationDuration) > DateTime.Now;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsRefineryEffectInLife(PlayerItemSnapshotItem item)
    {
        if (item.RefineryEffectDuration == 0)
        {
            return item.RefineryEffectId > 1000;
        }

        try
        {
            return DateTime.FromBinary(item.RefineryEffectAddedOn).AddSeconds(item.RefineryEffectDuration) > DateTime.Now;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsLegacySocketClearedPosition(int position)
    {
        return position is 7 or 12 or 13 or 15 or 16 or 17 or 18;
    }

    private static bool IsLegacyBlessEnchantClampedPosition(int position)
    {
        return position is 7 or 10 or 11 or 12 or 13 or 15 or 16 or 17 or 18 or 19;
    }

    private static bool IsLegacyPlusClearedPosition(int position)
    {
        return position is 0 or 7 or 13 or 15 or 16 or 17;
    }

    private static bool IsLegacyWornRunePosition(ushort position)
    {
        return position is >= 101 and <= 131;
    }

    private static int CalculateLegacyBeastLevel(
        uint totalPoints,
        IReadOnlyDictionary<int, BeastAttribute> beastAttributesByLevel)
    {
        var maxLevel = beastAttributesByLevel.Keys.Max();
        var level = 1;
        var points = totalPoints;

        foreach (var attribute in beastAttributesByLevel.Values.OrderBy(attribute => attribute.Level))
        {
            var requiredPoints = checked((uint)Math.Max(0, attribute.Parameters[0]));
            if (points < requiredPoints)
            {
                break;
            }

            points -= requiredPoints;
            level = Math.Min(level + 1, maxLevel);
        }

        return level;
    }

    private static uint GetLegacyItemAddBaseId(uint itemId)
    {
        var itemTypeFamily = itemId / 1000;
        if (itemId == 300000)
        {
            return itemId;
        }

        var firstTwoDigits = itemId / 10000;
        if ((firstTwoDigits is >= 11 and <= 16 ||
                firstTwoDigits is 20 or 42 or 90 ||
                firstTwoDigits is >= 60 and <= 62 ||
                firstTwoDigits is 67 or 68) &&
            itemTypeFamily != 420)
        {
            return itemId - (itemId % 10);
        }

        var firstDigit = itemId / 100000;
        return (firstDigit * 100000) + (firstDigit * 10000) + (firstDigit * 1000) + (itemId % 1000 - itemId % 10);
    }

    private static uint ReadEffectValue(ItemRefineEffect effect, int index)
    {
        return effect.Values.Count > index && effect.Values[index] > 0
            ? checked((uint)effect.Values[index])
            : 0;
    }

    private static uint ApplyAnimaPercent(uint value, uint percent)
    {
        return checked(value + (value * percent / 100));
    }

    private static uint ReadItemAddValue(ItemAddBonus itemAdd, int index)
    {
        return itemAdd.Values.Count > index && itemAdd.Values[index] > 0
            ? checked((uint)itemAdd.Values[index])
            : 0;
    }

    private static uint ReadBeastParameter(BeastAttribute attribute, int index)
    {
        return attribute.Parameters.Count > index && attribute.Parameters[index] > 0
            ? checked((uint)attribute.Parameters[index])
            : 0;
    }

    private static uint ReadUIntField(ItemType itemType, int fieldIndex)
    {
        return itemType.Fields.Count > fieldIndex &&
            uint.TryParse(itemType.Fields[fieldIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0;
    }

    private static uint ReadUIntMagicField(MagicType magic, int fieldIndex)
    {
        return TryReadIntField(magic.Fields, fieldIndex, out var value) && value > 0
            ? checked((uint)value)
            : 0;
    }

    private static bool TryGetLegacyMagicRow(
        IReadOnlyDictionary<int, IReadOnlyList<MagicType>>? magicByTypeId,
        int spellId,
        int spellLevel,
        out MagicType magic)
    {
        magic = null!;
        if (magicByTypeId is null ||
            !magicByTypeId.TryGetValue(spellId, out var rows))
        {
            return false;
        }

        MagicType? fallback = null;
        foreach (var row in rows)
        {
            if (TryReadIntField(row.Fields, LegacyMagicLevelFieldIndex, out var rowLevel) &&
                rowLevel == spellLevel)
            {
                magic = row;
                return true;
            }

            fallback ??= row;
        }

        if (fallback is null)
        {
            return false;
        }

        magic = fallback;
        return true;
    }

    private static bool HasLegacyStatusFlag(IReadOnlyDictionary<string, string> attributes, int flag)
    {
        if (flag < 0)
        {
            return false;
        }

        if (!attributes.TryGetValue("StatusFlags", out var raw) &&
            !attributes.TryGetValue("BitVector", out raw))
        {
            return false;
        }

        var parts = (raw ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var wordIndex = flag / 32;
        if (wordIndex >= parts.Length ||
            !ulong.TryParse(parts[wordIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var word))
        {
            return false;
        }

        return (word & (1UL << (flag % 32))) != 0;
    }

    private static bool TryReadIntField(IReadOnlyList<string> fields, int fieldIndex, out int value)
    {
        value = 0;
        return fields.Count > fieldIndex &&
            int.TryParse(fields[fieldIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static void Add(uint[] fields, int index, uint value)
    {
        fields[index] = checked(fields[index] + value);
    }

    private static void SubtractOrZero(uint[] fields, int index, uint value)
    {
        fields[index] = fields[index] >= value ? fields[index] - value : 0;
    }

    private static uint CheckedUInt32(long value, string name)
    {
        return value is >= uint.MinValue and <= uint.MaxValue
            ? (uint)value
            : throw new InvalidOperationException($"Character snapshot value '{name}'={value} cannot fit in UInt32.");
    }

    private static bool TryReadUIntAttribute(CharacterSnapshot snapshot, string key, out uint value)
    {
        value = 0;
        return snapshot.Attributes.TryGetValue(key, out var raw) &&
            uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool ReadBooleanAttribute(CharacterSnapshot snapshot, string key)
    {
        return snapshot.Attributes.TryGetValue(key, out var raw) &&
            bool.TryParse(raw, out var value) &&
            value;
    }
}
