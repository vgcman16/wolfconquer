using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Cell = WolfConquer.Core.World.LegacyNativeMapFlagType;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeMapPopulationTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle)
            .GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private static readonly FieldInfo GeneratorCache = typeof(LegacyNativeMonsterFamily)
            .GetField("ItemGeneratorLinker", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? oldBin = Bin.GetValue(null);
        private readonly object? oldGenerators = GeneratorCache.GetValue(null);
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime oldReset = LegacyNativePoolRandom.ResetRandom;
        private readonly Random oldFamilyRandom = LegacyNativeMonsterFamily.Random;
        private readonly LegacyNativeMapDictionary<uint, LegacyNativeGameMap>? oldMaps = LegacyNativePoolMaps.ServerMaps;
        private readonly Dictionary<int, string> oldContents = LegacyNativeGameMap.MapContents;
        private readonly Dictionary<uint, LegacyNativeMonsterFamily> oldFamilies = LegacyNativePoolMonsters.MonsterFamilies;
        private readonly Dictionary<uint, LegacyNativeNpcServer.Furniture> oldFurniture = LegacyNativeNpcServer.FurnitureInformations;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? oldClients = LegacyNativePoolClients.GamePoll;
        private readonly LegacyNativeCounter oldFloorUids = LegacyNativeFloorItemRuntime.UIDS;
        private readonly LegacyNativeCounter oldMobUids = LegacyNativeMobCollection.GenerateUid;
        private readonly LegacyNativeCounter oldStatueUids = LegacyNativeStatue.CounterUID;
        private readonly LegacyNativeStatue? oldStatue = LegacyNativeStatue.StaticStatue;
        private readonly LegacyNativeSobNpcRuntime? oldStaticSob = LegacyNativeStatue.StaticSobNpc;
        private readonly string oldDb = LegacyNativeServerConfig.DbLocation;
        private readonly string oldClient = LegacyNativeServerConfig.CO2Folder;
        private readonly string oldName = LegacyNativeServerConfig.ServerName;
        private readonly List<LegacyNativePacket> packets = [];
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePlayerRuntime Player => Client.Player;
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;
        internal string? OwnedDirectory { get; private set; }
        internal string Db => Path.Combine(OwnedDirectory!, "db");
        internal string ClientFiles => Path.Combine(OwnedDirectory!, "client");

        internal Scope()
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            LegacyNativeMonsterFamily.Random = new Random(91);
            GeneratorCache.SetValue(null, new Dictionary<uint, LegacyNativeMobItemGenerator>());
            LegacyNativePoolMaps.ServerMaps = new();
            LegacyNativeGameMap.MapContents = new();
            LegacyNativePoolMonsters.MonsterFamilies = new();
            LegacyNativeNpcServer.FurnitureInformations = new();
            LegacyNativePoolClients.GamePoll = new();
            LegacyNativeFloorItemRuntime.UIDS = new(900027);
            LegacyNativeMobCollection.GenerateUid = new(400000);
            LegacyNativeStatue.CounterUID = new(105175);
            LegacyNativeStatue.StaticStatue = null;
            LegacyNativeStatue.StaticSobNpc = null;
            Native = new();
            var bin = new ConcurrentQueue<LegacyNativePacket>();
            for (int i = 0; i < 4; i++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                packets.Add(packet);
                bin.Enqueue(packet);
            }
            Bin.SetValue(null, bin);
        }

        internal LegacyNativeGameMap Map(int id = 700, bool admitPlayer = true)
        {
            var map = NewMap(id);
            Assert.True(LegacyNativePoolMaps.ServerMaps!.Add((uint)id, map));
            if (admitPlayer)
            {
                Player.Map = (uint)id; Player.X = 10; Player.Y = 10;
                map.Enquer(Client);
            }
            return map;
        }

        internal LegacyNativeGameClientRuntime Peer(LegacyNativeGameMap map, uint uid,
            ushort x, ushort y, Queue<byte[]> queue, uint dynamicId = 0)
        {
            var peer = NativeSpellTestFixture.CreateOwner(queue);
            peer.Player.UID = uid; peer.Player.Map = map.ID;
            peer.Player.X = x; peer.Player.Y = y; peer.Player.DynamicID = dynamicId;
            map.Enquer(peer);
            return peer;
        }

        internal void OwnFiles()
        {
            OwnedDirectory = Path.Combine(Path.GetTempPath(), "WolfConquer-MapPopulation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Db);
            Directory.CreateDirectory(Path.Combine(Db, "maps"));
            Directory.CreateDirectory(Path.Combine(Db, "MonsterSpawns"));
            Directory.CreateDirectory(ClientFiles);
            LegacyNativeServerConfig.DbLocation = Db + Path.DirectorySeparatorChar;
            LegacyNativeServerConfig.CO2Folder = ClientFiles;
            LegacyNativeServerConfig.ServerName = "OwnedWorld";
            WriteDb("SobNpcs.txt", ""); // The native EnterMap requires this file even with no SOBs.
        }

        internal void WriteDb(string name, string content) =>
            File.WriteAllText(Path.Combine(Db, name), content, new UTF8Encoding(false));

        internal void Terrain(int catalogId = 700, string name = "owned.dmap", int width = 40, int height = 40)
        {
            using var output = new MemoryStream();
            using var writer = new BinaryWriter(output);
            writer.Write(new byte[268]); writer.Write(width); writer.Write(height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    writer.Write((short)0); writer.Write((short)0); writer.Write((short)0);
                }
                writer.Write(0);
            }
            File.WriteAllBytes(Path.Combine(ClientFiles, name), output.ToArray());
            LegacyNativeGameMap.MapContents[catalogId] = name;
        }

        public void Dispose()
        {
            Bin.SetValue(null, oldBin);
            foreach (var packet in packets) packet.Dispose();
            Native.Dispose();
            GeneratorCache.SetValue(null, oldGenerators);
            LegacyNativePoolRandom.GetRandom = oldRandom;
            LegacyNativePoolRandom.ResetRandom = oldReset;
            LegacyNativeMonsterFamily.Random = oldFamilyRandom;
            LegacyNativePoolMaps.ServerMaps = oldMaps;
            LegacyNativeGameMap.MapContents = oldContents;
            LegacyNativePoolMonsters.MonsterFamilies = oldFamilies;
            LegacyNativeNpcServer.FurnitureInformations = oldFurniture;
            LegacyNativePoolClients.GamePoll = oldClients;
            LegacyNativeFloorItemRuntime.UIDS = oldFloorUids;
            LegacyNativeMobCollection.GenerateUid = oldMobUids;
            LegacyNativeStatue.CounterUID = oldStatueUids;
            LegacyNativeStatue.StaticStatue = oldStatue;
            LegacyNativeStatue.StaticSobNpc = oldStaticSob;
            LegacyNativeServerConfig.DbLocation = oldDb;
            LegacyNativeServerConfig.CO2Folder = oldClient;
            LegacyNativeServerConfig.ServerName = oldName;
            if (OwnedDirectory != null)
            {
                string exact = Path.GetFullPath(OwnedDirectory);
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                Assert.StartsWith(temp, exact, StringComparison.OrdinalIgnoreCase);
                Assert.StartsWith("WolfConquer-MapPopulation-", Path.GetFileName(exact), StringComparison.Ordinal);
                if (Directory.Exists(exact)) Directory.Delete(exact, recursive: true);
            }
        }
    }

    private static LegacyNativeGameMap NewMap(int id = 700)
    {
        var map = new LegacyNativeGameMap(80, 80, id) { View = new LegacyNativeMapView(80, 80) };
        for (int x = 0; x < 80; x++)
            for (int y = 0; y < 80; y++) map.cells[x, y] = Cell.Valid;
        return map;
    }

    private static void Set(object owner, string field, object value) => owner.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, value);
    private static ushort Opcode(byte[] frame) => BitConverter.ToUInt16(frame, 2);
    private static byte[] Bytes(LegacyNativePacket packet) => NativeSpellTestFixture.Bytes(packet);
}
