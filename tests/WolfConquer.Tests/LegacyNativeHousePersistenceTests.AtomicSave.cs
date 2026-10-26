using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHousePersistenceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ValidCurrentHouseSaveReplacesExactCompleteFileAndReloadsAllFurniture(int count)
    {
        using var state = new HouseState();
        using var files = new NativeSaveRecordFiles();
        var path = SeedHouse(files, HouseBytes());
        var client = HouseSaveClient(count);
        var live = CaptureHouseSaveState(client);
        var expected = ExpectedHouseSaveBytes(client.MyHouse!);
        var expectedFiles = CaptureHouseSaveFiles(files);
        expectedFiles[Path.GetRelativePath(files.Root, path)] = expected;

        LegacyNativeHouseRuntime.Save(client);

        Assert.Equal(5 + 28 * count, expected.Length);
        AssertHouseSaveFiles(files, expectedFiles);
        AssertHouseSaveState(client, live);
        AssertHouseSaveReload(client, expected);
    }

    [Theory]
    [InlineData("null-client")]
    [InlineData("null-player")]
    [InlineData("foreign-player")]
    [InlineData("zero-player-uid")]
    [InlineData("changed-player-uid")]
    [InlineData("foreign-house")]
    [InlineData("missing-poll")]
    [InlineData("stale-house")]
    [InlineData("wrong-poll-owner")]
    [InlineData("level-zero")]
    [InlineData("level-seven")]
    [InlineData("null-furniture")]
    [InlineData("key-mismatch")]
    [InlineData("null-row")]
    [InlineData("zero-row-uid")]
    public void InvalidHouseSaveRejectsBeforeChangingPreviousCompleteFileOrLiveOwners(string invalid)
    {
        using var state = new HouseState();
        using var files = new NativeSaveRecordFiles();
        SeedHouse(files, HouseBytes());
        var client = HouseSaveClient(1);
        MakeHouseSaveInvalid(client, invalid);
        var live = CaptureHouseSaveState(client);
        var before = CaptureHouseSaveFiles(files);

        Assert.Throws<InvalidOperationException>(() => LegacyNativeHouseRuntime.Save(invalid == "null-client" ? null! : client));

        AssertHouseSaveFiles(files, before);
        AssertHouseSaveState(client, live);
    }

    [WindowsTradeJournalFailureFact]
    public void HouseSaveReadSharedLeafReportsIoFailurePreservesBytesAndSameOwnerRetries() =>
        AssertHouseSaveSharingRefusal(FileShare.Read);

    [WindowsTradeJournalFailureFact]
    public void HouseSaveExclusiveLeafReportsIoFailurePreservesBytesAndSameOwnerRetries() =>
        AssertHouseSaveSharingRefusal(FileShare.None);

    [Fact]
    public void FirstHouseSaveCreatesOnlyCurrentOwnersMissingLeafAndReloadsWithoutTouchingForeignFile()
    {
        using var state = new HouseState();
        using var files = new NativeSaveRecordFiles();
        var path = Path.Combine(files.Root, "Houses", "17.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(Path.Combine(files.Root, "Houses", "99.bin"), HouseBytes());
        var client = HouseSaveClient(2);
        var live = CaptureHouseSaveState(client);
        var expected = ExpectedHouseSaveBytes(client.MyHouse!);
        var expectedFiles = CaptureHouseSaveFiles(files);
        Assert.False(File.Exists(path));
        expectedFiles[Path.GetRelativePath(files.Root, path)] = expected;

        LegacyNativeHouseRuntime.Save(client);

        AssertHouseSaveFiles(files, expectedFiles);
        AssertHouseSaveState(client, live);
        AssertHouseSaveReload(client, expected);
    }

    [Fact]
    public void AbsentHouseSaveIsNoOpAndDoesNotEraseExistingOwnerFileOrPublishHouse()
    {
        using var state = new HouseState();
        using var files = new NativeSaveRecordFiles();
        SeedHouse(files, HouseBytes());
        var client = new LegacyNativeGameClientRuntime(); client.Player.UID = 17;
        Assert.Null(client.MyHouse);
        var before = CaptureHouseSaveFiles(files);
        var live = CaptureHouseSaveState(client);

        LegacyNativeHouseRuntime.Save(client);

        AssertHouseSaveFiles(files, before);
        AssertHouseSaveState(client, live);
    }

    private static void AssertHouseSaveSharingRefusal(FileShare sharing)
    {
        using var state = new HouseState();
        using var files = new NativeSaveRecordFiles();
        var path = SeedHouse(files, HouseBytes());
        var client = HouseSaveClient(2);
        var before = CaptureHouseSaveFiles(files);
        var live = CaptureHouseSaveState(client);
        var expected = ExpectedHouseSaveBytes(client.MyHouse!);
        using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, sharing))
        {
            // Real synchronous I/O refusal. Never release the lease to manufacture completion.
            Assert.ThrowsAny<IOException>(() => LegacyNativeHouseRuntime.Save(client));
        }
        AssertHouseSaveFiles(files, before);
        AssertHouseSaveState(client, live);

        // No rebind, house reconstruction, or filesystem repair between attempts.
        LegacyNativeHouseRuntime.Save(client);

        before[Path.GetRelativePath(files.Root, path)] = expected;
        AssertHouseSaveFiles(files, before);
        AssertHouseSaveState(client, live);
        AssertHouseSaveReload(client, expected);
    }
}
