using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

public sealed partial class CapricePairCommitOwnedTests
{
    [Fact]
    public Task CapricePairActualConsolidationRunePreservesOriginalAllowedTransferAndThunderDamage() =>
        F.CapricePairConsolidationRunAsync(async (f, world) =>
        {
            await F.CapricePairPrepareQueriesAsync(f, world);
            await F.CapricePairAttackAsync(f);
            Assert.Equal("60", f.Current(Actor).Character.Attributes["Stamina"]);
            Assert.Equal("60", f.Current(Peer).Character.Attributes["Stamina"]);
            Assert.Equal(5000L - 421, f.Current(Peer).Character.MinHitPoints);
            Assert.True(f.Current(Actor).Character.Attributes.ContainsKey("LegacyCapriceStampUtc"));
            Assert.Contains(f.Frames(Actor), bytes => Convert.ToHexString(bytes) == "0E00800808A0FA4E12040808103C");
            Assert.Contains(f.Frames(Peer), bytes => Convert.ToHexString(bytes) == "0E00800808A1FA4E12040808103C");
            RequireDurablePair(f, "60", "60"); Assert.False(f.Recovery.IsBlocked);
        });
}
