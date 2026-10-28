using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyCharacterItemsSpellsJournalTests
{
    [Fact]
    public void PairAndCharacterSpellsOnlyRecoveryNeverAuthorizeThreeRootDomain()
    {
        Run(f =>
        {
            var pair = new LegacyTradeRecoveryState(f.Users, f.Items);
            Assert.True(pair.TryEnsureInitialized(out var reason), reason);
            Assert.False(pair.IsReadyForCharacterItemsSpells(f.Users, f.Items, f.Spells));
            var v8 = new LegacyTradeRecoveryState(f.Users, null, null, null, null, f.Spells, null);
            Assert.True(v8.TryEnsureInitialized(out reason), reason);
            Assert.False(v8.IsReadyForCharacterItemsSpells(f.Users, f.Items, f.Spells));
            var actual = f.Recovery(); Assert.True(actual.TryEnsureInitialized(out reason), reason);
            Assert.True(actual.IsReadyForCharacterItemsSpells(f.Users, f.Items, f.Spells));
            Assert.False(actual.IsReadyForCharacterItemsSpells(f.Users, f.Items, Path.Combine(f.Root, "OtherSpells")));
            Assert.False(actual.IsReadyForCharacterItemsSpells(f.Users, Path.Combine(f.Root, "OtherItems"), f.Spells));
        });
    }
}
