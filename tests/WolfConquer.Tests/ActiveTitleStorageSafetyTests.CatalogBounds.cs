using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ActiveTitleStorageSafetyTests
{
    [Theory]
    [InlineData("4001@@10000@@Wing@@0@@0@@0@@0@@91@@")]
    [InlineData("4001@@4294967295@@Wing@@0@@0@@0@@0@@91@@")]
    public async Task UnrepresentableCatalogSubtypeRefusesWithoutPublication(string catalogRow)
    {
        using var fixture = new Fixture();
        fixture.WriteCatalog(catalogRow);
        var before = File.ReadAllBytes(fixture.SnapshotPath);

        await fixture.DispatchAsync();

        fixture.AssertRejected(fixture.Original, before);
    }

    [Theory]
    [InlineData(0U, "0@@17@@Invalid@@0@@0@@0@@0@@91@@")]
    [InlineData(429_497U, "429497@@0@@Overflow@@0@@0@@0@@0@@91@@")]
    public async Task InvalidCatalogIdRefusesBeforePackingOrSaving(uint id, string catalogRow)
    {
        using var fixture = new Fixture();
        fixture.WriteCatalog(catalogRow);
        var expected = fixture.ConfigureOwnedTitle(id);
        var before = File.ReadAllBytes(fixture.SnapshotPath);

        await fixture.DispatchAsync(id: id, context: fixture.Context with { Session = expected });

        fixture.AssertRejected(expected, before);
    }

    [Fact]
    public async Task MismatchedCatalogIndexRowCannotEquipAnUnrequestedOwnedTitle()
    {
        using var fixture = new Fixture();
        var catalog = LegacyItemWorldDataFixture.Create();
        // Deliberately corrupt this fixture-owned mutable index, not a provider or saver:
        // requesting 4001 must never authorize the different row ID 4002.
        var index = Assert.IsType<Dictionary<int, TitleType>>(catalog.TitleTypesById);
        index.Add(4_001, new TitleType(4_002, 19, "WrongIndexRow", 0, 0, 0, 0, 92,
            "fixture://catalog-index-mismatch", 1, string.Empty, []));
        var before = File.ReadAllBytes(fixture.SnapshotPath);

        await fixture.DispatchAsync(context: fixture.Context with { WorldData = catalog });

        fixture.AssertRejected(fixture.Original, before);
    }

    [Fact]
    public async Task ExactlyUInt32MaxPackedTitleRemainsRepresentable()
    {
        using var fixture = new Fixture { VerifyPersistenceBeforePublication = true };
        fixture.WriteCatalog("429496@@7295@@Boundary@@0@@0@@0@@0@@91@@");
        var expected = fixture.ConfigureOwnedTitle(429_496);

        await fixture.DispatchAsync(id: 429_496, subtype: 7_295,
            context: fixture.Context with { Session = expected });

        Assert.Equal("4294967295", fixture.Current().Character.Attributes["SpecialWingID"]);
        Assert.Equal("4294967295", fixture.ReadPersisted().Attributes["SpecialWingID"]);
        Assert.Equal(2, fixture.Sent.Count);
        AssertPeerSpawn(Assert.Single(fixture.PeerSent), uint.MaxValue);
        Assert.Equal(new LegacyTitleStorageTitle(429_496, 7_295, DwParam1: 1),
            ReadTitlePacket(fixture.Sent[1]).Title);
    }

    [Fact]
    public async Task FallbackCatalogPreservesSpacedNameColumnsAndScore150()
    {
        using var fixture = new Fixture { VerifyPersistenceBeforePublication = true };
        fixture.WriteCatalog("2149@@23@@Dice Champion (China)@@0@@0@@0@@0@@150@@");
        var expected = fixture.ConfigureOwnedTitle(2_149);

        await fixture.DispatchAsync(id: 2_149, subtype: 23,
            context: fixture.Context with { Session = expected });

        Assert.Equal("21490023", fixture.Current().Character.Attributes["SpecialTitleID"]);
        Assert.Equal("150", fixture.Current().Character.Attributes["SpecialTitleScore"]);
        Assert.Equal("150", fixture.ReadPersisted().Attributes["SpecialTitleScore"]);
        Assert.Equal(new LegacyTitleStorageTitle(2_149, 23, DwParam1: 150),
            ReadTitlePacket(fixture.Sent[1]).Title);
        Assert.True(LegacyPlayerSpawnPacket.TryParse(ReadFrame(Assert.Single(fixture.PeerSent)), out var spawn));
        Assert.Equal(21_490_023UL, spawn.GetNumericField(89));
        Assert.Equal(150UL, spawn.GetNumericField(90));
    }
}
