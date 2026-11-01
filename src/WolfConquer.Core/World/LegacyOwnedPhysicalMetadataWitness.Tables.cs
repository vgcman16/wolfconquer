using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOwnedPhysicalMetadataWitness
{
    private bool IsActualRaw(object source) => catalog is not null && new object[]
    {
        catalog.Items, catalog.ItemAddBonuses, catalog.RefineryMaterials, catalog.FateExperience,
        catalog.ItemRefineEffects, catalog.CombatGear, catalog.DaoqiDictTypes, catalog.CollectionRewardTiers,
        catalog.GouyuTypes, catalog.Magic, catalog.SpecialEffectAttributes, catalog.BeastAttributes,
        catalog.ProfessionLevelBenefits, catalog.JiangHuAttributes, catalog.HundredWeapons,
        catalog.SubProfessions, catalog.RuneStorageAttributes, catalog.NeiGongSecrets, catalog.NeiGongTypes,
        catalog.InnerPowerRanks, catalog.CombatGearRanks,
    }.Any(value => ReferenceEquals(value, source));

    private void RequireActualIndex(object source)
    {
        if (!IsActualIndex(source)) throw Invalid();
        actualIndices.Add(source);
    }

    private bool IsActualIndex(object source) => catalog is not null && new object[]
        {
            catalog.ItemsById, catalog.ItemAddBonusByItemTypeIdAndLevel, catalog.RefineryMaterialsByItemType,
            catalog.FateExperienceByType, catalog.ItemRefineEffectsById, catalog.CombatGearByGearTypeAndLevel,
            catalog.DaoqiDictTypesById, catalog.CollectionRewardTiersByCollectionId, catalog.GouyuTypesByType,
            catalog.MagicByTypeId, catalog.SpecialEffectAttributesById, catalog.BeastAttributesByLevel,
            catalog.ProfessionLevelBenefitsByProfessionCode, catalog.JiangHuAttributesById,
            catalog.JiangHuAttributesByTypeAndLevel, catalog.HundredWeaponsById, catalog.SubProfessionsById,
            catalog.InnerPowerRanksByCharacterId, catalog.NeiGongSecretsById, catalog.NeiGongTypeBySecretIdAndSlot,
        }.Any(value => ReferenceEquals(value, source));

    private object IndexKey(object source, object row) => row switch
    {
        ItemType value when ReferenceEquals(source, catalog!.ItemsById) => value.Id,
        ItemAddBonus value when ReferenceEquals(source, catalog!.ItemAddBonusByItemTypeIdAndLevel) => (value.ItemTypeId, value.Level),
        RefineryMaterial value when ReferenceEquals(source, catalog!.RefineryMaterialsByItemType) => value.ItemType,
        FateExperience value when ReferenceEquals(source, catalog!.FateExperienceByType) => value.Type,
        ItemRefineEffect value when ReferenceEquals(source, catalog!.ItemRefineEffectsById) => value.Id,
        CombatGear value when ReferenceEquals(source, catalog!.CombatGearByGearTypeAndLevel) => (value.GearTypeId, value.Level),
        DaoqiDictType value when ReferenceEquals(source, catalog!.DaoqiDictTypesById) => value.Id,
        CollectionRewardTier value when ReferenceEquals(source, catalog!.CollectionRewardTiersByCollectionId) => value.CollectionId,
        GouyuType value when ReferenceEquals(source, catalog!.GouyuTypesByType) => value.Type,
        MagicType value when ReferenceEquals(source, catalog!.MagicByTypeId) => value.TypeId,
        SpecialEffectAttribute value when ReferenceEquals(source, catalog!.SpecialEffectAttributesById) => value.Id,
        BeastAttribute value when ReferenceEquals(source, catalog!.BeastAttributesByLevel) => value.Level,
        ProfessionLevelBenefit value when ReferenceEquals(source, catalog!.ProfessionLevelBenefitsByProfessionCode) => value.ProfessionCode,
        JiangHuAttribute value when ReferenceEquals(source, catalog!.JiangHuAttributesById) => value.Id,
        JiangHuAttribute value when ReferenceEquals(source, catalog!.JiangHuAttributesByTypeAndLevel) => (value.AttributeType, value.Level),
        HundredWeapon value when ReferenceEquals(source, catalog!.HundredWeaponsById) => value.Id,
        SubProfessionEntry value when ReferenceEquals(source, catalog!.SubProfessionsById) => value.Id,
        InnerPowerRankEntry value when ReferenceEquals(source, catalog!.InnerPowerRanksByCharacterId) => value.CharacterId,
        NeiGongSecret value when ReferenceEquals(source, catalog!.NeiGongSecretsById) => value.Id,
        NeiGongType value when ReferenceEquals(source, catalog!.NeiGongTypeBySecretIdAndSlot) => (value.SecretId, value.Slot),
        _ => throw Invalid(),
    };

    private IEnumerable<object> RowsForIndex(object source)
    {
        if (ReferenceEquals(source, catalog!.ItemsById)) return Raw(catalog.Items);
        if (ReferenceEquals(source, catalog.ItemAddBonusByItemTypeIdAndLevel)) return Raw(catalog.ItemAddBonuses);
        if (ReferenceEquals(source, catalog.RefineryMaterialsByItemType)) return Raw(catalog.RefineryMaterials);
        if (ReferenceEquals(source, catalog.FateExperienceByType)) return Raw(catalog.FateExperience);
        if (ReferenceEquals(source, catalog.ItemRefineEffectsById)) return Raw(catalog.ItemRefineEffects);
        if (ReferenceEquals(source, catalog.CombatGearByGearTypeAndLevel)) return Raw(catalog.CombatGear);
        if (ReferenceEquals(source, catalog.DaoqiDictTypesById)) return Raw(catalog.DaoqiDictTypes);
        if (ReferenceEquals(source, catalog.CollectionRewardTiersByCollectionId)) return Raw(catalog.CollectionRewardTiers);
        if (ReferenceEquals(source, catalog.GouyuTypesByType)) return Raw(catalog.GouyuTypes);
        if (ReferenceEquals(source, catalog.MagicByTypeId)) return Raw(catalog.Magic);
        if (ReferenceEquals(source, catalog.SpecialEffectAttributesById)) return Raw(catalog.SpecialEffectAttributes);
        if (ReferenceEquals(source, catalog.BeastAttributesByLevel)) return Raw(catalog.BeastAttributes);
        if (ReferenceEquals(source, catalog.ProfessionLevelBenefitsByProfessionCode)) return Raw(catalog.ProfessionLevelBenefits);
        if (ReferenceEquals(source, catalog.JiangHuAttributesById) || ReferenceEquals(source, catalog.JiangHuAttributesByTypeAndLevel))
            return Raw(catalog.JiangHuAttributes);
        if (ReferenceEquals(source, catalog.HundredWeaponsById)) return Raw(catalog.HundredWeapons);
        if (ReferenceEquals(source, catalog.SubProfessionsById)) return Raw(catalog.SubProfessions);
        if (ReferenceEquals(source, catalog.InnerPowerRanksByCharacterId)) return Raw(catalog.InnerPowerRanks);
        if (ReferenceEquals(source, catalog.NeiGongSecretsById)) return Raw(catalog.NeiGongSecrets);
        if (ReferenceEquals(source, catalog.NeiGongTypeBySecretIdAndSlot)) return Raw(catalog.NeiGongTypes);
        throw Invalid();
    }

    private IEnumerable<object> RawRows(object row) => row switch
    {
        ItemType => Raw(catalog!.Items), ItemAddBonus => Raw(catalog!.ItemAddBonuses),
        RefineryMaterial => Raw(catalog!.RefineryMaterials), FateExperience => Raw(catalog!.FateExperience),
        ItemRefineEffect => Raw(catalog!.ItemRefineEffects), CombatGear => Raw(catalog!.CombatGear),
        DaoqiDictType => Raw(catalog!.DaoqiDictTypes), CollectionRewardTier => Raw(catalog!.CollectionRewardTiers),
        GouyuType => Raw(catalog!.GouyuTypes), MagicType => Raw(catalog!.Magic),
        SpecialEffectAttribute => Raw(catalog!.SpecialEffectAttributes), BeastAttribute => Raw(catalog!.BeastAttributes),
        ProfessionLevelBenefit => Raw(catalog!.ProfessionLevelBenefits), JiangHuAttribute => Raw(catalog!.JiangHuAttributes),
        HundredWeapon => Raw(catalog!.HundredWeapons), SubProfessionEntry => Raw(catalog!.SubProfessions),
        RuneStorageAttribute => Raw(catalog!.RuneStorageAttributes), NeiGongSecret => Raw(catalog!.NeiGongSecrets),
        NeiGongType => Raw(catalog!.NeiGongTypes), InnerPowerRankEntry => Raw(catalog!.InnerPowerRanks),
        CombatGearRankEntry => Raw(catalog!.CombatGearRanks), _ => throw Invalid(),
    };

    private IEnumerable<object> Raw<T>(IReadOnlyList<T> source) where T : class
    {
        if (!IsActualRaw(source)) throw Invalid();
        actualRaw.Add(source);
        UseList(source);
        if (capturedRaw.Add(source))
            foreach (var row in source)
            {
                if (row is null) throw Invalid();
                rawCounts[row] = rawCounts.TryGetValue(row, out var count) ? checked(count + 1) : 1;
            }
        return source;
    }
}
