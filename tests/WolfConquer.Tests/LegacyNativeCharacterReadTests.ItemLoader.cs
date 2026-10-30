using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCharacterReadTests
{
    [Fact]
    public Task CompleteItemLoaderRoutesNonemptyActualOwnersAndAppliesRealPerfectionPostlude() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles();
        foreach (uint id in new uint[] { 750000, 410009, 181305, 4020001, 4020002 }) scope.Metadata(id);
        scope.Metadata(4020002, 2001);
        var records = new[] { Row(1, 205901), Row(2, 750000), Row(3, position:4), Row(4, 181305, 9, 100),
            Row(5, warehouse:900), Row(6, position:LegacyNativeGameItemRuntime.MythsoulBag), Row(7, position:213),
            Row(8, 4020001, 101), Row(9, 4020002, 102) };
        records[2].Plus = 255; records[2].Bless = 9;
        var perfect = new LegacyNativeClientItemPerfectionRecord { ItemUID = 3, Level = 4, OwnerUID = 20, Progres = 31, OwnerName = "Smith", SpecialText = "Mark" };
        File.WriteAllBytes(scope.ItemPath, ItemsFile(records.Length, records, perfect));
        LegacyNativeServerDatabasePersistence.LoadClientItems(scope.Client);
        Assert.Equal(2u, scope.Client.DemonExterminator!.ItemUID);
        Assert.Equal(2u, Assert.Single(scope.Client.Inventory!.ClientItems).Value.Uid);
        var gear = scope.Client.Equipment!.ClientItems[3]; Assert.Equal((byte)12, gear.Plus); Assert.Equal((byte)7, gear.Bless);
        Assert.Equal((ushort)0, gear.Inscribed); Assert.Equal(4u, gear.PerfectionLevel); Assert.Equal(20u, gear.OwnerUID);
        Assert.Equal(31u, gear.PerfectionProgress); Assert.Equal("Smith", gear.OwnerName); Assert.Equal("Mark", gear.Signature);
        Assert.Same(scope.Client.Equipment.ClientItems[4], scope.Client.MyWardrobe!.Items[0][4]);
        Assert.Equal(5u, scope.Client.Warehouse!.ClientItems[900][5].Uid);
        Assert.Equal(6u, scope.Client.GearMythsoulBag![6].Uid); Assert.Equal(7u, scope.Client.RelicFuse![7].Uid);
        Assert.Equal(new uint[] { 8, 9 }, scope.Client.Rune!.Objects.Select(i => i.Uid));
        Assert.Equal((ushort)212, scope.Client.Rune.Objects.Single(i => i.Uid == 9).Position); // Actual prepared item follows the validated fallback.
        Assert.Empty(scope.Queue); using var reopened = File.OpenRead(scope.ItemPath); Assert.True(reopened.CanRead);
    });

    [Fact]
    public Task DepositKeepsParentRouteAndDepositedPerfectionHydratesOnlyTheActualChild() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); scope.Metadata(410009); scope.Metadata(750000);
        var outer = Row(50, position:4); outer.DepositeCount = 1;
        var child = Row(77, 750000, 54, 900); child.Plus = 255; child.Inscribed = 7;
        var perfect = new LegacyNativeClientItemPerfectionRecord { ItemUID = 77, Level = 6, OwnerUID = 90, OwnerName = "Child", SpecialText = "Raw" };
        File.WriteAllBytes(scope.ItemPath, ItemsFile(1, new[] { outer, child }, perfect));
        LegacyNativeServerDatabasePersistence.LoadClientItems(scope.Client);
        Assert.Equal(77u, scope.Client.DemonExterminator!.ItemUID);
        Assert.Empty(scope.Client.Inventory!.ClientItems); Assert.Empty(scope.Client.Warehouse!.ClientItems);
        var routed = Assert.Single(scope.Client.Equipment!.ClientItems).Value;
        Assert.Same(routed, scope.Client.Equipment.ClientItems[50]);
        Assert.Equal(50u, routed.Uid); Assert.Equal((ushort)4, routed.Position);
        var actualChild = routed.Deposite[77]; Assert.Equal((byte)255, actualChild.Plus); Assert.Equal((ushort)0, actualChild.Inscribed);
        Assert.Equal(6u, actualChild.PerfectionLevel); Assert.Equal(90u, actualChild.OwnerUID); Assert.Equal("Child", actualChild.OwnerName);
        Assert.Equal(0u, routed.PerfectionLevel); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task MissingFileAndZeroCountDoNotInventItemOwnersOrLoadingSuccess(bool exists) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles();
        scope.Client.Inventory = null; scope.Client.Equipment = null; scope.Client.Warehouse = null; scope.Client.Rune = null;
        if (exists) File.WriteAllBytes(scope.ItemPath, ItemsFile(0, Array.Empty<LegacyNativeClientItemRecord>()));
        LegacyNativeServerDatabasePersistence.LoadClientItems(scope.Client);
        Assert.Null(scope.Client.Inventory); Assert.Null(scope.Client.Equipment); Assert.Null(scope.Client.Warehouse); Assert.Null(scope.Client.Rune);
        Assert.False(scope.Client.FullLoading); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task PartialPostludeCountRefusesBeforePublishingAnyItem() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); scope.Metadata(410009);
        var bytes = new List<byte>(BitConverter.GetBytes(1)); bytes.AddRange(Raw(Row(71))); bytes.Add(0); bytes.Add(0);
        File.WriteAllBytes(scope.ItemPath, bytes.ToArray());
        var inventory = scope.Client.Inventory!.ClientItems;
        Assert.Throws<EndOfStreamException>(() => LegacyNativeServerDatabasePersistence.LoadClientItems(scope.Client));
        Assert.Same(inventory, scope.Client.Inventory.ClientItems); Assert.Empty(inventory); Assert.Empty(scope.Queue);
        Assert.Equal(bytes.ToArray(), File.ReadAllBytes(scope.ItemPath));
        using var reopened = File.Open(scope.ItemPath, FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.True(reopened.CanRead);
    });
}
