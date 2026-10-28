using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeClientSpellPersistenceTests
{
    [Fact]
    public void LoadPreservesExistingEntriesAndDuplicateFileIdsUseFirstSuccessfulTryAdd()
    {
        using var files = new OwnedSpells();
        var image = Image(4, Row(21, 1, 11), Row(22, 2, 22), Row(22, 9, 99), Row(21, 7, 77));
        files.Seed(image);
        var client = Client();
        var existing = new LegacyNativeMsgSpell { ID = 21, Level = 55, Experience = -5 };
        client.MySpells!.ClientSpells.TryAdd(21, existing);
        LegacyNativeServerDatabasePersistence.LoadClientSpells(client);
        Assert.Equal(2, client.MySpells.ClientSpells.Count);
        Assert.Same(existing, client.MySpells.ClientSpells[21]);
        Assert.Equal((ushort)2, client.MySpells.ClientSpells[22].Level);
        Assert.Equal(22, client.MySpells.ClientSpells[22].Experience);
        Assert.Equal(image, File.ReadAllBytes(files.FilePath));
        AssertClosed(files.FilePath);
    }

    [Fact]
    public void MalformedHeadersNeverInventDefaultSpellsWhileZeroCountRemainsAValidNoOp()
    {
        foreach (var image in new[] { Image(-1), Image(0), Array.Empty<byte>(), new byte[] { 1 } })
        {
            using var files = new OwnedSpells();
            files.Seed(image);
            var client = Client();
            var existing = SeedFileAdmissionSentinel(client);
            var map = client.MySpells!.ClientSpells;
            LegacyNativeServerDatabasePersistence.LoadClientSpells(client);
            if (image.SequenceEqual(Image(0))) Assert.Same(existing, Assert.Single(client.MySpells.ClientSpells).Value);
            else AssertFileAdmissionUnchanged(client, map, existing);
            Assert.Equal(image, File.ReadAllBytes(files.FilePath));
            AssertClosed(files.FilePath);
        }
    }


    [Fact]
    public void TruncatedLaterRecordPreservesTheEntireEarlierGraphWithoutBorrowingFields()
    {
        using var files = new OwnedSpells();
        var image = Image(2, Row(77, 3, 0x11223344, 0x55, 3, 2, 0xA5)).Concat(new byte[] { 88, 0 }).ToArray();
        files.Seed(image);
        var client = Client();
        var existing = SeedFileAdmissionSentinel(client);
        var map = client.MySpells!.ClientSpells;
        LegacyNativeServerDatabasePersistence.LoadClientSpells(client);
        AssertFileAdmissionUnchanged(client, map, existing);
        Assert.Equal(image, File.ReadAllBytes(files.FilePath));
        AssertClosed(files.FilePath);
    }


    [Fact]
    public void SparseMetadataStillRequiresTheActualRuneLevelAndItsRequiredItemMetadata()
    {
        using var files = new OwnedSpells();
        files.Seed(Image(1, Row(91, 5, 44)));
        files.Magic[91] = new() { [5] = new() { isRuneSkill = true } };
        LegacyNativePoolItems.ItemsBase = null;
        var client = Client();
        var existing = SeedFileAdmissionSentinel(client);
        var map = client.MySpells!.ClientSpells;
        LegacyNativeServerDatabasePersistence.LoadClientSpells(client);
        AssertFileAdmissionUnchanged(client, map, existing);
        AssertClosed(files.FilePath);
    }


    [Fact]
    public void UnknownSpellIdsRemainOpaqueButKnownNullDefinitionsRefuseWithoutPublishingAPrefix()
    {
        using (var files = new OwnedSpells())
        {
            files.Seed(Image(1, Row(100, 0)));
            var client = Client();
            LegacyNativeServerDatabasePersistence.LoadClientSpells(client);
            Assert.Equal((ushort)100, Assert.Single(client.MySpells!.ClientSpells).Key);
            AssertClosed(files.FilePath);
        }
        using (var files = new OwnedSpells())
        {
            files.Seed(Image(2, Row(100, 0), Row(101, 0)));
            files.Magic[101] = new() { [0] = null! };
            LegacyNativePoolItems.ItemsBase = null;
            var client = Client();
            var existing = SeedFileAdmissionSentinel(client);
            var map = client.MySpells!.ClientSpells;
            LegacyNativeServerDatabasePersistence.LoadClientSpells(client);
            AssertFileAdmissionUnchanged(client, map, existing);
            AssertClosed(files.FilePath);
        }
    }


    [Fact]
    public void KnownSpellMissingItsSelectedLevelRefusesWithoutPublishingTheSpell()
    {
        using var files = new OwnedSpells();
        files.Seed(Image(1, Row(102, 0, 77)));
        files.Magic[102] = new() { [1] = new() };
        var client = Client();
        var existing = SeedFileAdmissionSentinel(client);
        var map = client.MySpells!.ClientSpells;
        LegacyNativeServerDatabasePersistence.LoadClientSpells(client);
        AssertFileAdmissionUnchanged(client, map, existing);
        AssertClosed(files.FilePath);
    }


    [Fact]
    public void CoherentRuneMetadataRetainsActualJobAdmissionAndLegitimateJobDenial()
    {
        using var files = new OwnedSpells();
        files.Seed(Image(2, Row(900, 0, 19), Row(901, 0, 20)));
        files.Magic[900] = new() { [0] = new() { isRuneSkill = true } };
        files.Items[100] = new() { ID = 100, RuneSpellID = 90000, Class = 1000 };
        var allowed = Client(); SetClass(allowed, 1004);
        LegacyNativeServerDatabasePersistence.LoadClientSpells(allowed);
        Assert.Equal(2, allowed.MySpells!.ClientSpells.Count);
        files.Items[100].Class = 2000;
        var denied = Client(); SetClass(denied, 1004);
        LegacyNativeServerDatabasePersistence.LoadClientSpells(denied);
        Assert.Equal((ushort)901, Assert.Single(denied.MySpells!.ClientSpells).Key);
        AssertClosed(files.FilePath);
    }


    [Fact]
    public void UnavailableLoadFilesRemainNoOpsWhileSaveValidatesItsRequiredOwnerBeforeOpening()
    {
        using (var missing = new OwnedSpells(createDirectory: false))
        {
            var client = Client(spells: false);
            Assert.Null(Record.Exception(() => LegacyNativeServerDatabasePersistence.LoadClientSpells(client)));
            Assert.Throws<InvalidOperationException>(() => LegacyNativeServerDatabasePersistence.SaveClientSpells(client));
            Assert.Null(client.MySpells);
            Assert.False(Directory.Exists(missing.DirectoryPath));
        }
        using var files = new OwnedSpells();
        var original = Image(1, Row(50));
        files.Seed(original);
        var noOwner = Client(spells: false);
        using (var locked = new FileStream(files.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Null(Record.Exception(() => LegacyNativeServerDatabasePersistence.LoadClientSpells(noOwner)));
            Assert.Throws<InvalidOperationException>(() => LegacyNativeServerDatabasePersistence.SaveClientSpells(noOwner));
        }
        Assert.Equal(original, File.ReadAllBytes(files.FilePath));
    }


    [Fact]
    public void OpenedLoadMetadataFaultPreservesTheCompleteGraphAndClosesTheActualFile()
    {
        using var files = new OwnedSpells();
        var image = Image(2, Row(10, 0, 15), Row(11, 0, 16));
        files.Seed(image);
        files.Magic[11] = new() { [0] = new() { isRuneSkill = true } };
        LegacyNativePoolItems.ItemsBase = null;
        var client = Client();
        var existing = SeedFileAdmissionSentinel(client);
        var map = client.MySpells!.ClientSpells;
        var file = files.Open(FileMode.Open);
        try
        {
            Assert.Throws<InvalidOperationException>(() => LegacyNativeServerDatabasePersistence.LoadClientSpellsOpenedFile(file, client));
            AssertFileAdmissionUnchanged(client, map, existing);
            AssertClosed(files.FilePath);
            Assert.Equal(image, File.ReadAllBytes(files.FilePath));
        }
        finally { file.Close(); }
    }


    [Fact]
    public void NullClientEntryRefusesBeforeAnyFileAccessWithoutANullReferenceTrap()
    {
        using var files = new OwnedSpells();
        byte[] before = Image(1, Row(21, 2, 77, padding: 0xA5));
        files.Seed(before);
        using var output = new CapturedConsole();
        Assert.Throws<ArgumentNullException>(() => LegacyNativeServerDatabasePersistence.LoadClientSpells(null!));
        Assert.Throws<ArgumentNullException>(() => LegacyNativeServerDatabasePersistence.SaveClientSpells(null!));
        Assert.Equal(string.Empty, output.Text);
        Assert.Equal(before, File.ReadAllBytes(files.FilePath));
        Assert.Equal(new[] { files.FilePath }, Directory.GetFileSystemEntries(files.DirectoryPath));
        AssertClosed(files.FilePath);
    }

}
