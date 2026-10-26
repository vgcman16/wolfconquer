using System.Globalization;
using System.Text.Json;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private sealed record MailboxGearJournalDomain(string Users, string Items, string Mailbox, string CombatGear);

    internal static bool IsMailboxGearDomainConfigured(string usersRoot, string itemsRoot, string mailboxRoot, string combatGearRoot)
    {
        try { _ = GetMailboxGearRoots(usersRoot, itemsRoot, mailboxRoot, combatGearRoot); return true; }
        catch (Exception error) when (IsJournalException(error)) { return false; }
    }

    private static JournalRoots GetMailboxGearRoots(string usersRoot, string itemsRoot, string mailboxRoot, string combatGearRoot)
    {
        var roots = GetMailboxRoots(usersRoot, itemsRoot, mailboxRoot);
        var gear = NormalizeQuestRoot(combatGearRoot);
        EnsureSafePath(gear);
        if (!Directory.Exists(gear)) throw new IOException("Configured CombatGear root is missing.");
        if (new[] { roots.Users, roots.Items, roots.Mailbox!, roots.Journal }.Any(root => QuestRootsOverlap(root, gear)))
            throw new ArgumentException("CombatGear root overlaps the mailbox transaction domain.");
        // Existing three-root physical inspector covers all six configured pairs.
        // Role names here describe path slots, not fabricated quest/gameplay owners.
        RequirePhysicallySeparateQuestRoots(roots.Users, roots.Items, gear);
        RequirePhysicallySeparateQuestRoots(roots.Users, roots.Mailbox!, gear);
        return roots with { CombatGear = gear };
    }

    private static MailboxGearJournalDomain ReadMailboxGearDomain(JsonElement element)
    {
        var fields = GetProperties(element, "Users", "Items", "Mailbox", "CombatGear");
        if (fields.Values.Any(value => value.ValueKind != JsonValueKind.String))
            throw new JsonException("Invalid mailbox/gear transaction domain.");
        return new(fields["Users"].GetString()!, fields["Items"].GetString()!,
            fields["Mailbox"].GetString()!, fields["CombatGear"].GetString()!);
    }

    private static JournalTarget[] GetMailboxGearTargets(JournalRoots roots, JournalRecord record)
    {
        var domain = record.MailboxGearDomain;
        if (roots.Mailbox is null || roots.CombatGear is null || domain is null ||
            !SameQuestRoot(roots.Users, domain.Users) || !SameQuestRoot(roots.Items, domain.Items) ||
            !SameQuestRoot(roots.Mailbox, domain.Mailbox) || !SameQuestRoot(roots.CombatGear, domain.CombatGear))
            throw new ArgumentException("Mailbox/gear transaction domain differs from explicit configuration.");
        var targets = record.Changes.Select(change => new JournalTarget(Path.Combine(change.Kind switch
        {
            LegacyTradeJournalFileKind.Character => roots.Users,
            LegacyTradeJournalFileKind.Items => roots.Items,
            LegacyTradeJournalFileKind.Mailbox => roots.Mailbox!,
            LegacyTradeJournalFileKind.CombatGear => roots.CombatGear!,
            _ => throw new ArgumentException("Invalid mailbox/gear target kind."),
        }, change.FileId.ToString(CultureInfo.InvariantCulture) +
            (change.Kind is LegacyTradeJournalFileKind.Character or LegacyTradeJournalFileKind.CombatGear ? ".ini" : ".bin")), change)).ToArray();
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (targets.Select(target => target.Path).Distinct(comparer).Count() != targets.Length)
            throw new ArgumentException("Duplicate mailbox/gear target path.");
        foreach (var target in targets) EnsureSafePath(target.Path);
        return targets;
    }

    private static void RequireMailboxGearOrder(LegacyTradeJournalChange[] changes)
    {
        LegacyTradeJournalFileKind[] expected = [LegacyTradeJournalFileKind.Character, LegacyTradeJournalFileKind.Items,
            LegacyTradeJournalFileKind.Mailbox, LegacyTradeJournalFileKind.CombatGear];
        if (!changes.Select(change => change.Kind).SequenceEqual(expected))
            throw new JsonException("Mailbox/gear journal target order is not canonical.");
    }
}
