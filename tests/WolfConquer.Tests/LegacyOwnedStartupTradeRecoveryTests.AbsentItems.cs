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
        var foreignUid = LegacyOwnedStartupTradeRecoveryFixture.RightUid + 99;
        var foreignPath = f.ItemsPath(foreignUid);
        var foreign = PlayerItemSnapshot.Empty with { InventoryItems = [conflicting] };
        var before = f.Data.Files();
        Assert.False(File.Exists(foreignPath));
        var refused = Assert.Throws<IOException>(() =>
            new PlayerItemSnapshotLoader(f.Data.Items).SaveByCharacterUid(foreignUid, foreign));
        Assert.Contains("ordinaryAdmissionRecoveryRequired:pendingConfiguredJournalWrite", refused.Message,
            StringComparison.Ordinal);
        f.Data.AssertFiles(before);
        Assert.False(File.Exists(foreignPath));

        // Model an external/corrupt physical owner after the real writer refused.
        // Create a new owned-fixture file; no runtime or journal authority is granted.
        var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(foreign);
        using (var file = new FileStream(foreignPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            file.Write(bytes);
        Assert.Equal(bytes, File.ReadAllBytes(foreignPath));
        Assert.Equal(new[] { LegacyOwnedStartupTradeRecoveryFixture.LeftItemUid },
            LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(bytes));
        Assert.False(File.Exists(f.ItemsPath(LegacyOwnedStartupTradeRecoveryFixture.RightUid)));
        f.AssertRefused();

        // Keep the same foreign path and row, changing only the physical UID.
        // It is demonstrably unused by every other physical Items image and
        // lies below the actual fixture's already-established high-water mark.
        const uint safeUid = 1_000_093;
        Assert.InRange(safeUid, 1U, LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor);
        var otherUids = Directory.GetFiles(f.Data.Items, "*.bin")
            .Where(path => !string.Equals(path, foreignPath,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            .SelectMany(path => LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(File.ReadAllBytes(path)))
            .ToArray();
        Assert.DoesNotContain(safeUid, otherUids);
        var safe = PlayerItemSnapshot.Empty with { InventoryItems = [conflicting.WithUid(safeUid)] };
        var safeBytes = PlayerItemSnapshotLoader.ToLegacyBytes(safe);
        File.WriteAllBytes(foreignPath, safeBytes);
        Assert.Equal(new[] { safeUid }, LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(File.ReadAllBytes(foreignPath)));
        var runtime = f.Open();
        f.AssertRecoveredImages();
        Assert.Equal(safeBytes, File.ReadAllBytes(foreignPath));
        await f.AssertFreshFrameAsync(runtime);
        await f.CloseAsync();
        var settled = f.Data.Files();
        f.Open();
        f.AssertRecoveredImages();
        f.Data.AssertFiles(settled);
        Assert.Equal(safeBytes, File.ReadAllBytes(foreignPath));
    }, reciprocalItems: false, absentRightItems: true);
}
