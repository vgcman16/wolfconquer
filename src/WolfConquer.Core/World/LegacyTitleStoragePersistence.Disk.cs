using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTitleStoragePersistence
{
    private static bool TryReadState(string path, long uid, IReadOnlyDictionary<string, string> wanted,
        out CharacterSnapshotFileBaseline.Snapshot? snapshot, out string reason)
    {
        if (!CharacterSnapshotFileBaseline.TryRead(path, Prefix, out snapshot, out reason)) return false;
        var fields = snapshot!.Fields;
        if (!fields.TryGetValue("UID", out var uidText) ||
            !long.TryParse(uidText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var storedUid) || storedUid != uid)
        { reason = Prefix + "UidMismatch"; return false; }
        foreach (var key in TitleKeys)
        {
            var present = fields.TryGetValue(key, out var actual);
            if (present != wanted.TryGetValue(key, out var expected) ||
                (present && !string.Equals(actual, expected, StringComparison.Ordinal)))
            { reason = Prefix + "StateStale"; return false; }
        }
        return true;
    }

    private static bool TryBoundCandidate(CharacterSnapshotFileBaseline.Snapshot before,
        IReadOnlyDictionary<string, string> values, out string reason)
    {
        reason = Prefix + "CandidateSnapshotTooLarge";
        // The actual transformer mutates input elements: work only on a private clone.
        if (!CharacterSnapshotSectionLineTransformer.TryApply((string[])before.Lines.Clone(), "Character",
            values, createIfMissing: false, out var lines))
        { reason = Prefix + "CharacterSectionMissing"; return false; }
        long bytes = before.Encoding.GetPreamble().Length;
        var newlineBytes = before.Encoding.GetByteCount(Environment.NewLine);
        foreach (var line in lines)
        {
            bytes += (long)before.Encoding.GetByteCount(line) + newlineBytes;
            if (bytes > CharacterSnapshotFileBaseline.MaximumBytes) return false;
            if (!string.Equals(before.Encoding.GetString(before.Encoding.GetBytes(line)), line, StringComparison.Ordinal))
            { reason = Prefix + "CandidateEncodingLoss"; return false; }
        }
        reason = string.Empty;
        return true;
    }
}
