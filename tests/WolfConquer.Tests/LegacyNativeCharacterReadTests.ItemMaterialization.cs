using WolfConquer.Core.World;
using Materialization = WolfConquer.Core.World.LegacyNativeItemLoadMaterialization;
using Destination = WolfConquer.Core.World.LegacyNativeItemLoadOwners.Destination;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCharacterReadTests
{
    private static LegacyNativeItemLoadImage MaterializationImage(int roots,
        LegacyNativeClientItemRecord[] records, params LegacyNativeClientItemPerfectionRecord[] perfection) =>
        LegacyNativeItemLoadImage.Parse(ItemsFile(roots, records, perfection));

    private static void AssertMaterializationDidNotPublish(Scope scope)
    {
        Assert.Empty(scope.Client.Inventory!.ClientItems); Assert.Empty(scope.Client.Equipment!.ClientItems);
        Assert.Empty(scope.Client.Warehouse!.ClientItems); Assert.Empty(scope.Client.Rune!.Objects);
        Assert.Empty(scope.Client.RelicFuse!); Assert.Empty(scope.Client.GearMythsoulBag!);
        Assert.Empty(scope.Client.MyWardrobe!.GetAllItems()); Assert.Empty(scope.Queue);
        Assert.False(scope.Client.FullLoading); Assert.Equal(0u, scope.Client.DemonExterminator!.ItemUID);
    }

    [Theory]
    [InlineData(410009u, 0, 0u, (int)Destination.Inventory)]
    [InlineData(410009u, 4, 0u, (int)Destination.Equipment)]
    [InlineData(410009u, 63, 0u, (int)Destination.Equipment)]
    [InlineData(410009u, 213, 0u, (int)Destination.Relic)]
    [InlineData(410009u, 215, 0u, (int)Destination.Relic)]
    [InlineData(410009u, 217, 0u, (int)Destination.Gear)]
    [InlineData(410009u, 65535, 900u, (int)Destination.Warehouse)]
    [InlineData(4020001u, 211, 0u, (int)Destination.Rune)]
    [InlineData(4020001u, 135, 0u, (int)Destination.Rune)]
    public Task GenuinePreparedRootUsesItsOwnLocationAndDoesNotMutateOwners(
        uint id, int position, uint warehouse, int expectedValue) => NativePacketTestThread.Run(() =>
    {
        Destination expected = (Destination)expectedValue;
        using var scope = new Scope(); scope.Metadata(id);
        var record = Row(71, id, (ushort)position, warehouse);
        byte[] bytes = ItemsFile(1, [record]); byte[] original = bytes.ToArray();
        var image = LegacyNativeItemLoadImage.Parse(bytes);
        Assert.Equal(expected, Materialization.RequiredDestinations(image));
        using var owners = LegacyNativeItemLoadOwners.Capture(scope.Client, scope.Player, expected);
        var plan = Materialization.Prepare(image, scope.Client, scope.Player); plan.RequireCurrent();
        var placement = Assert.Single(plan.Placements);
        Assert.Equal(expected, placement.Destination); Assert.Equal(71u, placement.Item.Uid);
        Assert.Equal((ushort)position, placement.Item.Position); Assert.Equal(warehouse, placement.Item.WH_ID);
        Assert.Equal(expected == Destination.Warehouse ? warehouse : 0, placement.WarehouseId);
        Assert.Equal(original, bytes); Assert.Null(plan.QuestUid); AssertMaterializationDidNotPublish(scope);
    });

    [Fact]
    public Task NonRuneMaterializationDoesNotInventAnUnneededMetadataCatalog() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); LegacyNativePoolItems.ItemsBase = null;
        var image = MaterializationImage(1, [Row(71)]);
        using var owners = LegacyNativeItemLoadOwners.Capture(scope.Client, scope.Player, Destination.Inventory);
        var plan = Materialization.Prepare(image, scope.Client, scope.Player);
        Assert.Equal(410009u, Assert.Single(plan.Placements).Item.ITEM_ID); plan.RequireCurrent();
        Assert.Null(LegacyNativePoolItems.ItemsBase); AssertMaterializationDidNotPublish(scope);
    });

    [Theory]
    [InlineData(181305u, 9, (int)Destination.WardrobeGarment)]
    [InlineData(205007u, 17, (int)Destination.WardrobeMount)]
    public Task GenuineWardrobeEquipmentPlacementsKeepTheSamePreparedAlias(uint id, int position, int bucketValue) =>
        NativePacketTestThread.Run(() =>
    {
        Destination bucket = (Destination)bucketValue;
        using var scope = new Scope(); var image = MaterializationImage(1, [Row(71, id, (ushort)position, 100)]);
        Destination required = bucket | Destination.Equipment;
        Assert.Equal(required, Materialization.RequiredDestinations(image));
        using var owners = LegacyNativeItemLoadOwners.Capture(scope.Client, scope.Player, required);
        var plan = Materialization.Prepare(image, scope.Client, scope.Player); plan.RequireCurrent();
        Assert.Equal(2, plan.Placements.Length);
        Assert.Equal(bucket, plan.Placements[0].Destination);
        Assert.Equal(Destination.Equipment, plan.Placements[1].Destination);
        Assert.Same(plan.Placements[0].Item, plan.Placements[1].Item);
        Assert.Equal(100u, plan.Placements[0].Item.WH_ID); AssertMaterializationDidNotPublish(scope);
    });

    [Fact]
    public Task ActualRuneConstructorAndJobRuleChangeThePreparedItemNotTheSavedRow() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var metadata = scope.Metadata(4020002, 2001);
        metadata.RuneSpellID = 4020002;
        var image = MaterializationImage(1, [Row(71, 4020002, 102)]);
        using var owners = LegacyNativeItemLoadOwners.Capture(scope.Client, scope.Player, Destination.Rune);
        var plan = Materialization.Prepare(image, scope.Client, scope.Player); plan.RequireCurrent();
        var item = Assert.Single(plan.Placements).Item;
        Assert.Equal((ushort)212, item.Position); Assert.Equal((ushort)102, image.Roots[0].Record.Position);
        Assert.Equal(4020001u, item.SpellID); Assert.Equal(LegacyNativeGameItemFlags.ItemEffect.RuneEffect, item.Effect);
        AssertMaterializationDidNotPublish(scope);
    });

    [Fact]
    public Task ChildRuneRunsTheGenuineConstructorButStaysOnlyInItsParentDepositMap() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var metadata = scope.Metadata(4036109); metadata.RuneSpellID = 1234;
        var root = Row(71); root.DepositeCount = 1;
        var image = MaterializationImage(1, [root, Row(72, 4036109, 103)]);
        Assert.Equal(Destination.Inventory, Materialization.RequiredDestinations(image));
        using var owners = LegacyNativeItemLoadOwners.Capture(scope.Client, scope.Player, Destination.Inventory);
        var plan = Materialization.Prepare(image, scope.Client, scope.Player);
        var parent = Assert.Single(plan.Placements).Item; var child = Assert.Single(parent.Deposite).Value;
        Assert.NotSame(parent, child); Assert.Equal(72u, child.Uid); Assert.Equal((ushort)103, child.Position);
        Assert.Equal(1234u, child.SpellID); Assert.Equal(LegacyNativeGameItemFlags.ItemEffect.None, child.Effect);
        plan.RequireCurrent(); AssertMaterializationDidNotPublish(scope);
    });

    [Fact]
    public Task ParentRoutingOuterSanitationAndChildPerfectionUseTheActualNewReferences() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var root = Row(50, position: 4); root.DepositeCount = 1; root.Plus = 255; root.Bless = 9;
        var child = Row(77, 750000, 54, 900); child.Plus = 255; child.Bless = 9;
        var perfect = new LegacyNativeClientItemPerfectionRecord
        { ItemUID = 77, Level = 6, Progres = 19, OwnerUID = 90, OwnerName = "Child", SpecialText = "Raw" };
        var image = MaterializationImage(1, [root, child], perfect);
        Assert.Equal(Destination.Equipment, Materialization.RequiredDestinations(image));
        using var owners = LegacyNativeItemLoadOwners.Capture(scope.Client, scope.Player, Destination.Equipment);
        var plan = Materialization.Prepare(image, scope.Client, scope.Player); plan.RequireCurrent();
        var parent = Assert.Single(plan.Placements).Item; var deposited = parent.Deposite[77];
        Assert.Equal(50u, parent.Uid); Assert.Equal((ushort)4, parent.Position); Assert.Equal(0u, parent.WH_ID);
        Assert.Equal((byte)12, parent.Plus); Assert.Equal((byte)7, parent.Bless);
        Assert.Equal((byte)255, deposited.Plus); Assert.Equal((byte)9, deposited.Bless);
        Assert.Equal(6u, deposited.PerfectionLevel); Assert.Equal(19u, deposited.PerfectionProgress);
        Assert.Equal(90u, deposited.OwnerUID); Assert.Equal("Child", deposited.OwnerName); Assert.Equal("Raw", deposited.Signature);
        Assert.Equal(0u, parent.PerfectionLevel); Assert.Equal(77u, plan.QuestUid);
        Assert.Equal((byte)255, image.Roots[0].Record.Plus); AssertMaterializationDidNotPublish(scope);
    });

    [Fact]
    public Task PreparedQuestMarkerUsesDeterministicRootThenChildUidEncounterOrder() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var first = Row(71, 750000); first.DepositeCount = 1;
        var image = MaterializationImage(2, [first, Row(72, 750000), Row(73, 750000)]);
        using var owners = LegacyNativeItemLoadOwners.Capture(scope.Client, scope.Player, Destination.Inventory);
        var plan = Materialization.Prepare(image, scope.Client, scope.Player);
        Assert.Equal(73u, plan.QuestUid); Assert.Equal(new uint[] { 71, 73 }, plan.Placements.Select(row => row.Item.Uid));
        Assert.Equal(72u, plan.Placements[0].Item.Deposite[72].Uid); AssertMaterializationDidNotPublish(scope);
    });

    [Fact]
    public Task FilteredOnlyCompleteImageNeedsNoOwnersOrMetadataButFilteredPerfectionRefuses() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); LegacyNativePoolItems.ItemsBase = null;
        var image = MaterializationImage(1, [Row(71, 205901)]);
        Assert.Equal(Destination.None, Materialization.RequiredDestinations(image));
        var perfection = new LegacyNativeClientItemPerfectionRecord { ItemUID = 71, Level = 1 };
        var bad = MaterializationImage(1, [Row(71, 205901)], perfection);
        Assert.Throws<InvalidDataException>(() => Materialization.RequiredDestinations(bad));
        Assert.Null(LegacyNativePoolItems.ItemsBase); AssertMaterializationDidNotPublish(scope);
    });
    [Fact]
    public Task SupportedWardrobeEquipmentPerfectionHydratesTheSameGenuineAlias() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var perfect = new LegacyNativeClientItemPerfectionRecord
        { ItemUID = 71, Level = 3, OwnerUID = 20, OwnerName = "Smith", SpecialText = "Alias" };
        var image = MaterializationImage(1, [Row(71, 181305, 9, 100)], perfect);
        Destination required = Destination.WardrobeGarment | Destination.Equipment;
        using var owners = LegacyNativeItemLoadOwners.Capture(scope.Client, scope.Player, required);
        var plan = Materialization.Prepare(image, scope.Client, scope.Player); plan.RequireCurrent();
        Assert.Same(plan.Placements[0].Item, plan.Placements[1].Item);
        Assert.Equal(3u, plan.Placements[0].Item.PerfectionLevel);
        Assert.Equal("Smith", plan.Placements[1].Item.OwnerName);
        Assert.Equal("Alias", plan.Placements[1].Item.Signature); AssertMaterializationDidNotPublish(scope);
    });

    [Fact]
    public Task PerfectionNeverHydratesAnExistingOwnerItemWithTheSameIncomingUid() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var existing = scope.Item(71); existing.OwnerName = "Existing";
        scope.Client.Inventory!.ClientItems[71] = existing;
        var perfect = new LegacyNativeClientItemPerfectionRecord
        { ItemUID = 71, Level = 4, OwnerUID = 20, OwnerName = "New", SpecialText = "Prepared" };
        var image = MaterializationImage(1, [Row(71)], perfect);
        using var owners = LegacyNativeItemLoadOwners.Capture(scope.Client, scope.Player, Destination.Inventory);
        var plan = Materialization.Prepare(image, scope.Client, scope.Player); plan.RequireCurrent();
        var incoming = Assert.Single(plan.Placements).Item;
        Assert.NotSame(existing, incoming); Assert.Equal(4u, incoming.PerfectionLevel);
        Assert.Equal("New", incoming.OwnerName); Assert.Equal(0u, existing.PerfectionLevel);
        Assert.Equal("Existing", existing.OwnerName); Assert.Same(existing, scope.Client.Inventory.ClientItems[71]);
        Assert.Throws<InvalidDataException>(() => owners.Prepare(plan.Placements, plan.QuestUid));
        Assert.Same(existing, Assert.Single(scope.Client.Inventory.ClientItems).Value);
        Assert.Empty(scope.Queue); Assert.Equal(0u, scope.Client.DemonExterminator!.ItemUID);
    });

}
