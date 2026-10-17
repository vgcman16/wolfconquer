using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static class LegacyClassPkWarTitleFlags
{
    public const int TopWarriorFlag = 38;
    public const int TopTrojanFlag = 39;
    public const int TopArcherFlag = 40;
    public const int TopWaterTaoistFlag = 41;
    public const int TopFireTaoistFlag = 42;
    public const int TopNinjaFlag = 43;
    public const int TopMonkFlag = 114;
    public const int TopPirateFlag = 122;
    public const int TopDragonLeeFlag = 154;
    public const int TopWindWalkerFlag = 203;
    public const int TopThunderstrikerFlag = 232;

    public static string GetAuraName(LegacyClassPkWarTournamentType type)
    {
        return type switch
        {
            LegacyClassPkWarTournamentType.Trojan => "TopTrojan",
            LegacyClassPkWarTournamentType.Warrior => "TopWarrior",
            LegacyClassPkWarTournamentType.Archer => "TopArcher",
            LegacyClassPkWarTournamentType.Ninja => "TopNinja",
            LegacyClassPkWarTournamentType.Monk => "TopMonk",
            LegacyClassPkWarTournamentType.Pirate => "TopPirate",
            LegacyClassPkWarTournamentType.Fire => "TopFireTaoist",
            LegacyClassPkWarTournamentType.Water => "TopWaterTaoist",
            LegacyClassPkWarTournamentType.LongLee => "TopDragonLee",
            LegacyClassPkWarTournamentType.WindWalker => "TopWindWalker",
            LegacyClassPkWarTournamentType.Thunderstriker => "TopThunderstriker",
            _ => "Normal",
        };
    }

    public static int GetStatusFlag(LegacyClassPkWarTournamentType type)
    {
        return type switch
        {
            LegacyClassPkWarTournamentType.Trojan => TopTrojanFlag,
            LegacyClassPkWarTournamentType.Warrior => TopWarriorFlag,
            LegacyClassPkWarTournamentType.Archer => TopArcherFlag,
            LegacyClassPkWarTournamentType.Ninja => TopNinjaFlag,
            LegacyClassPkWarTournamentType.Monk => TopMonkFlag,
            LegacyClassPkWarTournamentType.Pirate => TopPirateFlag,
            LegacyClassPkWarTournamentType.Fire => TopFireTaoistFlag,
            LegacyClassPkWarTournamentType.Water => TopWaterTaoistFlag,
            LegacyClassPkWarTournamentType.LongLee => TopDragonLeeFlag,
            LegacyClassPkWarTournamentType.WindWalker => TopWindWalkerFlag,
            LegacyClassPkWarTournamentType.Thunderstriker => TopThunderstrikerFlag,
            _ => -1,
        };
    }

    public static bool TryReadStatusFlag(string raw, out int statusFlag)
    {
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out statusFlag))
        {
            return statusFlag >= 0;
        }

        foreach (var type in Enum.GetValues<LegacyClassPkWarTournamentType>())
        {
            if (type == LegacyClassPkWarTournamentType.Count)
            {
                continue;
            }

            if (raw.Equals(GetAuraName(type), StringComparison.OrdinalIgnoreCase))
            {
                statusFlag = GetStatusFlag(type);
                return statusFlag >= 0;
            }
        }

        statusFlag = -1;
        return false;
    }

    public static CharacterSnapshot WithStatusFlag(CharacterSnapshot character, int flag)
    {
        character = LegacyStatusFlagDeathRegistration.PrepareAdd(character, flag);
        if (flag < 0)
        {
            return character;
        }

        var key = "StatusFlags";
        var raw = "";
        if (TryGetStatusFlagsAttribute(character.Attributes, out var existingKey, out var existingRaw))
        {
            key = existingKey;
            raw = existingRaw;
        }

        var wordIndex = flag / 32;
        var bitIndex = flag % 32;
        var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var wordCount = Math.Max(Math.Max(10, parts.Length), wordIndex + 1);
        var words = new ulong[wordCount];
        for (var index = 0; index < Math.Min(parts.Length, words.Length); index++)
        {
            ulong.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out words[index]);
        }

        words[wordIndex] |= 1UL << bitIndex;
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            [key] = string.Join('/', words) + "/",
        };
        return character with { Attributes = attributes };
    }

    private static bool TryGetStatusFlagsAttribute(
        IReadOnlyDictionary<string, string> attributes,
        out string key,
        out string raw)
    {
        if (attributes.TryGetValue("StatusFlags", out var statusFlags))
        {
            key = "StatusFlags";
            raw = statusFlags ?? "";
            return true;
        }

        if (attributes.TryGetValue("BitVector", out var bitVector))
        {
            key = "BitVector";
            raw = bitVector ?? "";
            return true;
        }

        key = "";
        raw = "";
        return false;
    }
}
