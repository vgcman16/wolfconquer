using System.Globalization;

namespace WolfConquer.Core.Data;

public static class LegacyMonsterFamilyLoadRules
{
    private const int LegacyHighAttackHalvingThreshold = 3000;

    public static Monster ApplySourceLoadRules(Monster monster)
    {
        ArgumentNullException.ThrowIfNull(monster);

        var attributes = new Dictionary<string, string>(monster.Attributes, StringComparer.OrdinalIgnoreCase);
        if (!TryReadInt32(attributes, "attack_max", out var maxAttack) ||
            maxAttack <= LegacyHighAttackHalvingThreshold)
        {
            return monster with { Attributes = attributes };
        }

        var boss = TryReadInt32(attributes, "Boss", out var parsedBoss)
            ? parsedBoss
            : 0;
        if (boss != 0)
        {
            return monster with { Attributes = attributes };
        }

        attributes["attack_max"] = (maxAttack / 2).ToString(CultureInfo.InvariantCulture);
        if (TryReadInt32(attributes, "attack_min", out var minAttack))
        {
            attributes["attack_min"] = (minAttack / 2).ToString(CultureInfo.InvariantCulture);
        }

        return monster with { Attributes = attributes };
    }

    private static bool TryReadInt32(
        IReadOnlyDictionary<string, string> attributes,
        string key,
        out int value)
    {
        value = 0;
        return attributes.TryGetValue(key, out var raw) &&
            int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
