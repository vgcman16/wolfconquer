using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WindowsJournalRootIdentityTests
{
    private static string V9CanonicalAlias(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(ObservedVolumeGuidPath(path)));

    [WindowsTradeJournalFailureFact]
    public void V9RejectsPhysicalAliasesInEveryDomainPair() => Run(f =>
    {
        var alias = V9CanonicalAlias(f.Users);
        foreach (var (users, items, spells) in new[]
        { (f.Users, alias, f.Quests), (f.Users, f.Items, alias), (f.Items, f.Users, alias) })
            Assert.False(LegacyTradeJournal.IsCharacterItemsSpellsDomainConfigured(users, items, spells));
        Assert.False(LegacyTradeJournal.HasPending(f.Users));
    });

    [WindowsTradeJournalFailureFact]
    public void V9RejectsPhysicalAncestorAliasesInBothDirectionsForEveryPair() => Run(f =>
    {
        var child = V9CanonicalAlias(f.Child);
        foreach (var (users, items, spells) in new[]
        {
            (f.Users, child, f.Quests), (child, f.Users, f.Quests),
            (f.Users, f.Items, child), (child, f.Items, f.Users),
            (f.Items, f.Users, child), (f.Items, child, f.Users),
        }) Assert.False(LegacyTradeJournal.IsCharacterItemsSpellsDomainConfigured(users, items, spells));
        Assert.False(LegacyTradeJournal.HasPending(f.Users));
    });

    [WindowsTradeJournalFailureFact]
    public void V9AcceptsPhysicalSiblingVolumeAliasesWithoutCreatingJournal() => Run(f =>
    {
        Assert.True(LegacyTradeJournal.IsCharacterItemsSpellsDomainConfigured(
            f.Users, V9CanonicalAlias(f.Items), V9CanonicalAlias(f.Quests)));
        Assert.False(LegacyTradeJournal.HasPending(f.Users));
    });
}
