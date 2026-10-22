using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Msg = WolfConquer.Core.World.LegacyNativeMsgMessage;

namespace WolfConquer.Tests;

// Real native message/Packet/RoleView/queued-send components, no host route,
// socket Create, send pump, worker, model recipient or callback/provider.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeMsgMessageBehaviorTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Seal = typeof(LegacyNativePacket)
            .GetField("seal", BindingFlags.Static | BindingFlags.NonPublic)!;
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle)
            .GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? oldSeal = Seal.GetValue(null);
        private readonly object oldBin = Bin.GetValue(null)!;
        private readonly LegacyNativeDbSafeDictionary<uint, LegacyNativeGroupServerList.Server> oldServers =
            LegacyNativeGroupServerList.GroupServers;
        internal LegacyNativePacket Packet { get; } = new(Enumerable.Repeat((byte)0xCC, 1024).ToArray());
        internal LegacyNativeDbSafeDictionary<uint, LegacyNativeGroupServerList.Server> Servers { get; } = new();

        internal Scope(bool seal = false, bool recycle = false)
        {
            Seal.SetValue(null, seal ? "TQServer" : null);
            LegacyNativeGroupServerList.GroupServers = Servers;
            var owned = new ConcurrentQueue<LegacyNativePacket>();
            if (recycle) owned.Enqueue(Packet);
            Bin.SetValue(null, owned);
        }

        internal void Frame(Msg.MsgTalkProto proto)
        {
            Packet.InitWriter();
            Packet.ProtoBufferSerialize(proto);
            Packet.Finalize(2179);
            Assert.InRange(Packet.Size, 4, 512); // Every payload and later read stays owned/bounded.
        }

        public void Dispose()
        {
            LegacyNativeGroupServerList.GroupServers = oldServers;
            Bin.SetValue(null, oldBin);
            Seal.SetValue(null, oldSeal);
            Packet.Dispose();
        }
    }

    private static LegacyNativeGameClientRuntime Client(Queue<byte[]> queue)
    {
        var socket = new LegacyNativeSecuritySocket(null!, null!) { Alive = true };
        typeof(LegacyNativeSecuritySocket).GetField("SendQueue", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(socket, queue);
        Assert.Null(socket.Connection); Assert.Null(socket.Crypto);
        return new LegacyNativeGameClientRuntime { Socket = socket, FullLoading = false };
    }

    private static unsafe byte[] Bytes(LegacyNativePacket packet, int? count = null)
    {
        var bytes = new byte[count ?? packet.Size];
        Assert.InRange(bytes.Length, 0, 1024);
        Marshal.Copy((IntPtr)packet.Memory, bytes, 0, bytes.Length);
        return bytes;
    }

    private static Msg.MsgTalkProto Decode(byte[] frame)
    {
        Assert.InRange(frame.Length, 4, 512);
        Assert.Equal((ushort)2179, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2)));
        using var packet = new LegacyNativePacket(frame);
        return packet.ProtoBufferDeserialize(new Msg.MsgTalkProto());
    }

    private static Msg RetainedMessage() => new("old-message", 900u, "old-to", "old-from",
        Msg.MsgColor.white, Msg.ChatMode.World)
    {
        Color = 91, MessageUID1 = 92, MessageUID2 = 93, New1 = 94, New2 = 95,
        ServerName = "old-server", proto = new Msg.MsgTalkProto { Count = 777 }
    };

    private static Msg.MsgTalkProto Source(string[]? content) => new()
    {
        Color = uint.MaxValue, ChatType = 0x80000000, MessageUID1 = 11, MessageUID2 = 12,
        ServerID = 13, Mesh = 14, UnionType = 15, Count = 999, New1 = 16, New2 = 17,
        Content = content!
    };

    private static void AssertScalarsCopied(Msg message)
    {
        Assert.Equal(uint.MaxValue, message.Color);
        Assert.Equal((Msg.ChatMode)0x80000000u, message.ChatType);
        Assert.Equal(11u, message.MessageUID1); Assert.Equal(12u, message.MessageUID2);
        Assert.Equal(14u, message.Mesh); Assert.Equal(16u, message.New1); Assert.Equal(17u, message.New2);
        Assert.Equal(13u, message.proto.ServerID); Assert.Equal(15u, message.proto.UnionType);
        Assert.Equal(999u, message.proto.Count);
    }

    private static void AssertNoProtoOrAncillaryBirth(Msg message)
    {
        Assert.Null(message.proto); Assert.Equal(string.Empty, message.ServerName);
        Assert.Equal(0u, message.New1); Assert.Equal(0u, message.New2);
        Assert.Equal(0u, message.MessageUID1); Assert.Equal(0u, message.MessageUID2);
    }
}
