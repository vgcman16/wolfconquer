using System.Globalization;
using System.Text.Json;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private sealed record MailboxJournalDomain(string Users, string Items, string Mailbox);

    internal static bool IsMailboxDomainConfigured(string usersRoot, string itemsRoot, string mailboxRoot)
    {
        try { _ = GetMailboxRoots(usersRoot, itemsRoot, mailboxRoot); return true; }
        catch (Exception error) when (IsJournalException(error)) { return false; }
    }

    private static JournalRoots GetMailboxRoots(string usersRoot, string itemsRoot, string mailboxRoot)
    {
        // Reuse the existing path-only normalization/alias checks. No quest
        // owner or quest record is created for a mailbox transaction.
        var users = NormalizeQuestRoot(usersRoot);
        var items = NormalizeQuestRoot(itemsRoot);
        var mailbox = NormalizeQuestRoot(mailboxRoot);
        string[] roots = [users, items, mailbox];
        foreach (var root in roots)
        {
            EnsureSafePath(root);
            if (!Directory.Exists(root)) throw new IOException("A configured mailbox transaction root is missing.");
        }
        for (var index = 0; index < roots.Length; index++)
            for (var other = index + 1; other < roots.Length; other++)
                if (QuestRootsOverlap(roots[index], roots[other])) throw new ArgumentException("Mailbox roots overlap.");
        RequirePhysicallySeparateQuestRoots(users, items, mailbox);
        var journal = Path.Combine(users, JournalDirectoryName);
        EnsureSafePath(journal);
        if (QuestRootsOverlap(items, journal) || QuestRootsOverlap(mailbox, journal))
            throw new ArgumentException("Mailbox transaction roots overlap the shared journal.");
        return new(users, items, journal) { Mailbox = mailbox };
    }

    private static MailboxJournalDomain ReadMailboxDomain(JsonElement element)
    {
        var fields = GetProperties(element, "Users", "Items", "Mailbox");
        if (fields.Values.Any(value => value.ValueKind != JsonValueKind.String))
            throw new JsonException("Invalid mailbox transaction domain.");
        return new(fields["Users"].GetString()!, fields["Items"].GetString()!, fields["Mailbox"].GetString()!);
    }

    private static JournalTarget[] GetMailboxTargets(JournalRoots roots, JournalRecord record)
    {
        var domain = record.MailboxDomain;
        if (roots.Mailbox is null || domain is null || !SameQuestRoot(roots.Users, domain.Users) ||
            !SameQuestRoot(roots.Items, domain.Items) || !SameQuestRoot(roots.Mailbox, domain.Mailbox))
            throw new ArgumentException("Mailbox transaction domain differs from explicit configuration.");
        var targets = record.Changes.Select(change => new JournalTarget(Path.Combine(change.Kind switch
        {
            LegacyTradeJournalFileKind.Character => roots.Users,
            LegacyTradeJournalFileKind.Items => roots.Items,
            LegacyTradeJournalFileKind.Mailbox => roots.Mailbox!,
            _ => throw new ArgumentException("Invalid mailbox target kind."),
        }, change.FileId.ToString(CultureInfo.InvariantCulture) +
            (change.Kind == LegacyTradeJournalFileKind.Character ? ".ini" : ".bin")), change)).ToArray();
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (targets.Select(target => target.Path).Distinct(comparer).Count() != targets.Length)
            throw new ArgumentException("Duplicate mailbox target path.");
        foreach (var target in targets) EnsureSafePath(target.Path);
        return targets;
    }
}
