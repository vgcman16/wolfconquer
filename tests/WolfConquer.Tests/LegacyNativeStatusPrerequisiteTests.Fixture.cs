using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeStatusPrerequisiteTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? oldBin = Bin.GetValue(null);
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime oldReset = LegacyNativePoolRandom.ResetRandom;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? oldClients = LegacyNativePoolClients.GamePoll;
        private readonly List<LegacyNativePacket> packets = new();
        internal readonly NativeSpellTestFixture Native;
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePlayerRuntime Player => Client.Player;
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;
        internal Scope()
        {
            LegacyNativePoolRandom.GetRandom = new(171); LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            LegacyNativePoolClients.GamePoll = new();
            var bin = new ConcurrentQueue<LegacyNativePacket>();
            for (int i = 0; i < 8; i++) { var p = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE); p.InitWriter(); packets.Add(p); bin.Enqueue(p); }
            Bin.SetValue(null, bin); Native = new(); Client.Equipment = new(Client);
            Native.Packet.InitWriter(); // Caller prestate supplied by native RecycledPacket.GetStream.
        }
        internal void Class(uint value) => typeof(LegacyNativePlayerRuntime).GetField("_class", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Player, value);
        internal LegacyNativeGameMap Map()
        {
            var map = new LegacyNativeGameMap(320, 320, 1038);
            map.View = new LegacyNativeMapView(map.bounds.Width, map.bounds.Height);
            map.FloorType = new int[320, 320]; return map;
        }
        public void Dispose()
        {
            Native.Dispose(); Bin.SetValue(null, oldBin); foreach (var packet in packets) packet.Dispose();
            LegacyNativePoolRandom.GetRandom = oldRandom; LegacyNativePoolRandom.ResetRandom = oldReset;
            LegacyNativePoolClients.GamePoll = oldClients;
        }
    }
    private static byte[] Icon(uint uid, uint flag, uint power)
    {
        // Independent literal native MsgUpdate protobuf field mapping, not the production serializer.
        var row = new List<byte>(); F(row, 1, 78); F(row, 4, flag); F(row, 5, 3); F(row, 6, power * 30);
        var body = new List<byte>(); F(body, 1, uid); V(body, 18); V(body, (uint)row.Count); body.AddRange(row);
        var frame = new List<byte>(); frame.AddRange(BitConverter.GetBytes((ushort)(body.Count + 4)));
        frame.AddRange(BitConverter.GetBytes((ushort)2176)); frame.AddRange(body); return frame.ToArray();
    }
    private static void F(List<byte> b, uint field, uint value) { V(b, field << 3); V(b, value); }
    private static void V(List<byte> b, uint value)
    { do { byte part = (byte)(value & 127); value >>= 7; b.Add((byte)(part | (value == 0 ? 0 : 128))); } while (value != 0); }
}
