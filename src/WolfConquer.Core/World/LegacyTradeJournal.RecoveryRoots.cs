namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static bool TryResolveRecoveryRoots(string usersRoot, string itemsRoot, string? questsRoot, string? mailboxRoot,
        string? combatGearRoot, LegacyOrdinaryAdmissionRoots? admissionRoots, JournalRecord record,
        out JournalRoots roots, out string reason)
    {
        roots = null!; reason = string.Empty;
        if (record.Version == 19)
        {
            if (admissionRoots is null || mailboxRoot is null)
            { reason = RecoveryRequired + ":authenticatedCreationConfiguredRootsMissing"; return false; }
            roots = GetAuthenticatedCreationRoots(admissionRoots, mailboxRoot);
            if (!SameQuestRoot(usersRoot, roots.Users) || !SameQuestRoot(itemsRoot, roots.Items))
            { reason = RecoveryRequired + ":authenticatedCreationConfiguredRootsMismatch"; return false; }
        }
        else if (IsOrdinaryAdmissionVersion(record.Version))
            roots = GetAdmissionRoots(admissionRoots ?? throw new ArgumentException("Explicit ordinary-admission roots are required."));
        else if (record.Version == 8)
        {
            if (admissionRoots?.Spells is not { } configuredSpells)
            { reason = RecoveryRequired + ":characterSpellsRootMissing"; return false; }
            roots = GetCharacterSpellsRoots(admissionRoots.Users, configuredSpells);
            if (!SameQuestRoot(usersRoot, roots.Users))
            { reason = RecoveryRequired + ":characterSpellsUsersMismatch"; return false; }
        }
        else if (record.Version is 9 or 12 or 13 or 14 or 15 or 16 or 17 or 18)
        {
            if (admissionRoots?.Items is not { } configuredItems || admissionRoots.Spells is not { } configuredSpells)
            { reason = RecoveryRequired + ":characterItemsSpellsRootMissing"; return false; }
            roots = GetCharacterItemsSpellsRoots(admissionRoots.Users, configuredItems, configuredSpells);
            if (!SameQuestRoot(usersRoot, roots.Users) || !SameQuestRoot(itemsRoot, roots.Items))
            { reason = RecoveryRequired + ":characterItemsSpellsRootsMismatch"; return false; }
        }
        else if (record.Version == 11) roots = GetCharacterResourcesRoots(usersRoot);
        else roots = GetRoots(usersRoot, itemsRoot);
        if (record.Version == 3)
        {
            if (string.IsNullOrWhiteSpace(questsRoot)) { reason = RecoveryRequired + ":questsRootMissing"; return false; }
            roots = GetQuestRoots(usersRoot, itemsRoot, questsRoot);
        }
        if (record.Version == 4)
        {
            if (string.IsNullOrWhiteSpace(mailboxRoot)) { reason = RecoveryRequired + ":mailboxRootMissing"; return false; }
            roots = GetMailboxRoots(usersRoot, itemsRoot, mailboxRoot);
        }
        if (record.Version == 5)
        {
            if (string.IsNullOrWhiteSpace(mailboxRoot) || string.IsNullOrWhiteSpace(combatGearRoot))
            { reason = RecoveryRequired + ":mailboxOrCombatGearRootMissing"; return false; }
            roots = GetMailboxGearRoots(usersRoot, itemsRoot, mailboxRoot, combatGearRoot);
        }
        return true;
    }
}
