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
    public void NegativeZeroEmptyAndShortHeadersKeepTheNativeDefaultAndIgnoredReadBehavior()
    {
        foreach (var image in new[] { Image(-1), Image(0), Array.Empty<byte>(), new byte[] { 1 } })
        {
            using var files = new OwnedSpells();
            files.Seed(image);
            var client = Client();
            LegacyNativeServerDatabasePersistence.LoadClientSpells(client);
            if (image.Length == 1)
            {
                var spell = Assert.Single(client.MySpells!.ClientSpells).Value;
                Assert.Equal((ushort)0, spell.ID); Assert.Equal((ushort)0, spell.Level);
                Assert.Equal(0, spell.Experience); Assert.Equal((byte)0, spell.SoulLevel);
            }
            else Assert.Empty(client.MySpells!.ClientSpells);
            AssertClosed(files.FilePath);
        }
    }

    [Fact]
    public void TruncatedLaterRecordRetainsTheEarlierFieldsAndStillAdmitsItsNewId()
    {
        using var files = new OwnedSpells();
        var first = Row(77, 3, 0x11223344, 0x55, 2, 3, 0xA5);
        files.Seed(Image(2, first).Concat(new byte[] { 88, 0 }).ToArray());
        var client = Client();
        LegacyNativeServerDatabasePersistence.LoadClientSpells(client);
        Assert.Equal(2, client.MySpells!.ClientSpells.Count);
        var earlier = client.MySpells.ClientSpells[77];
        var later = client.MySpells.ClientSpells[88];
        Assert.NotSame(earlier, later);
        Assert.Equal((ushort)3, later.Level); Assert.Equal(0x11223344, later.Experience);
        Assert.Equal((byte)0x55, later.PreviousLevel); Assert.Equal((byte)2, later.SoulLevel);
        Assert.Equal((byte)3, later.UseSpellSoul);
        AssertClosed(files.FilePath);
    }

    [Fact]
    public void SparseMetadataCountGateBypassesRuneValidationEvenWhenThatLevelIsMarkedRune()
    {
        using var files = new OwnedSpells();
        files.Seed(Image(1, Row(91, 5, 44)));
        files.Magic[91] = new() { [5] = new() { isRuneSkill = true } }; // Count1 is not > level5.
        LegacyNativePoolItems.ItemsBase = null;
        var client = Client();
        LegacyNativeServerDatabasePersistence.LoadClientSpells(client);
        Assert.Equal((ushort)5, Assert.Single(client.MySpells!.ClientSpells).Value.Level);
        AssertClosed(files.FilePath);
    }

    [Fact]
    public void UnknownSpellIdsAndNullMagicRowsAreAdmittedWithoutRuneMetadataAccess()
    {
        using var files = new OwnedSpells();
        files.Seed(Image(2, Row(100, 0), Row(101, 0)));
        files.Magic[101] = new() { [0] = null! };
        LegacyNativePoolItems.ItemsBase = null;
        var client = Client();
        LegacyNativeServerDatabasePersistence.LoadClientSpells(client);
        Assert.Equal(new ushort[] { 100, 101 }, client.MySpells!.ClientSpells.Keys.OrderBy(value => value));
        AssertClosed(files.FilePath);
    }

    [Fact]
    public void MissingLevelWithinTheCountGateLogsTheExactNativeErrorAndStillAddsTheSpell()
    {
        using var files = new OwnedSpells();
        files.Seed(Image(1, Row(102, 0, 77)));
        files.Magic[102] = new() { [1] = new() };
        var client = Client();
        using var output = new CapturedConsole();
        LegacyNativeServerDatabasePersistence.LoadClientSpells(client);
        Assert.Equal("ERORR [SPELL LOAD] Id: 102 Level 0" + Environment.NewLine, output.Text);
        Assert.Equal(77, Assert.Single(client.MySpells!.ClientSpells).Value.Experience);
        AssertClosed(files.FilePath);
    }

    [Fact]
    public void RuneValidationUsesTheReverseLookupRowIdThenTheLiveJobMetadataAtThatKey()
    {
        using var files = new OwnedSpells();
        files.Seed(Image(2, Row(900, 0, 19), Row(901, 0, 20)));
        files.Magic[900] = new() { [0] = new() { isRuneSkill = true } };
        // Reverse lookup returns row.ID100, not dictionary key7. The subsequent
        // ItemsBase[100] job row is deliberately a different actual object.
        files.Items[7] = new() { ID = 100, RuneSpellID = 90000, Class = 2000 };
        files.Items[100] = new() { ID = 222, RuneSpellID = 0, Class = 1000 };
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
    public void MissingDirectoriesAndExclusiveOpenFailuresReturnBeforeReadingTheSpellOwner()
    {
        using (var missing = new OwnedSpells(createDirectory: false))
        {
            var client = Client(spells: false);
            Assert.Null(Record.Exception(() => LegacyNativeServerDatabasePersistence.LoadClientSpells(client)));
            Assert.Null(Record.Exception(() => LegacyNativeServerDatabasePersistence.SaveClientSpells(client)));
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
            Assert.Null(Record.Exception(() => LegacyNativeServerDatabasePersistence.SaveClientSpells(noOwner)));
        }
        Assert.Equal(original, File.ReadAllBytes(files.FilePath));
    }

    [Fact]
    public void OpenedLoadFaultPreservesEarlierAdmissionAndLeavesTheExactOwnerOpenUntilTestCleanup()
    {
        using var files = new OwnedSpells();
        files.Seed(Image(2, Row(10, 0, 15), Row(11, 0, 16)));
        files.Magic[11] = new() { [0] = new() { isRuneSkill = true } };
        LegacyNativePoolItems.ItemsBase = null;
        var client = Client();
        var file = files.Open(FileMode.Open);
        try
        {
            Assert.Throws<NullReferenceException>(() => LegacyNativeServerDatabasePersistence.LoadClientSpellsOpenedFile(file, client));
            Assert.Equal((ushort)10, Assert.Single(client.MySpells!.ClientSpells).Key);
            Assert.True(file.Success);
            Assert.Throws<IOException>(() =>
            {
                using var unexpected = new FileStream(files.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            });
        }
        finally { file.Close(); }
        AssertClosed(files.FilePath);
    }

    [Fact]
    public void NullClientEntryKeepsTheNativeLoadCatchAndUncaughtSaveBeforeAnyFileOpen()
    {
        using var files = new OwnedSpells();
        using var output = new CapturedConsole();
        Assert.Null(Record.Exception(() => LegacyNativeServerDatabasePersistence.LoadClientSpells(null!)));
        Assert.Contains("System.NullReferenceException", output.Text, StringComparison.Ordinal);
        Assert.Contains("LoadClientSpells", output.Text, StringComparison.Ordinal);
        Assert.Throws<NullReferenceException>(() => LegacyNativeServerDatabasePersistence.SaveClientSpells(null!));
        Assert.Empty(Directory.GetFileSystemEntries(files.DirectoryPath));
    }
}
