using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;
using Cell=WolfConquer.Core.World.LegacyNativeMapFlagType;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeMonsterAiTests
{
    private sealed class Scope:IDisposable
    {
        private static readonly FieldInfo Bin=typeof(LegacyNativePacketRecycle).GetField("bin",BindingFlags.NonPublic|BindingFlags.Static)!;
        private readonly object? oldBin=Bin.GetValue(null);
        private readonly LegacyNativeSafeRandom oldRandom=LegacyNativePoolRandom.GetRandom;
        private readonly ConcurrentDictionary<uint,LegacyNativeGameClientRuntime>? oldClients=LegacyNativePoolClients.GamePoll;
        private readonly List<LegacyNativePacket> packets=new();
        internal NativeSpellTestFixture Native {get;}
        internal LegacyNativeGameClientRuntime Client=>Native.Owner;
        internal LegacyNativePlayerRuntime Player=>Client.Player;
        internal Queue<byte[]> Queue=>Native.Queue;
        internal LegacyNativePacket Packet=>Native.Packet;
        internal LegacyNativeGameMap Map {get;}=new(80,80,700){View=new LegacyNativeMapView(80,80)};
        internal LegacyNativeMonsterActionHandler Ai {get;}=new(){Random=new LegacyNativeMSRandom(1)};
        internal Scope()
        {
            LegacyNativePoolRandom.GetRandom=new(71);LegacyNativePoolClients.GamePoll=new();Native=new();Client.Map=Map;Player.Map=Map.ID;Player.X=50;Player.Y=40;Alive(Player);
            for(int x=0;x<80;x++)for(int y=0;y<80;y++)Map.cells[x,y]=Cell.Valid;
            Assert.True(Map.View!.EnterMap((ILegacyNativeMapObj)Player));
            var bin=new ConcurrentQueue<LegacyNativePacket>();for(int i=0;i<8;i++){var p=new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);packets.Add(p);bin.Enqueue(p);}Bin.SetValue(null,bin);
        }
        internal LegacyNativeMonsterRole Monster()
        {
            var f=new LegacyNativeMonsterFamily{ID=300,Name="Pheasant",MaxAttack=200,MinAttack=100,MaxHealth=500,Defense=15,Mesh=100,Level=15,AttackRange=2,ViewRange=18,MoveSpeed=300,DropSpecials=[]};
            var monster=new LegacyNativeMonsterRole(f,400001,"owned-ai",Map){Map=Map.ID,X=40,Y=40};Assert.True(Map.View!.EnterMap(monster));Map.SetMonsterOnTile(40,40,true);return monster;
        }
        internal LegacyNativeGameClientRuntime Peer(uint uid,ushort x,ushort y,Queue<byte[]> queue)
        {
            var peer=NativeSpellTestFixture.CreateOwner(queue);peer.Map=Map;peer.Player.Map=Map.ID;peer.Player.UID=uid;peer.Player.X=x;peer.Player.Y=y;Alive(peer.Player);
            Assert.True(Map.View!.EnterMap((ILegacyNativeMapObj)peer.Player));return peer;
        }
        public void Dispose()
        {
            Bin.SetValue(null,oldBin);foreach(var p in packets)p.Dispose();Native.Dispose();LegacyNativePoolRandom.GetRandom=oldRandom;LegacyNativePoolClients.GamePoll=oldClients;
        }
    }
    private static void Alive(LegacyNativePlayerRuntime player,int hp=100)=>typeof(LegacyNativePlayerRuntime).GetField("_minhitpoints",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(player,hp);
    private static int MsNext(ref uint seed)=> (int)(((seed=unchecked(seed*214013+2531011))>>16)&0x7FFF);
    private static void Field(List<byte> bytes,int tag,ulong value){Varint(bytes,(ulong)(tag<<3));Varint(bytes,value);}
    private static void Varint(List<byte> bytes,ulong value){while(value>=128){bytes.Add((byte)((value&127)|128));value>>=7;}bytes.Add((byte)value);}
    private static byte[] Walk(uint direction,uint uid,uint running,uint stamp,uint map)
    {
        var payload=new List<byte>();Field(payload,1,direction);Field(payload,2,uid);Field(payload,3,running);Field(payload,4,stamp);if(map!=0)Field(payload,5,map);
        var frame=new List<byte>();frame.AddRange(BitConverter.GetBytes((ushort)(payload.Count+4)));frame.AddRange(BitConverter.GetBytes((ushort)2205));frame.AddRange(payload);return frame.ToArray();
    }
    private static Dictionary<int,List<ulong>> Scalars(byte[] frame)
    {
        int offset=4;ulong Read(){ulong v=0;int shift=0;byte b;do{b=frame[offset++];v|=(ulong)(b&127)<<shift;shift+=7;}while((b&128)!=0);return v;}
        var result=new Dictionary<int,List<ulong>>();while(offset<frame.Length){ulong key=Read();Assert.Equal(0UL,key&7);int tag=(int)(key>>3);if(!result.TryGetValue(tag,out var list))result[tag]=list=new();list.Add(Read());}return result;
    }
    private static uint AssertWalk(byte[] frame,uint direction,uint uid,uint map)
    {
        Assert.Equal((ushort)2205,BitConverter.ToUInt16(frame,2));var values=Scalars(frame);Assert.Equal((ulong)direction,Assert.Single(values[1]));Assert.Equal((ulong)uid,Assert.Single(values[2]));Assert.Equal(1UL,Assert.Single(values[3]));
        uint stamp=(uint)Assert.Single(values[4]);Assert.Equal(Walk(direction,uid,1,stamp,map),frame);return stamp;
    }
}
