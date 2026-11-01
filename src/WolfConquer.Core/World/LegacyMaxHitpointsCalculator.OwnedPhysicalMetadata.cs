using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class LegacyMaxHitpointsCalculator
{
    internal static void CaptureOwnedPhysicalMetadata(LegacyOwnedPhysicalMetadataWitness proof,
        WorldSession source, WorldDataCatalog? data)
    {
        // This calculator has a different plus-clearing policy from the status
        // calculator. Reuse its actual fallback-base helper for all candidates.
        foreach (var item in LegacyEquipmentSelector.SelectCurrentEquipment(source.PlayerItems, source.AlternateEquipmentActive))
        {
            if (item.Durability == 0) continue;
            proof.UseItem(item.ItemId, required: true);
            proof.RequireItemNumber(item.ItemId, 18, ushort.MaxValue);
            if (item.PurificationItemId != 0) proof.RequireItemNumber(item.PurificationItemId, 18, ushort.MaxValue);
            int plus = Math.Min((int)item.Plus, 12);
            if (plus == 0) continue;
            var id = GetLegacyItemAddBaseId(item.ItemId);
            proof.UseAdd(id, plus); proof.UseAdd(checked(id + 10), plus); proof.UseAdd(checked(id + 20), plus);
            if (TryGetItemAddBonus(data?.ItemAddBonusByItemTypeIdAndLevel, item.ItemId, plus, out var addition))
                proof.RequireItemAddNumbers(addition, 0);
        }
        if (data is null) return;
        if (source.Character.Class is >= int.MinValue and <= int.MaxValue &&
            proof.UseIndex(data.ProfessionLevelBenefitsByProfessionCode, (int)source.Character.Class) is { } profession)
            for (int at = 0; at < profession.BonusValues.Count; at++)
                proof.RequireRawInt32Equals(profession.Fields, at + 3, profession.BonusValues[at]);
        if (source.Character.Uid is >= 0 and <= int.MaxValue)
        {
            var inner = proof.UseGroup(data.InnerPowerRanksByCharacterId, (int)source.Character.Uid);
            if (inner is { Count: > 0 })
            {
                // All stages/slots of this actual saved row participate in its
                // early-stop rules. Definitions are finite, adopted data only.
                proof.UseAllIndex(data.NeiGongSecretsById);
                proof.UseAllIndex(data.NeiGongTypeBySecretIdAndSlot);
            }
        }
        if (source.Character.Attributes.TryGetValue("SubProfInfo", out var subclasses) && !string.IsNullOrWhiteSpace(subclasses))
            proof.UseRows(data.SubProfessions);
        if (new[] { "JiangHu", "Jiang", "JiangHuInfo", "JiangHuState" }.Any(key =>
            source.Character.Attributes.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)))
        {
            proof.UseRows(data.JiangHuAttributes);
            proof.UseAllIndex(data.JiangHuAttributesById);
            proof.UseAllIndex(data.JiangHuAttributesByTypeAndLevel);
        }
        if (HasLegacyStatusFlag(source.Character.Attributes, LegacySplashFlag))
        {
            proof.UseMagicLevel(LegacyCombatGearSplashSpellId, 0, required: false);
            if (TryGetLegacyMagicRow(data.MagicByTypeId, LegacyCombatGearSplashSpellId, 0, out var splash))
                _ = proof.RequireRawUnsigned(splash.Fields, LegacyMagicGDamageFieldIndex, uint.MaxValue);
        }
    }
}
