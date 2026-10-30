using System.Collections;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedNativeItemJoinTests
{
    [Fact]
    public async Task CompleteCanonicalEquipAndDropPublicationRetainsAllSevenActualOwnersAndUnchangedBagRevisions()
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async f =>
        {
            await f.Login();
            var binding = RequireGraph(f);
            var inventory = f.Native.Inventory!;
            var equipment = f.Native.Equipment!;
            var warehouse = f.Native.Warehouse!;
            var wardrobe = f.Native.MyWardrobe!;
            var rune = f.Native.Rune!;
            var relic = f.Native.RelicFuse!;
            var gear = f.Native.GearMythsoulBag!;
            long relicRevision = relic.Revision;
            long gearRevision = gear.Revision;
            var source = Assert.Single(f.Current.PlayerItems.InventoryItems).WithPosition(4);
            var equipped = f.Current.PlayerItems with
            {
                InventoryItems = Array.Empty<PlayerItemSnapshotItem>(),
                EquipmentItems = new[] { source }
            };

            f.Store.Set(f.Connection, f.Current with { PlayerItems = equipped });

            RequireGraph(f);
            Assert.Empty(inventory.ClientItems);
            Assert.Equal(source.Uid, Assert.Single(equipment.ClientItems).Value.Uid);
            Assert.Equal((ushort)4, Assert.Single(equipment.ClientItems).Value.Position);
            Assert.Equal(relicRevision, relic.Revision);
            Assert.Equal(gearRevision, gear.Revision);
            var dropped = f.Current.PlayerItems with { EquipmentItems = Array.Empty<PlayerItemSnapshotItem>() };

            f.Store.Set(f.Connection, f.Current with { PlayerItems = dropped });

            RequireGraph(f);
            Assert.Empty(inventory.ClientItems);
            Assert.Empty(equipment.ClientItems);
            Assert.Same(inventory, f.Native.Inventory);
            Assert.Same(equipment, f.Native.Equipment);
            Assert.Same(warehouse, f.Native.Warehouse);
            Assert.Same(wardrobe, f.Native.MyWardrobe);
            Assert.Same(rune, f.Native.Rune);
            Assert.Same(relic, f.Native.RelicFuse);
            Assert.Same(gear, f.Native.GearMythsoulBag);
            Assert.Same(binding, f.Native.OrdinarySpellBinding);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualJoinedAndRetiredContainersRejectStandaloneWritersIncludingBagInterfaceCasts(bool retired)
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async f =>
        {
            await f.Login();
            var binding = RequireGraph(f);
            var before = f.Current;
            var files = f.Files();
            var inventory = f.Native.Inventory!;
            var equipment = f.Native.Equipment!;
            var wardrobe = f.Native.MyWardrobe!;
            var warehouse = f.Native.Warehouse!;
            var rune = f.Native.Rune!;
            var bag = f.Native.RelicFuse!;
            long revision = bag.Revision;
            if (retired)
                Assert.True(f.Store.TryRemove(f.Connection, out _));
            var added = new LegacyNativeGameItemRuntime { Uid = 0, ITEM_ID = 410009 };

            Assert.Throws<InvalidOperationException>(() => inventory.AddDBItem(added));
            Assert.Throws<InvalidOperationException>(() => inventory.CheakUp(added));
            Assert.Equal(0u, added.Uid);
            var error = Assert.Throws<TargetInvocationException>(() => typeof(LegacyNativeEquipItemStorage)
                .GetMethod("CheakUp", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(equipment, new object[] { added }));
            Assert.IsType<InvalidOperationException>(error.InnerException);
            Assert.Equal(0u, added.Uid);
            var garment = new LegacyNativeGameItemRuntime { Uid = 900, ITEM_ID = 181305 };
            Assert.False(wardrobe.AddItem(garment));
            Assert.Equal(0u, garment.WH_ID);
            Assert.False(warehouse.AddItem(garment, 900));
            Assert.Equal(0u, garment.WH_ID);
            Assert.False(rune.Add(garment));
            Assert.Throws<InvalidOperationException>(() => warehouse.RemoveInscribedItems());
            IDictionary<uint, LegacyNativeGameItemRuntime> generic = bag;
            Assert.Throws<InvalidOperationException>(() => generic.Add(900, garment));
            IDictionary legacy = bag;
            Assert.Throws<InvalidOperationException>(() => legacy[900u] = garment);
            Assert.Equal(revision, bag.Revision);
            Assert.Empty(bag);
            Assert.Empty(wardrobe.Items[0]);
            Assert.Empty(warehouse.ClientItems);
            Assert.Single(inventory.ClientItems);
            Assert.Empty(equipment.ClientItems);
            f.CheckFiles(files);
            if (retired)
            {
                Assert.True(binding.IsRetired);
                Assert.False(binding.Items.PhysicalOwnerIsCurrentPlain());
            }
            else
            {
                Assert.Same(before, f.Current);
                RequireGraph(f);
            }
        });
    }

    [Fact]
    public async Task RawInPlaceMapDriftIsRejectedBeforeIncomingSnapshotCallbacksAreInvoked()
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async f =>
        {
            await f.Login();
            var before = f.Current;
            var files = f.Files();
            var external = new LegacyNativeGameItemRuntime { Uid = 900, ITEM_ID = 410009 };
            Assert.True(f.Native.Inventory!.ClientItems.TryAdd(900, external));
            var source = new ItemCallbackList(before.PlayerItems.InventoryItems.ToArray());
            var proposal = before with { PlayerItems = before.PlayerItems with { InventoryItems = source } };
            source.Armed = true;

            RefusesCurrentOwner(() => f.Store.Set(f.Connection, proposal));

            Assert.False(source.Triggered);
            Assert.Same(before, f.Current);
            Assert.Same(external, f.Native.Inventory.ClientItems[900]);
            f.CheckFiles(files);
        });
    }

    [Fact]
    public async Task OngoingCanonicalPublicationDoesNotReuseTheLoginOnlyFullLoadingFalseRequirement()
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async f =>
        {
            await f.Login();
            var graph = RequireGraph(f).Items;
            f.Native.FullLoading = true;

            f.Store.Set(f.Connection, f.Current with { ActiveNpcId = 991 });

            Assert.Equal(991u, f.Current.ActiveNpcId);
            Assert.Same(graph, RequireGraph(f).Items);
        });
    }

    private sealed class ItemCallbackList(PlayerItemSnapshotItem[] rows) : IReadOnlyList<PlayerItemSnapshotItem>
    {
        internal bool Armed, Triggered;

        private void Observe()
        {
            if (Armed)
            {
                Armed = false;
                Triggered = true;
            }
        }

        public int Count
        {
            get { Observe(); return rows.Length; }
        }

        public PlayerItemSnapshotItem this[int index]
        {
            get { Observe(); return rows[index]; }
        }

        public IEnumerator<PlayerItemSnapshotItem> GetEnumerator()
        {
            Observe();
            return ((IEnumerable<PlayerItemSnapshotItem>)rows).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
