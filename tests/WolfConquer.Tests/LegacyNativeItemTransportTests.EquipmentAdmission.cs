using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeItemTransportTests
{
    [Fact]
    public Task WholeEquipmentUidAdmissionRetainsASuppliedFreeUidWithoutConsumingTheGlobalCounter() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = new LegacyNativeGameItemRuntime { Uid = 777, ITEM_ID = 410003, Bless = 7 };
        Admit(scope.Equipment, item);
        Assert.Same(item, scope.Equipment.ClientItems[777]); Assert.Equal(777u, item.Uid);
        Assert.Equal(40u, LegacyNativePoolItems.ItemCounter.Count); Assert.Equal((byte)7, item.Bless);
        Assert.Empty(scope.Queue); Assert.Equal(333, scope.Packet.Size);
    });

    [Fact]
    public Task WholeEquipmentUidAdmissionConsumesEveryRealCollisionBeforePublishingTheSameItem() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var first = new LegacyNativeGameItemRuntime { Uid = 999 };
        var second = new LegacyNativeGameItemRuntime { Uid = 1000 };
        scope.Equipment.ClientItems.TryAdd(41, first); scope.Equipment.ClientItems.TryAdd(42, second);
        var item = new LegacyNativeGameItemRuntime { Uid = 42, ITEM_ID = 410003 };
        Admit(scope.Equipment, item);
        Assert.Equal(43u, item.Uid); Assert.Equal(43u, LegacyNativePoolItems.ItemCounter.Count);
        Assert.Same(item, scope.Equipment.ClientItems[43]); Assert.Same(first, scope.Equipment.ClientItems[41]);
        Assert.Same(second, scope.Equipment.ClientItems[42]); Assert.Equal(999u, first.Uid);
    });

    [Fact]
    public Task WholeEquipmentUidAdmissionRetainsAliasesAndDoesNotNormalizeThePriorDictionaryKey() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = new LegacyNativeGameItemRuntime { Uid = 11, ITEM_ID = 410003 };
        scope.Equipment.ClientItems.TryAdd(11, item); Admit(scope.Equipment, item);
        Assert.Equal(41u, item.Uid); Assert.Same(item, scope.Equipment.ClientItems[11]);
        Assert.Same(item, scope.Equipment.ClientItems[41]); Assert.Equal(2, scope.Equipment.ClientItems.Count);
    });

    [Theory]
    [InlineData(203003u, (byte)7, (byte)0)]
    [InlineData(203999u, (byte)2, (byte)0)]
    [InlineData(203003u, (byte)1, (byte)1)]
    [InlineData(203003u, (byte)0, (byte)0)]
    [InlineData(204003u, (byte)7, (byte)7)]
    public Task EquipmentBlessRepairRunsOnlyForTheNativeFullRidingCropQuotientAfterAdmission(uint id, byte bless, byte expected) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = new LegacyNativeGameItemRuntime { ITEM_ID = id, Bless = bless };
        Admit(scope.Equipment, item);
        Assert.Equal(41u, item.Uid); Assert.Same(item, scope.Equipment.ClientItems[41]);
        Assert.Equal(expected, item.Bless); Assert.Equal(41u, LegacyNativePoolItems.ItemCounter.Count);
    });

    [Fact]
    public Task EquipmentUidCounterWrapToZeroIsNotRepairedWhenTheNativeZeroKeyIsFree() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); LegacyNativePoolItems.ItemCounter.Set(uint.MaxValue);
        var item = new LegacyNativeGameItemRuntime(); Admit(scope.Equipment, item);
        Assert.Equal(0u, item.Uid); Assert.Equal(0u, LegacyNativePoolItems.ItemCounter.Count);
        Assert.Same(item, scope.Equipment.ClientItems[0]);
    });

    [Fact]
    public Task NullEquipmentDictionaryFaultRetainsEarlierUidMutationButPrecedesBlessRepair() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Equipment.ClientItems = null!;
        var item = new LegacyNativeGameItemRuntime { ITEM_ID = 203003, Bless = 7 };
        Assert.Throws<NullReferenceException>(() => Admit(scope.Equipment, item));
        Assert.Equal(41u, item.Uid); Assert.Equal(41u, LegacyNativePoolItems.ItemCounter.Count);
        Assert.Equal((byte)7, item.Bless); Assert.Null(scope.Equipment.ClientItems);
    });
}
