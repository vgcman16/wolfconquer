using System.Globalization;
using System.Text.Json;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private sealed record CharacterSpellsJournalDomain(string Users, string Spells);

    internal static bool IsCharacterSpellsDomainConfigured(string usersRoot, string spellsRoot)
    {
        try { _ = GetCharacterSpellsRoots(usersRoot, spellsRoot); return true; }
        catch (Exception error) when (IsJournalException(error)) { return false; }
    }

    private static JournalRoots GetCharacterSpellsRoots(string usersRoot, string spellsRoot)
    {
        if (string.IsNullOrWhiteSpace(usersRoot) || string.IsNullOrWhiteSpace(spellsRoot))
            throw new ArgumentException("Character and Spells roots are required.");
        var users = NormalizeQuestRoot(usersRoot);
        var spells = NormalizeQuestRoot(spellsRoot);
        if (!string.Equals(usersRoot, users, StringComparison.Ordinal) ||
            !string.Equals(spellsRoot, spells, StringComparison.Ordinal))
            throw new ArgumentException("Character and Spells roots must be canonical absolute paths.");
        EnsureSafePath(users);
        EnsureSafePath(spells);
        if (!Directory.Exists(users)) throw new IOException("Configured Character root is missing.");
        if (File.Exists(spells)) throw new IOException("Configured Spells root is a file.");
        var journal = Path.Combine(users, JournalDirectoryName);
        EnsureSafePath(journal);
        if (QuestRootsOverlap(users, spells) || QuestRootsOverlap(spells, journal))
            throw new ArgumentException("Character, Spells and journal roots overlap.");
        RequireAdmissionPhysicalRoots([users, spells]);
        return new(users, string.Empty, journal) { Spells = spells };
    }

    private static CharacterSpellsJournalDomain ReadCharacterSpellsDomain(JsonElement element)
    {
        var fields = GetProperties(element, "Users", "Spells");
        if (fields.Values.Any(value => value.ValueKind != JsonValueKind.String))
            throw new JsonException("Invalid Character and Spells transaction domain.");
        var users = fields["Users"].GetString()!;
        var spells = fields["Spells"].GetString()!;
        try
        {
            if (!string.Equals(users, NormalizeQuestRoot(users), StringComparison.Ordinal) ||
                !string.Equals(spells, NormalizeQuestRoot(spells), StringComparison.Ordinal))
                throw new JsonException("Character and Spells roots are not canonical absolute paths.");
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or
            System.Security.SecurityException)
        {
            throw new JsonException("Invalid Character and Spells root.", error);
        }
        return new(users, spells);
    }

    private static JournalTarget[] GetCharacterSpellsTargets(JournalRoots roots, JournalRecord record)
    {
        var domain = record.CharacterSpellsDomain;
        if (roots.Spells is null || domain is null || !SameQuestRoot(roots.Users, domain.Users) ||
            !SameQuestRoot(roots.Spells, domain.Spells))
            throw new ArgumentException("Character and Spells journal differs from explicit configuration.");
        RequireCharacterSpellsOrder(record.Changes);
        var targets = record.Changes.Select(change => new JournalTarget(Path.Combine(
            change.Kind == LegacyTradeJournalFileKind.Character ? roots.Users : roots.Spells,
            change.FileId.ToString(CultureInfo.InvariantCulture) +
            (change.Kind == LegacyTradeJournalFileKind.Character ? ".ini" : ".bin")), change)).ToArray();
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (targets.Select(target => target.Path).Distinct(comparer).Count() != 2)
            throw new ArgumentException("Duplicate Character and Spells target path.");
        foreach (var target in targets) EnsureSafePath(target.Path);
        return targets;
    }

    private static void RequireCharacterSpellsTargetsCurrent(JournalRoots roots, JournalRecord record,
        JournalTarget[] targets)
    {
        if (roots.Spells is null) throw new ArgumentException("Missing configured Spells root.");
        var current = GetCharacterSpellsRoots(roots.Users, roots.Spells);
        var expected = GetCharacterSpellsTargets(current, record);
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (targets.Length != expected.Length || targets.Where((target, index) =>
                !comparer.Equals(Path.GetFullPath(target.Path), Path.GetFullPath(expected[index].Path)) ||
                !ReferenceEquals(target.Change, expected[index].Change)).Any())
            throw new IOException("Character and Spells target ownership changed.");
    }

    private static void PrepareCharacterSpellsDirectories(JournalRoots roots, JournalRecord record,
        JournalTarget[] targets)
    {
        if (roots.Spells is null) throw new ArgumentException("Missing configured Spells root.");
        EnsureSafePath(roots.Spells);
        Directory.CreateDirectory(roots.Spells);
        RequireCharacterSpellsTargetsCurrent(roots, record, targets);
    }

    private static void RequireCharacterSpellsOrder(LegacyTradeJournalChange[] changes)
    {
        LegacyTradeJournalFileKind[] expected =
            [LegacyTradeJournalFileKind.Character, LegacyTradeJournalFileKind.Spells];
        if (!changes.Select(change => change.Kind).SequenceEqual(expected))
            throw new JsonException("Character and Spells journal target order is not canonical.");
    }

}
