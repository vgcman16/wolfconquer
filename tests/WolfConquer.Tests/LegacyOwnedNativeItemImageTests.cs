using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;
using Image = WolfConquer.Core.World.LegacyOwnedNativeItemImage;
using Item = WolfConquer.Core.Data.PlayerItemSnapshotItem;
using Destination = WolfConquer.Core.World.LegacyNativeItemLoadOwners.Destination;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOwnedNativeItemImageTests
{
    private const ConquerProtocolProfile Profile = ConquerProtocolProfile.VisibleDx8D13;
    private static Item Row(uint uid, uint id = 410009, ushort position = 0, uint warehouse = 0,
        Item[]? children = null) => new() { Uid = uid, ItemId = id, Position = position, WarehouseId = warehouse,
            Plus = 255, Bless = 254, Inscribed = 9, DepositedItems = children ?? Array.Empty<Item>() };
    private static PlayerItemSnapshot Snapshot(Item[]? inventory = null, Item[]? equipment = null, Item[]? runes = null,
        Item[]? wardrobe = null, Item[]? relic = null, Item[]? gear = null, Item[]? warehouse = null) =>
        new(inventory ?? Array.Empty<Item>(), relic ?? Array.Empty<Item>(), gear ?? Array.Empty<Item>(),
            equipment ?? Array.Empty<Item>(), runes ?? Array.Empty<Item>(), wardrobe ?? Array.Empty<Item>())
        { WarehouseItems = warehouse ?? Array.Empty<Item>() };
    private static ItemType Rune(uint id = 4020002, string spell = "4020002", string job = "2001")
    {
        var fields = Enumerable.Repeat("0", 67).ToArray(); fields[0] = id.ToString(CultureInfo.InvariantCulture);
        fields[1] = "OwnedRune"; fields[2] = job; fields[25] = spell;
        return new((int)id, fields[1], fields);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualNullCatalogIsPreservedForEmptyOrEntirelyNonRuneNativeImages(bool populated)
    {
        var previous = LegacyNativePoolItems.ItemsBase;
        try
        {
            LegacyNativePoolItems.ItemsBase = null;
            var source = populated ? Snapshot(new[] { Row(71, 205901, children: new[] { Row(72) }) }) : PlayerItemSnapshot.Empty;
            var prepared = Image.Prepare(null, source, Profile);
            Assert.Null(prepared.Image.Catalog);
            Assert.Equal(populated ? 1 : 0, prepared.Roots.Count);
            if (populated)
            {
                Assert.Equal(205901u, prepared.Roots[0].Item.ITEM_ID);
                Assert.Same(prepared.Roots[0].Children[0].Item, prepared.Roots[0].Item.Deposite[72]);
            }
            prepared.RequireCurrent(); Assert.True(prepared.IsCurrentPlain());
            var repeated = Image.Prepare(null, prepared.FinalSnapshot, Profile, prepared.Image);
            Assert.Same(prepared.Image, repeated.Image); Assert.Null(repeated.Image.Catalog);
            Assert.Same(prepared.FinalSnapshot, repeated.FinalSnapshot); Assert.Null(LegacyNativePoolItems.ItemsBase);
        }
        finally { LegacyNativePoolItems.ItemsBase = previous; }
    }

    [Fact]
    public void ChangedNonRuneParentWithActualNullCatalogReusesItsUnchangedGenuineChild()
    {
        var source = Snapshot(new[] { Row(71, children: new[] { Row(72) }) });
        var old = Image.Prepare(null, source, Profile).Image;
        var changed = Snapshot(new[] { Row(71, children: new[] { Row(72) }).WithBound(1) });
        var next = Image.Prepare(null, changed, Profile, old);
        Assert.Null(next.Image.Catalog); Assert.NotSame(old, next.Image);
        Assert.NotSame(old.Roots[0].Item, next.Roots[0].Item);
        Assert.Same(old.Roots[0].Children[0].Item, next.Roots[0].Children[0].Item);
        Assert.True(old.IsCurrentPlain()); next.RequireCurrent(); Assert.True(next.IsCurrentPlain());
    }

    [Fact]
    public void ActualEmptySnapshotAndItemTableParserFactoriesSupplySupportedPlainEvidence()
    {
        var parsed = ItemTypeParser.ParseLine(string.Join("@@", Rune().Fields), 67);
        Assert.IsType<string[]>(parsed.Fields);
        var data = LegacyItemWorldDataFixture.Create(parsed);
        var empty = Image.Prepare(data, PlayerItemSnapshot.Empty, Profile);
        Assert.Empty(empty.Roots); Assert.Empty(empty.Placements); Assert.True(empty.IsCurrentPlain());
        // Default DepositedItems and other groups are made by production factories.
        var row = new Item { Uid = 71, ItemId = 4020002, Position = 102 };
        var source = PlayerItemSnapshot.Empty with { RuneItems = new[] { row } };
        var populated = Image.Prepare(data, source, Profile);
        Assert.Equal(4020001u, Assert.Single(populated.Roots).Item.SpellID);
        Assert.True(populated.IsCurrentPlain());
    }

    [Fact]
    public void GenuineScopedRuneUsesActualCatalogAndNeverInstallsGlobalMetadataOrFallbacks()
    {
        var previous = LegacyNativePoolItems.ItemsBase;
        try
        {
            LegacyNativePoolItems.ItemsBase = null;
            var data = LegacyItemWorldDataFixture.Create(Rune());
            var source = Snapshot(runes: new[] { Row(71, 4020002, 102) });
            var plan = Image.Prepare(data, source, Profile);
            var item = Assert.Single(plan.Roots).Item;
            Assert.IsType<LegacyNativeGameItemRuntime>(item);
            Assert.Equal(4020001u, item.SpellID); Assert.Equal(LegacyNativeGameItemFlags.ItemEffect.RuneEffect, item.Effect);
            Assert.Equal((ushort)102, item.Position); Assert.Equal((byte)255, item.Plus); Assert.Equal((byte)254, item.Bless);
            Assert.Equal((ushort)9, item.Inscribed); Assert.Same(source, plan.SourceSnapshot);
            Assert.NotSame(source, plan.FinalSnapshot); Assert.Null(LegacyNativePoolItems.ItemsBase);
            plan.RequireCurrent(); Assert.True(plan.IsCurrentPlain()); Assert.True(plan.Image.IsCurrentPlain());
        }
        finally { LegacyNativePoolItems.ItemsBase = previous; }
    }

    [Fact]
    public void AllCanonicalGroupsAndNativeWardrobeEquipmentAliasUseGenuineDistinctRoots()
    {
        var data = LegacyItemWorldDataFixture.Create();
        var source = Snapshot(inventory: new[] { Row(1, 205901) }, equipment: new[] { Row(2, 181305, 9, 100) },
            wardrobe: new[] { Row(3, 205007, 0, 100) }, relic: new[] { Row(4, position: 213) },
            gear: new[] { Row(5, position: 217) }, warehouse: new[] { Row(6, warehouse: 900) });
        var plan = Image.Prepare(data, source, Profile);
        Assert.Equal(6, plan.Roots.Count); Assert.Equal(7, plan.Placements.Length);
        var garment = plan.Placements.Single(row => row.Destination == Destination.WardrobeGarment).Item;
        Assert.Same(garment, plan.Placements.Single(row => row.Destination == Destination.Equipment).Item);
        Assert.Equal(205901u, plan.Roots.Single(row => row.Item.Uid == 1).Item.ITEM_ID);
        Assert.Equal(900u, plan.Placements.Single(row => row.Destination == Destination.Warehouse).WarehouseId);
        Assert.Equal(6, plan.Roots.Select(node => node.Item).Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.True(plan.IsCurrentPlain());
    }

    [Fact]
    public void ExactPerfectionStringsAndDetachedProposalMutationDoNotCorruptPublishedCanonicalImage()
    {
        var input = Row(71); input.PerfectionLevel = 4; input.OwnerUid = 90;
        input.OwnerName = "狼Smith"; input.Signature = "标记";
        var source = Snapshot(new[] { input }); var data = LegacyItemWorldDataFixture.Create();
        var plan = Image.Prepare(data, source, Profile); var image = plan.Image;
        Assert.Equal("狼Smith", image.Roots[0].Item.OwnerName); Assert.Equal("标记", image.Roots[0].Item.Signature);
        input.PerfectionLevel = 99; input.OwnerName = "Detached";
        Assert.False(plan.IsCurrentPlain()); Assert.Throws<InvalidDataException>(() => plan.RequireCurrent());
        image.RequireCurrent(); Assert.Equal(4u, image.FinalSnapshot.InventoryItems[0].PerfectionLevel);
        Assert.Equal("狼Smith", image.Roots[0].Item.OwnerName);
        var next = Image.Prepare(data, image.FinalSnapshot, Profile, image);
        Assert.Same(image, next.Image); Assert.Same(image, next.BaseImage); Assert.Same(image.FinalSnapshot, next.FinalSnapshot);
    }

    [Fact]
    public void CanonicallyEqualNewProposalReusesWholeImageAndAllActualNativeReferences()
    {
        var data = LegacyItemWorldDataFixture.Create();
        var first = Image.Prepare(data, Snapshot(new[] { Row(71, children: new[] { Row(72) }) }), Profile).Image;
        var next = Image.Prepare(data, Snapshot(new[] { Row(71, children: new[] { Row(72) }) }), Profile, first);
        Assert.Same(first, next.Image); Assert.Same(first.Roots[0].Item, next.Roots[0].Item);
        Assert.Same(first.Roots[0].Children[0].Item, next.Roots[0].Children[0].Item);
    }

    [Fact]
    public void ChangedParentReconstructsOnlyNeededNodesAndRetainsUnchangedChildrenAndRoots()
    {
        var data = LegacyItemWorldDataFixture.Create();
        var source = Snapshot(new[] { Row(71, children: new[] { Row(72), Row(73) }), Row(80) });
        var old = Image.Prepare(data, source, Profile).Image;
        var changed = Row(71, children: new[] { Row(72), Row(73).WithBound(1) }).WithBound(1);
        var next = Image.Prepare(data, Snapshot(new[] { changed, Row(80) }), Profile, old);
        Assert.NotSame(old, next.Image); Assert.NotSame(old.Roots[0].Item, next.Roots[0].Item);
        Assert.Same(old.Roots[0].Children[0].Item, next.Roots[0].Children[0].Item);
        Assert.NotSame(old.Roots[0].Children[1].Item, next.Roots[0].Children[1].Item);
        Assert.Same(old.Roots[1].Item, next.Roots[1].Item);
        Assert.Same(next.Roots[0].Children[0].Item, next.Roots[0].Item.Deposite[72]);
        Assert.True(old.IsCurrentPlain()); Assert.True(next.IsCurrentPlain());
    }

    [Fact]
    public void ExpiredConstructorExtrasRemainDerivedWhileSavedValuesAndUnchangedObjectsSurviveRefresh()
    {
        var row = new Item { Uid = 71, ItemId = 410009, PurificationItemId = 800001, PurificationDuration = 1,
            PurificationAddedOn = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToBinary(), DepositedItems = Array.Empty<Item>() };
        var data = LegacyItemWorldDataFixture.Create(); var image = Image.Prepare(data, Snapshot(new[] { row }), Profile).Image;
        Assert.Equal(default(LegacyNativeItemExtra.Purification), image.Roots[0].Item.Purification);
        Assert.Equal(800001u, image.FinalSnapshot.InventoryItems[0].PurificationItemId);
        Assert.Same(image, Image.Prepare(data, image.FinalSnapshot, Profile, image).Image);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ValidDateTimeKindsArePreservedByTheRealScopedConstructor(int kind)
    {
        var date = new DateTime(2020, 1, 2, 3, 4, 5, (DateTimeKind)kind);
        var row = new Item { Uid = 71, ItemId = 410009, PurificationItemId = 1001,
            PurificationAddedOn = date.ToBinary(), DepositedItems = Array.Empty<Item>() };
        var image = Image.Prepare(LegacyItemWorldDataFixture.Create(), Snapshot(new[] { row }), Profile).Image;
        Assert.Equal(date.ToBinary(), image.Roots[0].Item.Purification.AddedOn.ToBinary());
        Assert.Equal(date.Kind, image.Roots[0].Item.Purification.AddedOn.Kind);
    }

    [Fact]
    public void FinalPublishedProofDoesNotAllocateAfterWarmup()
    {
        var image = Image.Prepare(LegacyItemWorldDataFixture.Create(Rune()),
            Snapshot(runes: new[] { Row(71, 4020002, 102) }), Profile).Image;
        for (int i = 0; i < 10; i++) Assert.True(image.IsCurrentPlain());
        long before = GC.GetAllocatedBytesForCurrentThread();
        bool valid = true; for (int i = 0; i < 100; i++) valid &= image.IsCurrentPlain();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(valid); Assert.Equal(0, allocated);
    }
}
