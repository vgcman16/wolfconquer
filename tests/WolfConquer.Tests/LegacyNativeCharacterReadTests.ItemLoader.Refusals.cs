using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCharacterReadTests
{
    [Fact]
    public Task LateItemConversionFailurePreservesTheWholeLoadAndCanRetryTheCorrectedFile() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); scope.Metadata(750000); scope.Metadata(410009);
        scope.Client.DemonExterminator!.ItemUID = 91;
        var records = new[] { Row(71, 750000), Row(72) };
        records[1].Expiration = long.MaxValue;
        var invalid = ItemsFile(2, records); File.WriteAllBytes(scope.ItemPath, invalid);
        var inventory = scope.Client.Inventory!.ClientItems;
        Assert.Throws<ArgumentException>(() => LegacyNativeServerDatabasePersistence.LoadClientItems(scope.Client));
        Assert.Same(inventory, scope.Client.Inventory.ClientItems); Assert.Empty(inventory);
        Assert.Equal(91u, scope.Client.DemonExterminator.ItemUID); Assert.Empty(scope.Queue);
        Assert.Equal(invalid, File.ReadAllBytes(scope.ItemPath)); Assert.False(scope.Client.FullLoading);
        using (var reopened = File.Open(scope.ItemPath, FileMode.Open, FileAccess.Read, FileShare.None)) Assert.True(reopened.CanRead);

        records[1].Expiration = 0; File.WriteAllBytes(scope.ItemPath, ItemsFile(2, records));
        LegacyNativeServerDatabasePersistence.LoadClientItems(scope.Client);
        Assert.Equal(new uint[] { 71, 72 }, scope.Client.Inventory.ClientItems.Keys.Order());
        Assert.Equal(71u, scope.Client.DemonExterminator.ItemUID); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task DuplicateExistingUidInAnotherDestinationRefusesWithoutReplacingItsActualOwner() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); scope.Metadata(410009);
        var prior = scope.Item(71); scope.Client.Inventory!.ClientItems.TryAdd(prior.Uid, prior);
        var inventory = scope.Client.Inventory.ClientItems; var banks = scope.Client.Warehouse!.ClientItems;
        var bytes = ItemsFile(1, new[] { Row(71, warehouse: 900) }); File.WriteAllBytes(scope.ItemPath, bytes);
        Assert.Throws<InvalidDataException>(() => LegacyNativeServerDatabasePersistence.LoadClientItems(scope.Client));
        Assert.Same(inventory, scope.Client.Inventory.ClientItems); Assert.Same(prior, Assert.Single(inventory).Value);
        Assert.Same(banks, scope.Client.Warehouse.ClientItems); Assert.Empty(banks);
        Assert.Equal(bytes, File.ReadAllBytes(scope.ItemPath)); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task MissingUntouchedRuneOwnerCannotMakeAnInventoryLoadAssumeAnEmptyGlobalUidDomain() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); scope.Metadata(410009);
        scope.Client.Rune = null; var inventory = scope.Client.Inventory!.ClientItems;
        File.WriteAllBytes(scope.ItemPath, ItemsFile(1, new[] { Row(71) }));
        Assert.Throws<InvalidDataException>(() => LegacyNativeServerDatabasePersistence.LoadClientItems(scope.Client));
        Assert.Same(inventory, scope.Client.Inventory.ClientItems); Assert.Empty(inventory);
        Assert.Null(scope.Client.Rune); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task LoginItemLoaderRefusesAlreadyLoadedOrFakeClientsBeforeOpeningTheFile(bool fake) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); scope.Metadata(410009);
        scope.Client.Fake = fake; scope.Client.FullLoading = !fake;
        var bytes = ItemsFile(1, new[] { Row(71) }); File.WriteAllBytes(scope.ItemPath, bytes);
        var inventory = scope.Client.Inventory!.ClientItems;
        Assert.Throws<InvalidDataException>(() => LegacyNativeServerDatabasePersistence.LoadClientItems(scope.Client));
        Assert.Same(inventory, scope.Client.Inventory.ClientItems); Assert.Empty(inventory);
        Assert.Equal(bytes, File.ReadAllBytes(scope.ItemPath)); Assert.Empty(scope.Queue);
        Assert.Equal(fake, scope.Client.Fake); Assert.Equal(!fake, scope.Client.FullLoading);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ActualMetadataCallbackChangingLoginPhaseCannotPublishEarlierItemsOrQuestMarker(bool fake) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); scope.Metadata(4020001);
        scope.Client.DemonExterminator!.ItemUID = 91;
        var catalog = LegacyNativePoolItems.ItemsBase!;
        var comparer = new MaterializationComparer { Trigger = 4020001 };
        typeof(Dictionary<uint, LegacyNativeItemRuneMetadata>)
            .GetField("_comparer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(catalog, comparer);
        comparer.Mutation = () => { scope.Client.Fake = fake; scope.Client.FullLoading = !fake; };
        var bytes = ItemsFile(2, new[] { Row(70, 750000), Row(71, 4020001, 101) });
        File.WriteAllBytes(scope.ItemPath, bytes); var inventory = scope.Client.Inventory!.ClientItems;
        Assert.Throws<InvalidDataException>(() => LegacyNativeServerDatabasePersistence.LoadClientItems(scope.Client));
        Assert.Equal(1, comparer.Calls); Assert.Same(inventory, scope.Client.Inventory.ClientItems);
        Assert.Empty(inventory); Assert.Empty(scope.Client.Rune!.Objects);
        Assert.Equal(91u, scope.Client.DemonExterminator.ItemUID); Assert.Empty(scope.Queue);
        Assert.Equal(fake, scope.Client.Fake); Assert.Equal(!fake, scope.Client.FullLoading);
        Assert.Equal(bytes, File.ReadAllBytes(scope.ItemPath));
    });

    [Fact]
    public Task PresentRootCountWithoutRequiredPostludeRefusesEvenWhenItemOwnersAreAbsent() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); scope.Client.Inventory = null; scope.Client.Rune = null;
        File.WriteAllBytes(scope.ItemPath, BitConverter.GetBytes(0));
        Assert.Throws<EndOfStreamException>(() => LegacyNativeServerDatabasePersistence.LoadClientItems(scope.Client));
        Assert.Null(scope.Client.Inventory); Assert.Null(scope.Client.Rune); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task LegacyFilteredRootCannotSilentlyDiscardDepositsOrPerfection(bool perfection) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles();
        var root = Row(71, 205901); if (!perfection) root.DepositeCount = 1;
        var rows = perfection ? new[] { root } : new[] { root, Row(72) };
        var extra = perfection ? new[] { new LegacyNativeClientItemPerfectionRecord { ItemUID = 71, Level = 2 } }
            : Array.Empty<LegacyNativeClientItemPerfectionRecord>();
        var bytes = ItemsFile(1, rows, extra); File.WriteAllBytes(scope.ItemPath, bytes);
        var inventory = scope.Client.Inventory!.ClientItems;
        Assert.Throws<InvalidDataException>(() => LegacyNativeServerDatabasePersistence.LoadClientItems(scope.Client));
        Assert.Same(inventory, scope.Client.Inventory.ClientItems); Assert.Empty(inventory);
        Assert.Equal(bytes, File.ReadAllBytes(scope.ItemPath)); Assert.Empty(scope.Queue);
    });
}
