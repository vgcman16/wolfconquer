using ProtoBuf;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Animation=WolfConquer.Core.World.LegacyNativeMsgSpellAnimation;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterAiTests
{
    [Theory]
    [InlineData(0u,0u,0u,0u)] [InlineData(4294967295u,4294967295u,4294967295u,4294967295u)] [InlineData(6u,400001u,1u,700u)]
    public Task WholeMovementWriterUsesRealTimestampAndReaderAcceptsFullMetadata(uint direction,uint uid,uint running,uint map)=>NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();var row=new LegacyNativeWalkQuery{Direction=direction,UID=uid,Running=running,MapID=map,TimeStamp=0xDEADBEEF};
        uint before=unchecked((uint)Environment.TickCount);s.Packet.Seek(311);s.Packet.MovementCreate(row);uint after=unchecked((uint)Environment.TickCount);
        Assert.Equal(Walk(direction,uid,running,row.TimeStamp,map),NativeSpellTestFixture.Bytes(s.Packet));Assert.Equal(4,s.Packet.Position);
        Assert.True(unchecked(row.TimeStamp-before+1000)<=unchecked(after-before+2000));
        var bytes=Walk(direction,uid,running,0xFFFFFFFF,map);using var input=new LegacyNativePacket(bytes);input.Seek(20);
        input.GetWalk(out var read);Assert.Equal(direction,read.Direction);Assert.Equal(uid,read.UID);Assert.Equal(running,read.Running);Assert.Equal(uint.MaxValue,read.TimeStamp);Assert.Equal(map,read.MapID);Assert.Equal(bytes.Length,input.Position);
    });

    [Fact]
    public void WalkContractIsTheGenuineFiveFieldNativeRecord()
    {
        var fields=typeof(LegacyNativeWalkQuery).GetFields();Assert.Equal(5,fields.Length);
        foreach(var field in fields){Assert.Equal(typeof(uint),field.FieldType);var tag=(ProtoMemberAttribute)Attribute.GetCustomAttribute(field,typeof(ProtoMemberAttribute))!;Assert.Equal(field.Name!="MapID",tag.IsRequired);}
        Assert.NotNull(Attribute.GetCustomAttribute(typeof(LegacyNativeWalkQuery),typeof(ProtoContractAttribute)));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(30)] [InlineData(31)] [InlineData(60)] [InlineData(61)]
    public Task WholeMonsterAnimationOverloadPublishesAllChunksThroughRealMonsterView(int count)=>NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();var role=s.Monster();var peerQueue=new Queue<byte[]>();s.Peer(31,45,40,peerQueue);
        var excludedQueue=new Queue<byte[]>();var excluded=s.Peer(32,45,40,excludedQueue);excluded.Player.DynamicID=9;
        var animation=new Animation(role.UID,0,41,42,9999,257,3,2);animation.NextSpell=17;animation.SetStream(s.Packet);
        var expected=new List<byte[]>();for(int i=0;i<count;i++)animation.Targets.Enqueue(new((uint)(100+i),(uint)(200+i),LegacyNativeMsgAttackPacket.AttackEffect.CriticalStrike){MoveX=(uint)(65535+i),MoveY=(uint)(65536+i)});
        var saved=animation.Targets.ToArray();var chunks=count==0?new[]{Array.Empty<Animation.SpellObj>()}:saved.Chunk(30).ToArray();
        foreach(var chunk in chunks)
        {
            var targets=chunk.Select(t=>LegacyNativeSpellAnimationTests.Target(t.UID,t.Damage,true,4,unchecked((ushort)t.MoveX),unchecked((ushort)t.MoveY))).ToArray();
            expected.Add(LegacyNativeSpellAnimationTests.Frame(LegacyNativeSpellAnimationTests.Payload(role.UID,0,41,42,9999,1,3,17,2,targets)));
        }
        animation.Send(role);Assert.Equal(expected.Count,s.Queue.Count);Assert.Equal(expected.Count,peerQueue.Count);Assert.Empty(excludedQueue);
        for(int i=0;i<expected.Count;i++){Assert.Equal(expected[i],s.Queue.ElementAt(i));Assert.Equal(expected[i],peerQueue.ElementAt(i));}
        Assert.Equal(saved,animation.Targets.ToArray());Assert.Equal(4,s.Packet.Position);
    });

    [Fact]
    public Task MonsterAnimationKeepsOriginalMissingPoolNullRoleAndDisposedQueueFaultOrder()=>NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();var role=s.Monster();var animation=new Animation(role.UID,0,41,42,9999,1,0);animation.SetStream(s.Packet);
        LegacyNativePoolClients.GamePoll=null;s.Packet.Seek(77);s.Packet.Size=91;
        Assert.Throws<NullReferenceException>(()=>animation.Send(role));Assert.Equal(4,s.Packet.Position);Assert.Equal(91,s.Packet.Size);Assert.Empty(s.Queue);
        LegacyNativePoolClients.GamePoll=new();Assert.Throws<NullReferenceException>(()=>animation.Send((LegacyNativeMonsterRole)null!));Assert.Equal(4,s.Packet.Position);Assert.True(s.Packet.Size>4);
        animation.Dispose();s.Packet.Seek(99);Assert.Throws<NullReferenceException>(()=>animation.Send(role));Assert.Equal(99,s.Packet.Position);Assert.Empty(s.Queue);
    });
}
