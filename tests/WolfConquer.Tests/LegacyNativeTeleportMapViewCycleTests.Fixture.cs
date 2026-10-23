using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using ProtoBuf;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeTeleportMapViewCycleTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private static readonly FieldInfo Generators = typeof(LegacyNativeMonsterFamily).GetField("ItemGeneratorLinker", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? oldBin = Bin.GetValue(null), oldGenerators = Generators.GetValue(null);
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime oldReset = LegacyNativePoolRandom.ResetRandom;
        private readonly Random oldFamilyRandom = LegacyNativeMonsterFamily.Random;
        private readonly LegacyNativeCounter oldMonsterUid = LegacyNativeMobCollection.GenerateUid;
        private readonly uint oldPetUid = LegacyNativeMsgGuild.UID;
        private readonly LegacyNativeMapDictionary<uint, LegacyNativeGameMap>? oldMaps = LegacyNativePoolMaps.ServerMaps;
        private readonly Dictionary<uint, LegacyNativeMonsterFamily> oldFamilies = LegacyNativePoolMonsters.MonsterFamilies;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? oldClients = LegacyNativePoolClients.GamePoll;
        private readonly List<LegacyNativePacket> owned = new();
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePlayerRuntime Player => Client.Player;
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;

        internal unsafe Scope()
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(113);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            LegacyNativeMonsterFamily.Random = new Random(127);
            Generators.SetValue(null, new Dictionary<uint, LegacyNativeMobItemGenerator>());
            LegacyNativeMobCollection.GenerateUid = new(400000);
            LegacyNativeMsgGuild.UID = 700010;
            LegacyNativePoolMaps.ServerMaps = new();
            LegacyNativePoolMonsters.MonsterFamilies = new();
            LegacyNativePoolClients.GamePoll = new();
            Native = new();
            Player.Name = "Wolf";
            Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)Packet.Memory, LegacyNativePacket.MAX_SIZE);
            Packet.InitWriter();
            var bin = new ConcurrentQueue<LegacyNativePacket>();
            for (int index = 0; index < 16; index++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
                owned.Add(packet);
                bin.Enqueue(packet);
            }
            Bin.SetValue(null, bin);
        }

        internal LegacyNativeGameMap Map()
        {
            var map = new LegacyNativeGameMap(80, 80, 700) { View = new LegacyNativeMapView(80, 80) };
            for (int x = 0; x < 80; x++)
                for (int y = 0; y < 80; y++) map.cells[x, y] = LegacyNativeMapFlagType.Valid;
            Assert.True(LegacyNativePoolMaps.ServerMaps!.Add(700, map));
            Player.Map = 700;
            Player.X = 10;
            Player.Y = 10;
            map.Enquer(Client);
            return map;
        }

        internal LegacyNativeGameClientRuntime Peer(LegacyNativeGameMap map, Queue<byte[]> queue)
        {
            var peer = NativeSpellTestFixture.CreateOwner(queue);
            peer.Player.UID = 99;
            peer.Player.Map = 700;
            peer.Player.X = 11;
            peer.Player.Y = 10;
            map.Enquer(peer);
            return peer;
        }

        internal LegacyNativeMonsterFamily PetFamily()
        {
            var family = new LegacyNativeMonsterFamily
            {
                ID = 1, Name = "Pheasant", MaxAttack = 30, MinAttack = 10,
                MaxHealth = 500, Mesh = 100, Level = 15, AttackRange = 2, DropSpecials = []
            };
            LegacyNativePoolMonsters.MonsterFamilies[1] = family;
            return family;
        }

        internal unsafe void Input(uint action, uint fairy, uint uid, uint unknown = 0)
        {
            byte[] frame = FairyFrame(action, fairy, uid, unknown);
            Marshal.Copy(frame, 0, (IntPtr)Packet.Memory, frame.Length);
            Packet.Size = frame.Length;
            Packet.Seek(4);
        }

        public void Dispose()
        {
            Bin.SetValue(null, oldBin);
            foreach (var packet in owned) packet.Dispose();
            Native.Dispose();
            Generators.SetValue(null, oldGenerators);
            LegacyNativeMonsterFamily.Random = oldFamilyRandom;
            LegacyNativePoolRandom.GetRandom = oldRandom;
            LegacyNativePoolRandom.ResetRandom = oldReset;
            LegacyNativeMobCollection.GenerateUid = oldMonsterUid;
            LegacyNativeMsgGuild.UID = oldPetUid;
            LegacyNativePoolMaps.ServerMaps = oldMaps;
            LegacyNativePoolMonsters.MonsterFamilies = oldFamilies;
            LegacyNativePoolClients.GamePoll = oldClients;
        }
    }

    private static ushort Opcode(byte[] frame) => BitConverter.ToUInt16(frame, 2);
    private static byte[] FairyFrame(uint action, uint fairy, uint uid, uint unknown = 0)
    {
        var result = new byte[20];
        BinaryPrimitives.WriteUInt16LittleEndian(result, 20);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(2), 2336);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), action);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8), unknown);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12), fairy);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16), uid);
        return result;
    }
    private static T Proto<T>(byte[] frame)
    {
        using var body = new MemoryStream(frame, 4, frame.Length - 4);
        return Serializer.Deserialize<T>(body);
    }
}
