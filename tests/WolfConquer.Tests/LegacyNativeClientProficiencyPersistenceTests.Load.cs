using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeClientProficiencyPersistenceTests
{
    [Fact]
    public void LoadPreservesExistingRowsAndMergesValidDistinctFileRows()
    {
        using var files = new OwnedProfs();
        var image = Image(2, Row(21, 1), Row(uint.MaxValue, uint.MaxValue, byte.MaxValue, 0x80000000, uint.MaxValue));
        files.Seed(image);
        var client = Client();
        var existing = new LegacyNativeMsgProficiency { ID = 21, Level = 55, Experience = 77 };
        client.MyProfs!.ClientProf.TryAdd(21, existing);
        LegacyNativeServerDatabasePersistence.LoadClientProfs(client);
        Assert.Equal(2, client.MyProfs.ClientProf.Count);
        Assert.Same(existing, client.MyProfs.ClientProf[21]);
        var admitted = client.MyProfs.ClientProf[uint.MaxValue];
        Assert.Equal(uint.MaxValue, admitted.ID); Assert.Equal(uint.MaxValue, admitted.Level);
        Assert.Equal(byte.MaxValue, admitted.PreviouseLevel);
        Assert.Equal(0x80000000u, admitted.Experience); Assert.Equal(uint.MaxValue, admitted.NeededExperience);
        Assert.False(client.MyProfs.ClientProf.ContainsKey(99));
        Assert.Equal(image, File.ReadAllBytes(files.FilePath));
        AssertClosed(files.FilePath);
    }

    [Fact]
    public void ZeroCountClosesWithTheActualProficiencyOwner()
    {
        foreach (var image in new[] { Image(0) })
        {
            using var files = new OwnedProfs();
            files.Seed(image);
            var client = Client();
            LegacyNativeServerDatabasePersistence.LoadClientProfs(client);
            Assert.Empty(client.MyProfs!.ClientProf);
            Assert.Equal(image, File.ReadAllBytes(files.FilePath));
            AssertClosed(files.FilePath);
        }
    }

    [Fact]
    public void MalformedImagesRefuseBeforePublishingAnyRowsAndCloseTheActualFile()
    {
        var images = new[]
        {
            Array.Empty<byte>(), new byte[] { 1 }, Image(-1), Image(int.MinValue), Image(int.MaxValue),
            Image(1), Image(2, Row(11, 1), BitConverter.GetBytes(88u)),
            Image(1, Row(0, 1)), Image(2, Row(11, 1), Row(11, 2)),
            Image(1, Row(11, 1), Row(12, 2)),
        };
        foreach (var image in images)
        {
            using var files = new OwnedProfs();
            files.Seed(image);
            var client = Client();
            var old = new LegacyNativeMsgProficiency { ID = 21, Level = 55, Experience = 77 };
            client.MyProfs!.ClientProf.TryAdd(21, old);
            var map = client.MyProfs.ClientProf;
            var file = files.Open(FileMode.Open);
            Assert.Throws<InvalidDataException>(() => LegacyNativeServerDatabasePersistence.LoadClientProfsOpenedFile(file, client));
            Assert.Same(map, client.MyProfs.ClientProf);
            Assert.Same(old, Assert.Single(map).Value);
            Assert.Equal(55u, old.Level); Assert.Equal(77u, old.Experience);
            Assert.Equal(image, File.ReadAllBytes(files.FilePath));
            AssertClosed(files.FilePath);
        }
    }
    [Fact]
    public void MissingFilesAndExclusiveOpenFailWithoutChangesForActualOwner()
    {
        using (var missing = new OwnedProfs(createDirectory: false))
        {
            var client = Client();
            LegacyNativeServerDatabasePersistence.LoadClientProfs(client);
            LegacyNativeServerDatabasePersistence.SaveClientProfs(client);
            Assert.Empty(client.MyProfs!.ClientProf);
            Assert.False(Directory.Exists(missing.DirectoryPath));
        }
        using var files = new OwnedProfs();
        var owner = Client();
        LegacyNativeServerDatabasePersistence.LoadClientProfs(owner);
        Assert.False(File.Exists(files.FilePath));
        var original = Image(1, Row(9, 4));
        files.Seed(original);
        using (var locked = new FileStream(files.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            LegacyNativeServerDatabasePersistence.LoadClientProfs(owner);
            Assert.Throws<IOException>(() => LegacyNativeServerDatabasePersistence.SaveClientProfs(owner));
        }
        Assert.Equal(original, File.ReadAllBytes(files.FilePath));
        Assert.Empty(owner.MyProfs!.ClientProf);
    }

    [Fact]
    public void MissingOwnerRefusesBeforeOpeningOrTruncatingAnExistingFile()
    {
        using var files = new OwnedProfs();
        var original = Image(1, Row(9, 4));
        files.Seed(original);
        var client = Client(profs: false);
        Assert.Throws<InvalidOperationException>(() => LegacyNativeServerDatabasePersistence.LoadClientProfs(client));
        Assert.Throws<InvalidOperationException>(() => LegacyNativeServerDatabasePersistence.SaveClientProfs(client));
        Assert.Null(client.MyProfs);
        Assert.Equal(original, File.ReadAllBytes(files.FilePath));
        AssertClosed(files.FilePath);
    }
    [Fact]
    public void OpenedLoadMissingOwnerRefusesBeforeReadingAndClosesOwnedFile()
    {
        using var files = new OwnedProfs();
        var original = Image(2, Row(11, 1), Row(12, 3, 4, 5, 6, 0xAA));
        files.Seed(original);
        var client = Client(profs: false);
        var file = files.Open(FileMode.Open);
        Assert.Throws<InvalidOperationException>(() => LegacyNativeServerDatabasePersistence.LoadClientProfsOpenedFile(file, client));
        Assert.Null(client.MyProfs);
        Assert.Equal(original, File.ReadAllBytes(files.FilePath));
        AssertClosed(files.FilePath);
    }
    [Fact]
    public void NullClientInBothPublicMethodsPropagatesBeforeAnyFileCanBeOpened()
    {
        using var files = new OwnedProfs();
        Assert.Throws<ArgumentNullException>(() => LegacyNativeServerDatabasePersistence.LoadClientProfs(null!));
        Assert.Throws<ArgumentNullException>(() => LegacyNativeServerDatabasePersistence.SaveClientProfs(null!));
        Assert.Empty(Directory.GetFileSystemEntries(files.DirectoryPath));
    }
}
