using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using ProtoBuf;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Player = WolfConquer.Core.World.LegacyNativePlayerRuntime;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativePlayerLifecycleTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? oldBin = Bin.GetValue(null);
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime oldReset = LegacyNativePoolRandom.ResetRandom;
        private readonly ConcurrentDictionary<uint, LegacyNativeAssociateRuntime.MyAsociats> oldAssociates = LegacyNativeAssociateRuntime.Associates;
        private readonly List<LegacyNativePacket> packets = new();
        internal readonly NativeSpellTestFixture Native;
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal Player Player => Client.Player;
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;
        internal Scope()
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(83);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            LegacyNativeAssociateRuntime.Associates = new();
            Native = new NativeSpellTestFixture();
            Client.HeroRewards = new LegacyNativeHeroRewardsRuntime(Client);
            var queue = new ConcurrentQueue<LegacyNativePacket>();
            for (int i = 0; i < 8; i++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Fill(packet); packets.Add(packet); queue.Enqueue(packet);
            }
            Bin.SetValue(null, queue); Fill(Packet); Packet.InitWriter();
        }
        internal void NullBin() => Bin.SetValue(null, null);
        internal void Flag(Flag flag, int seconds = 17) => Player.BitVector.TryAdd((int)flag, seconds, false, 0);
        public void Dispose()
        {
            Bin.SetValue(null, oldBin); foreach (var packet in packets) packet.Dispose(); Native.Dispose();
            LegacyNativePoolRandom.GetRandom = oldRandom; LegacyNativePoolRandom.ResetRandom = oldReset;
            LegacyNativeAssociateRuntime.Associates = oldAssociates;
        }
    }
    private static unsafe void Fill(LegacyNativePacket packet)
    {
        Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(), 0,
            (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE); packet.Size = 321; packet.Seek(237);
    }
    private static byte[] Update(uint uid, uint type, ulong scalar = 0, params uint[] values)
    {
        static void V(List<byte> bytes, ulong value)
        {
            do { byte next = (byte)(value & 127); value >>= 7; bytes.Add((byte)(next | (value == 0 ? 0 : 128))); } while (value != 0);
        }
        var body = new List<byte> { 8 }; V(body, uid);
        var entry = new List<byte>(); if (type != 0) { entry.Add(8); V(entry, type); }
        if (scalar != 0) { entry.Add(16); V(entry, scalar); }
        foreach (uint value in values) { entry.Add(24); V(entry, value); }
        body.Add(18); V(body, (uint)entry.Count); body.AddRange(entry);
        byte[] frame = new byte[body.Count + 4]; BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)frame.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2176); body.CopyTo(frame, 4); return frame;
    }
    private static LegacyNativeMsgMessage.MsgTalkProto Talk(byte[] bytes)
    {
        Assert.Equal((ushort)2179, BitConverter.ToUInt16(bytes, 2));
        using var body = new MemoryStream(bytes, 4, bytes.Length - 4);
        return Serializer.Deserialize<LegacyNativeMsgMessage.MsgTalkProto>(body);
    }
}
