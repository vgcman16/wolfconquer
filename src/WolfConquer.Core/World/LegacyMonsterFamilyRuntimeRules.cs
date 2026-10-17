using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static class LegacyMonsterFamilyRuntimeRules
{
    public const uint ExtraBossAttackRange = 12;

    private const int LegacyGoldBoxFamilyId = 20_211;
    private const int LegacyFirstDarkCrystalFamilyId = 40_121;
    private const int LegacyLastDarkCrystalFamilyId = 40_124;

    public static uint ResolveAttackRange(Monster monsterFamily)
    {
        ArgumentNullException.ThrowIfNull(monsterFamily);

        return UsesExtraBossCopyRules(monsterFamily)
            ? ExtraBossAttackRange
            : ReadUInt32(monsterFamily.Attributes, "attack_range", "AttackRange");
    }

    public static bool UsesExtraBossCopyRules(Monster monsterFamily)
    {
        ArgumentNullException.ThrowIfNull(monsterFamily);

        var boss = ReadUInt32(monsterFamily.Attributes, "Boss", "boss");
        var maxHealth = ReadUInt32(monsterFamily.Attributes, "life", "MaxHealth", "max_health");
        return boss > 0 && maxHealth is > 100_000 and < 7_000_000;
    }

    public static bool IsExcludedFromGenericAggro(Monster monsterFamily)
    {
        ArgumentNullException.ThrowIfNull(monsterFamily);

        return monsterFamily.Id == LegacyGoldBoxFamilyId ||
            monsterFamily.Id is >= LegacyFirstDarkCrystalFamilyId and <= LegacyLastDarkCrystalFamilyId;
    }

    private static uint ReadUInt32(IReadOnlyDictionary<string, string> attributes, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var value) &&
                uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }
}
