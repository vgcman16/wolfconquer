using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using ProtoBuf;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeSaveSchedulerTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? oldBin = Bin.GetValue(null);
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime oldReset = LegacyNativePoolRandom.ResetRandom;
        private readonly LegacyNativeMapDictionary<uint, LegacyNativeGameMap>? oldMaps = LegacyNativePoolMaps.ServerMaps;
        private readonly ushort oldMapId = LegacyNativeMsgSteedRace.MAPID;
        private readonly uint[]? oldSettings = LegacyNativeMsgSteedRace.Settings;
        private readonly uint oldRecord = LegacyNativeMsgSteedRace.RaceRecord;
        private readonly LegacyNativeMsgSteedRace? oldRace = LegacyNativeMsgSchedules.SteedRace;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? oldClients = LegacyNativePoolClients.GamePoll;
        private readonly List<LegacyNativePacket> packets = new();
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;

        internal unsafe Scope()
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(83);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            LegacyNativePoolMaps.ServerMaps = new();
            LegacyNativePoolClients.GamePoll = new();
            LegacyNativeMsgSteedRace.Settings = null; LegacyNativeMsgSteedRace.MAPID = 1950;
            LegacyNativeMsgSteedRace.RaceRecord = 0; LegacyNativeMsgSchedules.SteedRace = null;
            Native = new();
            Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)Packet.Memory, LegacyNativePacket.MAX_SIZE);
            var bin = new ConcurrentQueue<LegacyNativePacket>();
            for (int i = 0; i < 20; i++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
                packets.Add(packet); bin.Enqueue(packet);
            }
            Bin.SetValue(null, bin);
        }

        internal LegacyNativeGameMap Map(int id = 3844, bool client = true)
        {
            var map = new LegacyNativeGameMap(128, 128, id) { View = new LegacyNativeMapView(128, 128) };
            for (int x = 0; x < 128; x++) for (int y = 0; y < 128; y++) map.cells[x, y] = LegacyNativeMapFlagType.Valid;
            Assert.True(LegacyNativePoolMaps.ServerMaps!.Add((uint)id, map));
            if (client) Admit(map, Client, 17);
            return map;
        }

        internal static void Admit(LegacyNativeGameMap map, LegacyNativeGameClientRuntime client, uint uid)
        {
            client.Player.UID = uid; client.Player.Map = map.ID; client.Player.X = 85; client.Player.Y = 85;
            map.Enquer(client);
        }

        internal void FailNextTake() => Bin.SetValue(null, new ConcurrentQueue<LegacyNativePacket>(new[] { (LegacyNativePacket)null! }));
        public void Dispose()
        {
            Bin.SetValue(null, oldBin); foreach (var packet in packets) packet.Dispose(); Native.Dispose();
            LegacyNativePoolRandom.GetRandom = oldRandom; LegacyNativePoolRandom.ResetRandom = oldReset;
            LegacyNativePoolMaps.ServerMaps = oldMaps; LegacyNativePoolClients.GamePoll = oldClients;
            LegacyNativeMsgSteedRace.MAPID = oldMapId; LegacyNativeMsgSteedRace.Settings = oldSettings;
            LegacyNativeMsgSteedRace.RaceRecord = oldRecord; LegacyNativeMsgSchedules.SteedRace = oldRace;
        }
    }

    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static void Set(object owner, string name, object? value) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.SetValue(owner, value);
    private static object? Invoke(object owner, string name, params object[] args)
    {
        try { return owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }
    private static byte[] Bytes(LegacyNativePacket packet) => NativeSpellTestFixture.Bytes(packet);
    private static ushort Opcode(byte[] bytes) => BitConverter.ToUInt16(bytes, 2);
    private static LegacyNativeMsgMessage.MsgTalkProto Talk(byte[] bytes)
    {
        Assert.Equal((ushort)2179, Opcode(bytes));
        using var body = new MemoryStream(bytes, 4, bytes.Length - 4);
        return Serializer.Deserialize<LegacyNativeMsgMessage.MsgTalkProto>(body);
    }

    private static uint[] RaceRow(uint map = 1950) =>
        new uint[] { map, 10, 11, 100, 100, 4, 5, 5, 621, 64, 64, 200, 64, 64, 200, 64, 64, 200 };
    private static LegacyNativeMsgSteedRace CreateRace(Scope scope, bool player = false)
    {
        scope.Map(1950, player);
        var race = new LegacyNativeMsgSteedRace { RaceSettings = new[] { RaceRow() } };
        race.Create(1950);
        return race;
    }
}
