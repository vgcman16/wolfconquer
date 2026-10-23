using WolfConquer.Core.World;
using Cell = WolfConquer.Core.World.LegacyNativeMapFlagType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapPopulationTests
{
    [Fact]
    public Task BothNativeNpcRemovalOverloadsRemoveTheirActualObjectFamilyAndPublishAction() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map();
        var npc = LegacyNativeNpcRuntime.Create(); npc.UID = 51; npc.X = 12; npc.Y = 13; npc.Map = 700;
        map.AddNpc(npc); scope.Queue.Clear();
        map.RemoveNpc(npc, scope.Packet);
        Assert.Empty(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Npc));
        Assert.Equal(Cell.Valid, map.cells[12, 13]); Assert.Equal((ushort)2046, Opcode(Assert.Single(scope.Queue)));
        scope.Queue.Clear();
        var sob = new LegacyNativeSobNpcRuntime { UID = 52, X = 14, Y = 15, Map = 700, ObjType = LegacyNativeMapObjectType.SobNpc };
        Assert.True(map.View.EnterMap(sob)); map.SetFlagNpc(sob.X, sob.Y);
        map.RemoveNpc(sob, scope.Packet);
        Assert.Empty(map.View.GetAllMapRoles(LegacyNativeMapObjectType.SobNpc));
        Assert.Equal(Cell.Valid, map.cells[14, 15]); Assert.Equal((ushort)2046, Opcode(Assert.Single(scope.Queue)));
        scope.Queue.Clear(); map.RemoveNpc(npc, scope.Packet); map.RemoveNpc(sob, scope.Packet);
        Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(1u, 4u, 2u, 3u, 1u)]
    [InlineData(8u, 10u, 9u, 8u, 9u)]
    [InlineData(uint.MaxValue, 10u, 0u, 1u, 2u)]
    public void RealMrRayzoCounterRetainsExactFinishEqualityResetAndUIntWrap(uint start, uint finish,
        uint first, uint second, uint third)
    {
        var counter = new LegacyNativeWrappingCounter(start) { Finish = finish };
        Assert.Equal(start, counter.Now); Assert.Equal(first, counter.Next);
        Assert.Equal(second, counter.Next); Assert.Equal(third, counter.Next);
    }

    [Fact]
    public void GenuineBoothDefaultBirthHasRealSafeDictionaryAndNullOwnerFaultWithoutVendingFacade()
    {
        var booth = new LegacyNativeBooth();
        Assert.Empty(booth.ItemList); Assert.NotNull(booth.ItemList.SyncRoot);
        Assert.Null(booth.Base); Assert.Null((LegacyNativeSobNpcRuntime)booth);
        Assert.Throws<NullReferenceException>(() => booth.Name);
        Assert.Equal((ushort)1, (ushort)LegacyNativeMsgItemView.ActionMode.Gold);
        Assert.Equal((ushort)3, (ushort)LegacyNativeMsgItemView.ActionMode.CPs);
        Assert.Equal((ushort)4, (ushort)LegacyNativeMsgItemView.ActionMode.ViewEquip);
        Assert.Equal((ushort)7, (ushort)LegacyNativeMsgItemView.ActionMode.Rune);
    }
}
