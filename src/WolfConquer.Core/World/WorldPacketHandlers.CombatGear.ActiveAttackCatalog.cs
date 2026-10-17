using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryResolveLegacyCombatGearCatalogSpell(
        WorldSession session,
        WorldDataCatalog worldData,
        ushort spellId,
        out PlayerSpellSnapshotEntry playerSpell,
        out LegacyMagicRuntimeSpell spell)
    {
        foreach (var catalogSpell in EnumerateLegacyCombatGearCatalogSpells(session, worldData))
        {
            if (catalogSpell.Id != spellId)
            {
                continue;
            }

            if (LegacyMagicRuntime.TryResolveSpell(worldData, catalogSpell.Id, catalogSpell.Level, out spell))
            {
                playerSpell = catalogSpell;
                return true;
            }
        }

        playerSpell = default!;
        spell = default!;
        return false;
    }

    private static bool TryResolveLegacyCombatGearUpdateTargetSpell(
        WorldSession session,
        WorldDataCatalog worldData,
        ushort spellId,
        out PlayerSpellSnapshotEntry playerSpell,
        out LegacyMagicRuntimeSpell spell)
    {
        if (LegacyMagicRuntime.TryResolveLearnedSpell(session.PlayerSpells, worldData, spellId, out playerSpell, out spell))
        {
            return true;
        }

        foreach (var catalogSpell in EnumerateLegacyCombatGearUpdateTargetCatalogSpells(session, worldData))
        {
            if (catalogSpell.Id != spellId)
            {
                continue;
            }

            if (LegacyMagicRuntime.TryResolveSpell(worldData, catalogSpell.Id, catalogSpell.Level, out spell))
            {
                playerSpell = catalogSpell;
                return true;
            }
        }

        playerSpell = default!;
        spell = default!;
        return false;
    }


    private static IEnumerable<PlayerSpellSnapshotEntry> EnumerateLegacyCombatGearCatalogSpells(
        WorldSession session,
        WorldDataCatalog worldData)
    {
        var combatGear = session.CombatGear ?? CombatGearSnapshot.Empty;
        if (combatGear.Items.Count == 0)
        {
            yield break;
        }

        var classCode = session.Character.Class;
        var isPirate = classCode / 1000 == 7;
        foreach (var item in combatGear.Items)
        {
            if (isPirate && IsLegacyCombatGearForClass(classCode, item.ItemID))
            {
                foreach (var blackJadeSpell in EnumerateLegacyCombatGearBlackJadeSpells(item, worldData))
                {
                    yield return blackJadeSpell;
                }
            }

            if (!IsLegacyCombatGearForClass(classCode, item.ItemID) ||
                !isPirate && item.Equip != 1)
            {
                continue;
            }

            foreach (var attackSpell in EnumerateLegacyCombatGearOpenAttackSpells(item.ItemID))
            {
                yield return attackSpell;
            }

            if (!worldData.CombatGearByGearTypeAndLevel.TryGetValue(((int)item.ItemID, (int)item.Level), out var sourceGear))
            {
                continue;
            }

            foreach (var skill in EnumerateLegacyCombatGearClassSkills(sourceGear, classCode))
            {
                yield return skill;
            }
        }
    }

    private static IEnumerable<PlayerSpellSnapshotEntry> EnumerateLegacyCombatGearUpdateTargetCatalogSpells(
        WorldSession session,
        WorldDataCatalog worldData)
    {
        var combatGear = session.CombatGear ?? CombatGearSnapshot.Empty;
        if (combatGear.Items.Count == 0)
        {
            yield break;
        }

        var classCode = session.Character.Class;
        foreach (var item in combatGear.Items)
        {
            if (!IsLegacyCombatGearForClass(classCode, item.ItemID) ||
                item.Equip != 1 ||
                !worldData.CombatGearByGearTypeAndLevel.TryGetValue(((int)item.ItemID, (int)item.Level), out var sourceGear))
            {
                continue;
            }

            foreach (var skill in EnumerateLegacyCombatGearClassSkills(sourceGear))
            {
                yield return skill;
            }
        }
    }


    private static IEnumerable<PlayerSpellSnapshotEntry> EnumerateLegacyCombatGearOpenAttackSpells(uint itemId)
    {
        var spells = itemId switch
        {
            LegacyCombatGearDragonhowlItemId => LegacyCombatGearDragonhowlAttackSpells,
            LegacyCombatGearBloodlustItemId => LegacyCombatGearBloodlustAttackSpells,
            LegacyCombatGearRedcurseItemId => LegacyCombatGearRedcurseAttackSpells,
            LegacyCombatGearStoneCrackerItemId => LegacyCombatGearStoneCrackerAttackSpells,
            LegacyCombatGearColdMoonItemId => LegacyCombatGearColdMoonAttackSpells,
            LegacyCombatGearTharnCutterItemId => LegacyCombatGearTharnCutterAttackSpells,
            _ => [],
        };

        foreach (var spellId in spells)
        {
            yield return new PlayerSpellSnapshotEntry(
                spellId,
                Level: 0,
                Experience: 0,
                PreviousLevel: 0,
                SoulLevel: 0,
                UseSpellSoul: 0);
        }
    }


    private static IEnumerable<PlayerSpellSnapshotEntry> EnumerateLegacyCombatGearBlackJadeSpells(
        CombatGearSnapshotItem item,
        WorldDataCatalog worldData)
    {
        foreach (var blackJadeId in item.BlackJades)
        {
            if (blackJadeId == 0 ||
                blackJadeId > int.MaxValue ||
                !worldData.DaoqiDictTypesById.TryGetValue((int)blackJadeId, out var blackJade))
            {
                continue;
            }

            if (TryCreateLegacyCombatGearBlackJadeSpell(blackJade, 2, 3, out var firstSpell))
            {
                yield return firstSpell;
            }

            if (TryCreateLegacyCombatGearBlackJadeSpell(blackJade, 4, 5, out var secondSpell))
            {
                yield return secondSpell;
            }
        }
    }


    private static bool TryCreateLegacyCombatGearBlackJadeSpell(
        DaoqiDictType blackJade,
        int attrTypeFieldIndex,
        int attrValueFieldIndex,
        out PlayerSpellSnapshotEntry spell)
    {
        const int spellAttributeType = 24;

        spell = default!;
        if (!TryReadDaoqiField(blackJade, attrTypeFieldIndex, out var attrType) ||
            attrType != spellAttributeType ||
            !TryReadDaoqiField(blackJade, attrValueFieldIndex, out var attrValue) ||
            attrValue <= 0)
        {
            return false;
        }

        var spellId = attrValue / 1000;
        var spellLevel = attrValue % 100;
        if (spellId <= 0 ||
            spellId > ushort.MaxValue ||
            spellLevel > ushort.MaxValue)
        {
            return false;
        }

        spell = new PlayerSpellSnapshotEntry(
            checked((ushort)spellId),
            checked((ushort)spellLevel),
            Experience: 0,
            PreviousLevel: 0,
            SoulLevel: 0,
            UseSpellSoul: 0);
        return true;
    }


    private static IEnumerable<PlayerSpellSnapshotEntry> EnumerateLegacyCombatGearClassSkills(
        CombatGear sourceGear,
        long classCode)
    {
        var start = 0;
        var end = sourceGear.MagicTypeIds.Count;
        if (IsLegacyFireTaoistClass(classCode))
        {
            start = Math.Min(5, sourceGear.MagicTypeIds.Count);
        }
        else if (IsLegacyWaterTaoistClass(classCode))
        {
            end = Math.Min(5, sourceGear.MagicTypeIds.Count);
        }

        for (var index = start; index < end; index++)
        {
            var encodedSkill = sourceGear.MagicTypeIds[index];
            if (encodedSkill <= 0)
            {
                continue;
            }

            var spellId = encodedSkill / 1000;
            var spellLevel = encodedSkill % 100;
            if (spellId <= 0 ||
                spellId > ushort.MaxValue ||
                spellLevel > ushort.MaxValue)
            {
                continue;
            }

            yield return new PlayerSpellSnapshotEntry(
                checked((ushort)spellId),
                checked((ushort)spellLevel),
                Experience: 0,
                PreviousLevel: 0,
                SoulLevel: 0,
                UseSpellSoul: 0);
        }
    }

    private static IEnumerable<PlayerSpellSnapshotEntry> EnumerateLegacyCombatGearClassSkills(CombatGear sourceGear)
    {
        for (var index = 0; index < sourceGear.MagicTypeIds.Count; index++)
        {
            var encodedSkill = sourceGear.MagicTypeIds[index];
            if (encodedSkill <= 0)
            {
                continue;
            }

            var spellId = encodedSkill / 1000;
            var spellLevel = encodedSkill % 100;
            if (spellId <= 0 ||
                spellId > ushort.MaxValue ||
                spellLevel > ushort.MaxValue)
            {
                continue;
            }

            yield return new PlayerSpellSnapshotEntry(
                checked((ushort)spellId),
                checked((ushort)spellLevel),
                Experience: 0,
                PreviousLevel: 0,
                SoulLevel: 0,
                UseSpellSoul: 0);
        }
    }
}
