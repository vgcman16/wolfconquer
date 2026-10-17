using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static class LegacyMonsterDamageRules
{
    private const uint LegacyOneDamageMonsterFamilyId = 20_211;
    private const uint LegacyPhysicalOnMonsterFloorDamage = 2;
    private const ushort LegacyNonLethalPhysicalSpellId1 = 16_540;
    private const ushort LegacyNonLethalPhysicalSpellId2 = 16_550;

    public static bool TakesOnePlayerDamage(Monster monsterFamily)
    {
        ArgumentNullException.ThrowIfNull(monsterFamily);

        return IsLegacyOneDamageMonster(checked((uint)monsterFamily.Id), monsterFamily.Name);
    }

    public static bool TakesOnePlayerDamage(LegacyMonsterInstance monster, Monster monsterFamily)
    {
        ArgumentNullException.ThrowIfNull(monster);
        ArgumentNullException.ThrowIfNull(monsterFamily);

        return IsLegacyOneDamageMonster(monster.MonsterId, monster.Name) ||
            TakesOnePlayerDamage(monsterFamily);
    }

    public static bool TryGetLegacyPhysicalOnMonsterFloorDamage(
        LegacyMonsterInstance monster,
        out uint damage)
    {
        ArgumentNullException.ThrowIfNull(monster);

        if (monster.IsFloor)
        {
            damage = LegacyPhysicalOnMonsterFloorDamage;
            return true;
        }

        damage = 0;
        return false;
    }

    public static bool IsLegacyGuardFamily(Monster monsterFamily)
    {
        ArgumentNullException.ThrowIfNull(monsterFamily);

        return IsLegacyGuardName(monsterFamily.Name);
    }

    public static bool IsLegacyGuardFamily(LegacyMonsterInstance monster, Monster monsterFamily)
    {
        ArgumentNullException.ThrowIfNull(monster);
        ArgumentNullException.ThrowIfNull(monsterFamily);

        return IsLegacyGuardName(monster.Name) ||
            IsLegacyGuardFamily(monsterFamily);
    }

    public static uint ApplyLegacyPhysicalSpellNonLethalClamp(
        ushort spellId,
        uint currentHitpoints,
        uint damage)
    {
        if (spellId is not (LegacyNonLethalPhysicalSpellId1 or LegacyNonLethalPhysicalSpellId2))
        {
            return damage;
        }

        return currentHitpoints <= damage + 1UL ? 0 : damage;
    }

    private static bool IsLegacyOneDamageMonster(uint monsterId, string name)
    {
        return monsterId == LegacyOneDamageMonsterFamilyId ||
            string.Equals(name, "AncientDragon", StringComparison.Ordinal) ||
            string.Equals(name, "WarDevil", StringComparison.Ordinal) ||
            name.Contains("Sprite", StringComparison.Ordinal);
    }

    private static bool IsLegacyGuardName(string name)
    {
        return (name.Contains("Guard", StringComparison.Ordinal) &&
                !name.Contains("Chaos", StringComparison.Ordinal)) ||
            name.Contains("CitySoldier", StringComparison.Ordinal);
    }
}
