using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeClientProficiencyPersistenceTests
{
    [Fact]
    public void LoadPreservesExistingAliasesAndFirstDuplicateWhileHonoringTheDeclaredCount()
    {
        using var files = new OwnedProfs();
        var image = Image(4, Row(21, 1), Row(uint.MaxValue, uint.MaxValue, byte.MaxValue, 0x80000000, uint.MaxValue),
            Row(uint.MaxValue, 3), Row(21, 7), Row(99, 9));
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
    public void EmptyZeroAndNegativeCountsCloseWithoutEverDereferencingTheMissingProficiencyOwner()
    {
        foreach (var image in new[] { Array.Empty<byte>(), Image(0), Image(-1), Image(int.MinValue) })
        {
            using var files = new OwnedProfs();
            files.Seed(image);
            var client = Client(profs: false);
            LegacyNativeServerDatabasePersistence.LoadClientProfs(client);
            Assert.Null(client.MyProfs);
            Assert.Equal(image, File.ReadAllBytes(files.FilePath));
            AssertClosed(files.FilePath);
        }
    }

    [Fact]
    public void OneByteCountAndMissingFirstRecordStillAdmitTheNativeDefaultRow()
    {
        using var files = new OwnedProfs();
        files.Seed(new byte[] { 1 });
        var client = Client();
        LegacyNativeServerDatabasePersistence.LoadClientProfs(client);
        var entry = Assert.Single(client.MyProfs!.ClientProf);
        Assert.Equal(0u, entry.Key); Assert.Equal(0u, entry.Value.ID); Assert.Equal(0u, entry.Value.Level);
        Assert.Equal((byte)0, entry.Value.PreviouseLevel);
        Assert.Equal(0u, entry.Value.Experience); Assert.Equal(0u, entry.Value.NeededExperience);
        AssertClosed(files.FilePath);
    }

    [Fact]
    public void TruncatedLaterRecordReusesEarlierFieldsAndRepeatedEofDoesNotReplaceTheFirstAdmission()
    {
        using var files = new OwnedProfs();
        files.Seed(Image(3, Row(77, 0x12345678, 0xA9, uint.MaxValue, 0x87654321, 0xEE))
            .Concat(BitConverter.GetBytes(88u)).ToArray());
        var client = Client();
        LegacyNativeServerDatabasePersistence.LoadClientProfs(client);
        Assert.Equal(2, client.MyProfs!.ClientProf.Count);
        var first = client.MyProfs.ClientProf[77];
        var second = client.MyProfs.ClientProf[88];
        Assert.NotSame(first, second);
        Assert.Equal(88u, second.ID); Assert.Equal(0x12345678u, second.Level);
        Assert.Equal((byte)0xA9, second.PreviouseLevel);
        Assert.Equal(uint.MaxValue, second.Experience); Assert.Equal(0x87654321u, second.NeededExperience);
        AssertClosed(files.FilePath);
    }

    [Fact]
    public void MissingDirectoriesFilesAndExclusiveOpenFailuresReturnBeforeReadingTheOwner()
    {
        using (var missing = new OwnedProfs(createDirectory: false))
        {
            var client = Client(profs: false);
            LegacyNativeServerDatabasePersistence.LoadClientProfs(client);
            LegacyNativeServerDatabasePersistence.SaveClientProfs(client);
            Assert.Null(client.MyProfs);
            Assert.False(Directory.Exists(missing.DirectoryPath));
        }
        using var files = new OwnedProfs();
        var noOwner = Client(profs: false);
        LegacyNativeServerDatabasePersistence.LoadClientProfs(noOwner);
        Assert.False(File.Exists(files.FilePath));
        var original = Image(1, Row(9, 4));
        files.Seed(original);
        using (var locked = new FileStream(files.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            LegacyNativeServerDatabasePersistence.LoadClientProfs(noOwner);
            LegacyNativeServerDatabasePersistence.SaveClientProfs(noOwner);
        }
        Assert.Equal(original, File.ReadAllBytes(files.FilePath));
        Assert.Null(noOwner.MyProfs);
    }

    [Fact]
    public void OpenedLoadPropagatesMissingOwnerAfterConsumingOneRowAndIntentionallySkipsClose()
    {
        using var files = new OwnedProfs();
        var secondBytes = Row(12, 3, 4, 5, 6, 0xAA);
        files.Seed(Image(2, Row(11, 1), secondBytes));
        var client = Client(profs: false);
        var file = files.Open(FileMode.Open);
        try
        {
            Assert.Throws<NullReferenceException>(() => LegacyNativeServerDatabasePersistence.LoadClientProfsOpenedFile(file, client));
            Assert.Null(client.MyProfs);
            Assert.True(file.Success);
            AssertStillOpen(files.FilePath);
            var next = default(LegacyNativeClientProficiency.DBProf);
            Assert.True(file.ReadProf(ref next));
            Assert.Equal(secondBytes, Bytes(ref next));
        }
        finally { file.Close(); }
        AssertClosed(files.FilePath);
    }

    [Fact]
    public void NullClientInBothPublicMethodsPropagatesBeforeAnyFileCanBeOpened()
    {
        using var files = new OwnedProfs();
        Assert.Throws<NullReferenceException>(() => LegacyNativeServerDatabasePersistence.LoadClientProfs(null!));
        Assert.Throws<NullReferenceException>(() => LegacyNativeServerDatabasePersistence.SaveClientProfs(null!));
        Assert.Empty(Directory.GetFileSystemEntries(files.DirectoryPath));
    }
}
