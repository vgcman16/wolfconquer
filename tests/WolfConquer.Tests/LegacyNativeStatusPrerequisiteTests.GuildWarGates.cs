using WolfConquer.Core.World;
using N = WolfConquer.Core.World.LegacyNativeGuildWar.NPCID;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeStatusPrerequisiteTests
{
    [Theory]
    [InlineData(0u, false, 1491u, 1501u, 1511u)] [InlineData(9u, false, 1491u, 1501u, 1511u)]
    [InlineData(10u, false, 1551u, 1561u, 1571u)] [InlineData(14u, false, 1551u, 1561u, 1571u)]
    [InlineData(15u, false, 1611u, 1621u, 1631u)] [InlineData(19u, false, 1611u, 1621u, 1631u)]
    [InlineData(20u, false, 1671u, 1681u, 1691u)] [InlineData(uint.MaxValue, false, 1671u, 1681u, 1691u)]
    [InlineData(0u, true, 1467u, 1477u, 1487u)] [InlineData(9u, true, 1467u, 1477u, 1487u)]
    [InlineData(10u, true, 1647u, 1537u, 1547u)] [InlineData(14u, true, 1647u, 1537u, 1547u)]
    [InlineData(15u, true, 1587u, 1597u, 1607u)] [InlineData(19u, true, 1587u, 1597u, 1607u)]
    [InlineData(20u, true, 1647u, 1657u, 1667u)] [InlineData(uint.MaxValue, true, 1647u, 1657u, 1667u)]
    public void EveryLevelBandRetainsNativeMeshMappingsIncludingRightCloseTenAnomaly(uint level, bool right, uint close, uint open, uint demolition)
    {
        var war = new LegacyNativeGuildWar(); Assert.Equal(close, (uint)war.GateClose(level, right));
        Assert.Equal(open, (uint)war.GateOpen(level, right)); Assert.Equal(demolition, (uint)war.GateDemolition(level, right));
    }

    [Theory]
    [InlineData(false, 1501, true, false)] [InlineData(false, 1561, true, false)]
    [InlineData(false, 1621, true, false)] [InlineData(false, 1681, true, false)]
    [InlineData(false, 1511, false, true)] [InlineData(false, 1571, false, true)]
    [InlineData(false, 1631, false, true)] [InlineData(false, 1691, false, true)]
    [InlineData(true, 1477, true, false)] [InlineData(true, 1537, true, false)]
    [InlineData(true, 1597, true, false)] [InlineData(true, 1657, true, false)]
    [InlineData(true, 1487, false, true)] [InlineData(true, 1547, false, true)]
    [InlineData(true, 1607, false, true)] [InlineData(true, 1667, false, true)]
    [InlineData(false, 1491, false, false)] [InlineData(true, 1467, false, false)]
    public void GateQueriesReadGenuineFurnitureMeshNotLevelOrEventProcess(bool right, ushort mesh, bool open, bool demolished)
    {
        var war = new LegacyNativeGuildWar(); war.NPC_INFO[right ? N.RightGate : N.LeftGate] = new() { Npc = new() { Mesh = (LegacyNativeSobNpcRuntime.StaticMesh)mesh } };
        Assert.Equal(open, right ? war.RightGateOpen : war.LeftGateOpen);
        Assert.Equal(demolished, right ? war.RightGateDemolition : war.LeftGateDemolition);
    }

    [Fact]
    public void MissingFurnitureKeepsKeyThenNullFaults()
    {
        var war = new LegacyNativeGuildWar(); Assert.Throws<KeyNotFoundException>(() => war.LeftGateOpen);
        war.NPC_INFO[N.LeftGate] = new(); Assert.Throws<NullReferenceException>(() => war.LeftGateOpen);
    }

    [Fact]
    public Task CompleteWalkAndJumpQueriesRespectGateDistancesAndSpecialBypass() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var war = new LegacyNativeGuildWar { Map = s.Map() };
        war.NPC_INFO[N.LeftGate] = new() { Npc = new() { Mesh = (LegacyNativeSobNpcRuntime.StaticMesh)1501 } };
        war.NPC_INFO[N.RightGate] = new() { Npc = new() { Mesh = (LegacyNativeSobNpcRuntime.StaticMesh)1477 } };
        Assert.True(war.ValidWalk(3, out var next, 217, 177)); Assert.Equal(0, next);
        Assert.True(war.ValidJump(3, out next, 164, 209)); Assert.Equal(0, next);
        Assert.True(war.ValidWalk(3, out next, 164, 209)); Assert.Equal(0, next);
        Assert.True(war.ValidWalk(3, out next, 216, 181)); Assert.Equal(0, next);
        Assert.False(war.ValidWalk(3, out next, 166, 210)); Assert.Equal(3, next);
        war.NPC_INFO[N.LeftGate].Npc!.Mesh = (LegacyNativeSobNpcRuntime.StaticMesh)1491;
        Assert.False(war.ValidJump(3, out next, 164, 209)); Assert.Equal(3, next);
        war.Map!.FloorType[20, 20] = 7; Assert.True(war.ValidJump(3, out next, 20, 20)); Assert.Equal(7, next);
        Assert.True(war.ValidWalk(1, out next, 20, 20)); Assert.Equal(7, next);
        // Source signed-short distance wraps at65535 and takes the bypass before indexing.
        Assert.True(war.ValidJump(3, out next, 65535, 65535)); Assert.Equal(0, next);
        Assert.True(war.ValidWalk(3, out next, 65535, 65535)); Assert.Equal(0, next);
        Assert.Throws<IndexOutOfRangeException>(() => war.ValidJump(3, out _, 320, 320));
        Assert.Throws<IndexOutOfRangeException>(() => war.ValidWalk(3, out _, 320, 320));
    });

    [Fact]
    public Task FullFurnitureLookupUsesRealMapViewAndPreservesExistingDictionaryEntry() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var war = new LegacyNativeGuildWar { Map = s.Map() };
        var npc = new LegacyNativeSobNpcRuntime { UID = 55, X = 10, Y = 10, ObjType = LegacyNativeMapObjectType.SobNpc };
        Assert.True(war.Map!.View!.EnterMap(npc)); war.AddNpc(N.GuildPillar, 55);
        Assert.Same(npc, war.NPC_INFO[N.GuildPillar].Npc); war.AddNpc(N.GuildPillar, 999);
        Assert.Same(npc, war.NPC_INFO[N.GuildPillar].Npc); war.AddNpc(N.ExorcimPillar, 999);
        Assert.Null(war.NPC_INFO[N.ExorcimPillar].Npc); Assert.Equal(0u, war.AvailablePoints);
    });
}
