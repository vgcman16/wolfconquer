using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Action = WolfConquer.Core.World.LegacyNativeClientAchievement.TypeAchievement;
using Msg = WolfConquer.Core.World.LegacyNativeMsgMessage;

namespace WolfConquer.Tests;

// Actual native owners, unmanaged Packet and queued SecuritySocket.Send, not a host or SaveClient proof.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeAchievementTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Seal = typeof(LegacyNativePacket).GetField("seal", BindingFlags.Static | BindingFlags.NonPublic)!;
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? oldSeal = Seal.GetValue(null);
        private readonly object oldBin = Bin.GetValue(null)!;
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? oldPoll = LegacyNativePoolClients.GamePoll;
        private readonly List<LegacyNativePacket> packets = [];
        internal LegacyNativePacket Packet { get; }
        internal ConcurrentQueue<LegacyNativePacket> Recycle { get; } = new();
        internal LegacyNativePacket FirstRecycled { get; }
        internal ConcurrentDictionary<uint, LegacyNativeGameClientRuntime> Poll { get; } = new();
        internal Queue<byte[]> Queue { get; } = new();
        internal LegacyNativeGameClientRuntime Client { get; }

        internal Scope(bool seal = false)
        {
            Seal.SetValue(null, seal ? "TQServer" : null);
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
            LegacyNativePoolClients.GamePoll = Poll;
            Packet = Allocate();
            for (int i = 0; i < 4; i++) Recycle.Enqueue(Allocate());
            FirstRecycled = Recycle.First();
            Bin.SetValue(null, Recycle);
            Client = MakeClient(Queue, 17, "Wolf");
        }

        private unsafe LegacyNativePacket Allocate()
        {
            var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(), 0,
                (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
            packet.Size = 321;
            packet.Seek(237);
            packets.Add(packet);
            return packet;
        }

        internal void Request(Action action, uint uid, uint flag, uint reserved = 0xDEADBEEF)
        {
            Packet.InitWriter(); Packet.Write((uint)action); Packet.Write(uid); Packet.Write(reserved); Packet.Write(flag);
            Packet.Finalize(2307); Packet.Seek(4);
        }

        public void Dispose()
        {
            LegacyNativePoolClients.GamePoll = oldPoll;
            LegacyNativePoolRandom.GetRandom = oldRandom;
            Bin.SetValue(null, oldBin); Seal.SetValue(null, oldSeal);
            foreach (var packet in packets) packet.Dispose();
        }
    }

    private static LegacyNativeGameClientRuntime MakeClient(Queue<byte[]> queue, uint uid, string name)
    {
        var client = NativeSpellTestFixture.CreateOwner(queue);
        client.Player.UID = uid; client.Player.Name = name;
        return client;
    }

    private static LegacyNativeClientAchievement Attach(LegacyNativeGameClientRuntime client, params uint[] flags)
    {
        return client.Player.Achievement = new LegacyNativeClientAchievement(flags, client.Player.UID);
    }

    private static uint[] Words(uint first = 0, uint last = 0)
    {
        var words = new uint[14]; words[0] = first; words[13] = last; return words;
    }

    private static unsafe byte[] Bytes(LegacyNativePacket packet, int? count = null)
    {
        var bytes = new byte[count ?? packet.Size];
        Assert.InRange(bytes.Length, 0, LegacyNativePacket.MAX_SIZE);
        Marshal.Copy((IntPtr)packet.Memory, bytes, 0, bytes.Length); return bytes;
    }

    private static byte[] ArrayFrame(Action action, uint uid, params uint[] flags)
    {
        var bytes = new byte[20 + 4 * flags.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)bytes.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2307);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), (uint)action);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), uid);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), flags.Length);
        for (int i = 0; i < flags.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20 + 4 * i), flags[i]);
        return bytes;
    }

    private static byte[] ScalarFrame(Action action, uint uid, uint flag)
    {
        var bytes = new byte[24];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 24); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2307);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), (uint)action);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), uid);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), flag);
        return bytes;
    }

    private static void AssertChat(byte[] bytes, string to, string message, Msg.ChatMode mode, Msg.MsgColor color)
    {
        Assert.InRange(bytes.Length, 5, 1000);
        Assert.Equal((ushort)2179, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2)));
        using var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
        Copy(packet, bytes);
        var proto = packet.ProtoBufferDeserialize(new Msg.MsgTalkProto());
        Assert.Equal((uint)mode, proto.ChatType); Assert.Equal((uint)color, proto.Color);
        Assert.Equal(1u, proto.Count); Assert.Equal(0u, proto.UnionType); Assert.Equal(0u, proto.ServerID);
        Assert.Equal(new[] { "SYSTEM", to, "", message, "", "", "" }, proto.Content);
    }

    private static unsafe void Copy(LegacyNativePacket packet, byte[] bytes)
    {
        Assert.InRange(bytes.Length, 0, LegacyNativePacket.MAX_SIZE);
        Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
        Marshal.Copy(bytes, 0, (IntPtr)packet.Memory, bytes.Length); packet.Size = bytes.Length;
    }
}
