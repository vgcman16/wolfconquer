using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private static readonly string[] LegacyClassPkWarWinnerSuffixes =
    [
        "WinnerPlayerUID",
        "WinnerPlayerUid",
        "WinnerUID",
        "WinnerUid",
        "Winner",
    ];

    private static readonly string[] LegacyClassPkWarLastFlagSuffixes =
    [
        "LastFlag",
        "Aura",
        "TopFlag",
    ];

    private static CharacterSnapshot WithLegacyClassPkWarStartupTitle(CharacterSnapshot snapshot)
    {
        return TryReadLegacyClassPkWarStartupTitleFlag(snapshot, out var statusFlag)
            ? LegacyClassPkWarTitleFlags.WithStatusFlag(snapshot, statusFlag)
            : snapshot;
    }

    private static bool TryReadLegacyClassPkWarStartupTitleFlag(
        CharacterSnapshot snapshot,
        out int statusFlag)
    {
        foreach (var (key, value) in snapshot.Attributes)
        {
            if (!TryGetLegacyClassPkWarWinnerPrefix(key, out var prefix) ||
                !ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var winnerUid) ||
                snapshot.Uid < 0 ||
                winnerUid != checked((ulong)snapshot.Uid))
            {
                continue;
            }

            foreach (var suffix in LegacyClassPkWarLastFlagSuffixes)
            {
                if (snapshot.Attributes.TryGetValue(prefix + suffix, out var raw) &&
                    LegacyClassPkWarTitleFlags.TryReadStatusFlag(raw, out statusFlag))
                {
                    return true;
                }
            }
        }

        statusFlag = -1;
        return false;
    }

    private static bool TryGetLegacyClassPkWarWinnerPrefix(string key, out string prefix)
    {
        prefix = "";
        if (!key.Contains("ClassPkWar", StringComparison.OrdinalIgnoreCase) &&
            !key.Contains("ClassPKWar", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var suffix in LegacyClassPkWarWinnerSuffixes)
        {
            if (key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                prefix = key[..^suffix.Length];
                return true;
            }
        }

        return false;
    }
}
