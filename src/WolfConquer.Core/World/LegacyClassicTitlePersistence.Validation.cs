using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyClassicTitlePersistence
{
    private static bool TryPrepare(WorldSession expected, WorldSession updated, out string path,
        out Dictionary<string, string> candidate, out string reason)
    {
        path = string.Empty;
        candidate = new(StringComparer.OrdinalIgnoreCase);
        reason = Prefix + "BindingInvalid";
        if (!HasClassicTitleStateOwners(expected, updated) || !HasClassicTitleAccountBinding(expected) ||
            !TryResolveClassicTitleStorageRoot(expected, updated, out var root)) return false;
        // TryCommitSession skips autosave. The complete updated value may change
        // MyTitle only; a malicious direct caller cannot smuggle any other state.
        if (!HasOnlyClassicTitleSelectionChange(expected, updated))
        { reason = Prefix + "UnrelatedStateChanged"; return false; }
        if (!TryReadCanonicalClassicTitleCandidate(expected.Character.Attributes,
            updated.Character.Attributes, out var text))
        { reason = Prefix + "StateInvalid"; return false; }
        candidate.Add(LegacyClassicTitleState.SelectedKey, text);
        path = Path.Combine(root, expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".ini");
        return CharacterSnapshotFileBaseline.TryValidateSafePath(path, Prefix, out reason);
    }

    private static bool TryReadState(string path, long uid, IReadOnlyDictionary<string, string> wanted,
        bool sessionProjection, out CharacterSnapshotFileBaseline.Snapshot? snapshot, out string reason)
    {
        if (!CharacterSnapshotFileBaseline.TryRead(path, Prefix, out snapshot, out reason)) return false;
        var fields = snapshot!.Fields;
        if (!fields.TryGetValue("UID", out var uidText) ||
            !long.TryParse(uidText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var storedUid) || storedUid != uid)
        { reason = Prefix + "UidMismatch"; return false; }
        if (!LegacyClassicTitleState.TryRead(fields, out _, out _))
        { reason = Prefix + "DiskStateInvalid"; return false; }
        foreach (var key in new[] { LegacyClassicTitleState.SelectedKey, LegacyClassicTitleState.OwnedKey })
        {
            var present = fields.TryGetValue(key, out var actual);
            // IniDocument.Parse trims the entire RHS before copying attributes.
            // Only admission against that parsed session uses the same projection.
            // The captured raw fields/bytes/lines remain intact, and post-write
            // validation compares them without trimming or numeric equivalence.
            var observed = present && sessionProjection ? actual!.Trim() : actual;
            if (present != wanted.TryGetValue(key, out var expected) ||
                (present && !string.Equals(observed, expected, StringComparison.Ordinal)))
            { reason = Prefix + "StateStale"; return false; }
        }
        return true;
    }

    private static bool TryBoundCandidate(CharacterSnapshotFileBaseline.Snapshot before,
        IReadOnlyDictionary<string, string> candidate, out string[] projected, out string reason)
    {
        projected = [];
        reason = Prefix + "CandidateSnapshotTooLarge";
        if (!CharacterSnapshotSectionLineTransformer.TryApply((string[])before.Lines.Clone(), "Character",
            candidate, createIfMissing: false, out var lines))
        { reason = Prefix + "CharacterSectionMissing"; return false; }
        long bytes = before.Encoding.GetPreamble().Length;
        int newlineBytes = before.Encoding.GetByteCount(Environment.NewLine);
        foreach (var line in lines)
        {
            bytes += (long)before.Encoding.GetByteCount(line) + newlineBytes;
            if (bytes > CharacterSnapshotFileBaseline.MaximumBytes) return false;
            if (!string.Equals(before.Encoding.GetString(before.Encoding.GetBytes(line)), line, StringComparison.Ordinal))
            { reason = Prefix + "CandidateEncodingLoss"; return false; }
        }
        projected = lines;
        reason = string.Empty;
        return true;
    }
}
