using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeClientSpellPersistenceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void IncompleteCountNeverInventsASpellOrChangesTheExistingGraph(int headerBytes)
    {
        using var files = new OwnedSpells();
        var image = Image(1).Take(headerBytes).ToArray();
        files.Seed(image);
        var client = Client();
        var existing = SeedFileAdmissionSentinel(client);
        var map = client.MySpells!.ClientSpells;

        Assert.Null(Record.Exception(() => LegacyNativeServerDatabasePersistence.LoadClientSpells(client)));

        AssertFileAdmissionUnchanged(client, map, existing);
        Assert.Equal(image, File.ReadAllBytes(files.FilePath));
        AssertClosed(files.FilePath);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NegativeRecordCountsPreserveTheExistingGraph(int count)
    {
        using var files = new OwnedSpells();
        var image = Image(count);
        files.Seed(image);
        var client = Client();
        var existing = SeedFileAdmissionSentinel(client);
        var map = client.MySpells!.ClientSpells;

        Assert.Null(Record.Exception(() => LegacyNativeServerDatabasePersistence.LoadClientSpells(client)));

        AssertFileAdmissionUnchanged(client, map, existing);
        Assert.Equal(image, File.ReadAllBytes(files.FilePath));
        AssertClosed(files.FilePath);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    public void AnyTruncatedLaterRecordRefusesTheWholeAdmissionWithoutBorrowedFields(int tailBytes)
    {
        using var files = new OwnedSpells();
        var image = Image(2, Row(77, 3, 0x11223344, 0x55, 3, 2, 0xA5))
            .Concat(Row(88, 4, 0x55667788, 0x66, 3, 2).Take(tailBytes)).ToArray();
        files.Seed(image);
        var client = Client();
        var existing = SeedFileAdmissionSentinel(client);
        var map = client.MySpells!.ClientSpells;

        Assert.Null(Record.Exception(() => LegacyNativeServerDatabasePersistence.LoadClientSpells(client)));

        AssertFileAdmissionUnchanged(client, map, existing);
        Assert.Equal(image, File.ReadAllBytes(files.FilePath));
        AssertClosed(files.FilePath);
    }

    [Fact]
    public void ACompleteZeroCountRetainsExistingSpellValues()
    {
        using var files = new OwnedSpells();
        var image = Image(0);
        files.Seed(image);
        var client = Client();
        var existing = SeedFileAdmissionSentinel(client);

        LegacyNativeServerDatabasePersistence.LoadClientSpells(client);

        Assert.Same(existing, Assert.Single(client.MySpells!.ClientSpells).Value);
        Assert.Equal((ushort)500, existing.ID);
        Assert.Equal((ushort)4, existing.Level);
        Assert.Equal(250, existing.Experience);
        Assert.Equal(image, File.ReadAllBytes(files.FilePath));
        AssertClosed(files.FilePath);
    }

    private static LegacyNativeMsgSpell SeedFileAdmissionSentinel(LegacyNativeGameClientRuntime client)
    {
        var existing = new LegacyNativeMsgSpell { ID = 500, Level = 4, Experience = 250,
            PreviousLevel = 3, SoulLevel = 2, UseSpellSoul = 1 };
        Assert.True(client.MySpells!.ClientSpells.TryAdd(existing.ID, existing));
        return existing;
    }

    private static void AssertFileAdmissionUnchanged(LegacyNativeGameClientRuntime client,
        System.Collections.Concurrent.ConcurrentDictionary<ushort, LegacyNativeMsgSpell> map,
        LegacyNativeMsgSpell existing)
    {
        Assert.Same(map, client.MySpells!.ClientSpells);
        var row = Assert.Single(map);
        Assert.Equal((ushort)500, row.Key);
        Assert.Same(existing, row.Value);
        Assert.Equal((ushort)500, existing.ID);
        Assert.Equal((ushort)4, existing.Level);
        Assert.Equal(250, existing.Experience);
        Assert.Equal((byte)3, existing.PreviousLevel);
        Assert.Equal((byte)2, existing.SoulLevel);
        Assert.Equal((byte)1, existing.UseSpellSoul);
    }
}
