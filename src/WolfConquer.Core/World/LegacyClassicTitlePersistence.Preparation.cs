using System.Globalization;

namespace WolfConquer.Core.World;

internal static partial class LegacyClassicTitlePersistence
{
    private static bool HasClassicTitleStateOwners(WorldSession expected, WorldSession updated) =>
        expected is not null && updated is not null && expected.Character is not null && updated.Character is not null &&
        expected.Character.Attributes is not null && updated.Character.Attributes is not null;

    private static bool HasClassicTitleAccountBinding(WorldSession expected) =>
        expected.AccountId > 0 && expected.AccountId <= uint.MaxValue && expected.Character.Uid == expected.AccountId;

    private static bool TryResolveClassicTitleStorageRoot(WorldSession expected, WorldSession updated, out string root)
    {
        root = string.Empty;
        if (string.IsNullOrWhiteSpace(expected.UsersRoot) || string.IsNullOrWhiteSpace(updated.UsersRoot) ||
            !Path.IsPathFullyQualified(expected.UsersRoot) || !Path.IsPathFullyQualified(updated.UsersRoot)) return false;
        var first = Path.GetFullPath(expected.UsersRoot);
        var second = Path.GetFullPath(updated.UsersRoot);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.TrimEndingDirectorySeparator(first), Path.TrimEndingDirectorySeparator(second), comparison))
            return false;
        root = first;
        return true;
    }

    private static bool HasOnlyClassicTitleSelectionChange(WorldSession expected, WorldSession updated) =>
        updated == (expected with { Character = updated.Character }) &&
        (updated.Character with { Attributes = expected.Character.Attributes }) == expected.Character &&
        LegacyClassicTitleState.SameExceptSelection(expected.Character.Attributes, updated.Character.Attributes);

    private static bool TryReadCanonicalClassicTitleCandidate(IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> updated, out string text)
    {
        text = string.Empty;
        if (!LegacyClassicTitleState.TryRead(expected, out _, out var owned) ||
            !LegacyClassicTitleState.TryRead(updated, out var selected, out _) ||
            (selected != 0 && !owned.Contains(selected)) ||
            !updated.TryGetValue(LegacyClassicTitleState.SelectedKey, out var canonical) ||
            canonical != selected.ToString(CultureInfo.InvariantCulture)) return false;
        text = canonical;
        return true;
    }
}
