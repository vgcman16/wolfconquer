using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTitleStoragePersistence
{
    private static bool TryPrepare(WorldSession expected, WorldSession updated,
        IReadOnlyDictionary<string, string> values, out string path,
        out Dictionary<string, string> oldTitles, out Dictionary<string, string> candidate, out string reason)
    {
        path = string.Empty;
        oldTitles = new(StringComparer.OrdinalIgnoreCase);
        candidate = new(StringComparer.OrdinalIgnoreCase);
        reason = Prefix + "BindingInvalid";
        if (expected is null || updated is null || values is null || expected.Character is null ||
            updated.Character is null || expected.Character.Attributes is null || updated.Character.Attributes is null ||
            expected.AccountId <= 0 ||
            expected.Character.Uid != expected.AccountId || updated.AccountId != expected.AccountId ||
            updated.Character.Uid != expected.Character.Uid || string.IsNullOrWhiteSpace(expected.UsersRoot) ||
            string.IsNullOrWhiteSpace(updated.UsersRoot) || !Path.IsPathFullyQualified(expected.UsersRoot) ||
            !Path.IsPathFullyQualified(updated.UsersRoot)) return false;
        var root = Path.GetFullPath(expected.UsersRoot);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.TrimEndingDirectorySeparator(root),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(updated.UsersRoot)), comparison)) return false;
        // TryCommitSession intentionally skips normal autosave: reject any unrelated state change here.
        if (updated != (expected with { Character = updated.Character }) ||
            (updated.Character with { Attributes = expected.Character.Attributes }) != expected.Character ||
            !NonTitleFieldsEqual(expected.Character.Attributes, updated.Character.Attributes))
        { reason = Prefix + "UnrelatedStateChanged"; return false; }
        if (!TryTitleFields(expected.Character.Attributes, out oldTitles) ||
            !TryTitleFields(updated.Character.Attributes, out var newTitles))
        { reason = Prefix + "ExpectedOrUpdatedStateInvalid"; return false; }
        foreach (var pair in values)
        {
            if (!TitleKeys.Contains(pair.Key, StringComparer.OrdinalIgnoreCase) || pair.Value is null ||
                pair.Value.Contains('\r') || pair.Value.Contains('\n') ||
                pair.Value.Length > CharacterSnapshotFileBaseline.MaximumBytes ||
                !candidate.TryAdd(pair.Key, pair.Value))
            { reason = Prefix + "CandidateFieldsInvalid"; return false; }
        }
        foreach (var key in TitleKeys)
        {
            if (key == "SpecialTitles")
            {
                if (candidate.TryGetValue(key, out var text) != newTitles.TryGetValue(key, out var expectedText) ||
                    (text is not null && !string.Equals(text, expectedText, StringComparison.Ordinal)))
                { reason = Prefix + "CandidateFieldsInvalid"; return false; }
            }
            else
            {
                // The committed session must contain exactly what is written, including zeros.
                // Otherwise a later request would falsely appear stale after our own commit.
                if (!newTitles.TryGetValue(key, out var text) ||
                    !candidate.TryGetValue(key, out var actual) || !string.Equals(actual, text, StringComparison.Ordinal) ||
                    !string.Equals(text, ulong.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture)
                        .ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                { reason = Prefix + "CandidateFieldsInvalid"; return false; }
            }
        }
        path = Path.Combine(root, expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".ini");
        return CharacterSnapshotFileBaseline.TryValidateSafePath(path, Prefix, out reason);
    }

    private static bool TryTitleFields(IReadOnlyDictionary<string, string> attributes,
        out Dictionary<string, string> titles)
    {
        titles = new(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in attributes)
        {
            if (!TitleKeys.Contains(pair.Key, StringComparer.OrdinalIgnoreCase)) continue;
            if (pair.Value is null || pair.Value.Contains('\r') || pair.Value.Contains('\n') ||
                pair.Value.Length > CharacterSnapshotFileBaseline.MaximumBytes ||
                !titles.TryAdd(pair.Key, pair.Value)) return false;
            if (!pair.Key.Equals("SpecialTitles", StringComparison.OrdinalIgnoreCase) &&
                (!ulong.TryParse(pair.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ||
                 number > uint.MaxValue)) return false;
        }
        return true;
    }

    private static bool NonTitleFieldsEqual(IReadOnlyDictionary<string, string> first,
        IReadOnlyDictionary<string, string> second)
    {
        var left = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var right = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in first)
            if (!TitleKeys.Contains(pair.Key, StringComparer.OrdinalIgnoreCase) && !left.TryAdd(pair.Key, pair.Value))
                return false;
        foreach (var pair in second)
            if (!TitleKeys.Contains(pair.Key, StringComparer.OrdinalIgnoreCase) && !right.TryAdd(pair.Key, pair.Value))
                return false;
        return left.Count == right.Count && left.All(pair =>
            right.TryGetValue(pair.Key, out var value) && string.Equals(pair.Value, value, StringComparison.Ordinal));
    }
}
