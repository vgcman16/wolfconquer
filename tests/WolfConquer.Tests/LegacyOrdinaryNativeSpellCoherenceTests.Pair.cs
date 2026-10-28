using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PairPreparesBothActualOwnersBeforeRealJournalWriter(bool invalidSecond)
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var first = f.Current; var second = await f.AdmitSecondAsync();
            var firstStorage = RequireCoherent(f); var secondStorage = Assert.IsType<LegacyNativeSpellStorage>(second.Native.MySpells);
            var firstMap = firstStorage.ClientSpells; var secondMap = secondStorage.ClientSpells;
            var firstRows = first.PlayerSpells.Spells.Select(s => s.Id == 6001 ? s with { Experience = 2222 } : s).ToArray();
            var firstNext = first with { PlayerSpells = new PlayerSpellSnapshot(firstRows) };
            var secondNext = invalidSecond
                ? second.Session with { PlayerSpells = new PlayerSpellSnapshot([new(0, 0, 0, 0, 0, 0)]) }
                : second.Session;
            byte[] character = File.ReadAllBytes(f.CharacterPath), spellBefore = File.ReadAllBytes(f.SpellPath);
            byte[] spellAfter = PlayerSpellSnapshotLoader.ToLegacyBytes(firstRows);
            LegacyTradeJournalChange[] changes = [
                new(LegacyTradeJournalFileKind.Character, first.AccountId, character, character),
                new(LegacyTradeJournalFileKind.Spells, first.Character.Uid, spellBefore, spellAfter)];
            bool writerCalled = false;
            bool committed = false; WorldSession? finalFirst = null, finalSecond = null;
            bool Commit() => f.Store.TryCommitPair(f.Connection, first, firstNext, second.Connection, second.Session, secondNext, () =>
            {
                writerCalled = true;
                return LegacyTradeJournal.TryCommitCharacterSpells(f.Users, f.Spells, changes, out _);
            }, out finalFirst, out finalSecond);
            if (invalidSecond) Assert.Throws<InvalidOperationException>(() => Commit());
            else committed = Commit();
            if (invalidSecond)
            {
                Assert.False(committed); Assert.Null(finalFirst); Assert.Null(finalSecond); Assert.False(writerCalled); Assert.Same(first, f.Current);
                Assert.True(f.Store.TryGet(second.Connection, out var stillSecond)); Assert.Same(second.Session, stillSecond);
                Assert.Same(firstMap, firstStorage.ClientSpells); Assert.Same(secondMap, secondStorage.ClientSpells);
                Assert.Equal(spellBefore, File.ReadAllBytes(f.SpellPath));
            }
            else
            {
                Assert.True(committed); Assert.True(writerCalled); Assert.Same(f.Current, finalFirst); Assert.Same(second.Session, finalSecond); Assert.Equal(spellAfter, File.ReadAllBytes(f.SpellPath));
                Assert.Equal(2222, Assert.Single(f.Current.PlayerSpells.Spells, s => s.Id == 6001).Experience);
                RequireCoherent(f);
                Assert.True(f.Store.TryGet(second.Connection, out var stillSecond)); Assert.Same(second.Session, stillSecond);
                Assert.Same(secondMap, secondStorage.ClientSpells);
                var native = Assert.Single(secondMap.ToArray(), p => p.Key == 7001).Value;
                Assert.Equal(50, native.Experience); Assert.Equal(1, native.UseSpellSoul);
            }
        });
    }
}
