using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    [Theory]
    [InlineData((byte)1, false)]
    [InlineData((byte)1, true)]
    [InlineData((byte)2, false)]
    [InlineData((byte)2, true)]
    public void RebornSafetyHistoricalChampionOnlyTargetSelectsActualNextOrNull(byte reborn, bool hasSurvivor) =>
        RunPrestigeMutationSafety(() =>
        {
            var user = RebornUser(reborn);
            var rank = RebornSelectedOwner(user, survivor: hasSurvivor);
            var removed = rank.BestOfClass;
            var survivor = hasSurvivor ? rank.Items[42] : null;
            Assert.True(rank.Items.Remove(41)); // Owned historical raw-delete state.
            Assert.Same(removed, rank.BestOfClass);
            var before = new PrestigeRebornGraphImage();
            var clientBefore = new PrestigeRebornUserImage(user);

            Ranking.CheckReborn(user);

            Assert.Equal(hasSurvivor ? new uint[] { 42 } : [], rank.Items.Keys);
            Assert.Same(survivor, rank.BestOfClass);
            before.RequireOnlySelectedChanged(rank);
            clientBefore.RequireUnchanged();
        });

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    public void RebornSafetyAbsentUidPreservesItsMapAndUnrelatedStaleChampionExactly(byte reborn) =>
        RunPrestigeMutationSafety(() =>
        {
            var user = RebornUser(reborn);
            var rank = RebornSelectedOwner(user, targetPresent: false);
            var unrelated = Entry(77, 60_000);
            rank.BestOfClass = unrelated; // Neither Items nor champion exposes UID41.
            var before = new PrestigeRebornGraphImage();
            var clientBefore = new PrestigeRebornUserImage(user);

            Ranking.CheckReborn(user);

            before.RequireUnchanged();
            clientBefore.RequireUnchanged();
        });

    [Fact]
    public void RebornSafetySelectedRemovalKeepsStableUnsignedTiesCapsAndEscapedEntryArrays() =>
        RunPrestigeMutationSafety(() =>
        {
            var user = RebornUser(1);
            var rank = RebornSelectedOwner(user, survivor: false);
            rank.AddItem(Entry(42, uint.MaxValue));
            rank.AddItem(Entry(43, uint.MaxValue));
            rank.AddItem(Entry(44, 20_000));
            rank.MaxItems = 2; // Complete admission still scans every actual row.
            var first = rank.Items[42];
            var second = rank.Items[43];
            var before = new PrestigeRebornGraphImage();
            var clientBefore = new PrestigeRebornUserImage(user);

            Ranking.CheckReborn(user);

            Assert.Equal(new uint[] { 42, 43 }, rank.Items.Keys);
            Assert.Same(first, rank.Items[42]);
            Assert.Same(second, rank.Items[43]);
            Assert.Same(first, rank.BestOfClass);
            before.RequireOnlySelectedChanged(rank);
            clientBefore.RequireUnchanged();
        });

    [Fact]
    public void RebornSafetyAcceptsTheFullPositiveUInt32CharacterIdentity() =>
        RunPrestigeMutationSafety(() =>
        {
            var user = RebornUser(2, uid: uint.MaxValue);
            var rank = RebornSelectedOwner(user);
            var next = rank.Items[42];
            var before = new PrestigeRebornGraphImage();
            var clientBefore = new PrestigeRebornUserImage(user);

            Ranking.CheckReborn(user);

            Assert.Equal(new uint[] { 42 }, rank.Items.Keys);
            Assert.Same(next, rank.BestOfClass);
            before.RequireOnlySelectedChanged(rank);
            clientBefore.RequireUnchanged();
        });
}
