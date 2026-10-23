using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using ProtoBuf;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeNobilityPoleEventTests
{
    // Test-only dispatch over the four concrete native static owners. No gameplay
    // callback, replacement event, field projection or production visibility change.
    private static Type War(int kind) => kind switch
    {
        0 => typeof(LegacyNativeMsgNobilityPole),
        1 => typeof(LegacyNativeMsgNobilityPole1),
        2 => typeof(LegacyNativeMsgNobilityPole2),
        3 => typeof(LegacyNativeMsgNobilityPole3),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    private static string Rank(int kind) => kind switch
    {
        0 => "King", 1 => "Prince", 2 => "Duke", 3 => "Earl",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    private static readonly string[] StateFields = { "NobilityPoleList", "RoundOwner", "Proces", "Furnitures", "MapID" };
    private static FieldInfo Field(int kind, string name) =>
        War(kind).GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
    private static object? Get(int kind, string name) => Field(kind, name).GetValue(null);
    private static T Get<T>(int kind, string name) => (T)Get(kind, name)!;
    private static void Set(int kind, string name, object? value) => Field(kind, name).SetValue(null, value);
    private static object? Call(int kind, string name, params object?[] arguments)
    {
        try
        {
            return War(kind).GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(null, arguments);
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }
    private static IDictionary Scores(int kind) => Get<IDictionary>(kind, "NobilityPoleList");
    private static Dictionary<LegacyNativeSobNpcRuntime.StaticMesh, LegacyNativeSobNpcRuntime> Furniture(int kind) =>
        Get<Dictionary<LegacyNativeSobNpcRuntime.StaticMesh, LegacyNativeSobNpcRuntime>>(kind, "Furnitures");
    private static object Row(int kind, uint uid, uint score, string? name = "Wolf") => kind switch
    {
        0 => new LegacyNativeMsgNobilityPole.Info { UID = uid, Score = score, Name = name },
        1 => new LegacyNativeMsgNobilityPole1.Info { UID = uid, Score = score, Name = name },
        2 => new LegacyNativeMsgNobilityPole2.Info { UID = uid, Score = score, Name = name },
        3 => new LegacyNativeMsgNobilityPole3.Info { UID = uid, Score = score, Name = name },
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    private static T RowField<T>(object row, string name) => (T)row.GetType().GetField(name)!.GetValue(row)!;

    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? oldBin = Bin.GetValue(null);
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime oldReset = LegacyNativePoolRandom.ResetRandom;
        private readonly LegacyNativeMapDictionary<uint, LegacyNativeGameMap>? oldMaps = LegacyNativePoolMaps.ServerMaps;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? oldClients = LegacyNativePoolClients.GamePoll;
        private readonly object?[][] oldState = new object?[4][];
        private readonly List<LegacyNativePacket> ownedPackets = new();
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;
        internal ConcurrentQueue<LegacyNativePacket> RecycleBin { get; } = new();

        internal unsafe Scope()
        {
            for (int kind = 0; kind < 4; kind++)
            {
                oldState[kind] = StateFields.Select(name => Get(kind, name)).ToArray();
                Set(kind, "NobilityPoleList", Activator.CreateInstance(Field(kind, "NobilityPoleList").FieldType));
                Set(kind, "RoundOwner", null);
                Set(kind, "Proces", LegacyNativeProcesType.Dead);
                Set(kind, "Furnitures", new Dictionary<LegacyNativeSobNpcRuntime.StaticMesh, LegacyNativeSobNpcRuntime>());
                Set(kind, "MapID", (ushort)11140);
            }
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(109);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            LegacyNativePoolMaps.ServerMaps = new();
            LegacyNativePoolClients.GamePoll = new();
            Native = new();
            Client.Player.Name = "Wolf";
            Client.Player.Map = 11140;
            Client.Player.X = 64;
            Client.Player.Y = 64;
            Assert.True(LegacyNativePoolClients.GamePoll.TryAdd(Client.Player.UID, Client));
            Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)Packet.Memory, LegacyNativePacket.MAX_SIZE);
            Packet.InitWriter();
            for (int i = 0; i < 32; i++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
                ownedPackets.Add(packet);
                RecycleBin.Enqueue(packet);
            }
            Bin.SetValue(null, RecycleBin);
        }

        internal LegacyNativeGameMap Map(int id = 11140, bool member = true)
        {
            var map = new LegacyNativeGameMap(128, 128, id) { View = new LegacyNativeMapView(128, 128) };
            Assert.True(LegacyNativePoolMaps.ServerMaps!.Add((uint)id, map));
            if (member) map.Enquer(Client);
            return map;
        }

        internal static LegacyNativeSobNpcRuntime Pole(int kind, int hp = 1, int max = 500) =>
            new() { UID = (uint)(22340 + kind), Map = 11140, X = 64, Y = 64,
                ObjType = LegacyNativeMapObjectType.SobNpc, Mesh = LegacyNativeSobNpcRuntime.StaticMesh.Pole,
                Name = "Previous", Name2 = "OtherLanguage", HitPoints = hp, MaxHitPoints = max };

        internal LegacyNativeSobNpcRuntime Active(int kind, int hp = 1, int max = 500)
        {
            Set(kind, "Proces", LegacyNativeProcesType.Alive);
            var pole = Pole(kind, hp, max);
            Furniture(kind).Add(LegacyNativeSobNpcRuntime.StaticMesh.Pole, pole);
            return pole;
        }

        internal LegacyNativeGameClientRuntime Peer(uint uid, uint map, ushort x, uint dynamicId, Queue<byte[]> queue)
        {
            var peer = NativeSpellTestFixture.CreateOwner(queue);
            peer.Player.UID = uid;
            peer.Player.Map = map;
            peer.Player.X = x;
            peer.Player.Y = 64;
            peer.Player.DynamicID = dynamicId;
            Assert.True(LegacyNativePoolClients.GamePoll!.TryAdd(uid, peer));
            return peer;
        }

        public void Dispose()
        {
            for (int kind = 0; kind < 4; kind++)
                for (int field = 0; field < StateFields.Length; field++)
                    Set(kind, StateFields[field], oldState[kind][field]);
            Bin.SetValue(null, oldBin);
            foreach (var packet in ownedPackets) packet.Dispose();
            Native.Dispose();
            LegacyNativePoolRandom.GetRandom = oldRandom;
            LegacyNativePoolRandom.ResetRandom = oldReset;
            LegacyNativePoolMaps.ServerMaps = oldMaps;
            LegacyNativePoolClients.GamePoll = oldClients;
        }
    }

    private static ushort Opcode(byte[] bytes) => BitConverter.ToUInt16(bytes, 2);
    private static T Proto<T>(byte[] bytes)
    {
        using var body = new MemoryStream(bytes, 4, bytes.Length - 4);
        return Serializer.Deserialize<T>(body);
    }
    private static string Message(byte[] bytes)
    {
        Assert.Equal((ushort)2179, Opcode(bytes));
        return Proto<LegacyNativeMsgMessage.MsgTalkProto>(bytes).Content[3];
    }
}
