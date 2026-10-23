using System.Globalization;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyDeityChestPersistence
{
    private static bool TryReadCharacter(string path, WorldSession expected,
        out CharacterSnapshotFileBaseline.Snapshot character, out string reason)
    {
        character = null!;
        if (!CharacterSnapshotFileBaseline.TryRead(path, Prefix, out var baseline, out reason)) return false;
        var text = CharacterSnapshotFileEncoding.Decode(baseline!.Bytes, out var encoding);
        if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(baseline.Bytes))
        { reason = "deityChestEncodingUnproven"; return false; }
        // Exercise the actual required scalar parser, not a UID-only invented
        // account owner. IniDocument's outer-RHS trimming remains unchanged.
        var parsed = CharacterSnapshotParser.ParseText(text, expected.Character.SourceFile);
        if (parsed.Uid != expected.Character.Uid || parsed.Money != expected.Character.Money ||
            parsed.ConquerPoints != expected.Character.ConquerPoints || parsed.ConquerPoints is < 0 or > uint.MaxValue ||
            !LegacyTradeEscrowClaims.TryRead(parsed.Attributes, out var escrow, out _) || escrow is not null)
        { reason = "deityChestCharacterBaselineStaleOrEscrow"; return false; }
        character = baseline;
        reason = string.Empty;
        return true;
    }

    private static bool TryProjectCharacter(CharacterSnapshotFileBaseline.Snapshot baseline, WorldSession updated,
        bool changesCp, out byte[] bytes, out string reason)
    {
        bytes = baseline.Bytes;
        reason = string.Empty;
        if (!changesCp) return true;
        var decoded = CharacterSnapshotFileEncoding.Decode(baseline.Bytes, out var encoding);
        var parts = Regex.Split(decoded, "(\r\n|\r|\n)");
        var inCharacter = false;
        var changed = false;
        for (var index = 0; index < parts.Length; index += 2)
        {
            var line = parts[index];
            var trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            { inCharacter = trimmed[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase); continue; }
            if (!inCharacter || trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#')) continue;
            var equals = line.IndexOf('=');
            if (equals <= 0 || !line[..equals].Trim().Equals("ConquerPoints", StringComparison.OrdinalIgnoreCase)) continue;
            parts[index] = line[..(equals + 1)] + updated.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture);
            changed = true;
        }
        if (!changed) { reason = "deityChestConquerPointsFieldMissing"; return false; }
        var projected = string.Concat(parts);
        if (!string.Equals(encoding.GetString(encoding.GetBytes(projected)), projected, StringComparison.Ordinal))
        { reason = "deityChestCandidateEncodingLoss"; return false; }
        bytes = encoding.GetPreamble().Concat(encoding.GetBytes(projected)).ToArray();
        if (bytes.Length > CharacterSnapshotFileBaseline.MaximumBytes)
        { bytes = []; reason = "deityChestCharacterTooLarge"; return false; }
        var parsed = CharacterSnapshotParser.ParseText(projected);
        if (parsed.Uid != updated.Character.Uid || parsed.Money != updated.Character.Money ||
            parsed.ConquerPoints != updated.Character.ConquerPoints)
        { bytes = []; reason = "deityChestCharacterProjectionInvalid"; return false; }
        return true;
    }

    private static bool TryPaths(WorldSession expected, string? itemsRoot, out Paths paths, out string reason)
    {
        paths = null!;
        reason = "deityChestStorageBindingInvalid";
        if (expected.AccountId is <= 0 or > uint.MaxValue || expected.Character.Uid is <= 0 or > uint.MaxValue ||
            string.IsNullOrWhiteSpace(expected.UsersRoot) || string.IsNullOrWhiteSpace(itemsRoot) ||
            !Path.IsPathFullyQualified(expected.UsersRoot) || !Path.IsPathFullyQualified(itemsRoot)) return false;
        var users = Path.GetFullPath(expected.UsersRoot);
        var items = Path.GetFullPath(itemsRoot);
        if (!Directory.Exists(users) || !Directory.Exists(items)) return false;
        var name = expected.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini";
        var character = Path.Combine(users, name);
        var source = expected.Character.SourceFile;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(source, name, comparison) && (string.IsNullOrWhiteSpace(source) || !Path.IsPathFullyQualified(source) ||
            !string.Equals(Path.GetFullPath(source), character,
                comparison))) return false;
        paths = new(users, items, character, Path.Combine(items, expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin"));
        return true;
    }
}
