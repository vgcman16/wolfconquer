using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeStatusPrerequisiteTests
{
    [Fact]
    public void GuildWarWholeConstructorCreatesIndependentNativeCollectionsWithoutStartingEvent()
    {
        var a = new LegacyNativeGuildWar(); var b = new LegacyNativeGuildWar();
        Assert.Equal(LegacyNativeProcesType.Dead, a.Proces); Assert.Null(a.Map); Assert.False(a.S_30);
        Assert.Equal(default, a.Stamp); Assert.Equal(default, a.StampUpdate); Assert.Equal(0, a.TimeLeft.Value);
        Assert.Equal(1, a.Winner.LeaderReward); Assert.Equal(7, a.Winner.DeputiLeaderReward); Assert.Null(a.Winner.Name);
        Assert.Empty(a.NPC_INFO); Assert.Empty(a.SCORE_LIST); Assert.Empty(a.GUILD_RANK); Assert.Empty(a.GUILD_RANK_TOP4);
        Assert.Empty(a.USER_RANK); Assert.Empty(a.GuildConductors); Assert.Empty(a.RewardLeader); Assert.Empty(a.RewardDeputiLeader);
        Assert.NotSame(a.Winner, b.Winner); Assert.NotSame(a.NPC_INFO, b.NPC_INFO); Assert.NotSame(a.StatueCoords, b.StatueCoords);
        Assert.Equal(new ushort[] { 140, 134, 144, 124, 130, 138, 153, 124, 161, 124, 130, 147, 130, 155 }, a.StatueCoords.SelectMany(p => p));
        Assert.Null(new LegacyNativeGuildWar.GuildRank().Name); Assert.Null(new LegacyNativeGuildWar.GuildRankTop4().Name);
        Assert.Null(new LegacyNativeGuildWar.UserRank().Name); Assert.Null(new LegacyNativeGuildWar.NpcInfo().Npc);
    }

    [Fact]
    public void WholeConductorLoadUsesRealNpcCreateAndCommaCodecRetainsUShortDestinationMap()
    {
        var c = new LegacyNativeGuildWar.GuildConductor();
        Assert.Null(c.Npc); Assert.Throws<NullReferenceException>(() => c.ToString());
        c.Load("17,18,19,1002,1200,2,33,44,65535,", LegacyNativeNpcID.TeleGuild1);
        Assert.Equal(17u, c.Npc!.UID); Assert.Equal((ushort)18, c.Npc.X); Assert.Equal((ushort)19, c.Npc.Y);
        Assert.Equal(1002u, c.Npc.Map); Assert.Equal((ushort)1200, c.Npc.Mesh); Assert.Equal((LegacyNativeNpcType)2, c.Npc.NpcType);
        Assert.Equal((ushort)26, c.Npc.Leng); Assert.Equal((ushort)2210, c.Npc.PacketID); Assert.True(c.Npc.Alive);
        Assert.Equal(LegacyNativeMapObjectType.Npc, c.Npc.ObjType); Assert.False(c.Npc.AllowDynamic);
        c.GetCoords(out var x, out var y, out var map); Assert.Equal((ushort)33, x); Assert.Equal((ushort)44, y); Assert.Equal(65535u, map);
        Assert.Equal("17,18,19,1002,1200,2,33,44,65535,", c.ToString());
        c.Load("17,18,19,1002,1200,2,33,44,65536,", LegacyNativeNpcID.TeleGuild1);
        Assert.Equal(0u, c.ToMap); c.GetCoords(out x, out y, out map);
        Assert.Equal((ushort)300, x); Assert.Equal((ushort)278, y); Assert.Equal(1002u, map);
    }

    [Fact]
    public void EmptyConductorLoadReplacesNpcButPreservesPriorDestinationAndNativeUidAuthority()
    {
        var c = new LegacyNativeGuildWar.GuildConductor { ToMap = 70000, ToX = 11, ToY = 12 };
        c.Load("", LegacyNativeNpcID.TeleGuild2); var first = c.Npc;
        Assert.Equal((uint)LegacyNativeNpcID.TeleGuild2, first!.UID); Assert.Equal(70000u, c.ToMap);
        c.Load("", LegacyNativeNpcID.TeleGuild3); Assert.NotSame(first, c.Npc);
        Assert.Equal((uint)LegacyNativeNpcID.TeleGuild3, c.Npc!.UID);
        c.GetCoords(out var x, out var y, out var map); Assert.Equal((ushort)11, x); Assert.Equal((ushort)12, y); Assert.Equal(70000u, map);
    }

    [Theory]
    [InlineData(0u, 11, 12)] [InlineData(1002u, 0, 12)] [InlineData(1002u, 11, 0)]
    public void AnyMissingConductorCoordinateUsesTheWholeNativeFallback(uint map, ushort x, ushort y)
    {
        var c = new LegacyNativeGuildWar.GuildConductor { ToMap = map, ToX = x, ToY = y };
        c.GetCoords(out var resultX, out var resultY, out var resultMap);
        Assert.Equal((ushort)300, resultX); Assert.Equal((ushort)278, resultY); Assert.Equal(1002u, resultMap);
        ushort refX = 11, refY = 12; uint refMap = 1002;
        Assert.False(LegacyNativeGuildWar.GuildConductor.ChangeNpcLocation(null!, ref refX, ref refY, ref refMap));
        Assert.Equal((ushort)11, refX); Assert.Equal((ushort)12, refY); Assert.Equal(1002u, refMap);
    }

    [Fact]
    public void ConductorNullLineStillCreatesNpcAndReadsNativeDefaultsRatherThanEmptyShortcut()
    {
        var c = new LegacyNativeGuildWar.GuildConductor { ToMap = 1002, ToX = 1, ToY = 2 };
        c.Load(null!, LegacyNativeNpcID.TeleGuild1);
        Assert.NotNull(c.Npc); Assert.Equal(0u, c.Npc.UID); Assert.Equal(0u, c.ToMap); Assert.Equal((ushort)0, c.ToX); Assert.Equal((ushort)0, c.ToY);
    }
}
