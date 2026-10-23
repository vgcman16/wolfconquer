using System.Globalization;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyDragonSkinPersistence
{
    private static bool TryPrepareCharacter(string path, WorldSession expected, WorldSession updated, Change change,
        out byte[] before, out byte[] after, out string reason)
    {
        before = []; after = [];
        if (!CharacterSnapshotFileBaseline.TryRead(path, Prefix, out var baseline, out reason)) return false;
        var fields = baseline!.Fields;
        // The real IniDocument loader trims the whole RHS. Compare that same
        // projection, while the journal still binds the exact original bytes.
        var skinFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (fields.TryGetValue(LegacyDragonSkinBook.Key, out var rawSkin))
            skinFields[LegacyDragonSkinBook.Key] = rawSkin.Trim();
        if (!fields.TryGetValue("UID", out var textUid) ||
            !long.TryParse(textUid, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid) || uid != expected.Character.Uid ||
            !LegacyDragonSkinBook.TryRead(skinFields, out var stored, out reason) ||
            !stored.OwnedSkins.SequenceEqual(change.BeforeBook.State.OwnedSkins))
        { reason = "dragonSkinCharacterBaselineStale"; return false; }
        // Select/Restore are current-CAS presentation, not entitlement writes.
        // Valid equipped-only divergence must not break Select -> Retrieve.
        if (!LegacyTradeEscrowClaims.TryRead(fields, out var escrow, out _) || escrow is not null)
        { reason = "dragonSkinStoredEscrowActiveOrInvalid"; return false; }
        var decoded = CharacterSnapshotFileEncoding.Decode(baseline.Bytes, out var encoding);
        if (!encoding.GetPreamble().Concat(encoding.GetBytes(decoded)).SequenceEqual(baseline.Bytes))
        { reason = "dragonSkinEncodingUnproven"; return false; }
        var parts = Regex.Split(decoded, "(\r\n|\r|\n)");
        var inCharacter = false;
        var changed = false;
        var insertAt = -1;
        for (var index = 0; index < parts.Length; index += 2)
        {
            var line = parts[index];
            var trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                if (inCharacter && insertAt < 0) insertAt = index;
                inCharacter = trimmed[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (!inCharacter || trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#')) continue;
            var equals = line.IndexOf('=');
            if (equals <= 0 || !line[..equals].Trim().Equals(LegacyDragonSkinBook.Key, StringComparison.OrdinalIgnoreCase)) continue;
            parts[index] = line[..(equals + 1)] + change.AfterBook.State.ToSnapshotValue();
            changed = true;
        }
        if (!changed)
        {
            var newline = parts.Where((_, index) => index % 2 != 0).FirstOrDefault(value => value.Length != 0) ?? Environment.NewLine;
            var addition = LegacyDragonSkinBook.Key + "=" + change.AfterBook.State.ToSnapshotValue() + newline;
            if (insertAt >= 0) parts[insertAt] = addition + parts[insertAt];
            else
            {
                var last = parts.Length - 1;
                parts[last] += (parts[last].Length != 0 ? newline : string.Empty) + addition;
            }
        }
        var projected = string.Concat(parts);
        if (!string.Equals(encoding.GetString(encoding.GetBytes(projected)), projected, StringComparison.Ordinal))
        { reason = "dragonSkinCandidateEncodingLoss"; return false; }
        after = encoding.GetPreamble().Concat(encoding.GetBytes(projected)).ToArray();
        if (after.Length > CharacterSnapshotFileBaseline.MaximumBytes)
        { after = []; reason = "dragonSkinCharacterTooLarge"; return false; }
        before = baseline.Bytes;
        reason = string.Empty;
        return true;
    }

    private static bool TryReadItems(string path, out byte[]? bytes, out string reason)
    {
        bytes = null;
        reason = string.Empty;
        if (Directory.Exists(path)) { reason = "dragonSkinItemPathNotFile"; return false; }
        for (string? current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                { reason = "dragonSkinReparsePathRejected"; return false; }
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
        if (CharacterSnapshotFileBaseline.TryReadBytes(path, Prefix, out var image, out reason))
        { bytes = image; return true; }
        if (reason == Prefix + "SnapshotMissing") { reason = string.Empty; return true; }
        return false;
    }
}
