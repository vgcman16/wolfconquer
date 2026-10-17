using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static class LegacyMonsterMagicRetaliation
{
    public static MsgAttackEffect ResolveTailedBeastEffect(
        IReadOnlyDictionary<string, string> characterAttributes,
        IReadOnlyDictionary<int, BeastAttribute>? beastAttributesByLevel = null,
        uint? roll = null)
    {
        ArgumentNullException.ThrowIfNull(characterAttributes);

        if (!ReadBool(characterAttributes, "TailedBeastActivated", "BeastActivated", "BeastsActivated"))
        {
            return MsgAttackEffect.None;
        }

        var level = ResolveBeastLevel(characterAttributes, beastAttributesByLevel);
        var rate = level / 5;
        if (rate == 0)
        {
            return MsgAttackEffect.None;
        }

        var selectedRoll = roll ?? checked((uint)Random.Shared.Next(0, 100));
        return selectedRoll % 100 < rate ? MsgAttackEffect.TailedBeast : MsgAttackEffect.None;
    }

    private static uint ResolveBeastLevel(
        IReadOnlyDictionary<string, string> attributes,
        IReadOnlyDictionary<int, BeastAttribute>? beastAttributesByLevel)
    {
        if (TryReadUInt32(attributes, out var level, "TailedBeastLevel", "BeastLevel", "BeastsLevel"))
        {
            return level;
        }

        if (beastAttributesByLevel is null ||
            beastAttributesByLevel.Count == 0 ||
            !TryReadUInt32(attributes, out var totalPoints, "TailedBeastTotalPoints"))
        {
            return 1;
        }

        var maxLevel = beastAttributesByLevel.Keys.Max();
        var resolvedLevel = 1;
        var points = totalPoints;
        foreach (var attribute in beastAttributesByLevel.Values.OrderBy(attribute => attribute.Level))
        {
            var requiredPoints = checked((uint)Math.Max(0, attribute.Parameters[0]));
            if (points < requiredPoints)
            {
                break;
            }

            points -= requiredPoints;
            resolvedLevel = Math.Min(resolvedLevel + 1, maxLevel);
        }

        return checked((uint)resolvedLevel);
    }

    private static bool ReadBool(IReadOnlyDictionary<string, string> attributes, params string[] names)
    {
        foreach (var name in names)
        {
            if (!attributes.TryGetValue(name, out var raw) ||
                raw.Length == 0)
            {
                continue;
            }

            if (bool.TryParse(raw, out var parsed))
            {
                return parsed;
            }

            if (ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
            {
                return numeric != 0;
            }
        }

        return false;
    }

    private static bool TryReadUInt32(
        IReadOnlyDictionary<string, string> attributes,
        out uint value,
        params string[] names)
    {
        foreach (var name in names)
        {
            if (attributes.TryGetValue(name, out var raw) &&
                uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }
}
