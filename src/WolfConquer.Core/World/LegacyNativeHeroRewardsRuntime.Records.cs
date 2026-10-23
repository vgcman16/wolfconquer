using System.Globalization;
using System.Text;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHeroRewardsRuntime
{
    public void Load(string line)
    {
        if (!TryLoad(line)) throw new InvalidDataException("Hero goal record is malformed or its current owner/book is unavailable.");
    }

    internal bool TryLoad(string? line)
    {
        try { return TryParseGoalRecords(line, out var incoming) && TryWriteGoals(incoming); }
        catch (Exception) { return false; }
    }

    private static bool TryParseGoalRecords(string? line, out GoalRecord[] rows)
    {
        rows = [];
        if (line is null || line.Length > MaxGoalRecordLength) return false;
        if (line.Length == 0) return true; // Genuine absent saved field: no incoming goals.
        var tokens = line.Split('/');
        if (!int.TryParse(tokens[0], NumberStyles.None, CultureInfo.InvariantCulture, out int count) ||
            count < 0 || count > MaxGoalRecords) return false;
        int required = 1 + (count * 2);
        if (tokens.Length != required && !(tokens.Length == required + 1 && tokens[^1].Length == 0)) return false;
        var parsed = new GoalRecord[count];
        for (int index = 0; index < count; index++)
        {
            if (!uint.TryParse(tokens[(index * 2) + 1], NumberStyles.None, CultureInfo.InvariantCulture, out uint uid) || uid == 0 ||
                !byte.TryParse(tokens[(index * 2) + 2], NumberStyles.None, CultureInfo.InvariantCulture, out byte claim)) return false;
            parsed[index] = new(uid, claim);
        }
        rows = parsed;
        return true;
    }

    public override string ToString()
    {
        if (!TryReadGoalRows(out var rows)) throw new InvalidDataException("Hero goal book cannot be serialized from unavailable ownership/state.");
        var text = new StringBuilder().Append(rows.Length.ToString(CultureInfo.InvariantCulture)).Append('/');
        foreach (var row in rows)
            text.Append(row.Uid.ToString(CultureInfo.InvariantCulture)).Append('/').Append(row.Claim.ToString(CultureInfo.InvariantCulture)).Append('/');
        return text.ToString();
    }
}
