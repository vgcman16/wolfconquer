namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryConsumeLegacyWeaponProcSpellGate(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        out WorldSession updatedSession)
    {
        updatedSession = session;
        if (!IsLegacyWeaponProcSpell(spell))
        {
            return true;
        }

        if (session.LegacyRandomSpell == 0 &&
            LegacyMagicSortCapabilities.IsSupportedPerimeterSpecialRuntimeSpell(spell.Sort, spell.Id))
        {
            return true;
        }

        if (session.LegacyRandomSpell != spell.Id)
        {
            return false;
        }

        updatedSession = session with { LegacyRandomSpell = 0 };
        return true;
    }

    private static bool IsLegacyWeaponProcSpell(LegacyMagicRuntimeSpell spell)
    {
        return IsLegacyWeaponProcSpellId(spell.Id) &&
            HasLegacyWeaponProcSpellWeaponTypes(spell.WeaponType);
    }

    private static bool HasLegacyWeaponProcSpellWeaponTypes(uint weaponType)
    {
        if (weaponType is 0 or 50_000)
        {
            return false;
        }

        var primaryWeaponType = (ushort)(weaponType % 1_000);
        var secondaryWeaponType = (ushort)(weaponType / 1_000 % 1_000);
        return primaryWeaponType != 0 || secondaryWeaponType != 0;
    }

    private static bool IsLegacyWeaponProcSpellId(ushort spellId)
    {
        return spellId is
            1_250 or 1_260 or 1_290 or 1_300 or
            5_010 or 5_020 or 5_030 or 5_040 or 5_050 or
            7_000 or 7_010 or 7_020 or 7_030 or 7_040 or
            10_490 or 11_140 or 11_990 or
            12_110 or 12_210 or 12_220 or 12_230 or 12_240 or
            12_580 or 12_590 or 12_600 or
            12_740 or 12_750 or 12_760 or
            13_040 or 13_050 or 13_060 or
            14_570 or 14_580 or
            15_670 or 15_680 or 15_690;
    }
}
