using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using ProtoBuf;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeReceivePlayerNpcTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? oldBin = Bin.GetValue(null);
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime oldReset = LegacyNativePoolRandom.ResetRandom;
        private readonly LegacyNativeMapDictionary<uint, LegacyNativeGameMap>? oldMaps = LegacyNativePoolMaps.ServerMaps;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? oldClients = LegacyNativePoolClients.GamePoll;
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
            LegacyNativePoolClients.GamePoll = new();
            Native = new();
            Client.Player.Name = "Wolf";
            Client.Player.Map = 11083;
            Client.Player.X = 64;
            Client.Player.Y = 64;
            LegacyNativePoolClients.GamePoll.TryAdd(Client.Player.UID, Client);
            Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)Packet.Memory, LegacyNativePacket.MAX_SIZE);
            Packet.InitWriter(); // Same caller prestate as the real recycler GetStream.
            var bin = new ConcurrentQueue<LegacyNativePacket>();
            for (int i = 0; i < 32; i++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
                borrowed.Add(packet);
                bin.Enqueue(packet);
            }
            Bin.SetValue(null, bin);
        }

        internal LegacyNativeGameClientRuntime Peer(uint uid, uint map, uint dynamicId, Queue<byte[]> queue)
        {
            var peer = NativeSpellTestFixture.CreateOwner(queue);
            peer.Player.UID = uid;
            peer.Player.Map = map;
            peer.Player.DynamicID = dynamicId;
            peer.Player.X = 64;
            peer.Player.Y = 64;
            Assert.True(LegacyNativePoolClients.GamePoll!.TryAdd(uid, peer));
            return peer;
        }

        internal LegacyNativeGameMap Map(int id = 11083)
        {
            var map = new LegacyNativeGameMap(128, 128, id) { View = new LegacyNativeMapView(128, 128) };
            Assert.True(LegacyNativePoolMaps.ServerMaps!.Add((uint)id, map));
            return map;
        }

        internal void Guild(uint key = 7, uint guildInfo = 7, string name = "WolfGuild")
        {
            Client.Player.GuildID = key;
            var guild = LegacyNativeGuildRuntime.CreateLoadedGuild(name);
            guild.Info.GuildID = guildInfo;
            Client.Player.MyGuild = guild;
        }

        internal static LegacyNativeSobNpcRuntime Pole(int hp = 100, int maxHp = 500) =>
            // Native NpcServer.LoadSobNpcs sets ObjType before publishing to MapView.
            new() { ObjType = LegacyNativeMapObjectType.SobNpc, UID = 830, Map = 11083, X = 64, Y = 64, Mesh = LegacyNativeSobNpcRuntime.StaticMesh.Pole,
                Name = "OldPole", Name2 = "OtherLanguage", HitPoints = hp, MaxHitPoints = maxHp };

        internal static LegacyNativePoleDomination War(LegacyNativeSobNpcRuntime? pole = null)
        {
            var war = new LegacyNativePoleDomination { Proces = LegacyNativeProcesType.Alive };
            war.Furnitures.Add(LegacyNativeSobNpcRuntime.StaticMesh.Pole, pole ?? Pole());
            return war;
        }

        public void Dispose()
        {
            Bin.SetValue(null, oldBin);
            foreach (var packet in borrowed) packet.Dispose();
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
    private static LegacyNativeMsgMessage.MsgTalkProto Talk(byte[] bytes)
    {
        Assert.Equal((ushort)2179, Opcode(bytes));
        return Proto<LegacyNativeMsgMessage.MsgTalkProto>(bytes);
    }
    private static string Message(byte[] bytes) => Talk(bytes).Content[3];
    private static LegacyNativePoleDomination.PoleDomination Row(uint id, uint score, string? name = null) =>
        new() { GuildID = id, Score = score, Name = name ?? "Guild" + id };
}
