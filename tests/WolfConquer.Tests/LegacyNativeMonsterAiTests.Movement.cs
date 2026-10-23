using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;
using Cell=WolfConquer.Core.World.LegacyNativeMapFlagType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterAiTests
{
    [Fact]
    public Task WholeSearchTargetMovesThroughActualCellsAndPublishesNativeWalk()=>NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();var role=s.Monster();role.Target=s.Player;DateTime before=DateTime.Now;
        s.Ai.SearchTarget(null,role);Assert.Equal((ushort)41,role.X);Assert.Equal((ushort)40,role.Y);Assert.Equal((ushort)40,role.PX);
        Assert.False(s.Map.MonsterOnTile(40,40));Assert.True(s.Map.MonsterOnTile(41,40));Assert.InRange(role.MoveStamp,before,DateTime.Now);
        Assert.Equal(LegacyNativeMobStatus.Idle,role.State);AssertWalk(Assert.Single(s.Queue),6,role.UID,700);
        uint seed=1;MsNext(ref seed);Assert.Equal(MsNext(ref seed),s.Ai.Random.Next());
    });

    [Fact]
    public Task BlockedPrimaryUsesRealRandomFallbackAndPublishesSpawnToNewlyEnteredScreen()=>NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();var role=s.Monster();role.Target=s.Player;s.Map.cells[41,40]|=Cell.Monster;
        var peerQueue=new Queue<byte[]>();s.Peer(31,22,39,peerQueue);s.Ai.SearchTarget(null,role);
        Assert.Equal((ushort)39,role.X);Assert.Equal((ushort)39,role.Y);AssertWalk(Assert.Single(s.Queue),3,role.UID,700);
        Assert.Equal(2,peerQueue.Count);AssertWalk(peerQueue.First(),3,role.UID,700);Assert.Equal((ushort)2227,BitConverter.ToUInt16(peerQueue.Last(),2));
        Assert.True(s.Map.MonsterOnTile(39,39));Assert.True(s.Map.MonsterOnTile(41,40));Assert.False(s.Map.MonsterOnTile(40,40));
        uint seed=1;MsNext(ref seed);MsNext(ref seed);Assert.Equal(MsNext(ref seed),s.Ai.Random.Next());
    });

    [Fact]
    public Task BlockedBothStepsConsumesTwoDrawsAndCommitsStampWithoutClearingExistingOccupancy()=>NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();var role=s.Monster();role.Target=s.Player;s.Map.cells[41,40]|=Cell.Monster;s.Map.cells[39,39]|=Cell.Monster;DateTime before=DateTime.Now;
        s.Ai.SearchTarget(null,role);Assert.Equal((ushort)40,role.X);Assert.True(s.Map.MonsterOnTile(40,40));Assert.InRange(role.MoveStamp,before,DateTime.Now);Assert.Empty(s.Queue);
        uint seed=1;MsNext(ref seed);MsNext(ref seed);Assert.Equal(MsNext(ref seed),s.Ai.Random.Next());
    });

    [Fact]
    public Task SearchUsesTargetOwnerMapForCellsButMonsterGMapForPublication()=>NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();var role=s.Monster();role.Target=s.Player;var other=new LegacyNativeGameMap(80,80,701){View=new LegacyNativeMapView(80,80)};
        other.cells[41,40]=Cell.Valid;other.SetMonsterOnTile(40,40,true);s.Client.Map=other;s.Ai.SearchTarget(null,role);
        Assert.Same(s.Map,role.GMap);Assert.Equal((ushort)41,role.X);Assert.True(s.Map.MonsterOnTile(40,40));Assert.False(s.Map.MonsterOnTile(41,40));
        Assert.False(other.MonsterOnTile(40,40));Assert.True(other.MonsterOnTile(41,40));AssertWalk(Assert.Single(s.Queue),6,role.UID,700);
    });

    [Fact]
    public Task NullViewFaultKeepsNativePublishedCellPrefixAndCaughtIdleState()=>NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();var role=s.Monster();role.Target=s.Player;s.Map.View=null;DateTime before=DateTime.Now;
        s.Ai.SearchTarget(null,role);Assert.Equal((ushort)40,role.X);Assert.False(s.Map.MonsterOnTile(40,40));Assert.True(s.Map.MonsterOnTile(41,40));
        Assert.Equal(LegacyNativeMobStatus.Idle,role.State);Assert.InRange(role.MoveStamp,before,DateTime.Now);Assert.Empty(s.Queue);
    });

    [Theory]
    [InlineData(2,0,500u,true)] [InlineData(3,0,500u,false)] [InlineData(18,1,2000001u,true)] [InlineData(18,1,2000000u,false)] [InlineData(19,1,3000000u,false)]
    public Task SearchRetainsAllAttackRangeAndBossHealthGates(int distance,byte boss,uint hp,bool attacking)=>NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();var role=s.Monster();role.Target=s.Player;s.Player.X=(ushort)(40+distance);role.Boss=boss;role.HitPoints=hp;role.Family.ViewRange=30;role.MoveStamp=DateTime.Now.AddMinutes(1);
        s.Ai.SearchTarget(null,role);Assert.Equal(attacking?LegacyNativeMobStatus.Attacking:LegacyNativeMobStatus.Idle,role.State);Assert.Empty(s.Queue);Assert.Equal((ushort)40,role.X);
    });

    [Fact]
    public Task RandomRejectionStillCommitsStampAndDoesNotFabricateMovement()=>NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();var role=s.Monster();role.Target=s.Player;uint chosen=1;
        for(;chosen<1000;chosen++){uint trial=chosen;if(MsNext(ref trial)%100>=70)break;}Assert.True(chosen<1000);s.Ai.Random=new LegacyNativeMSRandom(chosen);
        DateTime before=DateTime.Now;s.Ai.SearchTarget(null,role);Assert.InRange(role.MoveStamp,before,DateTime.Now);Assert.Equal((ushort)40,role.X);Assert.Empty(s.Queue);
        uint control=chosen;MsNext(ref control);Assert.Equal(MsNext(ref control),s.Ai.Random.Next());
    });

    [Fact]
    public Task GuardReturnUsesWholeMapViewButDoesNotRepairTileFlagsOrCheckValidLocation()=>NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();var role=s.Monster();role.RespawnX=50;role.RespawnY=40;s.Map.cells[41,40]=Cell.None;var facing=role.Facing;
        s.Ai.CheckGuardPosition(s.Player,role);Assert.Equal((ushort)41,role.X);Assert.Equal(facing,role.Facing);Assert.Equal(Cell.None,s.Map.cells[41,40]);Assert.True(s.Map.MonsterOnTile(40,40));
        AssertWalk(Assert.Single(s.Queue),6,role.UID,700);
    });

    [Fact]
    public Task GuardReturnKeepsAliveHomeAndTimeGatesAndNativeNullViewFaultPrefix()=>NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();var role=s.Monster();role.RespawnX=50;role.RespawnY=40;role.HitPoints=0;s.Ai.CheckGuardPosition(null!,role);Assert.Equal(default,role.MoveStamp);
        role.HitPoints=1;role.MoveStamp=DateTime.Now.AddMinutes(1);s.Ai.CheckGuardPosition(s.Player,role);Assert.Equal((ushort)40,role.X);Assert.Empty(s.Queue);
        role.MoveStamp=default;s.Map.View=null;DateTime before=DateTime.Now;Assert.Throws<NullReferenceException>(()=>s.Ai.CheckGuardPosition(s.Player,role));
        Assert.InRange(role.MoveStamp,before,DateTime.Now);Assert.Equal((ushort)40,role.X);
    });
}
