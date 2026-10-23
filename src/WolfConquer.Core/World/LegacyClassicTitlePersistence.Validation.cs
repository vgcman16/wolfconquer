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
        if (expected is null || updated is null || expected.Character is null || updated.Character is null ||
            expected.Character.Attributes is null || updated.Character.Attributes is null ||
            expected.AccountId <= 0 || expected.AccountId > uint.MaxValue || expected.Character.Uid != expected.AccountId ||
            string.IsNullOrWhiteSpace(expected.UsersRoot) || string.IsNullOrWhiteSpace(updated.UsersRoot) ||
            !Path.IsPathFullyQualified(expected.UsersRoot) || !Path.IsPathFullyQualified(updated.UsersRoot)) return false;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(expected.UsersRoot)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(updated.UsersRoot)), comparison)) return false;
        // TryCommitSession skips autosave. The complete updated value may change
        // MyTitle only; a malicious direct caller cannot smuggle any other state.
        if (updated != (expected with { Character = updated.Character }) ||
            (updated.Character with { Attributes = expected.Character.Attributes }) != expected.Character ||
            !LegacyClassicTitleState.SameExceptSelection(expected.Character.Attributes, updated.Character.Attributes))
        { reason = Prefix + "UnrelatedStateChanged"; return false; }
        if (!LegacyClassicTitleState.TryRead(expected.Character.Attributes, out _, out var owned) ||
            !LegacyClassicTitleState.TryRead(updated.Character.Attributes, out var selected, out _) ||
            (selected != 0 && !owned.Contains(selected)) ||
            !updated.Character.Attributes.TryGetValue(LegacyClassicTitleState.SelectedKey, out var text) ||
            text != selected.ToString(CultureInfo.InvariantCulture))
        { reason = Prefix + "StateInvalid"; return false; }
        candidate.Add(LegacyClassicTitleState.SelectedKey, text);
        path = Path.Combine(Path.GetFullPath(expected.UsersRoot),
            expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".ini");
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
