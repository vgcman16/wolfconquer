using System.Globalization;
using System.Text.Json;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private sealed record QuestJournalDomain(string Users, string Items, string Quests);

    internal static bool IsQuestDomainConfigured(string usersRoot, string itemsRoot, string questsRoot)
    {
        try { _ = GetQuestRoots(usersRoot, itemsRoot, questsRoot); return true; }
        catch (Exception error) when (IsJournalException(error)) { return false; }
    }

    private static JournalRoots GetQuestRoots(string usersRoot, string itemsRoot, string questsRoot)
    {
        var users = NormalizeQuestRoot(usersRoot);
        var items = NormalizeQuestRoot(itemsRoot);
        var quests = NormalizeQuestRoot(questsRoot);
        string[] configured = [users, items, quests];
        foreach (var root in configured)
        {
            EnsureSafePath(root);
            if (!Directory.Exists(root)) throw new IOException("A configured quest-transaction root is missing or not a directory.");
        }
        for (var index = 0; index < configured.Length; index++)
            for (var other = index + 1; other < configured.Length; other++)
                if (QuestRootsOverlap(configured[index], configured[other]))
                    throw new ArgumentException("Quest-transaction roots overlap or alias.");
        RequirePhysicallySeparateQuestRoots(users, items, quests);
        var journal = Path.Combine(users, JournalDirectoryName);
        EnsureSafePath(journal);
        if (QuestRootsOverlap(items, journal) || QuestRootsOverlap(quests, journal))
            throw new ArgumentException("Quest-transaction roots overlap the shared journal.");
        return new(users, items, journal) { Quests = quests };
    }

    private static string NormalizeQuestRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            throw new ArgumentException("An explicit fully qualified root is required.");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
    }

    private static bool QuestRootsOverlap(string first, string second)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return first.Equals(second, comparison) ||
            first.StartsWith(QuestRootPrefix(second), comparison) || second.StartsWith(QuestRootPrefix(first), comparison);
    }

    private static string QuestRootPrefix(string root) =>
        Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;

    private static QuestJournalDomain ReadQuestDomain(JsonElement element)
    {
        var fields = GetProperties(element, "Users", "Items", "Quests");
        if (fields.Values.Any(value => value.ValueKind != JsonValueKind.String))
            throw new JsonException("Invalid quest-transaction domain.");
        return new(fields["Users"].GetString()!, fields["Items"].GetString()!, fields["Quests"].GetString()!);
    }

    private static JournalTarget[] GetQuestTargets(JournalRoots roots, JournalRecord record)
    {
        var domain = record.Domain;
        // Compare record paths lexically before inspecting any record-supplied
        // foreign location. All actual filesystem reads use configured roots.
        if (roots.Quests is null || domain is null ||
            !SameQuestRoot(roots.Users, domain.Users) || !SameQuestRoot(roots.Items, domain.Items) ||
            !SameQuestRoot(roots.Quests, domain.Quests))
            throw new ArgumentException("Quest-transaction domain differs from configuration.");
        var targets = record.Changes.Select(change => new JournalTarget(Path.Combine(
            change.Kind switch
            {
                LegacyTradeJournalFileKind.Character => roots.Users,
                LegacyTradeJournalFileKind.Items => roots.Items,
                LegacyTradeJournalFileKind.Quests => roots.Quests!,
                _ => throw new ArgumentException("Invalid quest-transaction target kind."),
            }, change.FileId.ToString(CultureInfo.InvariantCulture) +
                (change.Kind == LegacyTradeJournalFileKind.Character ? ".ini" : ".bin")), change)).ToArray();
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (targets.Select(target => target.Path).Distinct(comparer).Count() != targets.Length)
            throw new ArgumentException("Duplicate quest-transaction target path.");
        foreach (var target in targets) EnsureSafePath(target.Path);
        return targets;
    }

    private static bool SameQuestRoot(string configured, string recorded) => string.Equals(
        NormalizeQuestRoot(configured), NormalizeQuestRoot(recorded),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
