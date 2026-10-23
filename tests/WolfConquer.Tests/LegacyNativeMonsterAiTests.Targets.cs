using WolfConquer.Core.World;
using Flag=WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterAiTests
{
    [Fact]
    public Task InitialTargetLookupUsesActualHighestIndexThenLastNonFlyingFallback()=>NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();var role=s.Monster();var queue=new Queue<byte[]>();var peer=s.Peer(31,41,40,queue);
        s.Ai.CheckTarget(s.Player,role);Assert.Same(peer.Player,role.Target);Assert.Equal(LegacyNativeMobStatus.SearchTarget,role.State);
        role.Target=null;peer.Player.BitVector.TryAdd((int)Flag.Fly,100,false,0);s.Ai.CheckTarget(s.Player,role);
        Assert.Same(s.Player,role.Target);Assert.Equal(LegacyNativeMobStatus.SearchTarget,role.State);Assert.Empty(s.Queue);Assert.Empty(queue);
    });

    [Fact]
    public Task InitialLookupRetainsNativeDeadHighestIndexTargetRatherThanInventingAliveFilter()=>NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();var role=s.Monster();var peer=s.Peer(31,41,40,new());Alive(peer.Player,0);
        s.Ai.CheckTarget(s.Player,role);Assert.Same(peer.Player,role.Target);Assert.Equal(LegacyNativeMobStatus.SearchTarget,role.State);
        s.Ai.SearchTarget(s.Player,role);Assert.Same(peer.Player,role.Target);Assert.Equal(LegacyNativeMobStatus.Idle,role.State);
    });

    [Theory]
    [InlineData(false,false)] [InlineData(true,false)] [InlineData(false,true)]
    public Task InitialTriggerMustBeAliveAndWithinFamilyRange(bool dead,bool far)=>NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();var role=s.Monster();if(dead)Alive(s.Player,0);if(far)s.Player.X=70;
        s.Ai.CheckTarget(s.Player,role);Assert.Equal(!dead&&!far,role.Target!=null);Assert.Empty(s.Queue);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ExistingTargetInvalidationUsesTriggerDistanceAndRealSocketState(bool disconnect)=>NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();var role=s.Monster();var peer=s.Peer(31,41,40,new());role.Target=peer.Player;role.State=LegacyNativeMobStatus.Attacking;
        if(disconnect)peer.Socket!.Alive=false;else s.Player.X=70;
        s.Ai.CheckTarget(s.Player,role);Assert.Null(role.Target);Assert.Equal(LegacyNativeMobStatus.Attacking,role.State);
    });

    [Fact]
    public Task ExistingDeadTargetIsNotClearedByCheckTargetAndFlyingTriggerUsesWholeFallback()=>NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();var role=s.Monster();var peer=s.Peer(31,41,40,new());Alive(peer.Player,0);role.Target=peer.Player;role.State=LegacyNativeMobStatus.Idle;
        s.Ai.CheckTarget(s.Player,role);Assert.Same(peer.Player,role.Target);Assert.Equal(LegacyNativeMobStatus.Idle,role.State);
        role.Target=null;Alive(s.Player,0);s.Player.BitVector.TryAdd((int)Flag.Fly,100,false,0);Alive(peer.Player);
        s.Ai.CheckTarget(s.Player,role);Assert.Same(peer.Player,role.Target);Assert.Equal(LegacyNativeMobStatus.SearchTarget,role.State);
    });

    [Fact]
    public Task WholeTargetMethodsKeepNativeNullAndDeadMonsterPrefix()=>NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();s.Ai.CheckTarget(null!,null);s.Ai.SearchTarget(null,null);
        var role=s.Monster();role.HitPoints=0;role.Target=s.Player;role.State=LegacyNativeMobStatus.Attacking;
        s.Ai.CheckTarget(null!,role);s.Ai.SearchTarget(null,role);Assert.Same(s.Player,role.Target);Assert.Equal(LegacyNativeMobStatus.Attacking,role.State);
        role.HitPoints=1;Assert.Throws<NullReferenceException>(()=>s.Ai.CheckTarget(null!,role));
    });
}
