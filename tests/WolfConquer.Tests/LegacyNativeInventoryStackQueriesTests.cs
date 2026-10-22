using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.World;
using Inventory = WolfConquer.Core.World.LegacyNativeInventoryRuntime;
using Item = WolfConquer.Core.World.LegacyNativeGameItemRuntime;

namespace WolfConquer.Tests;

// Three complete native query bodies only. No Inventory.Add/Update, SQL bypass,
// native queue bootstrap, meteor removal/combining or host gameplay proof.
public sealed class LegacyNativeInventoryStackQueriesTests
{
    [Fact]
    public void NativeInventoryAddModeRetainsIntStorageAndLiteralValues()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(LegacyNativeInventoryAddMode)));
        Assert.Equal(new[] { "ADD", "MOVE", "REMOVE" }, Enum.GetNames<LegacyNativeInventoryAddMode>());
        Assert.Equal(0, (int)LegacyNativeInventoryAddMode.ADD);
        Assert.Equal(1, (int)LegacyNativeInventoryAddMode.MOVE);
        Assert.Equal(2, (int)LegacyNativeInventoryAddMode.REMOVE);
    }

    [Theory]
    [InlineData((ushort)0, (ushort)0, true)]
    [InlineData((ushort)0, (ushort)1, true)]
    [InlineData((ushort)0, (ushort)2, false)]
    [InlineData((ushort)1, (ushort)0, true)]
    [InlineData((ushort)1, (ushort)1, true)]
    [InlineData((ushort)1, (ushort)2, false)]
    [InlineData(ushort.MaxValue, ushort.MaxValue, true)]
    public void KeyedStackLookupKeepsNativeZeroStackExceptionAndExactUnsignedWidth(
        ushort stack, ushort requested, bool expected)
    {
        var inventory = new Inventory(null);
        var item = Physical(700, 600000, stack);
        inventory.ClientItems[12] = item;

        Assert.Equal(expected, inventory.ContainItemWithStack(12, requested));

        Assert.Same(item, inventory.ClientItems[12]);
        Assert.Equal(stack, item.StackSize);
        Assert.Equal(700u, item.Uid);
        Assert.Null(inventory.Owner);
    }

    [Fact]
    public void StackLookupUsesDictionaryKeyNotItemUidIdOrAggregateAliases()
    {
        var inventory = new Inventory(null);
        var item = Physical(700, 600000, 3);
        inventory.ClientItems[12] = item;
        inventory.ClientItems[13] = item;
        Assert.True(inventory.ContainItemWithStack(12, 3));
        Assert.True(inventory.ContainItemWithStack(13, 3));
        Assert.False(inventory.ContainItemWithStack(700, 0));
        Assert.False(inventory.ContainItemWithStack(600000, 1));
        Assert.False(inventory.ContainItemWithStack(12, 4));
        Assert.Equal(2, inventory.ClientItems.Count);
        Assert.Same(item, inventory.ClientItems[12]);
        Assert.Same(item, inventory.ClientItems[13]);
    }

    [Fact]
    public void StackLookupObservesReplacementDictionaryAndNativeNullDictionaryFault()
    {
        var inventory = new Inventory(null);
        var held = inventory.ClientItems;
        held[12] = Physical(7, 600000, 5);
        inventory.ClientItems = new ConcurrentDictionary<uint, Item>();
        Assert.False(inventory.ContainItemWithStack(12, 0));
        inventory.ClientItems[12] = Physical(8, 600001, 0);
        Assert.True(inventory.ContainItemWithStack(12, 1));
        Assert.Same(held[12], Assert.Single(held).Value);
        Assert.Equal((ushort)5, held[12].StackSize);
        inventory.ClientItems = null!;
        Assert.Throws<NullReferenceException>(() => inventory.ContainItemWithStack(12, 0));
        Assert.Null(inventory.ClientItems);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrivateNativeSelectorsReturnFreshEmptyOwnersWithoutInitializingClient(bool meteor)
    {
        var inventory = new Inventory(null);
        var one = Select(inventory, meteor);
        var two = Select(inventory, meteor);
        Assert.Empty(one);
        Assert.Empty(two);
        Assert.NotSame(one, two);
        Assert.Empty(inventory.ClientItems);
        Assert.Null(inventory.Owner);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectorsUseItemIdsAndItemUidsNotDictionaryKeysAndIgnoreBoundOrStack(bool meteor)
    {
        uint id = meteor ? 1088001u : 720027u;
        var inventory = new Inventory(null);
        var zero = Physical(0, id, 0, 0);
        var one = Physical(55, id, ushort.MaxValue, 1);
        var two = Physical(uint.MaxValue, meteor ? 1088002u : id, 1, byte.MaxValue);
        var wrong = Physical(77, meteor ? 720027u : 1088001u, 8, 1);
        inventory.ClientItems[100] = zero;
        inventory.ClientItems[200] = one;
        inventory.ClientItems[300] = two;
        inventory.ClientItems[400] = wrong;

        var selected = Select(inventory, meteor);

        Assert.Equal(3, selected.Count);
        Assert.Same(zero, selected[0]);
        Assert.Same(one, selected[55]);
        Assert.Same(two, selected[uint.MaxValue]);
        Assert.False(selected.ContainsKey(100));
        Assert.DoesNotContain(wrong, selected.Values);
        Assert.Equal((ushort)0, zero.StackSize);
        Assert.Equal(ushort.MaxValue, one.StackSize);
        Assert.Equal(byte.MaxValue, two.Bound);
        Assert.Equal(4, inventory.ClientItems.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectorsDeduplicateCurrentUidNotPhysicalObjectAndKeepFirstNativeEnumeratedValue(bool meteor)
    {
        uint id = meteor ? 1088001u : 720027u;
        var inventory = new Inventory(null);
        var shared = Physical(7, id);
        var sameUidOtherObject = Physical(7, meteor ? 1088002u : id, 9, 1);
        var other = Physical(8, id);
        inventory.ClientItems[100] = shared;
        inventory.ClientItems[200] = shared;
        inventory.ClientItems[300] = sameUidOtherObject;
        inventory.ClientItems[400] = other;
        // Source does not sort; the first current Values entry wins for a duplicate UID.
        Item expected = inventory.ClientItems.Values.First(value => value.Uid == 7);

        var selected = Select(inventory, meteor);

        Assert.Equal(2, selected.Count);
        Assert.Same(expected, selected[7]);
        Assert.Same(other, selected[8]);
        Assert.Equal(4, inventory.ClientItems.Count);
        Assert.Same(shared, inventory.ClientItems[100]);
        Assert.Same(shared, inventory.ClientItems[200]);
        Assert.Same(sameUidOtherObject, inventory.ClientItems[300]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReturnedSelectionIsIndependentMapOfSameLiveItemsAndLaterQueriesReadCurrentUids(bool meteor)
    {
        uint id = meteor ? 1088001u : 720027u;
        var inventory = new Inventory(null);
        var item = Physical(7, id);
        inventory.ClientItems[100] = item;
        var oldSelection = Select(inventory, meteor);
        item.Uid = 8;
        item.StackSize = 99;
        var newSelection = Select(inventory, meteor);
        Assert.Same(item, oldSelection[7]);
        Assert.Equal(8u, oldSelection[7].Uid);
        Assert.Equal((ushort)99, oldSelection[7].StackSize);
        Assert.Same(item, newSelection[8]);
        Assert.False(newSelection.ContainsKey(7));
        oldSelection.Clear();
        Assert.Same(item, inventory.ClientItems[100]);
        Assert.Same(item, newSelection[8]);
        inventory.ClientItems = new ConcurrentDictionary<uint, Item>();
        Assert.Empty(Select(inventory, meteor));
        Assert.Same(item, newSelection[8]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrivateSelectorsPreserveNullDictionaryFaultWithoutManufacturingReplacement(bool meteor)
    {
        var inventory = new Inventory(null) { ClientItems = null! };
        var fault = Assert.Throws<TargetInvocationException>(() => Select(inventory, meteor));
        Assert.IsType<NullReferenceException>(fault.InnerException);
        Assert.Null(inventory.ClientItems);
        Assert.Null(inventory.Owner);
    }

    private static Dictionary<uint, Item> Select(Inventory inventory, bool meteor) =>
        (Dictionary<uint, Item>)typeof(Inventory).GetMethod(meteor ? "GetMyMeteors" : "GetMyMetscrolls",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(inventory, null)!;

    private static Item Physical(uint uid, uint id, ushort stack = 1, byte bound = 0) =>
        new() { Uid = uid, ITEM_ID = id, StackSize = stack, Bound = bound };
}
