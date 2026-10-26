using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHousePersistenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HouseSaveCannotRedirectConstructedOrLoadedOwnerToAnotherConfiguredRoot(bool loaded)
    {
        using var state = new HouseState();
        using var files = new NativeSaveRecordFiles();
        var originalPath = SeedHouse(files, HouseBytes());
        LegacyNativeGameClientRuntime client;
        if (loaded)
        {
            client = new LegacyNativeGameClientRuntime();
            client.Player.UID = 17;
            Assert.True(LegacyNativeHouseRuntime.TryLoad(client));
            client.MyHouse!.Level = 5;
        }
        else client = HouseSaveClient(1);
        var originalConfiguration = LegacyNativeServerConfig.DbLocation;
        var alternateRoot = Path.Combine(files.Root, "alternate");
        var alternatePath = Path.Combine(alternateRoot, "Houses", "17.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(alternatePath)!);
        var sentinel = HouseBytes();
        sentinel[0] = 4;
        File.WriteAllBytes(alternatePath, sentinel);
        var before = CaptureHouseSaveFiles(files);
        var live = CaptureHouseSaveState(client);
        var expected = ExpectedHouseSaveBytes(client.MyHouse!);

        try
        {
            LegacyNativeServerConfig.DbLocation = alternateRoot + Path.DirectorySeparatorChar;
            Assert.Throws<InvalidOperationException>(() => LegacyNativeHouseRuntime.Save(client));
            AssertHouseSaveFiles(files, before);
            AssertHouseSaveState(client, live);
        }
        finally { LegacyNativeServerConfig.DbLocation = originalConfiguration; }

        // The genuine original binding remains usable; no owner reconstruction.
        LegacyNativeHouseRuntime.Save(client);
        before[Path.GetRelativePath(files.Root, originalPath)] = expected;
        AssertHouseSaveFiles(files, before);
        AssertHouseSaveState(client, live);
    }

    [Fact]
    public void HouseSaveDoesNotSilentlyRepairOrReplaceMalformedExistingCompleteImage()
    {
        using var state = new HouseState();
        using var files = new NativeSaveRecordFiles();
        SeedHouse(files, HouseBytes()[..32]);
        var client = HouseSaveClient(1);
        var before = CaptureHouseSaveFiles(files);
        var live = CaptureHouseSaveState(client);

        Assert.Throws<InvalidOperationException>(() => LegacyNativeHouseRuntime.Save(client));

        AssertHouseSaveFiles(files, before);
        AssertHouseSaveState(client, live);
    }

    [Fact]
    public void CapturedCreatePublishesMissingLeafButNeverClaimsOrOverwritesAnExistingLeaf()
    {
        using var files = new NativeSaveRecordFiles();
        var path = Path.Combine(files.Root, "Houses", "17.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var original = HouseBytes();
        Assert.True(CharacterSnapshotFileCoordinator.TryCreateCapturedImage(path, original,
            out var uncertain, out var reason));
        Assert.False(uncertain);
        Assert.Empty(reason);
        var before = CaptureHouseSaveFiles(files);

        // Equality alone grants no ownership of an existing creator's leaf.
        Assert.False(CharacterSnapshotFileCoordinator.TryCreateCapturedImage(path, original,
            out uncertain, out reason));
        Assert.False(uncertain);
        Assert.Equal("capturedImageLeafAlreadyExists", reason);
        AssertHouseSaveFiles(files, before);

        var replacement = original.ToArray();
        replacement[0] = 5;
        Assert.False(CharacterSnapshotFileCoordinator.TryCreateCapturedImage(path, replacement,
            out uncertain, out reason));
        Assert.False(uncertain);
        Assert.Equal("capturedImageLeafAlreadyExists", reason);
        AssertHouseSaveFiles(files, before);
    }
}
