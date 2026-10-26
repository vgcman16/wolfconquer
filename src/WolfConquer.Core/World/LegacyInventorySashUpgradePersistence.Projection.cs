using System.Globalization;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyInventorySashUpgradePersistence
{
    private static bool TryProject(string text, CharacterSnapshotFileBaseline.Snapshot baseline,
        long uid, long cp, ulong count, out byte[] image, out string reason)
    {
        image = [];
        reason = Prefix + "ProjectionInvalid";
        var parts = Regex.Split(text, "(\r\n|\r|\n)");
        var inCharacter = false;
        var changedCp = false;
        var changedCount = false;
        var insertIndex = parts.Length;
        for (var index = 0; index < parts.Length; index += 2)
        {
            var line = parts[index];
            var trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                if (inCharacter) insertIndex = index;
                inCharacter = trimmed[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (!inCharacter || trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#')) continue;
            var equals = line.IndexOf('=');
            if (equals <= 0) continue;
            var key = line[..equals].Trim();
            if (key.Equals("ConquerPoints", StringComparison.OrdinalIgnoreCase))
            {
                parts[index] = line[..(equals + 1)] + cp.ToString(CultureInfo.InvariantCulture);
                changedCp = true;
            }
            else if (key.Equals("InventorySashCount", StringComparison.OrdinalIgnoreCase))
            {
                parts[index] = line[..(equals + 1)] + count.ToString(CultureInfo.InvariantCulture);
                changedCount = true;
            }
        }
        if (!changedCp) return false;
        if (!changedCount)
        {
            // Missing source count is legitimately zero. Insert only its new row;
            // retain every pre-existing row, delimiter, encoding and terminal newline.
            var newline = parts.Where((_, index) => (index & 1) != 0).FirstOrDefault() ?? Environment.NewLine;
            var row = "InventorySashCount=" + count.ToString(CultureInfo.InvariantCulture);
            if (insertIndex < parts.Length) parts[insertIndex] = row + newline + parts[insertIndex];
            else parts[^1] += parts[^1].Length == 0 ? row + newline : newline + row;
        }
        var projected = string.Concat(parts);
        var encoding = baseline.Encoding;
        if ((long)encoding.GetByteCount(projected) + encoding.GetPreamble().Length > CharacterSnapshotFileBaseline.MaximumBytes ||
            !string.Equals(encoding.GetString(encoding.GetBytes(projected)), projected, StringComparison.Ordinal)) return false;
        var parsed = CharacterSnapshotParser.ParseText(projected);
        if (parsed.Uid != uid || parsed.ConquerPoints != cp || !TryReadCount(parsed.Attributes, out var parsedCount) ||
            parsedCount != count) return false;
        image = encoding.GetPreamble().Concat(encoding.GetBytes(projected)).ToArray();
        reason = string.Empty;
        return true;
    }
}
