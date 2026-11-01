using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class LegacyPlayerStatusCalculator
{
    internal static void CaptureOwnedPhysicalNativeQueryMetadata(LegacyOwnedPhysicalMetadataWitness proof,
        WorldSession source, WorldDataCatalog? data)
    {
        // Only the actual admitted query asks for this footprint. Its selector
        // and shared prestige differ from the ordinary full-status projection.
        uint prestige = 0;
        foreach (var item in LegacyEquipmentQuerySelector.Select(source))
        {
            prestige = checked(prestige + item.PerfectionLevel);
            if (item.Durability == 0) continue;
            proof.UseItem(item.ItemId, required: true);
            CaptureOwnedPhysicalBaseItemNumbers(proof, item.ItemId, item.Position % 50);
            if (NativeMagicEffectInLife(item.PurificationItemId, item.PurificationDuration, item.PurificationAddedOn))
                CaptureOwnedPhysicalPurificationNumbers(proof, item.PurificationItemId);
            if (item.AnimaItemId != 0)
            {
                proof.RequireItemNumber(item.AnimaItemId, 18, ushort.MaxValue);
                proof.RequireItemNumber(item.AnimaItemId, 61, ushort.MaxValue, optionalColumn: true);
            }
            if (data is not null && NativeMagicEffectInLife(item.RefineryEffectId, item.RefineryEffectDuration, item.RefineryEffectAddedOn) &&
                item.RefineryEffectId <= int.MaxValue && proof.UseIndex(data.RefineryMaterialsByItemType, (int)item.RefineryEffectId) is { } refinery)
            {
                foreach (int at in new[] { 3, 4 }) proof.RequireRawUnsigned(refinery.Fields, at, byte.MaxValue);
                foreach (int at in new[] { 2, 5 }) proof.RequireRawUnsigned(refinery.Fields, at, uint.MaxValue);
            }
        }
        if (data is null) return;
        proof.UseGroup(data.ItemRefineEffectsById, unchecked((ushort)prestige));
        CaptureOwnedPhysicalNativeContributorNumbers(proof, source, data);
        CaptureOwnedPhysicalNativeManaNumbers(proof, source, data);
    }

    private static void CaptureOwnedPhysicalNativeContributorNumbers(LegacyOwnedPhysicalMetadataWitness proof,
        WorldSession source, WorldDataCatalog data)
    {
        if (source.Character.Class is >= 0 and <= int.MaxValue &&
            proof.UseIndex(data.ProfessionLevelBenefitsByProfessionCode, (int)source.Character.Class) is { } profession)
            for (int at = 3; at < 7; at++) _ = proof.RequireRawUnsigned(profession.Fields, at, uint.MaxValue);
        if (data.BeastAttributesByLevel.Count != 0 && ReadBooleanAttribute(source.Character, "TailedBeastActivated") &&
            TryReadUIntAttribute(source.Character, "TailedBeastTotalPoints", out var points) &&
            data.BeastAttributesByLevel.TryGetValue(CalculateLegacyBeastLevel(points, data.BeastAttributesByLevel), out var beast))
            foreach (int at in new[] { 4, 8 }) _ = proof.RequireRawUnsigned(beast.Fields, at, uint.MaxValue);
        foreach (var item in source.PlayerItems.MythsoulBagItems)
            if (item.ItemId <= int.MaxValue && proof.UseIndex(data.SpecialEffectAttributesById, (int)item.ItemId) is { } myth)
                foreach (int at in new[] { 1, 2 }) _ = proof.RequireRawUnsigned(myth.Fields, at, uint.MaxValue);
        uint score = 0;
        foreach (var item in source.PlayerItems.RuneItems)
            if (item.Position is not (0 or 211))
                score = unchecked(score + 500U + (item.ItemId / 10_000 != 401 ? 300U * (item.ItemId % 100) : 0U));
        proof.UseRows(data.RuneStorageAttributes);
        foreach (var row in data.RuneStorageAttributes)
        {
            if (proof.RequireRawUnsigned(row.Fields, 1, uint.MaxValue) > score) continue;
            for (int at = 2; at <= 6; at += 2)
            {
                _ = proof.RequireRawUnsigned(row.Fields, at, byte.MaxValue);
                _ = proof.RequireRawUnsigned(row.Fields, at + 1, uint.MaxValue);
            }
        }
    }

    private static void CaptureOwnedPhysicalNativeManaNumbers(LegacyOwnedPhysicalMetadataWitness proof,
        WorldSession source, WorldDataCatalog data)
    {
        var family = source.Character.Class / 1_000;
        if (family is not (2 or 4 or 7 or 13 or 14) || source.CombatGear is not { } gear) return;
        if (source.Character.Class is >= 7_000 and <= 7_051)
        {
            proof.UseRows(data.CollectionRewardTiers); // Actual source scans ordered tiers even at score0.
            for (uint collection = 700; collection < 704; collection++)
            {
                int score = CaptureOwnedPhysicalNativeCollectionScore(proof, data, gear, collection);
                foreach (var row in data.CollectionRewardTiers)
                    if (proof.RequireRawUnsigned(row.Fields, 1, uint.MaxValue) == collection &&
                        (long)score >= proof.RequireRawUnsigned(row.Fields, 2, uint.MaxValue) &&
                        (long)score <= proof.RequireRawUnsigned(row.Fields, 3, uint.MaxValue) &&
                        proof.RequireRawUnsigned(row.Fields, 4, uint.MaxValue) == 23)
                        _ = proof.RequireRawUnsigned(row.Fields, 5, uint.MaxValue);
            }
        }
        var equipped = gear.Items.FirstOrDefault(item => item.Equip == 1 && (family switch
        {
            2 => item.ItemID is >= 201 and <= 203, 4 => item.ItemID is >= 401 and <= 403,
            7 => item.ItemID is >= 701 and <= 703, _ => item.ItemID is >= 1_301 and <= 1_305,
        }));
        if (equipped is null) return;
        foreach (var id in equipped.BlackJades)
            if (id <= int.MaxValue && proof.UseIndex(data.DaoqiDictTypesById, (int)id) is { } jade)
            {
                if (proof.RequireRawUnsigned(jade.Fields, 2, uint.MaxValue) == 23)
                    _ = proof.RequireRawUnsigned(jade.Fields, 3, uint.MaxValue);
                if (proof.RequireRawUnsigned(jade.Fields, 4, uint.MaxValue) == 23)
                    _ = proof.RequireRawUnsigned(jade.Fields, 5, uint.MaxValue);
            }
    }

    private static int CaptureOwnedPhysicalNativeCollectionScore(LegacyOwnedPhysicalMetadataWitness proof,
        WorldDataCatalog data, CombatGearSnapshot gear, uint collection)
    {
        int score = 0;
        if (collection != 700 && gear.Items.FirstOrDefault(item => item.ItemID == collection) is { } item)
        {
            var itemScore = item.ItemID <= int.MaxValue && item.Level <= int.MaxValue &&
                proof.UseIndex(data.CombatGearByGearTypeAndLevel, ((int)item.ItemID, (int)item.Level)) is { } row
                ? unchecked((uint)row.Score) : 0;
            score = unchecked(score + (int)(itemScore + item.MasteryScore));
        }
        foreach (var id in gear.BlackJadeBag.BlackJades)
            if (id <= int.MaxValue && proof.UseIndex(data.DaoqiDictTypesById, (int)id) is { } jade)
            {
                var type = proof.RequireRawUnsigned(jade.Fields, 14, uint.MaxValue);
                if (collection == 700 ? type == 0 && proof.RequireRawUnsigned(jade.Fields, 9, uint.MaxValue) % 100 == 7 : type == collection)
                    score = unchecked(score + (int)proof.RequireRawUnsigned(jade.Fields, 6, uint.MaxValue));
            }
        return score;
    }
}
