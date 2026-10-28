using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class WorldSessionFramePersistenceOwnerTests
{
    [Fact]
    public Task ActualFramePostludeStillPersistsBoundSpellChangesUnderItsIssuedStoreGate() =>
        WorldServerFrameHandlerTests.WithNativeSpellJoinAsync(async fixture =>
        {
            await fixture.LoginAsync(); fixture.RequireCommitObserved();
            var before = fixture.Current;
            var rows = before.PlayerSpells.Spells.Select(row => row.Id == 6001
                ? row with { Experience = row.Experience + 1 } : row).ToArray();
            var proposed = before with { PlayerSpells = new PlayerSpellSnapshot(rows) };

            Assert.True(fixture.Store.TryCompareAndSet(fixture.Connection, before, proposed, out var final));

            var actual = Assert.IsType<WorldSession>(final);
            Assert.Same(actual, fixture.Current);
            Assert.Equal(rows.OrderBy(row => row.Id), actual.PlayerSpells.Spells.OrderBy(row => row.Id));
            var bytes = File.ReadAllBytes(fixture.SpellPath);
            Assert.Equal(actual.PlayerSpells.Spells.OrderBy(row => row.Id),
                PlayerSpellSnapshotLoader.Parse(bytes).Spells.OrderBy(row => row.Id));
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(fixture.Native.OrdinarySpellBinding);
            Assert.True(binding.SavedSpells.Matches(bytes));
            Assert.True(fixture.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.False(fixture.Recovery.IsBlocked);
            Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
        }, lineage: false);
}
