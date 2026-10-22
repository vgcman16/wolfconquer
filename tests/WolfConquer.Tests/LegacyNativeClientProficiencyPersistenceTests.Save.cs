using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeClientProficiencyPersistenceTests
{
    [Fact]
    public void SaveEmptyDictionaryTruncatesAnExistingFileToTheNativeZeroCountAndCloses()
    {
        using var files = new OwnedProfs();
        files.Seed(Enumerable.Repeat((byte)0xA5, 240).ToArray());
        LegacyNativeServerDatabasePersistence.SaveClientProfs(Client());
        Assert.Equal(Image(0), File.ReadAllBytes(files.FilePath));
        AssertClosed(files.FilePath);
    }

    [Fact]
    public void SaveEnumeratesActualValuesNotKeysAndRoundTripsAllUnsignedFieldsWithoutChangingAliases()
    {
        using var files = new OwnedProfs();
        var client = Client();
        var first = new LegacyNativeMsgProficiency { ID = uint.MaxValue, Level = uint.MaxValue,
            PreviouseLevel = byte.MaxValue, Experience = 0x80000000, NeededExperience = uint.MaxValue };
        var second = new LegacyNativeMsgProficiency { ID = 0, Level = 21,
            PreviouseLevel = 77, Experience = 11, NeededExperience = 13 };
        client.MyProfs!.ClientProf.TryAdd(333, first);
        client.MyProfs.ClientProf.TryAdd(777, second);
        var values = client.MyProfs.ClientProf.Values.ToArray();
        var expected = Image(2, values.Select(prof => Row(prof.ID, prof.Level,
            prof.PreviouseLevel, prof.Experience, prof.NeededExperience)).ToArray());
        LegacyNativeServerDatabasePersistence.SaveClientProfs(client);
        Assert.Equal(expected, File.ReadAllBytes(files.FilePath));
        Assert.Same(first, client.MyProfs.ClientProf[333]); Assert.Same(second, client.MyProfs.ClientProf[777]);
        AssertClosed(files.FilePath);
        var loaded = Client();
        LegacyNativeServerDatabasePersistence.LoadClientProfs(loaded);
        Assert.Equal(new[] { 0u, uint.MaxValue }, loaded.MyProfs!.ClientProf.Keys.OrderBy(id => id));
        var savedMax = loaded.MyProfs.ClientProf[uint.MaxValue];
        Assert.NotSame(first, savedMax);
        Assert.Equal(uint.MaxValue, savedMax.Level); Assert.Equal(byte.MaxValue, savedMax.PreviouseLevel);
        Assert.Equal(0x80000000u, savedMax.Experience); Assert.Equal(uint.MaxValue, savedMax.NeededExperience);
        Assert.Equal(21u, loaded.MyProfs.ClientProf[0].Level);
        AssertClosed(files.FilePath);
    }

    [Fact]
    public void OpenedSaveMissingOwnerFaultsBeforeWritingCountAndLeavesTheCreatedFileOpen()
    {
        using var files = new OwnedProfs();
        var file = files.Open(FileMode.Create);
        try
        {
            Assert.Throws<NullReferenceException>(() => LegacyNativeServerDatabasePersistence.SaveClientProfsOpenedFile(file, Client(profs: false)));
            Assert.Empty(ReadWhileOpen(files.FilePath));
            Assert.True(file.Success);
            AssertStillOpen(files.FilePath);
        }
        finally { file.Close(); }
        AssertClosed(files.FilePath);
    }

    [Fact]
    public void OpenedSaveNullValueKeepsItsOriginalCountAndEarlierRecordBeforePropagating()
    {
        using var files = new OwnedProfs();
        var client = Client();
        client.MyProfs!.ClientProf.TryAdd(12, new());
        client.MyProfs.ClientProf.TryAdd(87, new());
        var keys = client.MyProfs.ClientProf.ToArray().Select(entry => entry.Key).ToArray();
        var first = new LegacyNativeMsgProficiency { ID = 40, Level = 2, PreviouseLevel = 3,
            Experience = 4, NeededExperience = uint.MaxValue };
        client.MyProfs.ClientProf[keys[0]] = first;
        client.MyProfs.ClientProf[keys[1]] = null!;
        Assert.Same(first, client.MyProfs.ClientProf.Values.First());
        var file = files.Open(FileMode.Create);
        try
        {
            Assert.Throws<NullReferenceException>(() => LegacyNativeServerDatabasePersistence.SaveClientProfsOpenedFile(file, client));
            Assert.Equal(Image(2, Row(40, 2, 3, 4, uint.MaxValue)), ReadWhileOpen(files.FilePath));
            Assert.True(file.Success);
            AssertStillOpen(files.FilePath);
        }
        finally { file.Close(); }
        AssertClosed(files.FilePath);
    }

    [Fact]
    public void OpenedPhasesKeepNativeFileVersusClientFaultOrderWithoutReadinessGuards()
    {
        var unopened = new LegacyNativeItemRecordFile();
        Assert.Throws<IOException>(() => LegacyNativeServerDatabasePersistence.LoadClientProfsOpenedFile(unopened, null!));
        Assert.Throws<NullReferenceException>(() => LegacyNativeServerDatabasePersistence.SaveClientProfsOpenedFile(unopened, null!));
        Assert.Throws<IOException>(() => LegacyNativeServerDatabasePersistence.SaveClientProfsOpenedFile(unopened, Client()));
    }
}
