using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.World.LegacyNativeNobility.NobilityRanking;

namespace WolfConquer.Tests;

// Genuine native Player/rank/dictionary/packet/send-queue components only.
// No socket Create/pump, host startup, persistence or live client is exercised.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeNobilityTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle)
            .GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object previousBin = Bin.GetValue(null)!;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? previousPool = LegacyNativePoolClients.GamePoll;
        private readonly LegacyNativeSafeRandom previousRandom = LegacyNativePoolRandom.GetRandom;
        private readonly CultureInfo previousCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo previousUi = CultureInfo.CurrentUICulture;
        private readonly List<LegacyNativePacket> additionalPackets = new();
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativePacket Packet => Native.Packet;
        internal ConcurrentDictionary<uint, LegacyNativeGameClientRuntime> Pool { get; } = new();
        internal ConcurrentQueue<LegacyNativePacket> Recycled { get; } = new();

        internal Scope()
        {
            // Player's actual field initializer consumes one genuine shared RNG draw.
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Native = new NativeSpellTestFixture();
            LegacyNativePoolClients.GamePoll = Pool;
            Initialize(Packet);
            Recycled.Enqueue(Packet);
            Bin.SetValue(null, Recycled);
        }

        internal void PrepareRecycled(int total)
        {
            while (Recycled.Count < total)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Initialize(packet);
                additionalPackets.Add(packet);
                Recycled.Enqueue(packet);
            }
        }

        private static unsafe void Initialize(LegacyNativePacket packet)
        {
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(),
                0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
            packet.Size = 321;
            packet.Seek(237);
        }

        public void Dispose()
        {
            Bin.SetValue(null, previousBin);
            LegacyNativePoolClients.GamePoll = previousPool;
            LegacyNativePoolRandom.GetRandom = previousRandom;
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUi;
            foreach (var packet in additionalPackets) packet.Dispose();
            Native.Dispose();
        }
    }

    private static LegacyNativeNobility Row(uint uid = 17, ulong donation = 300_000_000, int position = -1,
        string? name = "Wolf") => new(uid, name!, donation, 1003, 1) { Position = position };

    private static Ranking Fifty(ulong donation = 300_000_000)
    {
        var ranking = new Ranking();
        for (uint uid = 1; uid <= 50; uid++) ranking.ClientPoll.Add(uid, Row(uid, donation, (int)uid - 1));
        return ranking;
    }

    private static byte[] Icon(uint uid, ulong donation, byte rank, int position)
    {
        byte[] value = Encoding.UTF8.GetBytes($"{uid} {donation} {rank} {position}");
        byte[] bytes = new byte[123 + value.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)bytes.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2091);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), uid);
        bytes[120] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(121), checked((ushort)value.Length));
        value.CopyTo(bytes, 123);
        return bytes;
    }

    private static unsafe void AssertPromotion(byte[] frame, string name, string rank)
    {
        Assert.InRange(frame.Length, 4, 512);
        Assert.Equal((ushort)frame.Length, BinaryPrimitives.ReadUInt16LittleEndian(frame));
        Assert.Equal((ushort)2179, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2)));
        using var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
        Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
        Marshal.Copy(frame, 0, (IntPtr)packet.Memory, frame.Length);
        packet.Size = frame.Length;
        var message = packet.ProtoBufferDeserialize(new LegacyNativeMsgMessage.MsgTalkProto());
        Assert.Equal((uint)LegacyNativeMsgMessage.MsgColor.red, message.Color);
        Assert.Equal((uint)LegacyNativeMsgMessage.ChatMode.Center, message.ChatType);
        Assert.Equal(0u, message.MessageUID1); Assert.Equal(0u, message.MessageUID2);
        Assert.Equal(0u, message.ServerID); Assert.Equal(0u, message.Mesh);
        Assert.Equal(0u, message.UnionType); Assert.Equal(1u, message.Count);
        Assert.Equal(0u, message.New1); Assert.Equal(0u, message.New2);
        Assert.Equal(new[] { "SYSTEM", "ALL", "", $"{name} has been promoted to {rank}.", "", "", "" }, message.Content);
    }
}
