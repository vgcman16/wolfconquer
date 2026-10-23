using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;
using Cell = WolfConquer.Core.World.LegacyNativeMapFlagType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterAttackTests
{
    [Theory]
    [InlineData(18,18,true)] [InlineData(19,0,false)] [InlineData(0,19,false)] [InlineData(0,0,true)]
    public Task ViewUsesInclusiveChebyshevThresholdButNotAllowDynamic(int dx,int dy,bool visible) => NativePacketTestThread.Run(() =>
    {
        using var s=new Scope();var role=s.Monster();s.Player.X=(ushort)(26+dx);s.Player.Y=(ushort)(25+dy);
        Assert.Equal(visible,role.View.CanSee((ILegacyNativeMapObj)s.Player));s.Player.AllowDynamic=true;s.Player.DynamicID=9;
        Assert.False(role.View.CanSee((ILegacyNativeMapObj)s.Player));s.Player.DynamicID=0;s.Player.Map=701;Assert.False(role.View.CanSee((ILegacyNativeMapObj)s.Player));
        s.Player.Map=700;s.Player.UID=role.UID;Assert.False(role.View.CanSee((ILegacyNativeMapObj)s.Player));
    });

    [Theory]
    [InlineData(0,0,65535,65535,-1)] [InlineData(0,0,32768,32768,-32768)] [InlineData(10,20,12,30,10)] [InlineData(65535,0,0,0,0)]
    public Task MonsterViewKeepsOriginalSignedShortOverflow(ushort x,ushort y,ushort xx,ushort yy,short expected) => NativePacketTestThread.Run(() =>
    {
        Assert.Equal(expected,LegacyNativeMonsterView.GetDistance(x,y,xx,yy));
    });

    [Fact]
    public Task GetTargetUsesActualMapMembershipAndMostRecentlyIndexedPlayer() => NativePacketTestThread.Run(() =>
    {
        using var s=new Scope();var role=s.Monster();var queue=new Queue<byte[]>();var peer=s.Peer(99,27,25,queue);
        // No Alive check in original MonsterView; a dead player still participates.
        SetPrivate(peer.Player,"_minhitpoints",0);Assert.Same(peer.Player,role.View.GetTarget(s.Map,LegacyNativeMapObjectType.Player));
        peer.Player.DynamicID=9;Assert.Same(s.Player,role.View.GetTarget(s.Map,LegacyNativeMapObjectType.Player));
        s.Player.DynamicID=8;Assert.Null(role.View.GetTarget(s.Map,LegacyNativeMapObjectType.Player));
        role.View.CheckScren();Assert.Empty(queue);Assert.Empty(s.Queue);
    });

    [Fact]
    public Task MonsterFlagMethodsUseRealRowsAndBroadcastToActualVisibleClients() => NativePacketTestThread.Run(() =>
    {
        using var s=new Scope();var role=s.Monster();Assert.True(role.AddFlag(Flag.Poisoned,30,true,7));
        var old=role.BitVector.ArrayFlags[(int)Flag.Poisoned];Assert.Equal(7,old.InvokerSecouds);Assert.Equal((ushort)2176,Opcode(Assert.Single(s.Queue)));
        s.Queue.Clear();Assert.False(role.AddFlag(Flag.Poisoned,99,false));Assert.Same(old,role.BitVector.ArrayFlags[(int)Flag.Poisoned]);Assert.Empty(s.Queue);
        role.AddSpellFlag(Flag.Poisoned,55,false,3);var replacement=role.BitVector.ArrayFlags[(int)Flag.Poisoned];Assert.NotSame(old,replacement);Assert.Equal(55,replacement.Secounds);Assert.False(replacement.RemoveOnDead);
        Assert.True(role.UpdateFlag(Flag.Poisoned,7,true,90));Assert.Equal(7,replacement.Secounds);
        Assert.True(role.RemoveFlag(Flag.Poisoned,null!));Assert.False(role.RemoveFlag(Flag.Poisoned,null!));Assert.False(role.ContainFlag(Flag.Poisoned));
        role.AddFlag(Flag.Poisoned,15,true);role.AddFlag(Flag.Cyclone,20,false);s.Queue.Clear();role.ClearFlags();
        Assert.False(role.ContainFlag(Flag.Poisoned));Assert.True(role.ContainFlag(Flag.Cyclone));Assert.Empty(s.Queue);
        role.ClearFlags(true);Assert.Equal((ushort)2176,Opcode(Assert.Single(s.Queue)));
    });

    [Fact]
    public Task MapCoordinateUpdatePreservesCollisionAndBlockMembershipOrder() => NativePacketTestThread.Run(() =>
    {
        using var s=new Scope();var role=s.Monster();Assert.True(s.Map.View!.EnterMap(role));s.Map.SetMonsterOnTile(role.X,role.Y,true);
        Assert.True(role.UpdateMapCoords(55,56,s.Map));Assert.False(s.Map.MonsterOnTile(26,25));Assert.True(s.Map.MonsterOnTile(55,56));
        Assert.Contains(role,s.Map.View.Roles(LegacyNativeMapObjectType.Monster,55,56));Assert.Equal((ushort)26,role.PX);Assert.Equal((ushort)25,role.PY);
        s.Map.SetMonsterOnTile(56,56,true);Assert.False(role.UpdateMapCoords(56,56,s.Map));Assert.Equal((ushort)55,role.X);
        // Source doesn't gate ValidLocation or require MoveTo success before changing coordinates.
        s.Map.cells[79,79]=Cell.None;Assert.True(role.UpdateMapCoords(79,79,s.Map));Assert.Equal((ushort)79,role.X);
    });

    [Fact]
    public Task UpdateMonsterViewPublishesWholeSpawnOnlyOnSourceOutsideToInsideTransition() => NativePacketTestThread.Run(() =>
    {
        using var s=new Scope();var role=s.Monster();role.UpdateMonsterView(null,s.Packet);
        Assert.Equal(Spawn(role.UID,100,15,26,25,"Pheasant",300,500,500),Assert.Single(s.Queue));
        s.Queue.Clear();role.X=27;role.Y=25;role.UpdateMonsterView(null,s.Packet);Assert.Empty(s.Queue);
    });

    [Theory]
    [InlineData(18,17,true)] [InlineData(17,17,false)] [InlineData(18,18,false)] [InlineData(19,0,true)]
    public Task InViewIsAnOutsideToStrictInsideTransition(ushort oldDistance,ushort distance,bool expected) => NativePacketTestThread.Run(() =>
    {
        using var s=new Scope();var role=s.Monster();role.PX=oldDistance;role.PY=0;role._xx=distance;role._yy=0;
        Assert.Equal(expected,role.InView(0,0,18));
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task WholeRespawnPublishesNativeSequenceAndRetainsNonDeathFlags(bool effect) => NativePacketTestThread.Run(() =>
    {
        using var s=new Scope();var f=Family();f.MaxHealth=70000;var role=s.Monster(f);role.HitPoints=0;role.State=LegacyNativeMobStatus.Respawning;
        role.AddFlag(Flag.Poisoned,10,true);role.AddFlag(Flag.Cyclone,11,false);s.Queue.Clear();role.Respawn(effect);
        Assert.Equal(70000u,role.HitPoints);Assert.Equal(LegacyNativeMobStatus.Idle,role.State);Assert.False(role.ContainFlag(Flag.Poisoned));Assert.True(role.ContainFlag(Flag.Cyclone));
        var frames=s.Queue.ToArray();Assert.Equal(effect?5:4,frames.Length);Assert.Equal((ushort)2046,Opcode(frames[0]));Assert.Equal((ushort)2227,Opcode(frames[1]));
        if(effect)Assert.Equal((ushort)2046,Opcode(frames[2]));Assert.Equal((ushort)2176,Opcode(frames[^2]));Assert.Equal((ushort)2176,Opcode(frames[^1]));
        // MsgUpdate accumulates BOTH rows in the second publication, rather than replacing the first.
        Assert.True(frames[^1].Length>frames[^2].Length);
    });

    [Fact]
    public Task FadeAndRemoveViewUseRealTimeStampsAndDoNotClearOccupancyOrHeal() => NativePacketTestThread.Run(() =>
    {
        using var s=new Scope();var role=s.Monster();Assert.True(s.Map.View!.EnterMap(role));s.Map.SetMonsterOnTile(role.X,role.Y,true);
        role.RespawnX=55;role.RespawnY=56;role.HitPoints=0;SetPrivate(role,"DeadStamp",DateTime.Now.AddSeconds(-10));role.AddFadeAway(999,null);
        Assert.True(role.ContainFlag(Flag.FadeAway));Assert.False(role.RemoveView(999,s.Map));
        SetPrivate(role,"FadeAway",DateTime.Now.AddSeconds(-10));role.Target=s.Player;Assert.True(role.RemoveView(999,s.Map));
        Assert.Equal(LegacyNativeMobStatus.Respawning,role.State);Assert.Null(role.Target);Assert.Equal(0u,role.HitPoints);Assert.Equal((ushort)55,role.X);Assert.Equal((ushort)56,role.Y);
        Assert.True(s.Map.MonsterOnTile(26,25));Assert.False(s.Map.MonsterOnTile(55,56));Assert.False(role.RemoveView(999,s.Map));
        role.RespawnStamp=DateTime.Now.AddMinutes(-1);Assert.True(role.CanRespawn(s.Map));s.Map.SetMonsterOnTile(55,56,true);Assert.False(role.CanRespawn(s.Map));
    });
}
