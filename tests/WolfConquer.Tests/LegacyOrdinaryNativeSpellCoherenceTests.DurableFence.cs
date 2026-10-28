using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    [Theory]
    [InlineData("row")]
    [InlineData("key")]
    [InlineData("resize")]
    public async Task RealCommittedJournalFollowedByRawMapConflictFencesRecoveryAndRetiresHolder(string fault)
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); f.RequireCommitObserved();
            var before = f.Current; var storage = RequireCoherent(f); var map = storage.ClientSpells;
            var nextRows = before.PlayerSpells.Spells.Select(s => s.Id == 6001 ? s with { Experience = 2222 } : s).ToArray();
            var next = before with { PlayerSpells = new PlayerSpellSnapshot(nextRows) };
            byte[] character = File.ReadAllBytes(f.CharacterPath), oldSpells = File.ReadAllBytes(f.SpellPath);
            byte[] newSpells = PlayerSpellSnapshotLoader.ToLegacyBytes(nextRows);
            LegacyTradeJournalChange[] changes = [
                new(LegacyTradeJournalFileKind.Character, before.AccountId, character, character),
                new(LegacyTradeJournalFileKind.Spells, before.Character.Uid, oldSpells, newSpells)];
            Assert.True(f.Recovery.IsReadyForCharacterSpells(f.Users, f.Spells));
            bool wrote = false;
            var ex = Assert.Throws<LegacyCommittedSpellPublicationException>(() =>
                f.Store.TryCommitSession(f.Connection, before, next, () =>
                {
                    Assert.True(LegacyTradeJournal.TryCommitCharacterSpells(f.Users, f.Spells, changes, out var reason), reason);
                    wrote = true;
                    if (fault == "row")
                    {
                        var prior = map[6001];
                        map[6001] = new LegacyNativeMsgSpell { ID = prior.ID, Level = prior.Level, Experience = prior.Experience,
                            PreviousLevel = prior.PreviousLevel, SoulLevel = prior.SoulLevel, UseSpellSoul = prior.UseSpellSoul };
                    }
                    else if (fault == "key")
                    {
                        Assert.True(map.TryRemove(6001, out var prior));
                        Assert.True(map.TryAdd(6002, prior));
                    }
                    else
                    {
                        // Force table growth before final enumeration, then restore count while replacing a raw row.
                        for (ushort id = 10000; id < 11000; id++) Assert.True(map.TryAdd(id, new LegacyNativeMsgSpell { ID = id }));
                        for (ushort id = 10000; id < 11000; id++) Assert.True(map.TryRemove(id, out _));
                        var prior = map[6001];
                        map[6001] = new LegacyNativeMsgSpell { ID = prior.ID, Level = prior.Level, Experience = prior.Experience,
                            PreviousLevel = prior.PreviousLevel, SoulLevel = prior.SoulLevel, UseSpellSoul = prior.UseSpellSoul };
                    }
                    return true;
                }));
            Assert.NotNull(ex); Assert.True(wrote);
            Assert.Equal(newSpells, File.ReadAllBytes(f.SpellPath));
            Assert.False(f.Recovery.IsReadyForCharacterSpells(f.Users, f.Spells));
            Assert.False(f.Store.TryGet(f.Connection, out _));
            Assert.False(f.Store.TryCompareAndSet(f.Connection, before, next));
        });
    }
}
