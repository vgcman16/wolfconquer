using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static void RequirePhysicallySeparateQuestRoots(string users, string items, string quests)
    {
        // Scoped inspection under trusted configured/cooperating namespace rules.
        // The pins end here: this is NOT a retained write/domain lifetime, child
        // namespace lock or handle-relative replacement authorization.
        using var observed = WindowsJournalRootIdentity.Capture(users, items, quests);
        RequireDisjoint(observed.Inspect(WindowsJournalRootIdentity.RootRole.Users, WindowsJournalRootIdentity.RootRole.Items));
        RequireDisjoint(observed.Inspect(WindowsJournalRootIdentity.RootRole.Users, WindowsJournalRootIdentity.RootRole.Quests));
        RequireDisjoint(observed.Inspect(WindowsJournalRootIdentity.RootRole.Items, WindowsJournalRootIdentity.RootRole.Quests));
    }

    private static void RequireDisjoint(WindowsJournalRootIdentity.ObservedRootRelation relation)
    {
        if (relation != WindowsJournalRootIdentity.ObservedRootRelation.DisjointCapturedChains)
            throw new IOException("Configured quest-transaction directories physically overlap or alias.");
    }
}
