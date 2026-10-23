using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using ProtoBuf;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeDeathTournamentTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? oldBin = Bin.GetValue(null);
        private readonly List<LegacyNativePacket> buffers = new();
        private readonly LegacyNativeSafeRandom random = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime reset = LegacyNativePoolRandom.ResetRandom;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? clients = LegacyNativePoolClients.GamePoll;
        private readonly CultureInfo culture = CultureInfo.CurrentCulture;
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal Queue<byte[]> Queue => Native.Queue;
        internal LegacyNativePacket Packet => Native.Packet;
        internal readonly Queue<byte[]> OtherQueue = new();
        internal LegacyNativeGameClientRuntime Other { get; }
        internal unsafe Scope()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            LegacyNativePoolRandom.GetRandom = new(239); LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            LegacyNativePoolClients.GamePoll = new();
            var bin = new ConcurrentQueue<LegacyNativePacket>();
            for (int i = 0; i < 64; i++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
                buffers.Add(packet); bin.Enqueue(packet);
            }
            Bin.SetValue(null, bin);
            Native = new(); Other = NativeSpellTestFixture.CreateOwner(OtherQueue);
            Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)Packet.Memory, LegacyNativePacket.MAX_SIZE);
            Client.Player.UID = 17; Client.Player.Name = "Victim"; Client.Player.Map = 1505; Client.Player.DynamicID = 9;
            Other.Player.UID = 31; Other.Player.Name = "Killer"; Other.Player.Map = 1505; Other.Player.DynamicID = 9;
        }
        internal LegacyNativeGameMap Map(int id = 1505)
        {
            var map = new LegacyNativeGameMap(128, 128, id) { View = new LegacyNativeMapView(128, 128) };
            Client.Player.X = Other.Player.X = 60; Client.Player.Y = Other.Player.Y = 60;
            map.Enquer(Client); map.Enquer(Other);
            return map;
        }
        internal void Publish()
        {
            LegacyNativePoolClients.GamePoll![17] = Client;
            LegacyNativePoolClients.GamePoll[31] = Other;
        }
        internal void FailNextTake() => Bin.SetValue(null, new ConcurrentQueue<LegacyNativePacket>(new[] { (LegacyNativePacket)null! }));
        internal void FailSecondTake() => Bin.SetValue(null, new ConcurrentQueue<LegacyNativePacket>(new[] { buffers[0], null! }));
        public void Dispose()
        {
            Bin.SetValue(null, oldBin); Native.Dispose();
            foreach (var packet in buffers) packet.Dispose();
            LegacyNativePoolRandom.GetRandom = random; LegacyNativePoolRandom.ResetRandom = reset;
            LegacyNativePoolClients.GamePoll = clients; CultureInfo.CurrentCulture = culture;
        }
    }
    private static LegacyNativeMsgMessage.MsgTalkProto Chat(byte[] frame)
    {
        Assert.Equal((ushort)2179, BitConverter.ToUInt16(frame, 2));
        using var body = new MemoryStream(frame, 4, frame.Length - 4);
        return Serializer.Deserialize<LegacyNativeMsgMessage.MsgTalkProto>(body);
    }
    private static string Text(byte[] frame) => Chat(frame).Content[3];
}
