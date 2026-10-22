using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeClientSpellPersistenceTests
{
    [Fact]
    public void SaveEmptyDictionaryTruncatesAnOldFileToOneCountAndClosesNormally()
    {
        using var files = new OwnedSpells();
        files.Seed(Enumerable.Repeat((byte)0xA5, 500).ToArray());
        var client = Client();
        LegacyNativeServerDatabasePersistence.SaveClientSpells(client);
        Assert.Equal(Image(0), File.ReadAllBytes(files.FilePath));
        AssertClosed(files.FilePath);
    }

    [Fact]
    public void SaveUsesActualValueIdsAndNativeFieldsThenRealLoadReconstructsThem()
    {
        using var files = new OwnedSpells();
        var client = Client();
        var first = new LegacyNativeMsgSpell { ID = 31, Level = ushort.MaxValue, Experience = int.MinValue,
            PreviousLevel = 99, SoulLevel = 3, UseSpellSoul = 2 };
        var second = new LegacyNativeMsgSpell { ID = 32, Level = 5, Experience = int.MaxValue,
            PreviousLevel = 17, SoulLevel = 4, UseSpellSoul = 4 };
        client.MySpells!.ClientSpells.TryAdd(333, first);
        client.MySpells.ClientSpells.TryAdd(777, second);
        var orderedValues = client.MySpells.ClientSpells.Values.ToArray();
        var expected = Image(2, orderedValues.Select(spell => Row(spell.ID, spell.Level, spell.Experience,
            spell.PreviousLevel, spell.SoulLevel, spell.UseSpellSoul)).ToArray());
        LegacyNativeServerDatabasePersistence.SaveClientSpells(client);
        Assert.Equal(expected, File.ReadAllBytes(files.FilePath));
        Assert.Same(first, client.MySpells.ClientSpells[333]); Assert.Same(second, client.MySpells.ClientSpells[777]);
        AssertClosed(files.FilePath);
        var loaded = Client();
        LegacyNativeServerDatabasePersistence.LoadClientSpells(loaded);
        Assert.Equal(new ushort[] { 31, 32 }, loaded.MySpells!.ClientSpells.Keys.OrderBy(value => value));
        Assert.Equal(ushort.MaxValue, loaded.MySpells.ClientSpells[31].Level);
        Assert.Equal(int.MinValue, loaded.MySpells.ClientSpells[31].Experience);
        Assert.Equal((byte)2, loaded.MySpells.ClientSpells[31].UseSpellSoul);
        Assert.Equal((byte)4, loaded.MySpells.ClientSpells[32].SoulLevel);
        AssertClosed(files.FilePath);
    }

    [Fact]
    public void OpenedSaveConversionFaultKeepsHeaderAndEarlierRecordAndSkipsClose()
    {
        using var files = new OwnedSpells();
        var client = Client();
        client.MySpells!.ClientSpells.TryAdd(12, new());
        client.MySpells.ClientSpells.TryAdd(87, new());
        var keys = client.MySpells.ClientSpells.ToArray().Select(entry => entry.Key).ToArray();
        var first = new LegacyNativeMsgSpell { ID = 40, Level = 2, Experience = 3, PreviousLevel = 4, SoulLevel = 1, UseSpellSoul = 2 };
        client.MySpells.ClientSpells[keys[0]] = first;
        client.MySpells.ClientSpells[keys[1]] = null!;
        Assert.Same(first, client.MySpells.ClientSpells.Values.First());
        var file = files.Open(FileMode.Create);
        try
        {
            Assert.Throws<NullReferenceException>(() => LegacyNativeServerDatabasePersistence.SaveClientSpellsOpenedFile(file, client));
            Assert.Equal(Image(2, Row(40, 2, 3, 4, 1, 2)), ReadWhileOpen(files.FilePath));
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
    public void OpenedSaveMissingOwnerFaultsBeforeCountWriteAndLeavesCreatedFileOpen()
    {
        using var files = new OwnedSpells();
        var client = Client(spells: false);
        var file = files.Open(FileMode.Create);
        try
        {
            Assert.Throws<NullReferenceException>(() => LegacyNativeServerDatabasePersistence.SaveClientSpellsOpenedFile(file, client));
            Assert.Empty(ReadWhileOpen(files.FilePath));
            Assert.True(file.Success);
            Assert.Throws<IOException>(() =>
            {
                using var unexpected = new FileStream(files.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            });
        }
        finally { file.Close(); }
        AssertClosed(files.FilePath);
    }
}
