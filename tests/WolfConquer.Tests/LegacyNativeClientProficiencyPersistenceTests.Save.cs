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
    public void SaveRoundTripsValidFullWidthFieldsWithoutChangingExistingRows()
    {
        using var files = new OwnedProfs();
        var client = Client();
        var first = new LegacyNativeMsgProficiency { ID = uint.MaxValue, Level = uint.MaxValue,
            PreviouseLevel = byte.MaxValue, Experience = 0x80000000, NeededExperience = uint.MaxValue };
        var second = new LegacyNativeMsgProficiency { ID = 42, Level = 21,
            PreviouseLevel = 77, Experience = 11, NeededExperience = 13 };
        client.MyProfs!.ClientProf.TryAdd(uint.MaxValue, first);
        client.MyProfs.ClientProf.TryAdd(42, second);
        var values = client.MyProfs.ClientProf.Values.ToArray();
        var expected = Image(2, values.Select(prof => Row(prof.ID, prof.Level,
            prof.PreviouseLevel, prof.Experience, prof.NeededExperience)).ToArray());
        LegacyNativeServerDatabasePersistence.SaveClientProfs(client);
        Assert.Equal(expected, File.ReadAllBytes(files.FilePath));
        Assert.Same(first, client.MyProfs.ClientProf[uint.MaxValue]); Assert.Same(second, client.MyProfs.ClientProf[42]);
        AssertClosed(files.FilePath);
        var loaded = Client();
        LegacyNativeServerDatabasePersistence.LoadClientProfs(loaded);
        Assert.Equal(new[] { 42u, uint.MaxValue }, loaded.MyProfs!.ClientProf.Keys.OrderBy(id => id));
        var savedMax = loaded.MyProfs.ClientProf[uint.MaxValue];
        Assert.NotSame(first, savedMax);
        Assert.Equal(uint.MaxValue, savedMax.Level); Assert.Equal(byte.MaxValue, savedMax.PreviouseLevel);
        Assert.Equal(0x80000000u, savedMax.Experience); Assert.Equal(uint.MaxValue, savedMax.NeededExperience);
        Assert.Equal(21u, loaded.MyProfs.ClientProf[42].Level);
        AssertClosed(files.FilePath);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OpenedSaveReplacesLongerWholeFileEvenAfterNativeCursorAdvance(bool advanceCursor)
    {
        using var files = new OwnedProfs();
        files.Seed(Image(3, Row(11, 1), Row(12, 2), Row(13, 3)));
        var client = Client();
        var row = new LegacyNativeMsgProficiency { ID = 77, Level = 4, PreviouseLevel = 2,
            Experience = 100, NeededExperience = 500 };
        Assert.True(client.MyProfs!.ClientProf.TryAdd(77, row));
        var file = files.Open(FileMode.Open);
        if (advanceCursor)
        {
            int count = 0;
            Assert.True(file.ReadCount(ref count));
            Assert.Equal(3, count);
        }
        LegacyNativeServerDatabasePersistence.SaveClientProfsOpenedFile(file, client);
        Assert.Same(row, client.MyProfs.ClientProf[77]);
        Assert.Equal(Image(1, Row(77, 4, 2, 100, 500)), File.ReadAllBytes(files.FilePath));
        AssertClosed(files.FilePath);
    }
    [Fact]
    public void OpenedSaveMissingOwnerRefusesBeforeWritingAndClosesOwnedFile()
    {
        using var files = new OwnedProfs();
        var before = Image(1, Row(17, 3, 2, 4, 5, 0xAA));
        files.Seed(before);
        var file = files.Open(FileMode.Open);
        Assert.Throws<InvalidOperationException>(() => LegacyNativeServerDatabasePersistence.SaveClientProfsOpenedFile(file, Client(profs: false)));
        Assert.Equal(before, File.ReadAllBytes(files.FilePath));
        AssertClosed(files.FilePath);
    }
    [Fact]
    public void OpenedSaveNullRowMustRefuseBeforeChangingFileAndCloseOwner()
    {
        using var files = new OwnedProfs();
        var before = Image(1, Row(77, 4, 2, 100, 500, 0xA5));
        files.Seed(before);
        var client = Client();
        var first = new LegacyNativeMsgProficiency { ID = 40, Level = 2, PreviouseLevel = 3,
            Experience = 4, NeededExperience = uint.MaxValue };
        client.MyProfs!.ClientProf.TryAdd(40, first);
        client.MyProfs.ClientProf.TryAdd(87, null!);
        var file = files.Open(FileMode.Open);
        Assert.Throws<InvalidOperationException>(() => LegacyNativeServerDatabasePersistence.SaveClientProfsOpenedFile(file, client));
        Assert.Same(first, client.MyProfs.ClientProf[40]);
        Assert.Null(client.MyProfs.ClientProf[87]);
        Assert.Equal(before, File.ReadAllBytes(files.FilePath));
        AssertClosed(files.FilePath);
    }
    [Fact]
    public void OpenedPhasesRejectMissingClientBeforeFileAccess()
    {
        var unopened = new LegacyNativeItemRecordFile();
        Assert.Throws<ArgumentNullException>(() => LegacyNativeServerDatabasePersistence.LoadClientProfsOpenedFile(unopened, null!));
        Assert.Throws<ArgumentNullException>(() => LegacyNativeServerDatabasePersistence.SaveClientProfsOpenedFile(unopened, null!));
        Assert.Throws<IOException>(() => LegacyNativeServerDatabasePersistence.SaveClientProfsOpenedFile(unopened, Client()));
    }
    [Theory]
    [InlineData("null")]
    [InlineData("zero")]
    [InlineData("mismatch")]
    public void RootedSaveRejectsCorruptMapBeforeReplacingExistingFile(string kind)
    {
        using var files = new OwnedProfs();
        var before = Image(1, Row(77, 4, 2, 100, 500, 0xA5));
        files.Seed(before);
        var client = Client();
        var map = client.MyProfs!.ClientProf;
        if (kind == "null") map[40] = null!;
        else if (kind == "zero") map[0] = new LegacyNativeMsgProficiency { ID = 0 };
        else map[40] = new LegacyNativeMsgProficiency { ID = 41, Level = 3 };
        var original = map.ToArray();
        Assert.Throws<InvalidOperationException>(() => LegacyNativeServerDatabasePersistence.SaveClientProfs(client));
        Assert.Same(map, client.MyProfs.ClientProf);
        Assert.Equal(original.Length, map.Count);
        foreach (var pair in original) { Assert.True(map.TryGetValue(pair.Key, out var value)); Assert.Same(pair.Value, value); }
        Assert.Equal(before, File.ReadAllBytes(files.FilePath));
        AssertClosed(files.FilePath);
    }}
