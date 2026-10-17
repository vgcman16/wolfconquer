using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed record LegacySubClassHitpointsContribution(
    uint WranglerHitpoints,
    uint Total,
    IReadOnlyList<string> MissingInputs);

public sealed record LegacySubClassState(
    ushort StudyPoints,
    byte ActiveSubClass,
    IReadOnlyList<LegacySubClassEntry> Entries);

public sealed record LegacySubClassEntry(
    byte Id,
    byte Level,
    byte Phrase);

public static class LegacySubClassHitpointsCalculator
{
    private const string SnapshotSubClassKey = "SubProfInfo";
    private const byte WranglerSubClassId = 9;

    public static LegacySubClassHitpointsContribution Calculate(
        CharacterSnapshot snapshot,
        WorldDataCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.Attributes.TryGetValue(SnapshotSubClassKey, out var rawState) ||
            string.IsNullOrWhiteSpace(rawState))
        {
            return new LegacySubClassHitpointsContribution(
                0,
                0,
                [$"CharacterSnapshot.{SnapshotSubClassKey}"]);
        }

        if (!TryParseLegacyState(rawState, out var state))
        {
            return new LegacySubClassHitpointsContribution(
                0,
                0,
                [$"parseable CharacterSnapshot.{SnapshotSubClassKey}"]);
        }

        return Calculate(state, catalog?.SubProfessions);
    }

    public static LegacySubClassHitpointsContribution Calculate(
        LegacySubClassState state,
        IReadOnlyList<SubProfessionEntry>? subProfessions)
    {
        ArgumentNullException.ThrowIfNull(state);

        var wrangler = state.Entries.FirstOrDefault(entry => entry.Id == WranglerSubClassId);
        if (wrangler is null)
        {
            return new LegacySubClassHitpointsContribution(
                0,
                0,
                [$"{SnapshotSubClassKey} Wrangler subclass entry"]);
        }

        if (subProfessions is null)
        {
            return new LegacySubClassHitpointsContribution(
                0,
                0,
                ["WorldDataCatalog.SubProfessions"]);
        }

        var sectionId = GetWranglerSectionId(wrangler.Phrase);
        var entry = subProfessions.FirstOrDefault(candidate => candidate.Id == sectionId);
        if (entry is null)
        {
            return new LegacySubClassHitpointsContribution(
                0,
                0,
                [$"SubProfessionInfo.ini section [{sectionId}]"]);
        }

        if (!TryReadMaxHitpoints(entry.Intro, out var hitpoints))
        {
            return new LegacySubClassHitpointsContribution(
                0,
                0,
                [$"parseable SubProfessionInfo.ini section [{sectionId}] intro"]);
        }

        return new LegacySubClassHitpointsContribution(hitpoints, hitpoints, []);
    }

    public static bool TryParseLegacyState(string rawState, out LegacySubClassState state)
    {
        state = new LegacySubClassState(0, 0, []);
        if (string.IsNullOrWhiteSpace(rawState) ||
            !rawState.Contains('#', StringComparison.Ordinal))
        {
            return false;
        }

        var fields = rawState.Split('#', StringSplitOptions.None);
        if (!TryReadUInt16(fields, 0, out var studyPoints) ||
            !TryReadByte(fields, 1, out var activeSubClass) ||
            !TryReadInt32(fields, 2, out var count) ||
            count < 0)
        {
            return false;
        }

        var requiredFields = checked(3 + (count * 3));
        if (fields.Length < requiredFields)
        {
            return false;
        }

        var entries = new List<LegacySubClassEntry>(count);
        var offset = 3;
        for (var index = 0; index < count; index++)
        {
            if (!TryReadByte(fields, offset++, out var id) ||
                !TryReadByte(fields, offset++, out var level) ||
                !TryReadByte(fields, offset++, out var phrase))
            {
                return false;
            }

            entries.Add(new LegacySubClassEntry(id, level, phrase));
        }

        state = new LegacySubClassState(studyPoints, activeSubClass, entries);
        return true;
    }

    private static int GetWranglerSectionId(byte phrase)
    {
        return phrase == 0
            ? WranglerSubClassId
            : checked((WranglerSubClassId * 10) + phrase);
    }

    private static bool TryReadMaxHitpoints(string intro, out uint hitpoints)
    {
        hitpoints = 0;
        var plusIndex = intro.IndexOf('+', StringComparison.Ordinal);
        if (plusIndex < 0 || plusIndex == intro.Length - 1)
        {
            return false;
        }

        var value = intro[(plusIndex + 1)..];
        var percentIndex = value.IndexOf('%', StringComparison.Ordinal);
        if (percentIndex >= 0)
        {
            value = value[..percentIndex];
        }

        return uint.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out hitpoints);
    }

    private static bool TryReadByte(IReadOnlyList<string> fields, int index, out byte value)
    {
        value = 0;
        return fields.Count > index &&
            byte.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadUInt16(IReadOnlyList<string> fields, int index, out ushort value)
    {
        value = 0;
        return fields.Count > index &&
            ushort.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadInt32(IReadOnlyList<string> fields, int index, out int value)
    {
        value = 0;
        return fields.Count > index &&
            int.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
