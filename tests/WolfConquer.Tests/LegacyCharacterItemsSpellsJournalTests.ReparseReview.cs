using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyCharacterItemsSpellsJournalTests
{
    [WindowsTradeJournalFailureFact]
    public void RecoveryRejectsReparseReplacementOfMissingSpellRootAndThenRecoversOwnedPath() => Run(f =>
    {
        Assert.False(LegacyTradeJournal.TryCommitCharacterItemsSpells(f.Users, f.Items, f.Spells,
            f.Changes(missingSpells: true), LegacyCharacterItemsSpellsJournalInterruption.AfterPending, out _));
        byte[] pending = File.ReadAllBytes(f.Pending);
        string other = Path.Combine(f.Root, "other-owned-directory");
        Directory.CreateDirectory(other);
        string sentinel = Path.Combine(other, "sentinel.bin");
        byte[] sentinelBytes = [17, 34, 51];
        File.WriteAllBytes(sentinel, sentinelBytes);
        bool linked = false;
        try
        {
            try { Directory.CreateSymbolicLink(f.Spells, other); linked = true; }
            catch (UnauthorizedAccessException)
            { throw Xunit.Sdk.SkipException.ForSkip("Windows symbolic-link privilege is unavailable; no settings changed."); }
            catch (IOException error) when ((error.HResult & 0xffff) == 1314)
            { throw Xunit.Sdk.SkipException.ForSkip("Windows symbolic-link privilege is unavailable; no settings changed."); }
            Assert.True((File.GetAttributes(f.Spells) & FileAttributes.ReparsePoint) != 0);
            var blocked = f.Recovery();
            Assert.False(blocked.TryEnsureInitialized(out _));
            Assert.True(blocked.IsBlocked);
            Assert.Equal(pending, File.ReadAllBytes(f.Pending));
            Assert.Equal(f.Character, File.ReadAllBytes(f.CharacterPath));
            Assert.Equal(f.ItemBefore, File.ReadAllBytes(f.ItemsPath));
            Assert.Equal(sentinelBytes, File.ReadAllBytes(sentinel));
            Assert.False(File.Exists(Path.Combine(other, Fixture.Uid + ".bin")));
        }
        finally
        {
            if (linked) Directory.Delete(f.Spells, false);
        }
        var fresh = f.Recovery();
        Assert.True(fresh.TryEnsureInitialized(out var reason), reason);
        f.Images(f.Character, f.ItemAfter, f.SpellAfter);
        Assert.False(File.Exists(f.Pending));
        Assert.Equal(sentinelBytes, File.ReadAllBytes(sentinel));
    }, missingSpells: true);
}
