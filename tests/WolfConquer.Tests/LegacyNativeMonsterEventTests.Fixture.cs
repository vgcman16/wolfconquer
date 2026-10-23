using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using ProtoBuf;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeMonsterEventTests
{
    private sealed class State : IDisposable
    {
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? clients = LegacyNativePoolClients.GamePoll;
        private readonly LegacyNativeMapDictionary<uint, LegacyNativeGameMap>? maps = LegacyNativePoolMaps.ServerMaps;
        private readonly Dictionary<int, string> mapContents = LegacyNativeGameMap.MapContents;
        private readonly LegacyNativeSafeRandom random = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime reset = LegacyNativePoolRandom.ResetRandom;
        private readonly LegacyNativeMsgSaturn? saturn = LegacyNativeMsgSchedules.Saturn;
        private readonly LegacyNativeMsgPowerArena? power = LegacyNativeMsgSchedules.PowerArena;
        private readonly LegacyNativeMsgDisCity? dis = LegacyNativeMsgSchedules.DisCity;
        private readonly LegacyNativeGroupServerList.Server? server = LegacyNativeGroupServerList.MyServerInfo;
        internal State()
        {
            LegacyNativePoolClients.GamePoll = new(); LegacyNativePoolMaps.ServerMaps = new(); LegacyNativeGameMap.MapContents = new();
            LegacyNativePoolRandom.GetRandom = new(551); LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            LegacyNativeMsgSchedules.Saturn = null; LegacyNativeMsgSchedules.PowerArena = null; LegacyNativeMsgSchedules.DisCity = null;
            LegacyNativeGroupServerList.MyServerInfo = new() { ID = 2 };
        }
        public void Dispose()
        {
            LegacyNativePoolClients.GamePoll = clients; LegacyNativePoolMaps.ServerMaps = maps; LegacyNativeGameMap.MapContents = mapContents;
            LegacyNativePoolRandom.GetRandom = random; LegacyNativePoolRandom.ResetRandom = reset;
            LegacyNativeMsgSchedules.Saturn = saturn; LegacyNativeMsgSchedules.PowerArena = power; LegacyNativeMsgSchedules.DisCity = dis;
            LegacyNativeGroupServerList.MyServerInfo = server!;
        }
    }
    private sealed class Packets : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? prior = Bin.GetValue(null);
        private readonly List<LegacyNativePacket> buffers = new();
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal Queue<byte[]> Queue => Native.Queue;
        internal LegacyNativePacket Stream => Native.Packet;
        internal unsafe Packets()
        {
            var bin = new ConcurrentQueue<LegacyNativePacket>();
            for (int n = 0; n < 32; n++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
                buffers.Add(packet); bin.Enqueue(packet);
            }
            Bin.SetValue(null, bin); Native = new(); Client.Player.Name = "Wolf"; Client.Player.Map = 1002; Client.Player.IsCheckedPass = true;
            Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)Stream.Memory, LegacyNativePacket.MAX_SIZE);
        }
        internal void Publish() => LegacyNativePoolClients.GamePoll![17] = Client;
        internal void FailNextTake() => Bin.SetValue(null, new ConcurrentQueue<LegacyNativePacket>(new[] { (LegacyNativePacket)null! }));
        public void Dispose()
        {
            Bin.SetValue(null, prior); Native.Dispose(); foreach (var packet in buffers) packet.Dispose();
        }
    }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static void Put(object owner, string name, object? value) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, value);
    private static LegacyNativeGameMap Map(uint id) => new(64, 64, (int)id) { View = new LegacyNativeMapView(64, 64) };
    private static LegacyNativeMsgMessage.MsgTalkProto Chat(byte[] frame)
    {
        Assert.Equal((ushort)2179, BitConverter.ToUInt16(frame, 2));
        using var body = new MemoryStream(frame, 4, frame.Length - 4); return Serializer.Deserialize<LegacyNativeMsgMessage.MsgTalkProto>(body);
    }
    private static string Text(byte[] frame) => Chat(frame).Content[3];
}
