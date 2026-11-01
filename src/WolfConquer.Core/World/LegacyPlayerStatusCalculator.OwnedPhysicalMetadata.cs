using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class LegacyPlayerStatusCalculator
{
    // Lookup footprint only: formulas and their successful ordering remain in
    // the existing calculator. These helpers reuse its slot/lifetime selectors.
    internal static void CaptureOwnedPhysicalMetadata(LegacyOwnedPhysicalMetadataWitness proof,
        WorldSession source, WorldDataCatalog? data)
    {
        var equipment = LegacyEquipmentSelector.SelectCurrentEquipment(source.PlayerItems, source.AlternateEquipmentActive);
        uint prestige = LegacyUserTotalRefineLevPacket.FromEquipmentPerfection(checked((uint)source.Character.Uid), source.PlayerItems).Level;
        foreach (var item in equipment)
        {
            if (item.Durability == 0) continue;
            proof.UseItem(item.ItemId, required: true);
            CaptureOwnedPhysicalBaseItemNumbers(proof, item.ItemId, item.Position % 50);
            if (IsPurificationInLife(item))
            {
                proof.UseItem(item.PurificationItemId, required: true);
                CaptureOwnedPhysicalPurificationNumbers(proof, item.PurificationItemId);
            }
            proof.UseItem(item.AnimaItemId, required: item.AnimaItemId != 0);
            if (item.AnimaItemId != 0)
            {
                proof.RequireItemNumber(item.AnimaItemId, 18, ushort.MaxValue);
                for (int at = 60; at <= 63; at++) proof.RequireItemNumber(item.AnimaItemId, at, ushort.MaxValue, optionalColumn: true);
            }
            var plus = IsLegacyPlusClearedPosition(item.Position % 50) ? 0 : Math.Min((int)item.Plus, 12);
            if (plus != 0)
            {
                var id = GetLegacyItemAddBaseId(item.ItemId);
                proof.UseAdd(id, plus); proof.UseAdd(checked(id + 10), plus); proof.UseAdd(checked(id + 20), plus);
                if (TryGetItemAddBonus(data?.ItemAddBonusByItemTypeIdAndLevel, item.ItemId, plus, out var addition))
                    proof.RequireItemAddNumbers(addition, 1, 2, 3, 4, 5, 6, 7);
            }
            if (data is not null && IsRefineryEffectInLife(item) && item.RefineryEffectId <= int.MaxValue &&
                proof.UseIndex(data.RefineryMaterialsByItemType, (int)item.RefineryEffectId) is { } refinery)
            {
                proof.RequireRawInt32Equals(refinery.Fields, 2, refinery.PrimaryValue);
                proof.RequireRawInt32Equals(refinery.Fields, 3, refinery.EffectType);
                if (refinery.Parameters.Count >= 4)
                {
                    proof.RequireRawInt32Equals(refinery.Fields, 4, refinery.Parameters[2]);
                    proof.RequireRawInt32Equals(refinery.Fields, 5, refinery.Parameters[3]);
                }
            }
        }
        foreach (var item in source.PlayerItems.RuneItems)
            if (IsLegacyWornRunePosition(item.Position) || item.Position is >= 121 and <= 151)
                proof.UseItem(item.ItemId, required: true);
        if (data is null) return;
        if (prestige is > 0 and <= int.MaxValue) proof.UseGroup(data.ItemRefineEffectsById, (int)prestige);
        foreach (var power in LegacyChiState.FromSnapshot(source.Character).Powers)
            if (power.Unlocked) proof.UseGroup(data.FateExperienceByType, (int)power.Type);
        foreach (var item in source.PlayerItems.MythsoulBagItems)
            if (item.ItemId <= int.MaxValue && proof.UseIndex(data.SpecialEffectAttributesById, (int)item.ItemId) is { } myth &&
                myth.Values.Count != 0)
            {
                proof.RequireRawInt32Equals(myth.Fields, 1, myth.Type);
                proof.RequireRawInt32Equals(myth.Fields, 2, myth.Values[0]);
            }
        if (ReadBooleanAttribute(source.Character, "TailedBeastActivated") &&
            TryReadUIntAttribute(source.Character, "TailedBeastTotalPoints", out var beastPoints))
        {
            proof.UseAllIndex(data.BeastAttributesByLevel);
            foreach (var candidate in data.BeastAttributesByLevel.Values)
                proof.RequireRawInt32Equals(candidate.Fields, 1, candidate.Parameters[0]);
            if (data.BeastAttributesByLevel.Count != 0 && data.BeastAttributesByLevel.TryGetValue(
                CalculateLegacyBeastLevel(beastPoints, data.BeastAttributesByLevel), out var selectedBeast))
                for (int at = 2; at <= 9; at++)
                    if (selectedBeast.Parameters.Count > at)
                        proof.RequireRawInt32Equals(selectedBeast.Fields, at + 1, selectedBeast.Parameters[at]);
        }
        if (source.Character.Class is >= 0 and <= int.MaxValue &&
            proof.UseIndex(data.ProfessionLevelBenefitsByProfessionCode, (int)source.Character.Class) is { } profession)
            for (int at = 0; at < Math.Min(4, profession.BonusValues.Count); at++)
                proof.RequireRawInt32Equals(profession.Fields, at + 3, profession.BonusValues[at]);
        proof.UseRows(data.RuneStorageAttributes);
        var storageScore = LegacyRuneHitpointsCalculator.CalculateStorageScore(source.PlayerItems.RuneItems);
        foreach (var row in data.RuneStorageAttributes)
        {
            proof.RequireRawInt32Equals(row.Fields, 1, row.RequiredStorageScore);
            if (row.RequiredStorageScore < 0 || row.RequiredStorageScore > storageScore) continue;
            for (int at = 0; at < row.Bonuses.Count; at++)
            {
                proof.RequireRawInt32Equals(row.Fields, at * 2 + 2, row.Bonuses[at].AttributeType);
                proof.RequireRawInt32Equals(row.Fields, at * 2 + 3, row.Bonuses[at].Value);
            }
        }
        CaptureOwnedPhysicalGearMetadata(proof, source, data);
    }

    private static void CaptureOwnedPhysicalBaseItemNumbers(LegacyOwnedPhysicalMetadataWitness proof, uint id, int position)
    {
        foreach (int at in new[] { 13, 14, 15, 18, 19, 29, 30 }) proof.RequireItemNumber(id, at, ushort.MaxValue);
        if (position != 12)
        { proof.RequireItemNumber(id, 16, ushort.MaxValue); proof.RequireItemNumber(id, 17, byte.MaxValue); }
        if (position is not (11 or 19))
            foreach (int at in new[] { 39, 40, 41, 44, 45 }) proof.RequireItemNumber(id, at, uint.MaxValue);
    }

    private static void CaptureOwnedPhysicalPurificationNumbers(LegacyOwnedPhysicalMetadataWitness proof, uint id)
    {
        if (id == 0) return;
        foreach (int at in new[] { 13, 14, 15, 18, 19, 29, 30, 46, 47, 48, 49, 50 }) proof.RequireItemNumber(id, at, ushort.MaxValue);
        foreach (int at in new[] { 39, 40, 41, 42, 43, 44, 45, 51 }) proof.RequireItemNumber(id, at, uint.MaxValue);
    }

    private static void CaptureOwnedPhysicalGearMetadata(LegacyOwnedPhysicalMetadataWitness proof,
        WorldSession source, WorldDataCatalog data)
    {
        if (IsLegacyNinjaClass(source.Character.Class) && HasLegacyStatusFlag(source.Character.Attributes, LegacyMudWallFlag))
        {
            proof.UseGroup(data.GouyuTypesByType, LegacyNinjaMudSigilSolidityType);
            var mud = source.PlayerSpells.Spells.FirstOrDefault(row => row.Id == LegacyCombatGearMudWallSpellId);
            if (mud is not null)
            {
                proof.UseMagicLevel(mud.Id, mud.Level, required: false);
                if (TryGetLegacyMagicRow(data.MagicByTypeId, mud.Id, mud.Level, out var magic))
                    _ = proof.RequireRawInt32(magic.Fields, LegacyMagicGDamageFieldIndex);
            }
        }
        if (source.CombatGear is { } gear && gear.Items.Any(item => item.Equip != 0 && IsCombatGearForClass(source.Character.Class, item.ItemID)))
        {
            var equipped = gear.Items.First(item => item.Equip != 0 && IsCombatGearForClass(source.Character.Class, item.ItemID));
            foreach (var item in gear.Items)
            {
                if (!IsCombatGearForClass(source.Character.Class, item.ItemID)) continue;
                if (item.ItemID <= int.MaxValue && item.Level <= int.MaxValue)
                    proof.UseIndex(data.CombatGearByGearTypeAndLevel, ((int)item.ItemID, (int)item.Level));
                foreach (var id in ReferenceEquals(item, equipped) ? item.BlackJades : Array.Empty<uint>())
                    if (id is > 0 and <= int.MaxValue &&
                        proof.UseIndex(data.DaoqiDictTypesById, (int)id) is { } jade)
                        foreach (int at in new[] { 2, 3, 4, 5 }) _ = proof.RequireRawInt32(jade.Fields, at);
            }
            if (source.Character.Class / 1000 == 7)
            {
                foreach (var id in gear.BlackJadeBag.BlackJades)
                    if (id is > 0 and <= int.MaxValue && proof.UseIndex(data.DaoqiDictTypesById, (int)id) is { } jade &&
                        proof.RequireRawInt32(jade.Fields, 6) > 0 && proof.RequireRawInt32(jade.Fields, 14) == 0)
                        _ = proof.RequireRawInt32(jade.Fields, 9);
                for (int id = 700; id <= 703; id++) proof.UseGroup(data.CollectionRewardTiersByCollectionId, id);
            }
            foreach (var pair in OwnedPhysicalGearStatusRows)
                if (HasLegacyStatusFlag(source.Character.Attributes, pair.Flag))
                {
                    proof.UseMagicLevel(pair.Spell, 0, required: false);
                    if (TryGetLegacyMagicRow(data.MagicByTypeId, pair.Spell, 0, out var magic))
                        foreach (int at in pair.Fields) _ = proof.RequireRawInt32(magic.Fields, at);
                }
        }
    }

    private static readonly (int Flag, int Spell, int[] Fields)[] OwnedPhysicalGearStatusRows =
    [
        (LegacyPrisonSigilFutilityCursedFlag, LegacyCombatGearTharnCutterElementalArrowSpellId, [10]),
        (LegacyWhirlSigilAuraCursedFlag, LegacyCombatGearTharnCutterElementalArrowSpellId, [35, 36]),
        (LegacyTop8WeeklyHawksAmbitionFlag, LegacyCombatGearTharnCutterElementalArrowSpellId, [37]),
        (LegacyHolyProtectionFlag, LegacyCombatGearHolyProtectionSpellId, [35]), (LegacyPhoenixBlessingFlag, LegacyCombatGearPhoenixBlessingSpellId, [35]),
        (LegacyNobleSpiritFlag, LegacyCombatGearNobleSpiritSpellId, [10]), (LegacyFlameShieldFlag, LegacyCombatGearFlameShieldSpellId, [35, 36]),
        (LegacyFlowKnackFlag, LegacyCombatGearFlowKnackSpellId, [10, 44]), (LegacyDivineEmptinessFlag, LegacyCombatGearDivineEmptinessSpellId, [35]),
        (LegacyWackeSpiritFlag, LegacyCombatGearWackeSpiritSpellId, [10]), (LegacyCrackMantraPhysicalFlag, LegacyCombatGearCrackMantraSpellId, [35]),
        (LegacyCrackMantraMagicFlag, LegacyCombatGearCrackMantraSpellId, [36]), (LegacySenseFlag, LegacyCombatGearSenseSpellId, [36]),
        (LegacyDiabolizeFlag, LegacyCombatGearDiabolizeSpellId, [10]), (LegacySailingFlag, LegacyCombatGearSailingSpellId, [10]),
        (LegacyShellFlag, LegacyCombatGearShellSpellId, [10]), (LegacyBarrierFlag, LegacyCombatGearBarrierSpellId, [10]),
        (LegacyStormFlag, LegacyCombatGearStormSpellId, [10]), (LegacyThrashFlag, LegacyCombatGearThrashSpellId, [10]),
        (LegacyThunderFlag, LegacyCombatGearThunderSpellId, [10]), (LegacyTorrentFlag, LegacyCombatGearTorrentSpellId, [10]),
        (LegacyTideFlag, LegacyCombatGearTideSpellId, [10]), (LegacySplashFlag, LegacyCombatGearSplashSpellId, [10]),
        (LegacyVastFlag, LegacyCombatGearVastSpellId, [10]), (LegacyColdBloodlineFlag, LegacyCombatGearColdBloodlineSpellId, [35]),
    ];
}
