using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using RankType = WolfConquer.Core.Data.LegacyNativeNinjaRank.Type;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Fact]
    public Task UpdateRankPreservesAsymmetricThresholdsWindTypeAndIndependentNativeCopies() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Client.Player.Name = "Ninja";
            scope.SetLevel(300);
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
                RankType.Overall, RankType.Lightning }, entries.Select(entry => entry.Type));
            Assert.All(entries, entry =>
            {
                Assert.Equal(17u, entry.UID); Assert.Equal("Ninja", entry.Name);
                Assert.Equal(5000u, entry.Class); Assert.Equal((byte)44, entry.Level);
                Assert.Equal(123u, entry.HairStyle); Assert.Equal(456u, entry.Wing); Assert.Equal(789u, entry.Head);
            });
            entries[4].Name = "only wind";
            Assert.Equal("Ninja", overall.Name);
            Assert.Equal("Ninja", entries[1].Name);
            scope.Client.Player.Name = "changed owner";
            Assert.Equal("Ninja", overall.Name);
            Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available); scope.Untaken(0);
        });

    [Fact]
    public Task LowScoreNinjaKeepsStaleEntriesButChangingOutOfNinjaRemovesOnlyThatUidFromEveryRank() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            foreach (RankType type in Enum.GetValues<RankType>().Where(type => type != RankType.Count))
            {
                scope.Rank(type).UpdateItem(new LegacyNativeNinjaRank.Entry { UID = 17, Type = type, TotalPoints = 2000 });
                scope.Rank(type).UpdateItem(new LegacyNativeNinjaRank.Entry { UID = 18, Type = type, TotalPoints = 3000 });
            }
            var stale = scope.Rank(RankType.Overall).RankingList[17];
            scope.Ninja.FireMastery = 999;
            scope.Ninja.UpdateRank();
            Assert.Same(stale, scope.Rank(RankType.Overall).RankingList[17]);
            Assert.All(LegacyNativeNinjaRank.Ranks.Values, rank => Assert.Equal(2, rank.RankingList.Count));
            scope.SetClass(4999);
            scope.Ninja.Items = null!; // Non-Ninja branch never attempts Score/Items.
            scope.Ninja.UpdateRank();
            Assert.All(LegacyNativeNinjaRank.Ranks.Values, rank => Assert.Equal(18u, Assert.Single(rank.RankingList).Key));
            Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available);
        });

    [Fact]
    public Task UpdateRankScoresItsReceiverRatherThanClientMyNinjaAndDoesNotTakePackets() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Ninja.FireMastery = 1000;
            scope.Client.MyNinja = new LegacyNativeNinja(scope.Client) { FireMastery = 9 };
            scope.Ninja.UpdateRank();
            Assert.Equal(1000u, scope.Rank(RankType.Overall).RankingList[17].TotalPoints);
            Assert.Equal(1000u, scope.Rank(RankType.Fire).RankingList[17].TotalPoints);
            Assert.Equal(9u, scope.Client.MyNinja.FireMastery);
            Assert.Equal(12, scope.Available); scope.Untaken(0); Assert.Empty(scope.Queue);
        });

    [Fact]
    public Task MissingElementRankFaultsAfterOverallPublicationWithoutRollingItBack() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Ninja.FireMastery = 1000;
            LegacyNativeNinjaRank.Ranks.Remove(RankType.Fire);
            Assert.Throws<NullReferenceException>(() => scope.Ninja.UpdateRank());
            Assert.Equal(1000u, scope.Rank(RankType.Overall).RankingList[17].TotalPoints);
            Assert.False(LegacyNativeNinjaRank.Ranks.ContainsKey(RankType.Fire));
            Assert.Equal(12, scope.Available); scope.Untaken(0); Assert.Empty(scope.Queue);
        });
}
