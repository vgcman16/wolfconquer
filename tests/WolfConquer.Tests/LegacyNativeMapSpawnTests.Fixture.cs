using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Cell = WolfConquer.Core.World.LegacyNativeMapFlagType;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeMapSpawnTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private static readonly FieldInfo Generators = typeof(LegacyNativeMonsterFamily).GetField("ItemGeneratorLinker", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? oldBin = Bin.GetValue(null), oldGenerators = Generators.GetValue(null);
        private readonly Random oldFamilyRandom = LegacyNativeMonsterFamily.Random;
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime oldReset = LegacyNativePoolRandom.ResetRandom;
        private readonly LegacyNativeMapDictionary<uint, LegacyNativeGameMap>? oldMaps = LegacyNativePoolMaps.ServerMaps;
        private readonly Dictionary<int, string> oldContents = LegacyNativeGameMap.MapContents;
        private readonly Dictionary<uint, LegacyNativeMonsterFamily> oldFamilies = LegacyNativePoolMonsters.MonsterFamilies;
        private readonly LegacyNativeCounter oldMobUids = LegacyNativeMobCollection.GenerateUid;
        private readonly LegacyNativeCounter oldFloorUids = LegacyNativeFloorItemRuntime.UIDS;
        private readonly string oldDb = LegacyNativeServerConfig.DbLocation;
        private readonly List<LegacyNativePacket> packets = [];
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePlayerRuntime Player => Client.Player;
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;
        internal string? OwnedDirectory { get; private set; }
        internal string Db => Path.Combine(OwnedDirectory!, "db");

        internal Scope()
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            LegacyNativeMonsterFamily.Random = new Random(91);
            Generators.SetValue(null, new Dictionary<uint, LegacyNativeMobItemGenerator>());
            LegacyNativePoolMaps.ServerMaps = new(); LegacyNativeGameMap.MapContents = new();
            LegacyNativePoolMonsters.MonsterFamilies = new();
            LegacyNativeMobCollection.GenerateUid = new(400000); LegacyNativeFloorItemRuntime.UIDS = new(900027);
            Native = new();
            var bin = new ConcurrentQueue<LegacyNativePacket>();
            for (int i = 0; i < 8; i++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE); packets.Add(packet); bin.Enqueue(packet);
            }
            Bin.SetValue(null, bin);
        }

        internal LegacyNativeGameMap Map(int id = 700, bool admitPlayer = true, int width = 80, int height = 80)
        {
            var map = new LegacyNativeGameMap(width, height, id) { View = new LegacyNativeMapView(width, height) };
            for (int x = 0; x < width; x++) for (int y = 0; y < height; y++) map.cells[x, y] = Cell.Valid;
            Assert.True(LegacyNativePoolMaps.ServerMaps!.Add((uint)id, map));
            if (admitPlayer) { Player.Map = (uint)id; Player.X = 10; Player.Y = 10; map.Enquer(Client); }
            return map;
        }

        internal void OwnFiles()
        {
            OwnedDirectory = Path.Combine(Path.GetTempPath(), "WolfConquer-MapSpawn-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Db, "Monsters")); Directory.CreateDirectory(Path.Combine(Db, "MonsterSpawns"));
            LegacyNativeServerConfig.DbLocation = Db + Path.DirectorySeparatorChar;
        }

        internal string WriteDb(string relative, string content)
        {
            string path = Path.Combine(Db, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content, new UTF8Encoding(false)); return path;
        }

        public void Dispose()
        {
            Bin.SetValue(null, oldBin); foreach (var packet in packets) packet.Dispose(); Native.Dispose();
            Generators.SetValue(null, oldGenerators); LegacyNativeMonsterFamily.Random = oldFamilyRandom;
            LegacyNativePoolRandom.GetRandom = oldRandom; LegacyNativePoolRandom.ResetRandom = oldReset;
            LegacyNativePoolMaps.ServerMaps = oldMaps; LegacyNativeGameMap.MapContents = oldContents;
            LegacyNativePoolMonsters.MonsterFamilies = oldFamilies; LegacyNativeMobCollection.GenerateUid = oldMobUids;
            LegacyNativeFloorItemRuntime.UIDS = oldFloorUids; LegacyNativeServerConfig.DbLocation = oldDb;
            if (OwnedDirectory != null)
            {
                string exact = Path.GetFullPath(OwnedDirectory);
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                Assert.StartsWith(temp, exact, StringComparison.OrdinalIgnoreCase);
                Assert.StartsWith("WolfConquer-MapSpawn-", Path.GetFileName(exact), StringComparison.Ordinal);
                if (Directory.Exists(exact)) Directory.Delete(exact, recursive: true);
            }
        }
    }

    private static LegacyNativeMonsterFamily Family(uint id = 111, string name = "Pheasant", int hp = 500) => new()
    {
        ID = id, Name = name, MaxAttack = 30, MinAttack = 10, MaxHealth = hp,
        Defense = 7, Mesh = 100, Level = 15, AttackRange = 2, DropSpecials = [],
        SpawnX = 12, SpawnY = 13, MaxSpawnX = 13, MaxSpawnY = 14, SpawnCount = 1
    };
    private static ushort Opcode(byte[] bytes) => BitConverter.ToUInt16(bytes, 2);
    private static byte[] Bytes(LegacyNativePacket packet) => NativeSpellTestFixture.Bytes(packet);
    private static T SingleRole<T>(LegacyNativeGameMap map, LegacyNativeMapObjectType type) =>
        Assert.IsType<T>(Assert.Single(map.View!.GetAllMapRoles(type)));
}
