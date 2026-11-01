using System.Globalization;

namespace WolfConquer.Core.World;

// DATA validation for the actual proc's captured policy view. No environment,
// current holder, XP amount or publication capability is supplied by this type.
internal static class LegacyDragonhowlExperienceRateInputs
{
    internal static void RequireSelected(IReadOnlyDictionary<string, string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        RequireFirstPresent(fields, ["UserExpRate", "ServerExperienceRate", "ExperienceRate", "MonsterKillExperienceRate"]);
        RequireFirstPresent(fields, ["MaximumMonsterKillExperienceRate", "MaximumServerExperienceRate"]);
        RequireFirstPresent(fields, ["RateExp", "DoubleExperienceRate", "DExpRate"]);
    }

    private static void RequireFirstPresent(IReadOnlyDictionary<string, string> fields, string[] precedence)
    {
        foreach (string name in precedence)
        {
            string? raw = null; int count = 0;
            foreach (var pair in fields)
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                { raw = pair.Value; count++; }
            if (count == 0) continue;
            if (count != 1 || !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var rate) ||
                !double.IsFinite(rate) || rate < 0)
                throw new InvalidDataException("ownedDragonhowlSelectedExperienceRateInvalid:" + name);
            return; // Existing valid distinct-name precedence, not alias coalescing.
        }
    }
}
