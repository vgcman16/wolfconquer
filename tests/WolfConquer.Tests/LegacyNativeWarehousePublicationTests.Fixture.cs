using System.Buffers.Binary;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeWarehousePublicationTests
{
    private sealed class Scope : IDisposable
    {
        private readonly LegacyNativeSafeRandom previousRandom=LegacyNativePoolRandom.GetRandom;
        private readonly DateTime previousReset=LegacyNativePoolRandom.ResetRandom;
        private readonly LegacyNativeGroupServerList.Server? previousServer=LegacyNativeGroupServerList.MyServerInfo;
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client=>Native.Owner;
        internal LegacyNativePacket Packet=>Native.Packet;
        internal Queue<byte[]> Queue=>Native.Queue;
        internal LegacyNativeWarehouseRuntime Warehouse { get; }
        internal unsafe Scope()
        {
            LegacyNativePoolRandom.GetRandom=new LegacyNativeSafeRandom(61);
            LegacyNativePoolRandom.ResetRandom=new DateTime(2026,1,1);
            Native=new NativeSpellTestFixture();
            LegacyNativeGroupServerList.MyServerInfo=new LegacyNativeGroupServerList.Server { ID=2 };
            Client.Player.ServerID=2;
            Warehouse=new LegacyNativeWarehouseRuntime(Client); Client.Warehouse=Warehouse;
            Marshal.Copy(Enumerable.Repeat((byte)0xA5,LegacyNativePacket.MAX_SIZE).ToArray(),0,
                (IntPtr)Packet.Memory,LegacyNativePacket.MAX_SIZE);
            Packet.Size=321; Packet.Seek(237);
        }
        public void Dispose()
        {
            Native.Dispose(); LegacyNativeGroupServerList.MyServerInfo=previousServer!;
            LegacyNativePoolRandom.GetRandom=previousRandom; LegacyNativePoolRandom.ResetRandom=previousReset;
        }
    }
    private static void U16(byte[] bytes,int offset,ushort value)=>BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset),value);
    private static void U32(byte[] bytes,int offset,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset),value);
    private static LegacyNativeGameItemRuntime Item(uint uid)=>new() { Uid=uid,ITEM_ID=1000000,StackSize=1 };
    private static byte[] Frame(uint bank,ushort capacity,LegacyNativeMsgWarehouse.DepositActionID action,params uint[] uids)
    {
        byte[] bytes=new byte[24+224*uids.Length]; U16(bytes,0,(ushort)bytes.Length); U16(bytes,2,2080);
        U32(bytes,4,bank); U32(bytes,12,(uint)action); U16(bytes,16,capacity); U16(bytes,22,(ushort)uids.Length);
        for(int index=0;index<uids.Length;index++)
        {
            int offset=24+224*index; U32(bytes,offset,uids[index]); U32(bytes,offset+4,1000000); U16(bytes,offset+44,1);
        }
        return bytes;
    }
}
