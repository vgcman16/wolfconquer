using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaProfileTests
{
    [Fact]
    public void OrdinaryLevelsUsePositionsOneThroughEightNotIdsKeysOrMetadata()
    {
        using var scope = new Scope();
        LegacyNativeNinjaFile.gouyu_type = null!;
        for (uint position = 1; position <= 8; position++)
            scope.Item(900 + position, uint.MaxValue, position * 10, position);
        scope.Item(1, 100, 1000, 0);
        scope.Item(2, 101, 2000, 9);
        scope.Item(3, 102, 3000, 16);
        scope.Item(4, 103, 4000, 17);
        Assert.False(scope.Client.Equipment!.Alternante);
        Assert.Equal(360u, scope.Ninja.Levels);
    }

    [Fact]
    public void AlternateSlotsWinEvenAtLevelZeroAndOnlyMissingSlotsFallBackToTheirOrdinaryPair()
    {
        using var scope = new Scope();
        for (uint position = 1; position <= 8; position++)
            scope.Item(100 + position, 999, position, position);
        scope.Item(209, 888, 100, 9);
        scope.Item(210, 777, 200, 10);
        scope.Item(212, 666, 0, 12);
        scope.Item(216, 555, 800, 16);
        scope.Item(217, 444, 10000, 17);
        Assert.Equal(36u, scope.Ninja.Levels);
        scope.Client.Equipment!.Alternante = true;
        Assert.Equal(1121u, scope.Ninja.Levels);
        scope.Client.Equipment = new LegacyNativeEquipItemStorage(scope.Client);
        Assert.Equal(36u, scope.Ninja.Levels);
    }

    [Fact]
    public void AlternateModeWithNoAlternatePositionsFallsBackForAllEightSlots()
    {
        using var scope = new Scope();
        scope.Client.Equipment!.Alternante = true;
        for (uint position = 1; position <= 8; position++)
            scope.Item(300 + position, 0, position * 100, position);
        scope.Item(400, 9, 90000, 0);
        scope.Item(401, 16, 90000, uint.MaxValue);
        Assert.Equal(3600u, scope.Ninja.Levels);
    }

    [Fact]
    public void DuplicatePhysicalPositionsContributeOnlyTheFirstCurrentValuesMatch()
    {
        using var scope = new Scope();
        scope.Item(4000, 100, 11, 1);
        scope.Item(2, 200, 22, 1);
        scope.Item(6000, 300, 33, 2);
        uint first = scope.Ninja.Items.Values.First(item => item.Position == 1).Level;
        Assert.True(first is 11 or 22);
        Assert.Equal(first + 33, scope.Ninja.Levels);
        Assert.NotEqual(66u, scope.Ninja.Levels);
    }

    [Fact]
    public void AliasedRowsAndLivePositionLevelMutationsAreNotSummedPerDictionaryKey()
    {
        using var scope = new Scope();
        var item = scope.Item(1, 100, 1234, 1);
        Assert.True(scope.Ninja.Items.TryAdd(2, item));
        Assert.Equal(1234u, scope.Ninja.Levels);
        item.Position = 8;
        item.Level = 5678;
        Assert.Equal(5678u, scope.Ninja.Levels);
        item.Position = 16;
        Assert.Equal(0u, scope.Ninja.Levels);
        scope.Client.Equipment!.Alternante = true;
        Assert.Equal(5678u, scope.Ninja.Levels);
    }

    [Fact]
    public void LevelsKeepUncappedUInt32ValuesAndWrapTheirTotalInBothModes()
    {
        using var scope = new Scope();
        scope.Item(1, 100, uint.MaxValue, 1);
        scope.Item(2, 100, 2, 2);
        Assert.Equal(1u, scope.Ninja.Levels);
        scope.Client.Equipment!.Alternante = true;
        Assert.Equal(1u, scope.Ninja.Levels);
        scope.Item(3, 100, 5, 10);
        Assert.Equal(4u, scope.Ninja.Levels);
    }

    [Fact]
    public void ActualUserEquipmentDictionaryAndNullValuesFaultWithoutMetadataOrOwnerRepair()
    {
        using var scope = new Scope();
        LegacyNativeNinjaFile.gouyu_type = null!;
        Assert.Equal(0u, scope.Ninja.Levels);
        scope.Ninja.user = null!;
        Assert.Throws<NullReferenceException>(() => _ = scope.Ninja.Levels);
        scope.Ninja.user = scope.Client;
        scope.Client.Equipment = null!;
        Assert.Throws<NullReferenceException>(() => _ = scope.Ninja.Levels);
        scope.Client.Equipment = new LegacyNativeEquipItemStorage(scope.Client);
        scope.Ninja.Items = null!;
        Assert.Throws<NullReferenceException>(() => _ = scope.Ninja.Levels);
        scope.Ninja.Items = new() { [1] = null! };
        Assert.Throws<NullReferenceException>(() => _ = scope.Ninja.Levels);
        scope.Client.Equipment!.Alternante = true;
        Assert.Throws<NullReferenceException>(() => _ = scope.Ninja.Levels);
    }
}
