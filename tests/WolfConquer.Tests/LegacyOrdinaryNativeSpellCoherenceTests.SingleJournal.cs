using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    [Fact]
    public async Task ActualSingleCharacterSpellJournalPublishesFrozenStoreAndNativeMembership()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var before = f.Current; var storage = RequireCoherent(f);
            var mutable = before.PlayerSpells.Spells.Select(s => s.Id == 6001 ? s with { Experience = 2222, UseSpellSoul = 4 } : s).ToList();
            var proposed = before with { PlayerSpells = new PlayerSpellSnapshot(mutable) };
            byte[] character = File.ReadAllBytes(f.CharacterPath), spellBefore = File.ReadAllBytes(f.SpellPath);
            byte[] spellAfter = PlayerSpellSnapshotLoader.ToLegacyBytes(mutable);
            LegacyTradeJournalChange[] changes = [
                new(LegacyTradeJournalFileKind.Character, before.AccountId, character, character),
                new(LegacyTradeJournalFileKind.Spells, before.Character.Uid, spellBefore, spellAfter)];
            Assert.True(f.Recovery.IsReadyForCharacterSpells(f.Users, f.Spells));
            bool invoked = false;
            Assert.True(f.Store.TryCommitSession(f.Connection, before, proposed, () =>
            {
                invoked = true;
                return LegacyTradeJournal.TryCommitCharacterSpells(f.Users, f.Spells, changes, out _);
            }, out var committed));
            Assert.True(invoked); Assert.Same(f.Current, committed); Assert.Same(storage, f.Native.MySpells);
            Assert.Equal(spellAfter, File.ReadAllBytes(f.SpellPath));
            Assert.Equal(2222, Assert.Single(f.Current.PlayerSpells.Spells, s => s.Id == 6001).Experience);
            mutable.Clear(); Assert.Equal(2222, Assert.Single(f.Current.PlayerSpells.Spells, s => s.Id == 6001).Experience);
            RequireCoherent(f); Assert.False(LegacyTradeJournal.HasPending(f.Users));
        });
    }
}
