using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStartupTradeRecoveryTests
{
    [Theory]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)] [InlineData(3, true)] [InlineData(4, true)]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)] [InlineData(3, false)] [InlineData(4, false)]
    public Task GenuineAbsentEmptyItemsTradeRecoversEveryPairPrefix(int boundary, bool complete) => WithFixture(async f =>
    {
        Assert.False(File.Exists(f.ItemsPath(LegacyOwnedStartupTradeRecoveryFixture.RightUid)));
        await f.StageAsync(boundary);
        await f.CloseAsync();
        var runtime = f.Open(); f.AssertRecoveredImages();
        await f.AssertFreshFrameAsync(runtime);
        await f.CloseAsync();
        var settled = f.Data.Files();
        f.Open(); f.AssertRecoveredImages(); f.Data.AssertFiles(settled);
    }, complete: complete, reciprocalItems: false, absentRightItems: true);

    [Fact]
    public Task CapturedPairUidCannotBeBorrowedByAnUnrelatedPhysicalOwnerWhenTargetIsAbsent() => WithFixture(async f =>
    {
        await f.StageAsync(0); await f.CloseAsync();
        Assert.False(File.Exists(f.ItemsPath(LegacyOwnedStartupTradeRecoveryFixture.RightUid)));
        var conflicting = new PlayerItemSnapshotItem
        { Uid = LegacyOwnedStartupTradeRecoveryFixture.LeftItemUid, ItemId = LegacyOwnedStartupTradeRecoveryFixture.ItemId, StackSize = 1 };
        new PlayerItemSnapshotLoader(f.Data.Items).SaveByCharacterUid(
            LegacyOwnedStartupTradeRecoveryFixture.RightUid + 99,
            PlayerItemSnapshot.Empty with { InventoryItems = [conflicting] });
        f.AssertRefused();
    }, reciprocalItems: false, absentRightItems: true);
}
