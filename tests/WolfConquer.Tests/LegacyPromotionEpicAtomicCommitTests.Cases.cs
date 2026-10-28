using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyPromotionEpicAtomicCommitTests
{
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ActualPaidEpicFirstOutputObservesBothDurableFilesAndCanonicalStore(bool ninja)
    {
        using var f = new Fixture(ninja);
        int observations = 0;
        f.BeforeFirstSend = () =>
        {
            observations++;
            Assert.Equal(850, f.Current.Character.ConquerPoints);
            Assert.True(f.Current.PlayerSpells.ContainsSpell(f.Spell));
            Assert.Equal(850, CharacterSnapshotParser.ParseText(File.ReadAllText(f.CharacterPath)).ConquerPoints);
            Assert.True(new PlayerSpellSnapshotLoader(f.Spells).LoadByCharacterUid(f.Session.Character.Uid).ContainsSpell(f.Spell));
            Assert.False(LegacyTradeJournal.HasPending(f.Session.UsersRoot!));
        };
        await f.InvokeAsync();
        Assert.Equal(1, observations);
        Assert.NotEmpty(f.Sent);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task DefiniteRealWriterFailureRestoresCpSpellsAndLeavesNoSuccess(bool ninja)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture(ninja);
        var character = File.ReadAllBytes(f.CharacterPath); var spells = File.ReadAllBytes(f.SpellPath);
        using (var held = new FileStream(f.SpellPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            await f.InvokeAsync();
        Assert.Same(f.Session, f.Current);
        Assert.Equal(3_000, f.Current.Character.ConquerPoints);
        Assert.Empty(f.Current.PlayerSpells.Spells); Assert.Empty(f.Sent);
        Assert.Equal(character, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(spells, File.ReadAllBytes(f.SpellPath));
        Assert.False(LegacyTradeJournal.HasPending(f.Session.UsersRoot!));
        Assert.Contains("tradeJournalWriteFailed:", f.Log.ToString());
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task DuplicateAtZeroBalanceDoesNotDebitOrWriteEitherFile(bool ninja)
    {
        using var f = new Fixture(ninja, learned: true, cp: 0);
        var character = File.ReadAllBytes(f.CharacterPath); var spells = File.ReadAllBytes(f.SpellPath);
        await f.InvokeAsync();
        Assert.Same(f.Session, f.Current); Assert.Equal(0, f.Current.Character.ConquerPoints);
        Assert.Equal(character, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(spells, File.ReadAllBytes(f.SpellPath));
        Assert.Equal(4, f.Sent.Count); // source dialog/option/avatar/finish, no scalar or learned-spell frame.
        Assert.Contains("already learned costConquerPoints=0", f.Log.ToString());
    }
}
