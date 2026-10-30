using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.NativeItemSpellCallerFixture;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class OwnedNativeItemJoinTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualAdmissionPublishesAllRealContainersAndTheExactCanonicalSnapshot(bool rune)
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(rune, async f =>
        {
            var globalMetadata = LegacyNativePoolItems.ItemsBase;
            Assert.Null(f.Native.Inventory); Assert.Null(f.Native.Rune);
            await f.Login();

            var binding = RequireGraph(f);
            Assert.NotNull(f.Native.Inventory); Assert.NotNull(f.Native.Equipment);
            Assert.NotNull(f.Native.Warehouse); Assert.NotNull(f.Native.MyWardrobe);
            Assert.NotNull(f.Native.Rune); Assert.NotNull(f.Native.RelicFuse); Assert.NotNull(f.Native.GearMythsoulBag);
            Assert.Same(globalMetadata, LegacyNativePoolItems.ItemsBase);
            if (rune)
            {
                var actual = Assert.Single(f.Native.Rune!.Objects, row => row.Uid == Fixture.ItemUid);
                Assert.Equal(4020517u, actual.ITEM_ID); Assert.Equal((ushort)102, actual.Position);
                Assert.Equal(1416016u, actual.SpellID);
            }
            else Assert.Equal(725016u, Assert.Single(f.Native.Inventory!.ClientItems).Value.ITEM_ID);
            Assert.Same(f.Current.PlayerItems, binding.Items.Image.FinalSnapshot);
        });
    }

    [Theory]
    [InlineData(ItemType.RequiredClassFieldIndex)]
    [InlineData(ItemType.RuneSpellIdFieldIndex)]
    public async Task MalformedActualRuneMetadataRefusesBeforePrefixFilesOrNativeContainerAdoption(int index)
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(true, async f =>
        {
            var row = Assert.Single(f.Catalog.Items, item => item.Id == 4020517);
            Assert.IsType<string[]>(row.Fields)[index] = "malformed-owned-field";
            var before = f.Files();

            await f.Login();

            Assert.False(f.Store.TryGet(f.Connection, out _));
            Assert.Null(f.Native.OrdinarySpellBinding);
            Assert.Null(f.Native.Inventory); Assert.Null(f.Native.Equipment); Assert.Null(f.Native.Rune);
            Assert.Equal(0u, f.Native.Player.UID);
            f.CheckFiles(before);
        });
    }

    [Fact]
    public async Task CentralSetKeepsRealContainersAndReusesAnUnchangedWholeImage()
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async f =>
        {
            await f.Login(); var binding = RequireGraph(f);
            var inventory = f.Native.Inventory; var equipment = f.Native.Equipment;
            var image = binding.Items.Image;
            var item = Assert.Single(inventory!.ClientItems).Value;

            f.Store.Set(f.Connection, f.Current with { ActiveNpcId = 123 });

            Assert.Same(image, RequireGraph(f).Items.Image);
            Assert.Same(item, Assert.Single(inventory.ClientItems).Value);
            Assert.Same(inventory, f.Native.Inventory); Assert.Same(equipment, f.Native.Equipment);
            Assert.Equal(123u, f.Current.ActiveNpcId);
        });
    }

    [Fact]
    public async Task CentralCasPublishesCanonicalItemsAndDoesNotRetainTheDetachedProposalAsAuthority()
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async f =>
        {
            await f.Login(); var before = f.Current; var inventory = f.Native.Inventory;
            var source = Assert.Single(before.PlayerItems.InventoryItems).WithBound(1);
            source.OwnerUid = 77; source.OwnerName = "Owned"; source.Signature = "Before";
            var proposed = before with { PlayerItems = before.PlayerItems with { InventoryItems = new[] { source } } };

            Assert.True(f.Store.TryCompareAndSet(f.Connection, before, proposed, out var final));

            Assert.Same(final, f.Current); var binding = RequireGraph(f);
            Assert.Same(binding.ItemSnapshot, f.Current.PlayerItems);
            Assert.Same(inventory, f.Native.Inventory);
            var actual = Assert.Single(inventory!.ClientItems).Value;
            Assert.Equal((byte)1, actual.Bound); Assert.Equal("Owned", actual.OwnerName);
            source.OwnerName = "External"; source.Signature = "Changed";
            Assert.Equal("Owned", Assert.Single(f.Current.PlayerItems.InventoryItems).OwnerName);
            Assert.Equal("Before", actual.Signature);
            f.Store.Set(f.Connection, f.Current with { ActiveNpcId = 124 });
            RequireGraph(f); Assert.Equal("Owned", Assert.Single(inventory.ClientItems).Value.OwnerName);
        });
    }

    [Fact]
    public async Task PartialPreexistingRealContainersAreNeverOverwrittenAtAdmission()
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async f =>
        {
            var partial = new LegacyNativeInventoryRuntime(f.Native);
            f.Native.Inventory = partial;
            var before = f.Files();

            await f.Login();

            Assert.False(f.Store.TryGet(f.Connection, out _));
            Assert.Same(partial, f.Native.Inventory); Assert.Empty(partial.ClientItems);
            Assert.Null(f.Native.Equipment); Assert.Null(f.Native.Rune);
            Assert.Null(f.Native.OrdinarySpellBinding); f.CheckFiles(before);
        });
    }

    [Fact]
    public async Task RawNativeDriftRefusesTheNextCentralPublicationWithoutUndoingTheExternalWrite()
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async f =>
        {
            await f.Login(); var before = f.Current; var files = f.Files();
            var actual = Assert.Single(f.Native.Inventory!.ClientItems).Value;
            byte changed = unchecked((byte)(actual.Bound ^ 1)); actual.Bound = changed;

            RefusesCurrentOwner(() => f.Store.Set(f.Connection, before with { ActiveNpcId = 125 }));

            Assert.Same(before, f.Current); Assert.Equal(changed, actual.Bound); f.CheckFiles(files);
        });
    }

    [Fact]
    public async Task ActualHolderRetirementPermanentlyRetiresItsJoinedGraphAndForbidsUnboundReuse()
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async f =>
        {
            await f.Login(); var before = f.Current; var binding = RequireGraph(f); var graph = binding.Items;
            Assert.True(f.Store.TryRemove(f.Connection, out var removed));

            Assert.Same(before, removed); Assert.True(binding.IsRetired);
            Assert.False(graph.PhysicalOwnerIsCurrentPlain());
            Assert.False(f.Store.IsBoundSpellOwnerCurrent(binding));
            RefusesCurrentOwner(() => f.Store.Set(f.Connection, before));
            Assert.False(f.Store.TryGet(f.Connection, out _));
        });
    }

    private static LegacyOrdinarySpellBinding RequireGraph(Fixture f)
    {
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        Assert.Same(f.Store, binding.Store); Assert.Same(f.Connection, binding.Connection);
        Assert.Same(f.Native, binding.Client); Assert.Same(f.Native.Player, binding.Player);
        Assert.Same(f.Current.PlayerItems, binding.ItemSnapshot);
        Assert.Same(f.Current.PlayerItems, binding.Items.Image.FinalSnapshot);
        Assert.True(binding.Items.PhysicalOwnerIsCurrentPlain());
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.Equal(f.Current.PlayerItems.InventoryItems.Select(row => row.Uid).Order(),
            f.Native.Inventory!.ClientItems.Values.Select(row => row.Uid).Order());
        Assert.Equal(f.Current.PlayerItems.EquipmentItems.Select(row => row.Uid).Order(),
            f.Native.Equipment!.ClientItems.Values.Select(row => row.Uid).Order());
        Assert.Equal(f.Current.PlayerItems.RuneItems.Select(row => row.Uid).Order(),
            f.Native.Rune!.Objects.Select(row => row.Uid).Order());
        return binding;
    }

    private static void RefusesCurrentOwner(Action action)
    {
        var error = Record.Exception(action);
        Assert.True(error is InvalidDataException or InvalidOperationException,
            "Expected a concrete invalid-image/current-owner refusal, received " + error?.GetType().Name);
    }
}
