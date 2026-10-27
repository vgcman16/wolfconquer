using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    public void RebornSafetySelectedCleanupPreservesSharedWorldItemsAndEveryUnrelatedOwner(byte reborn) =>
        RunPrestigeMutationSafety(() =>
        {
            var user = RebornUser(reborn);
            var selected = RebornSelectedOwner(user);
            var world = new Ranking.Rank(Ranking.Type.World) { MaxItems = 100, Items = selected.Items,
                BestOfClass = selected.BestOfClass };
            Ranking.Ranks.Add(Ranking.Type.World, world);
            var unrelated = new Ranking.Rank(Ranking.Type.Archer) { MaxItems = 1 };
            unrelated.Items.Add(77, null!); // Not consumed by this selected-class API.
            Ranking.Ranks.Add(Ranking.Type.Archer, unrelated);
            Ranking.Ranks.Add(Ranking.Type.Count, null!); // No blanket registry normalization.
            _ = Ranking.Ranks.GetValues();
            var before = new PrestigeRebornGraphImage();
            var clientBefore = new PrestigeRebornUserImage(user);
            var oldItems = selected.Items;
            var removed = selected.Items[41];
            var next = selected.Items[42];

            Ranking.CheckReborn(user);

            Assert.Equal(new uint[] { 42 }, selected.Items.Keys);
            Assert.Same(next, selected.BestOfClass);
            Assert.NotSame(oldItems, selected.Items);
            Assert.Same(oldItems, world.Items);
            Assert.Same(removed, world.Items[41]);
            Assert.Same(removed, world.BestOfClass);
            before.RequireOnlySelectedChanged(selected);
            clientBefore.RequireUnchanged();
        });

    [Theory]
    [InlineData((byte)1, 1051u, 1u)]
    [InlineData((byte)1, 1052u, 1u)]
    [InlineData((byte)1, 1057u, 1u)]
    [InlineData((byte)2, 2000u, 2u)]
    [InlineData((byte)2, 2057u, 2u)]
    [InlineData((byte)1, 13057u, 8u)]
    public void RebornSafetyUsesTargetKnownHistoricalFamiliesThroughSuffix57(byte reborn, uint historicalClass, uint category) =>
        RunPrestigeMutationSafety(() =>
        {
            var user = RebornUser(reborn, firstClass: historicalClass, secondClass: historicalClass);
            var selected = RebornSelectedOwner(user);
            Assert.Equal((Ranking.Type)category, selected._Type);
            Assert.Single(Ranking.Ranks); // No World or unrelated class owner is required.
            var before = new PrestigeRebornGraphImage();

            Ranking.CheckReborn(user);

            Assert.Equal(new uint[] { 42 }, selected.Items.Keys);
            Assert.Equal(0u, Ranking.GetMyRank((Ranking.Type)category, 41));
            before.RequireOnlySelectedChanged(selected);
        });

    [Fact]
    public void RebornSafetyCurrentIncludedFamilyPreservesItsBucketWorldAndClientExactly() =>
        RunPrestigeMutationSafety(() =>
        {
            var user = RebornUser(1);
            SetRebornCurrentClass(user, 1001);
            Assert.Equal(Ranking.Type.Trojan, Ranking.GetIndex(user.Player.Class));
            var selected = RebornSelectedOwner(user);
            var world = new Ranking.Rank(Ranking.Type.World) { MaxItems = 100, Items = selected.Items,
                BestOfClass = selected.BestOfClass };
            Ranking.Ranks.Add(Ranking.Type.World, world);
            var before = new PrestigeRebornGraphImage();
            var clientBefore = new PrestigeRebornUserImage(user);

            Ranking.CheckReborn(user);

            Assert.True(selected.Items.ContainsKey(41));
            Assert.Equal(1001u, user.Player.Class);
            before.RequireUnchanged();
            clientBefore.RequireUnchanged();
        });

    [Theory]
    [InlineData((byte)1, 1001u, 1000u)]
    [InlineData((byte)1, 1052u, 1000u)]
    [InlineData((byte)2, 2001u, 2000u)]
    [InlineData((byte)2, 2057u, 2000u)]
    public void RebornSafetyCurrentIncludedFamilyDoesNotConsultRankOrGate(byte reborn, uint currentClass, uint previousClass) =>
        RunPrestigeMutationSafety(() =>
        {
            var user = RebornUser(reborn, firstClass: previousClass, secondClass: previousClass);
            SetRebornCurrentClass(user, currentClass);
            RebornSelectedOwner(user);
            var escaped = Ranking.Ranks;
            Ranking.SynRoot = null!;
            Ranking.Ranks = null!;
            var before = new PrestigeRebornGraphImage(escaped);
            var clientBefore = new PrestigeRebornUserImage(user);

            var error = Record.Exception(() => Ranking.CheckReborn(user));

            before.RequireUnchanged();
            clientBefore.RequireUnchanged();
            Assert.Null(error);
        });

    [Theory]
    [InlineData(10000u)]
    [InlineData(10001u)]
    [InlineData(10002u)]
    public void RebornSafetyLiteralWorldOnlyCurrentClassesStillCleanTheOldWaterFamily(uint currentClass) =>
        RunPrestigeMutationSafety(() =>
        {
            var user = RebornUser(1, firstClass: 13002);
            SetRebornCurrentClass(user, currentClass);
            user.PrestrigeEntry!.Class = 13002;
            user.PrestrigeEntry.type = Ranking.Type.WaterTao;
            var selected = RebornSelectedOwner(user);
            var current = Entry(41, 60_000);
            current.Class = currentClass;
            current.type = Ranking.Type.World;
            user.PrestrigeEntry = current;
            user.MyPrestigePoints = current.TotalPoints;
            var world = new Ranking.Rank(Ranking.Type.World) { MaxItems = 100 };
            world.AddItem(current);
            Ranking.Ranks.Add(Ranking.Type.World, world);
            var before = new PrestigeRebornGraphImage();
            var clientBefore = new PrestigeRebornUserImage(user);

            Ranking.CheckReborn(user);

            Assert.Equal(new uint[] { 42 }, selected.Items.Keys);
            Assert.Same(current, world.Items[41]);
            Assert.Same(current, world.BestOfClass);
            before.RequireOnlySelectedChanged(selected);
            clientBefore.RequireUnchanged();
        });

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)3)]
    [InlineData(byte.MaxValue)]
    public void RebornSafetyOtherRebornValuesDoNotConsultUidGateOrRegistry(byte reborn) =>
        RunPrestigeMutationSafety(() =>
        {
            var user = RebornUser(reborn, uid: 0);
            var escaped = Ranking.Ranks;
            Ranking.SynRoot = null!;
            Ranking.Ranks = null!;
            var before = new PrestigeRebornGraphImage(escaped);
            var clientBefore = new PrestigeRebornUserImage(user);

            var error = Record.Exception(() => Ranking.CheckReborn(user));

            before.RequireUnchanged();
            clientBefore.RequireUnchanged();
            Assert.Null(error);
        });
}
