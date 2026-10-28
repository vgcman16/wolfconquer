using System.Globalization;
using System.Text.Json;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private sealed record CharacterItemsSpellsJournalDomain(string Users, string Items, string Spells);

    internal static bool IsCharacterItemsSpellsDomainConfigured(string usersRoot, string itemsRoot, string spellsRoot)
    {
        try { _ = GetCharacterItemsSpellsRoots(usersRoot, itemsRoot, spellsRoot); return true; }
        catch (Exception error) when (IsJournalException(error)) { return false; }
    }

    private static JournalRoots GetCharacterItemsSpellsRoots(string usersRoot, string itemsRoot, string spellsRoot)
    {
        if (string.IsNullOrWhiteSpace(usersRoot) || string.IsNullOrWhiteSpace(itemsRoot) || string.IsNullOrWhiteSpace(spellsRoot))
            throw new ArgumentException("Character, Items and Spells roots are required.");
        var users = NormalizeQuestRoot(usersRoot);
        var items = NormalizeQuestRoot(itemsRoot);
        var spells = NormalizeQuestRoot(spellsRoot);
        if (!string.Equals(usersRoot, users, StringComparison.Ordinal) ||
            !string.Equals(itemsRoot, items, StringComparison.Ordinal) ||
            !string.Equals(spellsRoot, spells, StringComparison.Ordinal))
            throw new ArgumentException("Character, Items and Spells roots must be canonical absolute paths.");
        EnsureSafePath(users);
        EnsureSafePath(items);
        EnsureSafePath(spells);
        if (!Directory.Exists(users) || !Directory.Exists(items)) throw new IOException("Configured Character or Items root is missing.");
        if (File.Exists(spells)) throw new IOException("Configured Spells root is a file.");
        var journal = Path.Combine(users, JournalDirectoryName);
        EnsureSafePath(journal);
        if (QuestRootsOverlap(users, items) || QuestRootsOverlap(users, spells) ||
            QuestRootsOverlap(items, spells) || QuestRootsOverlap(items, journal) || QuestRootsOverlap(spells, journal))
            throw new ArgumentException("Character, Items, Spells and journal roots overlap.");
        RequireAdmissionPhysicalRoots([users, items, spells]);
        return new(users, items, journal) { Spells = spells };
    }

    private static CharacterItemsSpellsJournalDomain ReadCharacterItemsSpellsDomain(JsonElement element)
    {
        var fields = GetProperties(element, "Users", "Items", "Spells");
        if (fields.Values.Any(value => value.ValueKind != JsonValueKind.String))
            throw new JsonException("Invalid Character, Items and Spells transaction domain.");
        var users = fields["Users"].GetString()!;
        var items = fields["Items"].GetString()!;
        var spells = fields["Spells"].GetString()!;
        try
        {
            if (!string.Equals(users, NormalizeQuestRoot(users), StringComparison.Ordinal) ||
                !string.Equals(items, NormalizeQuestRoot(items), StringComparison.Ordinal) ||
                !string.Equals(spells, NormalizeQuestRoot(spells), StringComparison.Ordinal))
                throw new JsonException("Character, Items and Spells roots are not canonical absolute paths.");
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or
            System.Security.SecurityException)
        {
            throw new JsonException("Invalid Character, Items and Spells root.", error);
        }
        return new(users, items, spells);
    }

    private static JournalTarget[] GetCharacterItemsSpellsTargets(JournalRoots roots, JournalRecord record)
    {
        var domain = record.CharacterItemsSpellsDomain;
        if (roots.Spells is null || domain is null || !SameQuestRoot(roots.Users, domain.Users) ||
            !SameQuestRoot(roots.Items, domain.Items) || !SameQuestRoot(roots.Spells, domain.Spells))
            throw new ArgumentException("Character, Items and Spells journal differs from explicit configuration.");
        RequireCharacterItemsSpellsOrder(record.Changes);
        var targets = record.Changes.Select(change => new JournalTarget(Path.Combine(
            change.Kind switch
            {
                LegacyTradeJournalFileKind.Character => roots.Users,
                LegacyTradeJournalFileKind.Items => roots.Items,
                LegacyTradeJournalFileKind.Spells => roots.Spells,
                _ => throw new ArgumentException("Unexpected journal target kind."),
            },
            change.FileId.ToString(CultureInfo.InvariantCulture) +
            (change.Kind == LegacyTradeJournalFileKind.Character ? ".ini" : ".bin")), change)).ToArray();
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (targets.Select(target => target.Path).Distinct(comparer).Count() != 3)
            throw new ArgumentException("Duplicate Character, Items and Spells target path.");
        foreach (var target in targets) EnsureSafePath(target.Path);
        return targets;
    }

    private static void RequireCharacterItemsSpellsTargetsCurrent(JournalRoots roots, JournalRecord record,
        JournalTarget[] targets)
    {
        if (roots.Spells is null) throw new ArgumentException("Missing configured Spells root.");
        var current = GetCharacterItemsSpellsRoots(roots.Users, roots.Items, roots.Spells);
        var expected = GetCharacterItemsSpellsTargets(current, record);
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (targets.Length != expected.Length || targets.Where((target, index) =>
                !comparer.Equals(Path.GetFullPath(target.Path), Path.GetFullPath(expected[index].Path)) ||
                !ReferenceEquals(target.Change, expected[index].Change)).Any())
            throw new IOException("Character, Items and Spells target ownership changed.");
    }

    private static void PrepareCharacterItemsSpellsDirectories(JournalRoots roots, JournalRecord record,
        JournalTarget[] targets)
    {
        if (roots.Spells is null) throw new ArgumentException("Missing configured Spells root.");
        EnsureSafePath(roots.Spells);
        Directory.CreateDirectory(roots.Spells);
        RequireCharacterItemsSpellsTargetsCurrent(roots, record, targets);
    }

    private static void RequireCharacterItemsSpellsOrder(LegacyTradeJournalChange[] changes)
    {
        LegacyTradeJournalFileKind[] expected =
            [LegacyTradeJournalFileKind.Character, LegacyTradeJournalFileKind.Items, LegacyTradeJournalFileKind.Spells];
        if (!changes.Select(change => change.Kind).SequenceEqual(expected))
            throw new JsonException("Character, Items and Spells journal target order is not canonical.");
    }

}
