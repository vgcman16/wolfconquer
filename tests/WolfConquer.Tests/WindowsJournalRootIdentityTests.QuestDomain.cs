using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WindowsJournalRootIdentityTests
{
    [WindowsTradeJournalFailureFact]
    public void V3RootConfigurationRejectsActualVolumeAliasesForEveryRolePair() => Run(f =>
    {
        var alias = ObservedVolumeGuidPath(f.Users);
        var cases = new[] { (f.Users, alias, f.Quests), (f.Users, f.Items, alias), (f.Items, f.Users, alias) };
        foreach (var (users, items, quests) in cases)
            Assert.False(LegacyTradeJournal.IsQuestDomainConfigured(users, items, quests));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(f.Users)!, "*", SearchOption.AllDirectories));
        Assert.False(LegacyTradeJournal.HasPending(f.Users));
    });

    [WindowsTradeJournalFailureFact]
    public void V3RootConfigurationRejectsActualVolumeAncestorAliasesInEveryDirection() => Run(f =>
    {
        var child = ObservedVolumeGuidPath(f.Child);
        var cases = new[]
        {
            (f.Users, child, f.Quests), (child, f.Users, f.Quests),
            (f.Users, f.Items, child), (child, f.Items, f.Users),
            (f.Items, f.Users, child), (f.Items, child, f.Users),
        };
        foreach (var (users, items, quests) in cases)
            Assert.False(LegacyTradeJournal.IsQuestDomainConfigured(users, items, quests));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(f.Users)!, "*", SearchOption.AllDirectories));
    });

    [WindowsTradeJournalFailureFact]
    public void V3RootConfigurationAcceptsRealVolumeGuidSiblingsWithoutWritingOrRetainingPins() => Run(f =>
    {
        var leaf = Path.Combine(f.Users, "root-guard-sentinel.bin");
        File.WriteAllBytes(leaf, [1, 2, 3]);
        var aliasItems = ObservedVolumeGuidPath(f.Items);
        var aliasQuests = ObservedVolumeGuidPath(f.Quests);
        Assert.True(LegacyTradeJournal.IsQuestDomainConfigured(f.Users, aliasItems, aliasQuests));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(leaf));
        Assert.Equal(new[] { leaf }, Directory.EnumerateFiles(Path.GetDirectoryName(f.Users)!, "*", SearchOption.AllDirectories));
        Assert.False(LegacyTradeJournal.HasPending(f.Users));
        // The scoped guard must not pretend its inspection pins are a retained
        // journal/domain lifetime. A normal rename works after the call returns.
        var moved = f.Items + "-moved";
        Directory.Move(f.Items, moved);
        Directory.Move(moved, f.Items);
    });
}
