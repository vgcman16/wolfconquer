using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Cell = WolfConquer.Core.World.LegacyNativeMapFlagType;
using Setting = WolfConquer.Core.World.LegacyNativeMonsterSettings;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeMonsterAttackTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private static readonly FieldInfo Cache = typeof(LegacyNativeMonsterFamily).GetField("ItemGeneratorLinker", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? oldBin = Bin.GetValue(null), oldCache = Cache.GetValue(null);
        private readonly Random oldFamilyRandom = LegacyNativeMonsterFamily.Random;
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly LegacyNativeItemTypeRuneMetadata? oldItems = LegacyNativePoolItems.ItemsBase;
        private readonly Dictionary<ushort, Dictionary<uint, LegacyNativeItemRuneMetadata>> oldSouls = LegacyNativeItemTypeRuneMetadata.PurificationItems;
        private readonly Dictionary<uint, LegacyNativeItemRuneMetadata> oldAccessories = LegacyNativeItemTypeRuneMetadata.Accessorys;
        private readonly LegacyNativeMapDictionary<uint, LegacyNativeGameMap>? oldMaps = LegacyNativePoolMaps.ServerMaps;
        private readonly LegacyNativeCounter oldMonsterCounter = LegacyNativeMobCollection.GenerateUid;
        private readonly List<LegacyNativePacket> packets = new();
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePlayerRuntime Player => Client.Player;
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;
        internal LegacyNativeGameMap Map { get; } = CreateMap();

        internal Scope()
        {
            Cache.SetValue(null, new Dictionary<uint, LegacyNativeMobItemGenerator>());
            LegacyNativeMonsterFamily.Random = new Random(91);
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
            LegacyNativePoolItems.ItemsBase = new(); LegacyNativeItemTypeRuneMetadata.PurificationItems = new(); LegacyNativeItemTypeRuneMetadata.Accessorys = new();
            LegacyNativePoolMaps.ServerMaps = new(); LegacyNativePoolMaps.ServerMaps.Add(Map.ID, Map);
            LegacyNativeMobCollection.GenerateUid = new(400000);
            Native = new(); Client.Map = Map; Player.Map = Map.ID; Player.X = 25; Player.Y = 25;
            Assert.True(Map.View!.EnterMap((ILegacyNativeMapObj)Player));
            var bin = new ConcurrentQueue<LegacyNativePacket>();
            for (int i = 0; i < 10; i++) { var p = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE); packets.Add(p); bin.Enqueue(p); }
            Bin.SetValue(null, bin);
        }

        internal LegacyNativeMonsterRole Monster(LegacyNativeMonsterFamily? family = null, uint uid = 400001)
        {
            var role = new LegacyNativeMonsterRole(family ?? Family(), uid, "owned-spawn", Map) { Map = Map.ID, X = 26, Y = 25 };
            return role;
        }

        internal LegacyNativeGameClientRuntime Peer(uint uid, ushort x, ushort y, Queue<byte[]> queue)
        {
            var peer = NativeSpellTestFixture.CreateOwner(queue); peer.Map = Map;
            peer.Player.UID = uid; peer.Player.Map = Map.ID; peer.Player.X = x; peer.Player.Y = y;
            Assert.True(Map.View!.EnterMap((ILegacyNativeMapObj)peer.Player)); return peer;
        }

        public void Dispose()
    {
            Bin.SetValue(null, oldBin); foreach (var p in packets) p.Dispose(); Native.Dispose();
            Cache.SetValue(null, oldCache); LegacyNativeMonsterFamily.Random = oldFamilyRandom; LegacyNativePoolRandom.GetRandom = oldRandom;
            LegacyNativePoolItems.ItemsBase = oldItems; LegacyNativeItemTypeRuneMetadata.PurificationItems = oldSouls; LegacyNativeItemTypeRuneMetadata.Accessorys = oldAccessories;
            LegacyNativePoolMaps.ServerMaps = oldMaps; LegacyNativeMobCollection.GenerateUid = oldMonsterCounter;
        }
    }

    private static LegacyNativeMonsterFamily Family(uint id = 300) => new()
    {
        ID = id, Name = "Pheasant", MaxAttack = 20, MinAttack = 9, MaxHealth = 500,
        Defense = 7, Mesh = 100, Level = 15, AttackRange = 2, Settings = Setting.Standard,
        DropSpecials = [], SpawnX = 30, SpawnY = 30, MaxSpawnX = 50, MaxSpawnY = 50, SpawnCount = 1,
    };

    private static LegacyNativeGameMap CreateMap(int id = 700, Cell fill = Cell.Valid)
    {
        var map = new LegacyNativeGameMap(80, 80, id) { View = new LegacyNativeMapView(80, 80) };
        for (int x = 0; x < 80; x++) for (int y = 0; y < 80; y++) map.cells[x, y] = fill;
        return map;
    }

    private static void SetPrivate(object owner, string name, object value) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, value);
    private static LegacyNativeMobRateWatcher Watcher(LegacyNativeMobItemGenerator owner, string name) =>
        (LegacyNativeMobRateWatcher)typeof(LegacyNativeMobItemGenerator).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static ushort Opcode(byte[] bytes) => BitConverter.ToUInt16(bytes, 2);

    // Independent wire primitives; no production serializer creates expectations.
    private static void Varint(List<byte> bytes, ulong value)
    {
        while (value >= 128) { bytes.Add((byte)((value & 127) | 128)); value >>= 7; } bytes.Add((byte)value);
    }
    private static void Field(List<byte> bytes, int tag, ulong value) { Varint(bytes, (ulong)(tag << 3)); Varint(bytes, value); }
    private static byte[] Frame(ushort opcode, List<byte> payload)
    {
        var bytes = new List<byte>(); bytes.AddRange(BitConverter.GetBytes((ushort)(payload.Count + 4))); bytes.AddRange(BitConverter.GetBytes(opcode)); bytes.AddRange(payload); return bytes.ToArray();
    }
    private static byte[] Spawn(uint uid, uint mesh, byte level, ushort x, ushort y, string name, uint monsterId,
        uint hp, uint max, bool boss = false, ushort action = 0, ulong[]? flags = null,
        uint owner = 0, uint pet = 0, byte facing = 0, uint guild = 0)
    {
        int[] required = [1,2,3,4,5,8,9,10,11,12,26,27,28,29,30,31,34,35,36,37,38,42,43,63,64,65,66,67,68,69,79,108,110,111,112,113,121];
        var scalars = required.ToDictionary(tag => tag, _ => 0UL);
        scalars[1] = mesh; scalars[2] = uid; scalars[3] = guild; scalars[26] = level; scalars[27] = x; scalars[28] = y;
        scalars[30] = facing; scalars[31] = action; scalars[35] = level; scalars[63] = boss ? 1UL : 0UL; scalars[111] = monsterId;
        if (hp != 0) scalars[21] = hp; if (max != 0) scalars[23] = max;
        if (pet != 0) scalars[81] = pet; if (owner != 0) { scalars[82] = 15; scalars[83] = owner; }
        var payload = new List<byte>();
        for (int tag = 1; tag <= 121; tag++)
        {
            if (scalars.TryGetValue(tag, out ulong value)) Field(payload, tag, value);
            if (tag == 6) foreach (ulong word in flags ?? new ulong[10]) Field(payload, 6, word);
            if (tag == 107) foreach (string text in new[] { name, "", "", "" })
            {
                byte[] utf8 = Encoding.UTF8.GetBytes(text); Varint(payload, (107UL << 3) | 2); Varint(payload, (ulong)utf8.Length); payload.AddRange(utf8);
            }
        }
        return Frame(2227, payload);
    }
}
