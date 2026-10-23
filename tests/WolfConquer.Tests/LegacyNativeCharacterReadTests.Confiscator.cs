using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCharacterReadTests
{
    [Theory]
    [InlineData(0, 50)] [InlineData(1, 51)] [InlineData(2, 52)] [InlineData(3, 55)]
    [InlineData(4, 65)] [InlineData(5, 80)] [InlineData(6, 140)] [InlineData(7, 320)]
    [InlineData(8, 650)] [InlineData(9, 2000050)] [InlineData(255, 2000050)]
    public void FullCostKeepsNativePlusTableAndDefaultPenalty(int plus, int expected)
    {
        var item = new LegacyNativeMsgDetainedItem { ItemID = 410009, Plus = (byte)plus };
        Assert.Equal(expected, LegacyNativeConfiscatorRuntime.CalculateCpsCost(item));
    }

    [Theory]
    [InlineData(0u, 0, 0)] [InlineData(1u, 30, 30)] [InlineData(2u, 90, 90)]
    [InlineData(3u, 200, 180)] [InlineData(4u, 400, 300)] [InlineData(5u, 600, 450)]
    [InlineData(6u, 1000000, 1000000)] [InlineData(7u, 1000000, 1000000)] [InlineData(8u, 0, 0)]
    public void FullCostKeepsDistinctRefineryAndPurificationTables(uint level, int refinery, int purification)
    {
        var item = new LegacyNativeMsgDetainedItem { ItemID = 410008 };
        item.Refinary.EffectLevel = level; Assert.Equal(10 + refinery, LegacyNativeConfiscatorRuntime.CalculateCpsCost(item));
        item.Refinary.EffectLevel = 0; item.Purification.PurificationLevel = level;
        Assert.Equal(10 + purification, LegacyNativeConfiscatorRuntime.CalculateCpsCost(item));
    }

    [Theory]
    [InlineData(410009u, 11, 0, 60)] [InlineData(410009u, 0, 11, 50)]
    [InlineData(410009u, 11, 11, 1000050)] [InlineData(130009u, 11, 0, 1000050)]
    [InlineData(130009u, 0, 11, 50)] [InlineData(130009u, 11, 11, 1000050)]
    public void SocketCostKeepsWeaponFirstSocketDistinctionAndSecondOnlyQuirk(uint id, int first, int second, int expected)
    {
        var item = new LegacyNativeMsgDetainedItem { ItemID = id, SocketOne = (LegacyNativeGameItemFlags.Gem)first, SocketTwo = (LegacyNativeGameItemFlags.Gem)second };
        Assert.Equal(expected, LegacyNativeConfiscatorRuntime.CalculateCpsCost(item));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task CompleteConfiscationPublishesActualContainersThenOptionalFloorFrameAndRealPool(bool valid) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map(valid); var gainer = scope.Other(33);
        var item = scope.Item(); scope.Client.Confiscator!.AddItem(scope.Client, gainer.Client, item, scope.Packet);
        var redeem = Assert.Single(scope.Client.Confiscator.RedeemContainer).Value;
        var claim = Assert.Single(gainer.Client.Confiscator!.ClaimContainer).Value;
        Assert.Equal(2u, redeem.UID); Assert.Equal(17u, redeem.OwnerUID); Assert.Equal(33u, redeem.GainerUID);
        Assert.Equal("Owner", redeem.OwnerName); Assert.Equal("User33", redeem.GainerName);
        Assert.Equal(LegacyNativeMsgDetainedItem.ContainerType.ClaimPage, redeem.Action); Assert.Equal(redeem.UID, claim.UID);
        Assert.Equal(50, redeem.ConquerPointsCost); Assert.Equal((ushort)2262, Opcode(scope.Queue.Peek()));
        Assert.Equal((uint)LegacyNativeMsgDetainedItem.ContainerType.DetainPage, U32(scope.Queue.Peek(), 20));
        Assert.Equal((uint)LegacyNativeMsgDetainedItem.ContainerType.ClaimPage, U32(Assert.Single(gainer.Queue), 20));
        Assert.Equal(valid ? 2 : 1, scope.Queue.Count);
        if (valid) { Assert.Equal((ushort)2164, Opcode(scope.Queue.Last())); Assert.True(map.IsFlagPresent(4, 4, LegacyNativeMapFlagType.Item)); }
        Assert.Same(scope.Client.Confiscator, LegacyNativePoolConfiscator.QueueContainer.PollContainers[17]);
        Assert.Same(gainer.Client.Confiscator, LegacyNativePoolConfiscator.QueueContainer.PollContainers[33]);
    });

    [Fact]
    public Task MissingMapFaultOccursAfterBothMessagesAndContainersButBeforePoolQueueing() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var gainer = scope.Other(33); var item = scope.Item();
        Assert.Throws<NullReferenceException>(() => scope.Client.Confiscator!.AddItem(scope.Client, gainer.Client, item, scope.Packet));
        Assert.Single(scope.Client.Confiscator!.RedeemContainer); Assert.Single(gainer.Client.Confiscator!.ClaimContainer);
        Assert.Single(scope.Queue); Assert.Single(gainer.Queue); Assert.Empty(LegacyNativePoolConfiscator.QueueContainer.PollContainers);
        Assert.Equal(2u, LegacyNativeConfiscatorRuntime.CounterUID.Count);
    });

    [Fact]
    public void GenuineContainerBirthQueriesAndDateHelpersKeepSourceFaults()
    {
        var owner = new LegacyNativeConfiscatorRuntime(); Assert.True(owner.IsEmptyReadem()); Assert.True(owner.IsEmptyClaim());
        owner.RedeemContainer[1] = default; Assert.False(owner.IsEmptyReadem()); Assert.True(owner.IsEmptyClaim());
        Assert.Equal(20261023, LegacyNativeConfiscatorRuntime.CreateTimer(2026, 10, 23));
        Assert.Equal(new DateTime(2026, 10, 23), LegacyNativeConfiscatorRuntime.GetTimer(20261023));
        Assert.Equal(20261399, LegacyNativeConfiscatorRuntime.CreateTimer(2026, 13, 99));
        Assert.Throws<ArgumentOutOfRangeException>(() => LegacyNativeConfiscatorRuntime.GetTimer(20260230));
    }
}
