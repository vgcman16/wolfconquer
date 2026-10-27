using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using RankType = WolfConquer.Core.Data.LegacyNativeNinjaRank.Type;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Fact]
    public Task UpdateRankKeepsAsymmetricThresholdsAndMatchingIndependentCategoryRows() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Client.Player.Name = "Ninja";
            scope.SetLevel(140);
            scope.SetPlayerField("_hair", 123u);
            scope.Client.Player.SpecialWingID = 456;
            scope.Client.Equipment!.HeadID = 789;
            scope.Ninja.FireMastery = scope.Ninja.WaterMastery = scope.Ninja.EarthMastery = 1000;
            scope.Ninja.WindMastery = scope.Ninja.LightningMastery = 1000;
            scope.Ninja.UpdateRank();
            Assert.Equal(5000u, scope.Rank(RankType.Overall).RankingList[17].TotalPoints);
            foreach (RankType type in new[] { RankType.Fire, RankType.Water, RankType.Earth })
                Assert.Equal(1000u, scope.Rank(type).RankingList[17].TotalPoints);
            Assert.Empty(scope.Rank(RankType.Wind).RankingList);
            Assert.Empty(scope.Rank(RankType.Lightning).RankingList);

            scope.Ninja.WindMastery = scope.Ninja.LightningMastery = 1001;
            scope.Ninja.UpdateRank();
            var overall = scope.Rank(RankType.Overall).RankingList[17];
            Assert.Equal(5002u, overall.TotalPoints);
            var entries = Enum.GetValues<RankType>().Where(type => type != RankType.Count)
                .Select(type => scope.Rank(type).RankingList[17]).ToArray();
            Assert.Equal(6, entries.Distinct(ReferenceEqualityComparer.Instance).Count());
            Assert.Equal(new[] { RankType.Overall, RankType.Fire, RankType.Water, RankType.Earth,
                RankType.Wind, RankType.Lightning }, entries.Select(entry => entry.Type));
            Assert.All(entries, entry =>
            {
                Assert.Equal(17u, entry.UID); Assert.Equal("Ninja", entry.Name);
                Assert.Equal(5000u, entry.Class); Assert.Equal((byte)140, entry.Level);
                Assert.Equal(123u, entry.HairStyle); Assert.Equal(456u, entry.Wing); Assert.Equal(789u, entry.Head);
            });
            entries[4].Name = "only wind";
            Assert.Equal("Ninja", overall.Name);
            Assert.Equal("Ninja", entries[1].Name);
            scope.Client.Player.Name = "changed owner";
            Assert.Equal("Ninja", overall.Name);
            AssertNoRankingPackets(scope);
        });

    [Fact]
    public Task LowScoreAndKnownNonNinjaRefreshRemoveOnlyTheActiveUidFromEveryRank() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            SeedRankingRows(scope);
            scope.Ninja.FireMastery = 999;
            scope.Ninja.UpdateRank();
            Assert.All(LegacyNativeNinjaRank.Ranks.Values,
                rank => Assert.Equal(18u, Assert.Single(rank.RankingList).Key));
            SeedRankingRows(scope);
            scope.SetClass(4000);
            scope.Ninja.Items = null!;
            scope.Client.Equipment = null;
            scope.Client.MySpells = null;
            LegacyNativeNinjaFile.gouyu_type = null!;
            scope.Ninja.UpdateRank();
            Assert.All(LegacyNativeNinjaRank.Ranks.Values,
                rank => Assert.Equal(18u, Assert.Single(rank.RankingList).Key));
            AssertNoRankingPackets(scope);
        });

    [Fact]
    public Task DetachedReceiverRefusesWithoutUsingForeignClientMyNinjaAuthority() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            SeedRankingRows(scope);
            scope.Ninja.FireMastery = 1000;
            scope.Client.MyNinja = new LegacyNativeNinja(scope.Client) { FireMastery = 9 };
            var before = new RankingGraphWitness();
            Assert.Throws<InvalidOperationException>(() => scope.Ninja.UpdateRank());
            before.RequireUnchanged();
            Assert.Equal(9u, scope.Client.MyNinja.FireMastery);
            AssertNoRankingPackets(scope);
        });

    [Fact]
    public Task MissingElementRankRefusesBeforeAnyOverallOrOtherRankPublication() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            SeedRankingRows(scope);
            scope.Ninja.FireMastery = 1000;
            LegacyNativeNinjaRank.Ranks.Remove(RankType.Fire);
            var before = new RankingGraphWitness();
            Assert.Throws<InvalidOperationException>(() => scope.Ninja.UpdateRank());
            before.RequireUnchanged();
            AssertNoRankingPackets(scope);
        });
}
