using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    [Fact]
    public async Task ExpectedPublicationFreezesMutableInputAndPreservesNativeTransientFields()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); f.RequireCommitObserved();
            var original = f.Current;
            var storage = RequireCoherent(f);
            var row = Assert.Single(storage.ClientSpells.ToArray(), p => p.Key == 6001).Value;
            row.ColdTime = new DateTime(2024, 2, 3, 4, 5, 6, DateTimeKind.Utc);
            row.StampSpell = new DateTime(2024, 3, 4, 5, 6, 7, DateTimeKind.Utc);
            row.IsSpellWithColdTime = true; row.PacketStamp = 123456; row.Size = 71;
            row.PacketType = 2101; row.Type = 42; row.UnKnow = 39; row.Soul = 7;
            int hash = row.HashLevelSoul;
            var proposedRows = original.PlayerSpells.Spells.Select(s => s.Id == 6001 ? s with { Experience = 2222, UseSpellSoul = 4 } : s).ToList();
            var proposal = original with { PlayerSpells = new PlayerSpellSnapshot(proposedRows) };
            Assert.True(f.Store.TryCompareAndSet(f.Connection, original, proposal));
            var committed = f.Current;
            var frozen = Assert.Single(committed.PlayerSpells.Spells, s => s.Id == 6001);
            Assert.Equal(2222, frozen.Experience); Assert.Equal(4, frozen.UseSpellSoul);
            proposedRows[0] = proposedRows[0] with { Experience = 9999 };
            proposedRows.Clear();
            Assert.Equal(frozen, Assert.Single(f.Current.PlayerSpells.Spells, s => s.Id == 6001));
            var changed = Assert.Single(RequireCoherent(f).ClientSpells.ToArray(), p => p.Key == 6001).Value;
            Assert.Equal(row.ColdTime, changed.ColdTime); Assert.Equal(row.StampSpell, changed.StampSpell);
            Assert.True(changed.IsSpellWithColdTime); Assert.Equal(123456, changed.PacketStamp);
            Assert.Equal(71, changed.Size); Assert.Equal(2101, changed.PacketType);
            Assert.Equal(42, changed.Type); Assert.Equal(39, changed.UnKnow); Assert.Equal(7, changed.Soul);
            Assert.Equal(hash, changed.HashLevelSoul);
            Assert.False(f.Store.TryCompareAndSet(f.Connection, original, original));
            Assert.Same(committed, f.Current); RequireCoherent(f);
        });
    }

    [Fact]
    public async Task EquivalentMembershipCarriesRealRowsAndStaleUnrelatedSetCannotRestoreOldSpells()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var original = f.Current;
            var originalMap = RequireCoherent(f).ClientSpells;
            var originalRow = Assert.Single(originalMap.ToArray(), p => p.Key == 6001).Value;
            originalRow.PacketStamp = 76543;
            var equivalent = original with { PlayerSpells = new PlayerSpellSnapshot(original.PlayerSpells.Spells.ToArray()) };
            Assert.True(f.Store.TryCompareAndSet(f.Connection, original, equivalent));
            var equalCurrent = f.Current;
            Assert.Same(originalRow, Assert.Single(RequireCoherent(f).ClientSpells.ToArray(), p => p.Key == 6001).Value);
            var changed = equalCurrent with { PlayerSpells = new PlayerSpellSnapshot(equalCurrent.PlayerSpells.Spells.Select(s => s.Id == 6001 ? s with { Experience = 3333 } : s).ToArray()) };
            Assert.True(f.Store.TryCompareAndSet(f.Connection, equalCurrent, changed));
            var current = f.Current;
            Assert.Throws<InvalidOperationException>(() => f.Store.Set(f.Connection, original));
            Assert.Same(current, f.Current); Assert.Equal(3333, Assert.Single(current.PlayerSpells.Spells, s => s.Id == 6001).Experience);
            Assert.Equal(76543, Assert.Single(RequireCoherent(f).ClientSpells.ToArray(), p => p.Key == 6001).Value.PacketStamp);
        });
    }

    [Theory]
    [InlineData("zero")]
    [InlineData("duplicate")]
    [InlineData("negative")]
    [InlineData("soul")]
    public async Task ExpectedInvalidMembershipCannotChangeGenuineOwnerOrFiles(string fault)
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var before = f.Current; var storage = RequireCoherent(f);
            var map = storage.ClientSpells; var rows = map.ToArray(); var files = f.Files(); int sends = f.Sent.Count;
            var bad = before.PlayerSpells.Spells.ToList();
            var first = bad[0];
            switch (fault)
            {
                case "zero": bad[0] = first with { Id = 0 }; break;
                case "negative": bad[0] = first with { Experience = -1 }; break;
                case "soul": bad[0] = first with { UseSpellSoul = 5 }; break;
                case "duplicate": bad.Add(first); break;
            }
            Assert.Throws<InvalidOperationException>(() => f.Store.TryCompareAndSet(f.Connection, before, before with { PlayerSpells = new PlayerSpellSnapshot(bad) }));
            Assert.Same(before, f.Current); Assert.Same(map, storage.ClientSpells);
            Assert.Equal(rows, map.ToArray()); f.RequireFiles(files); Assert.Equal(sends, f.Sent.Count);
        });
    }
}
