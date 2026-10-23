using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCloneTests
{
    [Fact]
    public Task FlagUpdatesSendOnlyOwnerAndPreserveGenuineIdempotenceAndRawBitmap()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var map=scope.Map();var peerQueue=new Queue<byte[]>();scope.Peer(map,peerQueue);
        var clone=new LegacyNativeClone(scope.Player,"x",3,scope.Packet);scope.Queue.Clear();peerQueue.Clear();
        clone.AddFlag(Flag.Poisoned);clone.AddFlag(Flag.Poisoned);
        Assert.True(clone.BitVector.Contain((int)Flag.Poisoned));Assert.Single(scope.Queue);Assert.Empty(peerQueue);
        clone.RemoveFlag(Flag.Poisoned);clone.RemoveFlag(Flag.Poisoned);
        Assert.False(clone.BitVector.Contain((int)Flag.Poisoned));Assert.Equal(2,scope.Queue.Count);Assert.Empty(peerQueue);
        clone.UpdateSpawnPacket();Assert.Equal(3,scope.Queue.Count);Assert.Empty(peerQueue);
        Assert.All(scope.Queue,frame=>Assert.Equal((ushort)2176,Opcode(frame)));
    });

    [Theory]
    [InlineData(223,false,true)] [InlineData(224,true,false)] [InlineData(225,false,false)]
    public Task CloneFlagBoundariesRetainNativeBitmapFaultAndUpdateBehavior(int index,bool faults,bool present)=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var clone=new LegacyNativeClone(scope.Player,"x",3,scope.Packet);scope.Queue.Clear();
        if(faults){Assert.Throws<IndexOutOfRangeException>(()=>clone.AddFlag((Flag)index));Assert.Empty(scope.Queue);}
        else
        {
            clone.AddFlag((Flag)index);Assert.Equal(present,clone.BitVector.Contain(index));Assert.Single(scope.Queue);
        }
    });

    [Fact]
    public Task WholeSendAndSendViewUseExplicitRecipientButRemovalUsesCloneOwnerNotIgnoredArgument()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var map=scope.Map();var peerQueue=new Queue<byte[]>();var peer=scope.Peer(map,peerQueue);
        var clone=new LegacyNativeClone(scope.Player,"x",3,scope.Packet);scope.Queue.Clear();peerQueue.Clear();
        clone.Send(peer,scope.Packet);Assert.Empty(scope.Queue);Assert.Equal((ushort)2227,Opcode(Assert.Single(peerQueue)));
        peerQueue.Clear();clone.SendView(peer,scope.Packet);Assert.Single(peerQueue);Assert.Single(scope.Queue);
        scope.Queue.Clear();peerQueue.Clear();clone.RemoveThat(null!);
        Assert.Equal((ushort)2046,Opcode(Assert.Single(scope.Queue)));Assert.Equal((ushort)2046,Opcode(Assert.Single(peerQueue)));
    });

    [Fact]
    public Task FullRoleViewClearResetsOnlyNativeOldCoordinatesAndCloneListThenSendsRemovalSequence()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var map=scope.Map();var peerQueue=new Queue<byte[]>();var peer=scope.Peer(map,peerQueue);
        LegacyNativeClone.CreateShadowClone(scope.Player,scope.Packet);var held=scope.Player.MyClones;
        scope.Player.Px=40;scope.Player.Py=41;
        scope.Player.View.src[(int)LegacyNativeMapObjectType.Player].Add(peer.Player.UID,(ILegacyNativeMapObj)peer.Player);
        scope.Queue.Clear();peerQueue.Clear();
        scope.Player.View.Clear(scope.Packet);
        Assert.Equal((ushort)0,scope.Player.Px);Assert.Equal((ushort)0,scope.Player.Py);Assert.Same(held,scope.Player.MyClones);Assert.Empty(held);
        Assert.Equal(2,scope.Queue.Count);Assert.Equal(3,peerQueue.Count);
        Assert.All(scope.Queue,frame=>Assert.Equal((ushort)2046,Opcode(frame)));Assert.All(peerQueue,frame=>Assert.Equal((ushort)2046,Opcode(frame)));
        Assert.Single(scope.Player.View.src[(int)LegacyNativeMapObjectType.Player]);
        Assert.Equal(2,map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Player).Count());
    });

    [Fact]
    public Task WholeClearMissingCloneListFaultsOnlyAfterCoordinateAndPlayerRemovalPrefix()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var map=scope.Map();var peerQueue=new Queue<byte[]>();scope.Peer(map,peerQueue);
        scope.Player.Px=40;scope.Player.Py=41;scope.Player.MyClones=null!;scope.Queue.Clear();peerQueue.Clear();
        Assert.Throws<NullReferenceException>(()=>scope.Player.View.Clear(scope.Packet));
        Assert.Equal((ushort)0,scope.Player.Px);Assert.Equal((ushort)0,scope.Player.Py);Assert.Null(scope.Player.MyClones);
        Assert.Empty(scope.Queue);Assert.Equal((ushort)2046,Opcode(Assert.Single(peerQueue)));
        Assert.Equal(2,map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Player).Count());
    });
}
