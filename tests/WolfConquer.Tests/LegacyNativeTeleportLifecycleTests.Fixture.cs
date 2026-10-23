using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using ProtoBuf;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeTeleportLifecycleTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? oldBin = Bin.GetValue(null);
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime oldReset = LegacyNativePoolRandom.ResetRandom;
        private readonly LegacyNativeMapDictionary<uint, LegacyNativeGameMap>? oldMaps = LegacyNativePoolMaps.ServerMaps;
        private readonly LegacyNativeCounter oldDynamic = LegacyNativeGameMap.DinamicIDS;
        private readonly LegacyNativeCounter oldVendor = LegacyNativeVendorRuntime.VendorCounter;
        private readonly List<LegacyNativePacket> borrowed = new();
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;

        internal unsafe Scope()
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(97);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            LegacyNativePoolMaps.ServerMaps = new();
            LegacyNativeGameMap.DinamicIDS = new LegacyNativeCounter(10000001u);
            LegacyNativeVendorRuntime.VendorCounter = new LegacyNativeCounter(100000u);
            Native = new();
            Client.Player.Name = "Owner";
            Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)Packet.Memory, LegacyNativePacket.MAX_SIZE);
            var bin = new ConcurrentQueue<LegacyNativePacket>();
            for (int i = 0; i < 16; i++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
                borrowed.Add(packet); bin.Enqueue(packet);
            }
            Bin.SetValue(null, bin);
        }

        internal LegacyNativeGameClientRuntime Peer(uint uid, Queue<byte[]> queue)
        {
            var peer = NativeSpellTestFixture.CreateOwner(queue);
            peer.Player.UID = uid; peer.Player.Name = "Peer" + uid;
            return peer;
        }

        internal LegacyNativeGameMap Map(int id = 1002)
        {
            var map = new LegacyNativeGameMap(128, 128, id) { View = new LegacyNativeMapView(128, 128) };
            Assert.True(LegacyNativePoolMaps.ServerMaps!.Add((uint)id, map));
            return map;
        }

        internal static void Admit(LegacyNativeGameMap map, LegacyNativeGameClientRuntime client, ushort x = 64, ushort y = 64)
        {
            client.Player.Map = map.ID; client.Player.X = x; client.Player.Y = y;
            map.Enquer(client);
        }

        public void Dispose()
        {
            Bin.SetValue(null, oldBin);
            foreach (var packet in borrowed) packet.Dispose();
            Native.Dispose();
            LegacyNativePoolRandom.GetRandom = oldRandom;
            LegacyNativePoolRandom.ResetRandom = oldReset;
            LegacyNativePoolMaps.ServerMaps = oldMaps;
            LegacyNativeGameMap.DinamicIDS = oldDynamic;
            LegacyNativeVendorRuntime.VendorCounter = oldVendor;
        }
    }

    private static byte[] Bytes(LegacyNativePacket packet) => NativeSpellTestFixture.Bytes(packet);
    private static ushort Opcode(byte[] bytes) => BitConverter.ToUInt16(bytes, 2);
    private static T ReadProto<T>(byte[] bytes)
    {
        using var body = new MemoryStream(bytes, 4, bytes.Length - 4);
        return Serializer.Deserialize<T>(body);
    }

    private static LegacyNativeGameItemRuntime Item(uint uid, uint id = 410000) => new() { Uid = uid, ITEM_ID = id };

    private static byte[] ProtoFrame(ushort opcode, params (uint tag, ulong value)[] fields)
    {
        var body = new List<byte>();
        foreach (var field in fields) { Varint(body, (ulong)field.tag << 3); Varint(body, field.value); }
        var frame = new byte[body.Count + 4];
        BitConverter.GetBytes((ushort)frame.Length).CopyTo(frame, 0);
        BitConverter.GetBytes(opcode).CopyTo(frame, 2);
        body.CopyTo(frame, 4);
        return frame;
    }

    private static void Varint(List<byte> bytes, ulong value)
    {
        while (value >= 128) { bytes.Add((byte)(value | 128)); value >>= 7; }
        bytes.Add((byte)value);
    }

    private static byte[] TradeBytes(ulong action, long money, ulong item = 0, uint cps = 0, ulong unknown = 0, ulong uid = 0) =>
        ProtoFrame(2119, (1, action), (2, unchecked((ulong)money)), (3, item), (4, cps), (5, unknown), (6, uid));

    private static byte[] RemoveBytes(uint uid) => ProtoFrame(2046, (1, uid), (9, 0), (12, 135), (13, 0), (14, 0), (15, 0));
}
