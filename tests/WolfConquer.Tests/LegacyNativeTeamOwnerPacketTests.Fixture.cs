using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeTeamOwnerPacketTests
{
    private sealed class Scope : IDisposable
    {
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime oldReset = LegacyNativePoolRandom.ResetRandom;
        private readonly LegacyNativeTeamArenaUser[] oldTop10 = LegacyNativeTeamArenaRankings.Top10;
        private readonly LegacyNativeTeamArenaUser[] oldToday = LegacyNativeTeamArenaRankings.Top1000Today;
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;

        internal Scope()
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(83);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026,1,1);
            Native = new(); Client.TeamArenaStatistic = Row();
            Client.Player.Name = "OwnerWolf";
            typeof(LegacyNativePlayerRuntime).GetField("_class",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Client.Player,16004u);
            typeof(LegacyNativePlayerRuntime).GetField("_level",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Client.Player,(ushort)137);
            LegacyNativeTeamArenaRankings.Top10 = new LegacyNativeTeamArenaUser[10];
            LegacyNativeTeamArenaRankings.Top1000Today = new LegacyNativeTeamArenaUser[1000];
            Fill(Packet);
        }

        public void Dispose()
        {
            Native.Dispose(); LegacyNativeTeamArenaRankings.Top10=oldTop10; LegacyNativeTeamArenaRankings.Top1000Today=oldToday;
            LegacyNativePoolRandom.GetRandom=oldRandom; LegacyNativePoolRandom.ResetRandom=oldReset;
        }
    }

    private static LegacyNativeTeamArenaUser Row(uint rank=3,string name="WolfOne")
    {
        var row = new LegacyNativeTeamArenaUser { UID=91,Name=name,Mesh=1003,Class=16004,Level=137,
            LastSeasonRank=rank,LastSeasonArenaPoints=11,LastSeasonWin=12,LastSeasonLose=13 };
        row.Info.TodayRank=rank; row.Info.ArenaPoints=456; return row;
    }

    private static unsafe void Fill(LegacyNativePacket packet)
    {
        Marshal.Copy(Enumerable.Repeat((byte)0xA5,LegacyNativePacket.MAX_SIZE).ToArray(),0,(IntPtr)packet.Memory,LegacyNativePacket.MAX_SIZE);
        packet.Size=321; packet.Seek(237);
    }

    private static byte[] Bytes(LegacyNativePacket packet)=>NativeSpellTestFixture.Bytes(packet);
    private static byte[] Frame(ushort opcode,List<byte> body)
    {
        byte[] bytes=new byte[4+body.Count];BinaryPrimitives.WriteUInt16LittleEndian(bytes,(ushort)bytes.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2),opcode);body.CopyTo(bytes,4);return bytes;
    }
    private static void U16(List<byte> bytes,ushort value)=>bytes.AddRange(BitConverter.GetBytes(value));
    private static void U32(List<byte> bytes,uint value)=>bytes.AddRange(BitConverter.GetBytes(value));
    private static void Fixed(List<byte> bytes,string value,int length)
    {
        int chars=Math.Min(value.Length,length);bytes.AddRange(Encoding.UTF8.GetBytes(value).Take(chars));bytes.AddRange(new byte[length-chars]);
    }
    private static void TopRow(List<byte> body,string name,uint rank,uint mesh=1003,uint job=16004,uint level=137,uint points=11,uint win=12,uint lose=13)
    {
        Fixed(body,name,32);foreach(uint value in new[]{rank,mesh,job,level,points,win,lose})U32(body,value);
    }
    private static void TodayRow(List<byte> body,uint rank,string name,uint points=456,uint job=16004,uint level=137,byte gender=0)
    {
        foreach(uint value in new[]{rank,2979015u,points,job,level})U32(body,value);body.Add(gender);Fixed(body,name,32);body.AddRange(new byte[3]);
    }
    private static void Query(LegacyNativePacket packet,ushort page)
    {
        packet.InitWriter();packet.Write(page);packet.Seek(4);
    }
    private static void TopHandler(Scope scope)
    {
        var handler=typeof(LegacyNativeMsgTeamArenaRank10).GetMethod("Handler",BindingFlags.Static|BindingFlags.NonPublic)!;
        handler.Invoke(null,new object[]{scope.Client,scope.Packet});
    }
}
