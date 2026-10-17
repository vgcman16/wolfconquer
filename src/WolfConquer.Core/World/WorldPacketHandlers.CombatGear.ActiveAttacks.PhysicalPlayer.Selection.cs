using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TrySelectLegacyDirectPhysicalCombatGearSpell(
        WorldSession session,
        WorldDataCatalog? worldData,
        CharacterSnapshot target,
        out uint gearItemId,
        out ushort spellId,
        out bool piratePassive)
    {
        gearItemId = 0;
        spellId = 0;
        piratePassive = false;
        var combatGear = session.CombatGear ?? CombatGearSnapshot.Empty;
        var classCode = session.Character.Class;

        if (worldData is not null &&
            HasLegacyPirateCombatGearEquipped(session) &&
            TrySelectLegacyDirectPhysicalCombatGearPirateSpell(session, worldData, out var passiveSpellId))
        {
            gearItemId = combatGear.Items.First(item =>
                item.Equip == 1 && IsLegacyCombatGearForClass(classCode, item.ItemID)).ItemID;
            spellId = passiveSpellId;
            piratePassive = true;
            return true;
        }

        ushort[] selectedSpells = [];
        if (HasLegacyCombatGearEquippedForClass(combatGear, classCode, LegacyCombatGearStoneCrackerItemId))
        {
            gearItemId = LegacyCombatGearStoneCrackerItemId;
            selectedSpells = LegacyCombatGearStoneCrackerAttackSpells;
            if (HasLegacyStatusFlag(session.Character, LegacyCombatGearWaterTop4WeeklyFlag))
            {
                spellId = LegacyCombatGearStoneCrackerWaterAttackSpellId;
                return true;
            }
        }

        if (HasLegacyCombatGearEquippedForClass(combatGear, classCode, LegacyCombatGearColdMoonItemId))
        {
            gearItemId = LegacyCombatGearColdMoonItemId;
            selectedSpells = LegacyCombatGearColdMoonAttackSpells;
        }

        if (HasLegacyCombatGearEquippedForClass(combatGear, classCode, LegacyCombatGearTharnCutterItemId))
        {
            gearItemId = LegacyCombatGearTharnCutterItemId;
            selectedSpells = LegacyCombatGearTharnCutterAttackSpells;
        }

        if (HasLegacyCombatGearEquippedForClass(combatGear, classCode, LegacyCombatGearDragonhowlItemId))
        {
            gearItemId = LegacyCombatGearDragonhowlItemId;
            selectedSpells = LegacyCombatGearDragonhowlAttackSpells;
            if (worldData is not null)
            {
                if (LegacyGetDistance(session.Character.X, session.Character.Y, target.X, target.Y) <= 3 &&
                    CheckLegacyDirectPhysicalCombatGearLearnedRateSpell(session, worldData, 17_490, 1))
                {
                    spellId = 17_490;
                    return true;
                }

                if (CheckLegacyDirectPhysicalCombatGearLearnedRateSpell(session, worldData, 17_650, 2))
                {
                    spellId = 17_650;
                    return true;
                }
            }
        }

        if (HasLegacyCombatGearEquippedForClass(combatGear, classCode, LegacyCombatGearBloodlustItemId))
        {
            gearItemId = LegacyCombatGearBloodlustItemId;
            selectedSpells = LegacyCombatGearBloodlustAttackSpells;
            if (worldData is not null &&
                LegacyGetDistance(session.Character.X, session.Character.Y, target.X, target.Y) <= 3 &&
                CheckLegacyDirectPhysicalCombatGearLearnedRateSpell(session, worldData, 17_530, 1))
            {
                spellId = 17_530;
                return true;
            }
        }

        if (HasLegacyCombatGearEquippedForClass(combatGear, classCode, LegacyCombatGearRedcurseItemId))
        {
            gearItemId = LegacyCombatGearRedcurseItemId;
            selectedSpells = LegacyCombatGearRedcurseAttackSpells;
            if (worldData is not null &&
                LegacyGetDistance(session.Character.X, session.Character.Y, target.X, target.Y) <= 3 &&
                CheckLegacyDirectPhysicalCombatGearLearnedRateSpell(session, worldData, 17_530, 1) &&
                session.PlayerSpells.ContainsSpell(17_570))
            {
                spellId = 17_570;
                return true;
            }
        }

        if (selectedSpells.Length == 0)
        {
            return false;
        }

        spellId = selectedSpells[ReadLegacyCombatGearAttackSpellIndex(session.Character, selectedSpells.Length)];
        return true;
    }

    private static bool CheckLegacyDirectPhysicalCombatGearLearnedRateSpell(
        WorldSession session,
        WorldDataCatalog worldData,
        ushort spellId,
        uint rateDivisor)
    {
        if (!LegacyMagicRuntime.TryResolveLearnedSpell(
                session.PlayerSpells, worldData, spellId, out var playerSpell, out _) ||
            !TryReadLegacyMagicUInt32Field(worldData, spellId, playerSpell.Level, LegacyMagicRateField, out var rate))
        {
            return false;
        }

        var effectiveRate = rateDivisor <= 1 ? rate : rate / rateDivisor;
        return CheckLegacyCombatGearAttackRateRoll(session.Character, spellId, effectiveRate);
    }

    private static bool TrySelectLegacyDirectPhysicalCombatGearPirateSpell(
        WorldSession session,
        WorldDataCatalog worldData,
        out ushort spellId)
    {
        for (var attempt = 0; attempt < LegacyCombatGearPiratePassiveSpells.Length; attempt++)
        {
            var index = ReadLegacyCombatGearPiratePassiveSpellIndex(session.Character, attempt);
            var candidate = LegacyCombatGearPiratePassiveSpells[index];
            if (!LegacyMagicRuntime.TryResolveLearnedSpell(
                    session.PlayerSpells, worldData, candidate, out var playerSpell, out _) ||
                !CheckLegacyCombatGearPiratePassiveRateRoll(session.Character, worldData, candidate, playerSpell.Level))
            {
                continue;
            }

            spellId = candidate;
            return true;
        }

        spellId = 0;
        return false;
    }
}
